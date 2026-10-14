using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// A chunk's main mesh laid out the way a Mesh keeps it, put together off the main thread so that making the Mesh there is a few
    /// block copies (<see cref="ApplyTo"/>).
    ///
    /// THE CLASSIC MESH API COST 20-46 MS OF ONE FRAME FOR A BLOCK OF TOWERS (250-300k vertices; measured 2026-10-14 in
    /// CityStreamerFramePlayTests, they were the worst streaming frames): SetVertices, SetNormals, SetUVs, SetColors and SetTangents each
    /// repack the whole vertex buffer to fit one more attribute in, SetTriangles checks every index, and RecalculateBounds reads every
    /// vertex again. Here the layout is given once, each vertex stream is one copy of an array made on the worker, the indices of every
    /// submesh are one array, and the submeshes' vertex ranges and the bounds come worked out, so nothing is checked or recalculated on the
    /// main thread: 2-3 ms for the same blocks, nearly all of it the copying. It is the same mesh as before -- the same vertices with the
    /// same values, the same indices, submeshes and bounds -- only kept in four streams instead of one (ChunkMeshDataTests builds real
    /// chunks both ways and compares them).
    /// </summary>
    public sealed class ChunkMeshData
    {
        /// <summary>A vertex's colour and UV, which share the fourth vertex stream: a mesh has at most four, and there are five attributes.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ColorAndUv
        {
            public Color32 Color;
            public Vector2 Uv;
        }

        /// <summary>Above this many vertices the indices are 32-bit; up to it 16-bit, half the size (a 16-bit index reaches 65,535).</summary>
        public const int MaxVerticesFor16BitIndices = 65000;

        // Each attribute in the format the classic API stored it in (Float32, the colour as four bytes); the streams match the arrays below.
        private static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 2),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3),
        };

        /// <summary>
        /// Everything here is made from the generator's own buffers, indices in range and bounds worked out, and the mesh is new (nothing
        /// uses it yet): none of Unity's checks or recalculations are needed.
        /// </summary>
        private const MeshUpdateFlags Trusted = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds
            | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;

        public readonly int VertexCount;
        public readonly int IndexCount;

        // The four vertex streams. They and the indices are arrays from ChunkArrayPool, used again chunk after chunk (2026-10-14: fresh ones
        // for every chunk were tens of MB of garbage for a block of towers, and a collection stops every thread): longer than what is in
        // them, so VertexCount and IndexCount say how much is, and given back by Release once the Mesh is made.
        public readonly Vector3[] Positions;
        public readonly Vector3[] Normals;
        public readonly Vector4[] Tangents;
        public readonly ColorAndUv[] ColorsAndUvs;

        /// <summary>Every submesh's indices one after another: 16-bit up to <see cref="MaxVerticesFor16BitIndices"/> vertices, else 32-bit (the other is null).</summary>
        public readonly ushort[] Indices16;
        public readonly int[] Indices32;

        /// <summary>The submeshes that have something in them, in group order; a mesh with nothing at all has one, empty.</summary>
        public readonly SubMeshDescriptor[] SubMeshes;

        /// <summary>Which group (its material) each submesh with something in it is: one entry per submesh, none for the empty one.</summary>
        public readonly int[] SubMeshGroups;

        public readonly Bounds Bounds;

        private ChunkMeshData(int vertexCount, int indexCount, Vector3[] positions, Vector3[] normals, Vector4[] tangents,
            ColorAndUv[] colorsAndUvs, ushort[] indices16, int[] indices32, SubMeshDescriptor[] subMeshes, int[] subMeshGroups, Bounds bounds)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
            Positions = positions;
            Normals = normals;
            Tangents = tangents;
            ColorsAndUvs = colorsAndUvs;
            Indices16 = indices16;
            Indices32 = indices32;
            SubMeshes = subMeshes;
            SubMeshGroups = subMeshGroups;
            Bounds = bounds;
        }

        /// <summary>
        /// One mesh from groups of buffers, a submesh per group (each is drawn with one material), the vertices and indices in the order
        /// given; a group with nothing in it gets no submesh. Tangents (MeshTangents) and bounds included. Any thread.
        /// </summary>
        public static ChunkMeshData Combine(IReadOnlyList<MeshBuffer[]> groups)
        {
            int vertexCount = 0, indexCount = 0, used = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                int groupIndices = 0;
                foreach (MeshBuffer buffer in groups[g])
                {
                    vertexCount += buffer.Vertices.Count;
                    groupIndices += buffer.Triangles.Count;
                }
                indexCount += groupIndices;
                if (groupIndices > 0)
                {
                    used++;
                }
            }

            Vector3[] positions = ChunkArrayPool<Vector3>.Rent(vertexCount);
            Vector3[] normals = ChunkArrayPool<Vector3>.Rent(vertexCount);
            ColorAndUv[] colorsAndUvs = ChunkArrayPool<ColorAndUv>.Rent(vertexCount);
            int[] indices = ChunkArrayPool<int>.Rent(indexCount);
            var subMeshes = new SubMeshDescriptor[Mathf.Max(1, used)];
            var subMeshGroups = new int[used];
            subMeshes[0] = new SubMeshDescriptor(0, 0);   // a mesh with nothing at all still has one (empty) submesh

            int vertex = 0, index = 0, slot = 0;
            bool anyBounds = false;
            Vector3 meshMin = Vector3.zero, meshMax = Vector3.zero;
            for (int g = 0; g < groups.Count; g++)
            {
                int indexStart = index;
                int lowest = int.MaxValue, highest = -1;
                foreach (MeshBuffer buffer in groups[g])
                {
                    int count = buffer.Vertices.Count;
                    buffer.Vertices.CopyTo(positions, vertex);
                    buffer.Normals.CopyTo(normals, vertex);
                    List<Color32> colors = buffer.Colors;
                    List<Vector2> uvs = buffer.Uvs;
                    for (int i = 0; i < count; i++)
                    {
                        colorsAndUvs[vertex + i] = new ColorAndUv { Color = colors[i], Uv = uvs[i] };
                    }
                    // Each buffer's indices count from its own first vertex: shifted by the vertices that came before it.
                    List<int> triangles = buffer.Triangles;
                    for (int i = 0; i < triangles.Count; i++)
                    {
                        int shifted = triangles[i] + vertex;
                        indices[index + i] = shifted;
                        if (shifted < lowest) lowest = shifted;
                        if (shifted > highest) highest = shifted;
                    }
                    vertex += count;
                    index += triangles.Count;
                }
                if (index == indexStart)
                {
                    continue;
                }

                // The vertices this submesh uses and their bounds, as Mesh.SetSubMesh would work them out.
                Vector3 min = positions[lowest], max = min;
                for (int i = lowest + 1; i <= highest; i++)
                {
                    Vector3 p = positions[i];
                    if (p.x < min.x) min.x = p.x;
                    if (p.y < min.y) min.y = p.y;
                    if (p.z < min.z) min.z = p.z;
                    if (p.x > max.x) max.x = p.x;
                    if (p.y > max.y) max.y = p.y;
                    if (p.z > max.z) max.z = p.z;
                }
                subMeshes[slot] = new SubMeshDescriptor(indexStart, index - indexStart)
                {
                    firstVertex = lowest,
                    vertexCount = highest - lowest + 1,
                    bounds = FromMinMax(min, max),
                };
                subMeshGroups[slot] = g;
                slot++;
                meshMin = anyBounds ? Vector3.Min(meshMin, min) : min;
                meshMax = anyBounds ? Vector3.Max(meshMax, max) : max;
                anyBounds = true;
            }

            Vector4[] tangents = ChunkArrayPool<Vector4>.Rent(vertexCount);
            MeshTangents.Compute(positions, normals, colorsAndUvs, vertexCount, indices, indexCount, tangents);
            ushort[] indices16 = null;
            int[] indices32 = indices;
            if (vertexCount <= MaxVerticesFor16BitIndices)
            {
                indices16 = ChunkArrayPool<ushort>.Rent(indexCount);
                for (int i = 0; i < indexCount; i++)
                {
                    indices16[i] = (ushort)indices[i];
                }
                ChunkArrayPool<int>.Return(indices);
                indices32 = null;
            }
            return new ChunkMeshData(vertexCount, indexCount, positions, normals, tangents, colorsAndUvs, indices16, indices32,
                subMeshes, subMeshGroups, anyBounds ? FromMinMax(meshMin, meshMax) : default);
        }

        /// <summary>As Unity works bounds out from the corners: the centre halfway between them, the size the span.</summary>
        private static Bounds FromMinMax(Vector3 min, Vector3 max)
        {
            return new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>
        /// Fills a new mesh with it (main thread): the vertex layout once, one copy per vertex stream, one for every submesh's indices, the
        /// submeshes and the bounds as they are.
        /// </summary>
        public void ApplyTo(Mesh mesh)
        {
            if (_released)
            {
                throw new System.InvalidOperationException("ChunkMeshData used after Release: its arrays may already hold another chunk");
            }
            mesh.SetVertexBufferParams(VertexCount, Layout);
            if (VertexCount > 0)
            {
                mesh.SetVertexBufferData(Positions, 0, 0, VertexCount, 0, Trusted);
                mesh.SetVertexBufferData(Normals, 0, 0, VertexCount, 1, Trusted);
                mesh.SetVertexBufferData(Tangents, 0, 0, VertexCount, 2, Trusted);
                mesh.SetVertexBufferData(ColorsAndUvs, 0, 0, VertexCount, 3, Trusted);
            }
            if (Indices32 != null)
            {
                mesh.SetIndexBufferParams(IndexCount, IndexFormat.UInt32);
                mesh.SetIndexBufferData(Indices32, 0, 0, IndexCount, Trusted);
            }
            else
            {
                mesh.SetIndexBufferParams(IndexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(Indices16, 0, 0, IndexCount, Trusted);
            }
            mesh.SetSubMeshes(SubMeshes, 0, SubMeshes.Length, Trusted);
            mesh.bounds = Bounds;
        }

        private bool _released;

        /// <summary>
        /// Gives its arrays back to ChunkArrayPool for the next chunk (the Mesh made from it keeps its own copy). Nothing may read it after
        /// this; a second call does nothing. Any thread.
        /// </summary>
        public void Release()
        {
            if (_released)
            {
                return;
            }
            _released = true;
            ChunkArrayPool<Vector3>.Return(Positions);
            ChunkArrayPool<Vector3>.Return(Normals);
            ChunkArrayPool<Vector4>.Return(Tangents);
            ChunkArrayPool<ColorAndUv>.Return(ColorsAndUvs);
            ChunkArrayPool<ushort>.Return(Indices16);
            ChunkArrayPool<int>.Return(Indices32);
        }
    }
}
