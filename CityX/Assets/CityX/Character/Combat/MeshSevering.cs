using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A skinned mesh cut in two by its bones (docs/DISMEMBERMENT.md), with no scene: every vertex goes with the bone that moves it most, every
    /// triangle with the side most of its corners went to, so the cut runs where the cut-off bones take over. What stays is the same skinned
    /// mesh without those triangles (same vertices, same weights), so it keeps following the skeleton; what goes is a plain mesh of them in the
    /// pose the body is in now (from a baked copy), compacted to the vertices it uses. The cut's middle and radius, from the corners along its
    /// edge, size the stump. <see cref="Carve"/> cuts a mesh into any number of pieces the same way at once (a whole body bursting).
    /// </summary>
    public static class MeshSevering
    {
        /// <summary>The stump's radius over the edge's mean distance from the middle: a little more, so the cap covers the hole.</summary>
        public const float CapCover = 1.2f;

        public sealed class Cut
        {
            /// <summary>The skinned mesh without the cut-off triangles (a new mesh; the shared one is untouched).</summary>
            public Mesh Kept;

            /// <summary>The cut-off triangles, baked in the pose the body was in, in the renderer's space; null when nothing went.</summary>
            public Mesh Piece;

            /// <summary>The middle of the cut and how far its edge reaches from it, in the renderer's space; radius 0 when the cut has no edge.</summary>
            public Vector3 Centre;

            public float Radius;

            public int RemovedTriangles;
        }

        /// <summary>A mesh carved into pieces: each group's piece (null when it has no triangle) and its cut's middle and radius.</summary>
        public sealed class Carving
        {
            public Mesh[] Pieces;
            public Vector3[] Centres;
            public float[] Radii;
        }

        /// <summary>
        /// Which vertices go: those whose heaviest bone is one of <paramref name="cutOff"/> (bone indices into the renderer's bones).
        /// </summary>
        public static bool[] Going(BoneWeight[] weights, HashSet<int> cutOff)
        {
            var going = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                going[i] = cutOff.Contains(Heaviest(weights[i]));
            }
            return going;
        }

        /// <summary>The bone (an index into the renderer's bones) that moves a vertex most.</summary>
        public static int Heaviest(BoneWeight w)
        {
            int heaviest = w.boneIndex0;
            float most = w.weight0;
            if (w.weight1 > most) { most = w.weight1; heaviest = w.boneIndex1; }
            if (w.weight2 > most) { most = w.weight2; heaviest = w.boneIndex2; }
            if (w.weight3 > most) { heaviest = w.boneIndex3; }
            return heaviest;
        }

        /// <summary>
        /// Cuts <paramref name="shared"/> where <paramref name="going"/> says, the piece taken from <paramref name="baked"/> (the same mesh baked
        /// in its current pose: SkinnedMeshRenderer.BakeMesh). Returns null when no triangle goes.
        /// </summary>
        public static Cut Split(Mesh shared, Mesh baked, bool[] going)
        {
            int subMeshes = shared.subMeshCount;
            var keptTriangles = new List<int>[subMeshes];
            var pieceTriangles = new List<int>[subMeshes];
            var edge = new HashSet<int>();
            int removed = 0;
            for (int s = 0; s < subMeshes; s++)
            {
                int[] triangles = shared.GetTriangles(s);
                keptTriangles[s] = new List<int>(triangles.Length);
                pieceTriangles[s] = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    int count = (going[a] ? 1 : 0) + (going[b] ? 1 : 0) + (going[c] ? 1 : 0);
                    List<int> into = count >= 2 ? pieceTriangles[s] : keptTriangles[s];
                    into.Add(a);
                    into.Add(b);
                    into.Add(c);
                    if (count >= 2)
                    {
                        removed++;
                    }
                    if (count == 1 || count == 2)
                    {
                        edge.Add(a);
                        edge.Add(b);
                        edge.Add(c);
                    }
                }
            }
            if (removed == 0)
            {
                return null;
            }

            Mesh kept = Object.Instantiate(shared);
            kept.name = shared.name + " (cut)";
            for (int s = 0; s < subMeshes; s++)
            {
                kept.SetTriangles(keptTriangles[s], s, false);
            }
            kept.RecalculateBounds();

            Vector3[] positions = baked.vertices;
            EdgeCircle(positions, edge, out Vector3 centre, out float radius);
            return new Cut
            {
                Kept = kept,
                Piece = Compact(shared, positions, baked.normals, shared.uv, pieceTriangles, shared.name + " (piece)"),
                Centre = centre,
                Radius = radius,
                RemovedTriangles = removed,
            };
        }

        /// <summary>
        /// Cuts <paramref name="shared"/> into <paramref name="groups"/> pieces at once by <paramref name="groupOf"/> (each vertex's group): a
        /// triangle goes with the group most of its corners are in, or its first corner's when all three differ. Each piece is baked from
        /// <paramref name="baked"/> as <see cref="Split"/>'s is, and each group's cut measured from the corners along its edges with the others.
        /// </summary>
        public static Carving Carve(Mesh shared, Mesh baked, int[] groupOf, int groups)
        {
            int subMeshes = shared.subMeshCount;
            var pieceTriangles = new List<int>[groups][];
            var edges = new HashSet<int>[groups];
            for (int g = 0; g < groups; g++)
            {
                pieceTriangles[g] = new List<int>[subMeshes];
                for (int s = 0; s < subMeshes; s++)
                {
                    pieceTriangles[g][s] = new List<int>();
                }
                edges[g] = new HashSet<int>();
            }
            var counted = new int[groups];
            for (int s = 0; s < subMeshes; s++)
            {
                int[] triangles = shared.GetTriangles(s);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    int ga = groupOf[a], gb = groupOf[b], gc = groupOf[c];
                    int g = ga == gb || ga == gc ? ga : gb == gc ? gb : ga;
                    List<int> into = pieceTriangles[g][s];
                    into.Add(a);
                    into.Add(b);
                    into.Add(c);
                    counted[g]++;
                    if (ga != gb || gb != gc)
                    {
                        foreach (int h in new[] { ga, gb, gc })
                        {
                            edges[h].Add(a);
                            edges[h].Add(b);
                            edges[h].Add(c);
                        }
                    }
                }
            }

            Vector3[] positions = baked.vertices;
            Vector3[] normals = baked.normals;
            Vector2[] uvs = shared.uv;
            var carving = new Carving { Pieces = new Mesh[groups], Centres = new Vector3[groups], Radii = new float[groups] };
            for (int g = 0; g < groups; g++)
            {
                if (counted[g] == 0)
                {
                    continue;
                }
                carving.Pieces[g] = Compact(shared, positions, normals, uvs, pieceTriangles[g], $"{shared.name} (piece {g})");
                EdgeCircle(positions, edges[g], out carving.Centres[g], out carving.Radii[g]);
            }
            return carving;
        }

        /// <summary>A plain mesh of the given triangles, its corners taken from the baked pose and compacted to the ones it uses.</summary>
        private static Mesh Compact(Mesh shared, Vector3[] positions, Vector3[] normals, Vector2[] uvs, List<int>[] triangles, string name)
        {
            int subMeshes = triangles.Length;
            var remap = new Dictionary<int, int>();
            var piecePositions = new List<Vector3>();
            var pieceNormals = new List<Vector3>();
            var pieceUvs = new List<Vector2>();
            var compacted = new List<int>[subMeshes];
            for (int s = 0; s < subMeshes; s++)
            {
                compacted[s] = new List<int>(triangles[s].Count);
                foreach (int index in triangles[s])
                {
                    if (!remap.TryGetValue(index, out int to))
                    {
                        to = piecePositions.Count;
                        remap[index] = to;
                        piecePositions.Add(positions[index]);
                        pieceNormals.Add(normals.Length > index ? normals[index] : Vector3.up);
                        pieceUvs.Add(uvs.Length > index ? uvs[index] : Vector2.zero);
                    }
                    compacted[s].Add(to);
                }
            }
            var piece = new Mesh { name = name };
            piece.indexFormat = piecePositions.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            piece.SetVertices(piecePositions);
            piece.SetNormals(pieceNormals);
            piece.SetUVs(0, pieceUvs);
            piece.subMeshCount = subMeshes;
            for (int s = 0; s < subMeshes; s++)
            {
                piece.SetTriangles(compacted[s], s, false);
            }
            piece.RecalculateBounds();
            return piece;
        }

        /// <summary>The middle of a cut's edge corners and the stump radius that covers them (0 with no edge).</summary>
        private static void EdgeCircle(Vector3[] positions, HashSet<int> edge, out Vector3 centre, out float radius)
        {
            centre = Vector3.zero;
            radius = 0.0f;
            if (edge.Count == 0)
            {
                return;
            }
            foreach (int index in edge)
            {
                centre += positions[index];
            }
            centre /= edge.Count;
            foreach (int index in edge)
            {
                radius += Vector3.Distance(positions[index], centre);
            }
            radius = radius / edge.Count * CapCover;
        }
    }
}
