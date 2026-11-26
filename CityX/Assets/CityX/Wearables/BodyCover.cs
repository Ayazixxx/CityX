using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Wearables
{
    /// <summary>
    /// The body's skin with the worn garments' hidden triangles left out (SkinnedGarment.HiddenBodyTriangles, one bit per triangle). Made once
    /// per body mesh and set of garments, shared by everyone wearing them.
    ///
    /// Only the points the shown triangles use are kept (2026-11-25, docs/CROWD_TEST.md): the graphics card bends every point of a skinned mesh
    /// every frame, drawn or not, and a dressed body hides most of its skin, so a crowd of dressed bodies bent all of it for nothing. Whatever
    /// needs the whole body (a cape's fit, gloves made from the hands) asks WornBodyCover.Whole.
    /// </summary>
    public static class BodyCover
    {
        private static readonly Dictionary<(Mesh body, byte[] mask), Mesh> Made = new Dictionary<(Mesh, byte[]), Mesh>();

        /// <summary>The masks combined for several garments worn at once, kept so the same set cuts the same mesh again.</summary>
        private static readonly List<(byte[][] masks, byte[] combined)> Combined = new List<(byte[][], byte[])>();

        /// <summary>Whether triangle <paramref name="triangle"/> is set in <paramref name="mask"/>.</summary>
        public static bool IsHidden(byte[] mask, int triangle) => (mask[triangle / 8] & (1 << (triangle % 8))) != 0;

        /// <summary>
        /// <paramref name="body"/> without the masked triangles; the body itself when the mask was made for a body with another triangle count
        /// (a rebuilt body the garment was not rebuilt for), so a stale mask never cuts the wrong holes.
        /// </summary>
        public static Mesh Cut(Mesh body, byte[] mask, int madeForTriangles)
        {
            if (body == null || mask == null || mask.Length == 0)
            {
                return body;
            }
            if (Made.TryGetValue((body, mask), out Mesh made) && made != null)
            {
                return made;
            }
            int[] triangles = body.triangles;
            if (!Fits(triangles.Length / 3, mask, madeForTriangles))
            {
                return body;
            }
            var shown = new List<int>(triangles.Length);
            for (int t = 0; t < madeForTriangles; t++)
            {
                if (!IsHidden(mask, t))
                {
                    shown.Add(triangles[t * 3]);
                    shown.Add(triangles[t * 3 + 1]);
                    shown.Add(triangles[t * 3 + 2]);
                }
            }
            made = Compact(body, shown);
            made.name = body.name + "Covered";
            Made[(body, mask)] = made;
            return made;
        }

        /// <summary>
        /// <paramref name="body"/> with only the points <paramref name="shown"/> (its triangles' corners, three a triangle) uses, renumbered:
        /// every per-point stream carried over (place, facing, tangents, colours, each set of texture coordinates, bone weights however many a
        /// point has, blend shapes), the bind poses and the bounds as they were.
        /// </summary>
        public static Mesh Compact(Mesh body, List<int> shown)
        {
            int count = body.vertexCount;
            var renumber = new int[count];
            for (int i = 0; i < count; i++) renumber[i] = -1;
            var kept = new List<int>(count);
            var triangles = new int[shown.Count];
            for (int i = 0; i < shown.Count; i++)
            {
                int v = shown[i];
                if (renumber[v] < 0)
                {
                    renumber[v] = kept.Count;
                    kept.Add(v);
                }
                triangles[i] = renumber[v];
            }

            var made = new Mesh { indexFormat = kept.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            made.SetVertices(Pick(body.vertices, kept));
            Vector3[] normals = body.normals;
            if (normals.Length == count) made.SetNormals(Pick(normals, kept));
            Vector4[] tangents = body.tangents;
            if (tangents.Length == count) made.SetTangents(Pick(tangents, kept));
            Color32[] colours = body.colors32;
            if (colours.Length == count) made.SetColors(Pick(colours, kept));
            for (int channel = 0; channel < 8; channel++)
            {
                if (!body.HasVertexAttribute(VertexAttribute.TexCoord0 + channel)) continue;
                var uvs = new List<Vector4>(count);
                body.GetUVs(channel, uvs);
                switch (body.GetVertexAttributeDimension(VertexAttribute.TexCoord0 + channel))
                {
                    case 2: made.SetUVs(channel, Pick(uvs, kept).ConvertAll(uv => (Vector2)uv)); break;
                    case 3: made.SetUVs(channel, Pick(uvs, kept).ConvertAll(uv => (Vector3)uv)); break;
                    default: made.SetUVs(channel, Pick(uvs, kept)); break;
                }
            }

            NativeArray<byte> perPoint = body.GetBonesPerVertex();
            if (perPoint.Length == count)
            {
                NativeArray<BoneWeight1> all = body.GetAllBoneWeights();
                var start = new int[count];
                for (int i = 1; i < count; i++) start[i] = start[i - 1] + perPoint[i - 1];
                var keptPerPoint = new NativeArray<byte>(kept.Count, Allocator.Temp);
                int total = 0;
                for (int k = 0; k < kept.Count; k++)
                {
                    keptPerPoint[k] = perPoint[kept[k]];
                    total += perPoint[kept[k]];
                }
                var keptWeights = new NativeArray<BoneWeight1>(total, Allocator.Temp);
                int at = 0;
                foreach (int v in kept)
                {
                    for (int w = 0; w < perPoint[v]; w++) keptWeights[at++] = all[start[v] + w];
                }
                made.SetBoneWeights(keptPerPoint, keptWeights);
                keptPerPoint.Dispose();
                keptWeights.Dispose();
            }
            made.bindposes = body.bindposes;

            var deltas = new Vector3[count];
            var deltaNormals = new Vector3[count];
            var deltaTangents = new Vector3[count];
            for (int shape = 0; shape < body.blendShapeCount; shape++)
            {
                for (int frame = 0; frame < body.GetBlendShapeFrameCount(shape); frame++)
                {
                    body.GetBlendShapeFrameVertices(shape, frame, deltas, deltaNormals, deltaTangents);
                    made.AddBlendShapeFrame(body.GetBlendShapeName(shape), body.GetBlendShapeFrameWeight(shape, frame),
                        Pick(deltas, kept).ToArray(), Pick(deltaNormals, kept).ToArray(), Pick(deltaTangents, kept).ToArray());
                }
            }

            made.SetTriangles(triangles, 0, false);
            made.bounds = body.bounds;
            return made;
        }

        private static List<T> Pick<T>(IList<T> from, List<int> kept)
        {
            var picked = new List<T>(kept.Count);
            foreach (int v in kept) picked.Add(from[v]);
            return picked;
        }

        /// <summary>
        /// <paramref name="body"/> without what any of <paramref name="garments"/> hides (a shirt and jeans worn together): their masks made
        /// for this body combined. Masks made for another body are left out, as <see cref="Cut(Mesh, byte[], int)"/> leaves out a lone one.
        /// </summary>
        public static Mesh Cut(Mesh body, IReadOnlyList<(byte[] mask, int madeForTriangles)> garments)
        {
            if (body == null)
            {
                return null;
            }
            int bodyTriangles = body.triangles.Length / 3;
            var masks = new List<byte[]>();
            foreach ((byte[] mask, int madeFor) in garments)
            {
                if (mask != null && Fits(bodyTriangles, mask, madeFor) && !masks.Contains(mask))
                {
                    masks.Add(mask);
                }
            }
            if (masks.Count == 0)
            {
                return body;
            }
            return Cut(body, masks.Count == 1 ? masks[0] : Union(masks), bodyTriangles);
        }

        private static bool Fits(int bodyTriangles, byte[] mask, int madeForTriangles) =>
            mask.Length > 0 && bodyTriangles == madeForTriangles && mask.Length * 8 >= madeForTriangles;

        /// <summary>Every triangle any of <paramref name="masks"/> hides; the same array for the same set of masks, in any order.</summary>
        private static byte[] Union(List<byte[]> masks)
        {
            foreach ((byte[][] set, byte[] combined) in Combined)
            {
                if (set.Length == masks.Count && System.Array.TrueForAll(set, masks.Contains))
                {
                    return combined;
                }
            }
            int length = 0;
            foreach (byte[] mask in masks)
            {
                length = Mathf.Max(length, mask.Length);
            }
            var union = new byte[length];
            foreach (byte[] mask in masks)
            {
                for (int i = 0; i < mask.Length; i++)
                {
                    union[i] |= mask[i];
                }
            }
            Combined.Add((masks.ToArray(), union));
            return union;
        }
    }
}
