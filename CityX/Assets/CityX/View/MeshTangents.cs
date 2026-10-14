using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A mesh's tangents worked out from plain arrays, so it can run on a worker thread (Mesh.RecalculateTangents can only run on the main
    /// thread, and on a city chunk it cost about 5 ms there every time one was built or rebuilt: measured 2026-10-14 while an AK-47 sprayed a
    /// row of windows, one of the two halves of each 17 ms hitch). The classic per-triangle method (Lengyel): each triangle's direction of
    /// increasing U (and V) in space is added to its three corners, then each corner's sum is made square to its normal; w says which way the
    /// bitangent runs (the facade shader builds it as w * cross(normal, tangent)). On the flat faces a city is made of this is what Unity's
    /// own gives (MeshTangentsTests compares them).
    /// </summary>
    public static class MeshTangents
    {
        public static Vector4[] Compute(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[][] submeshTriangles)
        {
            int count = vertices.Length;
            var tangents = new Vector4[count];
            if (normals == null || uvs == null || normals.Length != count || uvs.Length != count)
            {
                for (int i = 0; i < count; i++)
                {
                    tangents[i] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                }
                return tangents;
            }
            var along = new Vector3[count];
            var up = new Vector3[count];
            foreach (int[] triangles in submeshTriangles)
            {
                if (triangles != null)
                {
                    Accumulate(vertices, uvs, triangles, triangles.Length, along, up);
                }
            }
            Finish(normals, along, up, count, tangents);
            return tangents;
        }

        /// <summary>
        /// The same for a chunk's main mesh as ChunkMeshData lays it out: the first <paramref name="vertexCount"/> vertices, their UVs read
        /// from the colour-and-UV stream, the first <paramref name="indexCount"/> indices. Allocates nothing and holds as little as it can:
        /// each corner's U directions are summed in <paramref name="tangents"/> itself before it is made the tangent, and only the V sums
        /// need a working array (from ChunkArrayPool; the streamer's arrays are used again, and are longer than what is in them). The same
        /// sums in the same order as above: the same tangents, to the bit.
        /// </summary>
        public static void Compute(Vector3[] vertices, Vector3[] normals, ChunkMeshData.ColorAndUv[] colorsAndUvs, int vertexCount, int[] indices,
            int indexCount, Vector4[] tangents)
        {
            Vector3[] up = ChunkArrayPool<Vector3>.Rent(vertexCount);
            System.Array.Clear(up, 0, vertexCount);
            System.Array.Clear(tangents, 0, vertexCount);
            for (int t = 0; t + 2 < indexCount; t += 3)
            {
                int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                Vector3 e1 = vertices[b] - vertices[a];
                Vector3 e2 = vertices[c] - vertices[a];
                Vector2 d1 = colorsAndUvs[b].Uv - colorsAndUvs[a].Uv;
                Vector2 d2 = colorsAndUvs[c].Uv - colorsAndUvs[a].Uv;
                float determinant = d1.x * d2.y - d2.x * d1.y;
                if (Mathf.Abs(determinant) < 1e-12f)
                {
                    continue;   // no area in UV: it says nothing about which way U runs
                }
                float r = 1.0f / determinant;
                Vector3 sdir = (e1 * d2.y - e2 * d1.y) * r;
                Vector3 tdir = (e2 * d1.x - e1 * d2.x) * r;
                AddTo(ref tangents[a], sdir);
                AddTo(ref tangents[b], sdir);
                AddTo(ref tangents[c], sdir);
                up[a] += tdir;
                up[b] += tdir;
                up[c] += tdir;
            }
            for (int i = 0; i < vertexCount; i++)
            {
                Vector4 along = tangents[i];
                tangents[i] = Tangent(normals[i], new Vector3(along.x, along.y, along.z), up[i]);
            }
            ChunkArrayPool<Vector3>.Return(up);
        }

        private static void AddTo(ref Vector4 sum, Vector3 direction)
        {
            sum.x += direction.x;
            sum.y += direction.y;
            sum.z += direction.z;
        }

        /// <summary>Adds each triangle's directions of increasing U (<paramref name="along"/>) and V (<paramref name="up"/>) to its corners.</summary>
        private static void Accumulate(Vector3[] vertices, Vector2[] uvs, int[] triangles, int indexCount, Vector3[] along, Vector3[] up)
        {
            for (int t = 0; t + 2 < indexCount; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                Vector3 e1 = vertices[b] - vertices[a];
                Vector3 e2 = vertices[c] - vertices[a];
                Vector2 d1 = uvs[b] - uvs[a];
                Vector2 d2 = uvs[c] - uvs[a];
                float determinant = d1.x * d2.y - d2.x * d1.y;
                if (Mathf.Abs(determinant) < 1e-12f)
                {
                    continue;   // no area in UV: it says nothing about which way U runs
                }
                float r = 1.0f / determinant;
                Vector3 sdir = (e1 * d2.y - e2 * d1.y) * r;
                Vector3 tdir = (e2 * d1.x - e1 * d2.x) * r;
                along[a] += sdir;
                along[b] += sdir;
                along[c] += sdir;
                up[a] += tdir;
                up[b] += tdir;
                up[c] += tdir;
            }
        }

        /// <summary>Each corner's summed U direction made square to its normal, with the bitangent's sign in w.</summary>
        private static void Finish(Vector3[] normals, Vector3[] along, Vector3[] up, int count, Vector4[] tangents)
        {
            for (int i = 0; i < count; i++)
            {
                tangents[i] = Tangent(normals[i], along[i], up[i]);
            }
        }

        /// <summary>One corner's tangent from its normal and its summed U (<paramref name="along"/>) and V (<paramref name="up"/>) directions.</summary>
        private static Vector4 Tangent(Vector3 n, Vector3 along, Vector3 up)
        {
            Vector3 t = along - n * Vector3.Dot(n, along);
            if (t.sqrMagnitude < 1e-12f)
            {
                // Nothing to go on (no UVs across it): any direction square to the normal.
                t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right);
                if (t.sqrMagnitude < 1e-12f)
                {
                    t = Vector3.right;
                }
            }
            t.Normalize();
            float w = Vector3.Dot(Vector3.Cross(n, t), up) < 0.0f ? -1.0f : 1.0f;
            return new Vector4(t.x, t.y, t.z, w);
        }
    }
}
