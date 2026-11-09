using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A body part burst into pieces of itself (docs/DISMEMBERMENT.md), with no scene. The part's own surfaces (its skin or skull, the clothes
    /// and hair on it), baked in its pose and in world space, are split into chunks of about even size: each chunk is the patch of every surface
    /// nearest one of a few seeds spread over the part, so its outside is that body's own skin and clothes. Behind a patch of flesh (the skin,
    /// the skull, or clothes with hardly any skin under them) a layer of meat is built, as thick as the chunk is big: round the torn edge a pale
    /// line of fat under the skin and red meat below it, darker meat on the inside, closed all round. A patch of cloth or hair over skin gets a
    /// thin back of its own. Games whose gore reads as people do the same (Fallout's meat caps, Dead Island 2's skin, fat, muscle): the outside is
    /// the victim's own, the torn face is meat. The same surfaces and the same random numbers make the same chunks on every screen.
    /// </summary>
    public static class GoreShatter
    {
        public enum Layer
        {
            Flesh,
            Cloth,
            Hair,
        }

        /// <summary>The meat behind a flesh patch is this share of the chunk's own radius thick, within these bounds at the body's scale, m.</summary>
        public const float MeatShare = 0.3f;

        public const float MinMeat = 0.015f;
        public const float MaxMeat = 0.06f;

        /// <summary>The fat line: this outer share of the meat's torn edge.</summary>
        public const float FatShare = 0.22f;

        /// <summary>How thick a patch of cloth or hair is, m at the body's scale.</summary>
        public const float ClothThickness = 0.006f;

        /// <summary>Clothes are the flesh of a chunk when the skin under them is less than this share of their area (a garment hides the skin it covers).</summary>
        public const float SkinUnderClothShare = 0.25f;

        /// <summary>How often the seeds are moved to the middles of their chunks, evening the chunks out (Lloyd's steps).</summary>
        public const int SpreadSteps = 2;

        /// <summary>The meat mesh's submeshes: the fat line, the meat round the edge, the inside.</summary>
        public const int FatSubmesh = 0;

        public const int MeatSubmesh = 1;
        public const int InsideSubmesh = 2;

        /// <summary>Corners closer than this are one corner when the torn edge is found (a mesh repeats a corner along its UV seams), m.</summary>
        private const float WeldStep = 1e-4f;

        public sealed class Surface
        {
            /// <summary>Corners and their normals in world space, as baked; texture coordinates (may be empty).</summary>
            public Vector3[] Positions;

            public Vector3[] Normals;
            public Vector2[] Uvs;

            /// <summary>The triangles of each submesh (each submesh keeps its material).</summary>
            public int[][] Triangles;

            public Layer Layer;
        }

        public sealed class Patch
        {
            /// <summary>Which of the surfaces this is a piece of.</summary>
            public int Surface;

            /// <summary>Its triangles (with a back, for cloth or hair over skin) in the chunk's space, in the surface's own submeshes.</summary>
            public Mesh Mesh;
        }

        public sealed class Chunk
        {
            /// <summary>Its middle in world space: the chunk's space is this point, unturned.</summary>
            public Vector3 Centre;

            /// <summary>How far its outside reaches, m: the radius of a disc of the same area.</summary>
            public float Radius;

            public float Area;

            /// <summary>How thick the meat behind its flesh is, m.</summary>
            public float Thickness;

            public readonly List<Patch> Patches = new List<Patch>();

            /// <summary>The fat, meat and inside behind its flesh (<see cref="FatSubmesh"/>...); null when it has no flesh.</summary>
            public Mesh Meat;

            /// <summary>What it covers in its own space.</summary>
            public Bounds Bounds;
        }

        private struct Triangle
        {
            public int Surface;
            public int Submesh;
            public int A, B, C;
            public Vector3 Middle;
            public float Area;
        }

        /// <summary>
        /// <paramref name="surfaces"/> split into at most <paramref name="count"/> chunks, at a body of <paramref name="scale"/> (1 = the player's
        /// 2.6 m). The chunks own their meshes.
        /// </summary>
        public static List<Chunk> Shatter(IList<Surface> surfaces, int count, float scale, System.Random random)
        {
            var chunks = new List<Chunk>();
            List<Triangle> triangles = Triangles(surfaces);
            if (triangles.Count == 0 || count <= 0)
            {
                return chunks;
            }
            count = Mathf.Min(count, triangles.Count);
            Vector3[] seeds = Seeds(triangles, count, random);
            int[] owner = new int[triangles.Count];
            for (int step = 0; step <= SpreadSteps; step++)
            {
                Assign(triangles, seeds, owner);
                if (step < SpreadSteps)
                {
                    Spread(triangles, seeds, owner);
                }
            }

            var members = new List<int>[seeds.Length];
            for (int i = 0; i < members.Length; i++)
            {
                members[i] = new List<int>();
            }
            for (int i = 0; i < triangles.Count; i++)
            {
                members[owner[i]].Add(i);
            }
            foreach (List<int> chunkTriangles in members)
            {
                if (chunkTriangles.Count > 0)
                {
                    chunks.Add(Build(surfaces, triangles, chunkTriangles, scale));
                }
            }
            return chunks;
        }

        private static List<Triangle> Triangles(IList<Surface> surfaces)
        {
            var triangles = new List<Triangle>();
            for (int s = 0; s < surfaces.Count; s++)
            {
                Surface surface = surfaces[s];
                if (surface?.Positions == null || surface.Triangles == null)
                {
                    continue;
                }
                Vector3[] p = surface.Positions;
                for (int m = 0; m < surface.Triangles.Length; m++)
                {
                    int[] indices = surface.Triangles[m];
                    if (indices == null)
                    {
                        continue;
                    }
                    for (int t = 0; t + 2 < indices.Length; t += 3)
                    {
                        int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                        triangles.Add(new Triangle
                        {
                            Surface = s,
                            Submesh = m,
                            A = a,
                            B = b,
                            C = c,
                            Middle = (p[a] + p[b] + p[c]) / 3.0f,
                            Area = 0.5f * Vector3.Cross(p[b] - p[a], p[c] - p[a]).magnitude,
                        });
                    }
                }
            }
            return triangles;
        }

        /// <summary>The first seeds, spread out: each next one picked with odds by area times the square of its distance from the nearest seed so far.</summary>
        private static Vector3[] Seeds(List<Triangle> triangles, int count, System.Random random)
        {
            var seeds = new Vector3[count];
            var nearest = new float[triangles.Count];
            seeds[0] = triangles[Pick(triangles, null, random)].Middle;
            for (int i = 0; i < triangles.Count; i++)
            {
                nearest[i] = (triangles[i].Middle - seeds[0]).sqrMagnitude;
            }
            for (int k = 1; k < count; k++)
            {
                seeds[k] = triangles[Pick(triangles, nearest, random)].Middle;
                for (int i = 0; i < triangles.Count; i++)
                {
                    nearest[i] = Mathf.Min(nearest[i], (triangles[i].Middle - seeds[k]).sqrMagnitude);
                }
            }
            return seeds;
        }

        private static int Pick(List<Triangle> triangles, float[] weights, System.Random random)
        {
            double total = 0.0;
            for (int i = 0; i < triangles.Count; i++)
            {
                total += triangles[i].Area * (weights != null ? weights[i] : 1.0f);
            }
            if (total <= 0.0)
            {
                return random.Next(triangles.Count);
            }
            double target = random.NextDouble() * total;
            for (int i = 0; i < triangles.Count; i++)
            {
                target -= triangles[i].Area * (weights != null ? weights[i] : 1.0f);
                if (target <= 0.0)
                {
                    return i;
                }
            }
            return triangles.Count - 1;
        }

        private static void Assign(List<Triangle> triangles, Vector3[] seeds, int[] owner)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                Vector3 middle = triangles[i].Middle;
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int k = 0; k < seeds.Length; k++)
                {
                    float distance = (middle - seeds[k]).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = k;
                    }
                }
                owner[i] = best;
            }
        }

        /// <summary>Each seed moved to the middle of its chunk, then onto the nearest of the chunk's own triangles (a middle can fall inside the body).</summary>
        private static void Spread(List<Triangle> triangles, Vector3[] seeds, int[] owner)
        {
            var sum = new Vector3[seeds.Length];
            var weight = new float[seeds.Length];
            for (int i = 0; i < triangles.Count; i++)
            {
                float area = Mathf.Max(triangles[i].Area, 1e-9f);
                sum[owner[i]] += triangles[i].Middle * area;
                weight[owner[i]] += area;
            }
            var snapped = new float[seeds.Length];
            var middles = new Vector3[seeds.Length];
            for (int k = 0; k < seeds.Length; k++)
            {
                middles[k] = weight[k] > 0.0f ? sum[k] / weight[k] : seeds[k];
                snapped[k] = float.MaxValue;
            }
            for (int i = 0; i < triangles.Count; i++)
            {
                int k = owner[i];
                float distance = (triangles[i].Middle - middles[k]).sqrMagnitude;
                if (distance < snapped[k])
                {
                    snapped[k] = distance;
                    seeds[k] = triangles[i].Middle;
                }
            }
        }

        private static Chunk Build(IList<Surface> surfaces, List<Triangle> triangles, List<int> chunkTriangles, float scale)
        {
            var chunk = new Chunk();
            Vector3 sum = Vector3.zero;
            float area = 0.0f, fleshArea = 0.0f;
            var areaBySurface = new Dictionary<int, float>();
            foreach (int i in chunkTriangles)
            {
                Triangle triangle = triangles[i];
                float weight = Mathf.Max(triangle.Area, 1e-9f);
                sum += triangle.Middle * weight;
                area += triangle.Area;
                areaBySurface.TryGetValue(triangle.Surface, out float surfaceArea);
                areaBySurface[triangle.Surface] = surfaceArea + triangle.Area;
                if (surfaces[triangle.Surface].Layer == Layer.Flesh)
                {
                    fleshArea += triangle.Area;
                }
            }
            float weights = 0.0f;
            foreach (int i in chunkTriangles)
            {
                weights += Mathf.Max(triangles[i].Area, 1e-9f);
            }
            chunk.Centre = sum / weights;
            chunk.Area = area;
            chunk.Radius = Mathf.Sqrt(area / Mathf.PI);
            float meat = Mathf.Clamp(MeatShare * chunk.Radius, MinMeat * scale, MaxMeat * scale);
            chunk.Thickness = meat;

            var meatBuilder = new GoreMeshes.Builder(3);
            var bySurface = new SortedDictionary<int, List<int>>();
            foreach (int i in chunkTriangles)
            {
                if (!bySurface.TryGetValue(triangles[i].Surface, out List<int> list))
                {
                    list = new List<int>();
                    bySurface[triangles[i].Surface] = list;
                }
                list.Add(i);
            }
            var bounds = new Bounds();
            bool any = false;
            foreach (KeyValuePair<int, List<int>> entry in bySurface)
            {
                Surface surface = surfaces[entry.Key];
                bool flesh = surface.Layer == Layer.Flesh || fleshArea < SkinUnderClothShare * areaBySurface[entry.Key];
                GoreMeshes.Builder patch = PatchOf(surface, triangles, entry.Value, chunk.Centre, flesh ? meat : ClothThickness * scale, flesh, meatBuilder);
                Mesh mesh = patch.ToMesh("GorePatch");
                Encapsulate(ref bounds, ref any, mesh.bounds);
                chunk.Patches.Add(new Patch { Surface = entry.Key, Mesh = mesh });
            }
            if (meatBuilder.Positions.Count > 0)
            {
                chunk.Meat = meatBuilder.ToMesh("GoreMeat");
                Encapsulate(ref bounds, ref any, chunk.Meat.bounds);
            }
            chunk.Bounds = bounds;
            return chunk;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds more)
        {
            if (any)
            {
                bounds.Encapsulate(more);
            }
            else
            {
                bounds = more;
                any = true;
            }
        }

        /// <summary>
        /// One surface's share of a chunk: its triangles as they are, and behind them either meat (into <paramref name="meat"/>) or, for cloth
        /// and hair over skin, a thin back in its own submeshes. Both close the patch round its torn edge.
        /// </summary>
        private static GoreMeshes.Builder PatchOf(Surface surface, List<Triangle> triangles, List<int> members, Vector3 centre, float thickness, bool flesh,
            GoreMeshes.Builder meat)
        {
            var patch = new GoreMeshes.Builder(surface.Triangles.Length);
            var local = new Dictionary<int, int>();
            var weldOf = new Dictionary<int, int>();
            var weldKeys = new Dictionary<Vector3Int, int>();
            var weldNormals = new List<Vector3>();
            var weldPositions = new List<Vector3>();
            bool uvs = surface.Uvs != null && surface.Uvs.Length == surface.Positions.Length;

            int Corner(int index)
            {
                if (!local.TryGetValue(index, out int at))
                {
                    at = patch.Add(surface.Positions[index] - centre, surface.Normals != null && surface.Normals.Length > index ? surface.Normals[index] : Vector3.up,
                        uvs ? surface.Uvs[index] : Vector2.zero);
                    local[index] = at;
                }
                return at;
            }

            int Weld(int index)
            {
                if (weldOf.TryGetValue(index, out int weld))
                {
                    return weld;
                }
                Vector3 p = surface.Positions[index] - centre;
                var key = new Vector3Int(Mathf.RoundToInt(p.x / WeldStep), Mathf.RoundToInt(p.y / WeldStep), Mathf.RoundToInt(p.z / WeldStep));
                if (!weldKeys.TryGetValue(key, out weld))
                {
                    weld = weldPositions.Count;
                    weldKeys[key] = weld;
                    weldPositions.Add(p);
                    weldNormals.Add(Vector3.zero);
                }
                weldOf[index] = weld;
                return weld;
            }

            // The patch's own triangles, and every corner's share of a smooth normal (seams repeat a corner: welded, they agree).
            var welded = new List<(int a, int b, int c, int submesh)>(members.Count);
            foreach (int i in members)
            {
                Triangle t = triangles[i];
                patch.Triangle(t.Submesh, Corner(t.A), Corner(t.B), Corner(t.C));
                int wa = Weld(t.A), wb = Weld(t.B), wc = Weld(t.C);
                Vector3 face = Vector3.Cross(weldPositions[wb] - weldPositions[wa], weldPositions[wc] - weldPositions[wa]);
                weldNormals[wa] += face;
                weldNormals[wb] += face;
                weldNormals[wc] += face;
                welded.Add((wa, wb, wc, t.Submesh));
            }
            for (int w = 0; w < weldNormals.Count; w++)
            {
                weldNormals[w] = GoreMeshes.Direction(weldNormals[w], Vector3.up);
            }

            // The torn edge: every edge only one triangle of the patch has.
            var edgeCount = new Dictionary<(int, int), int>();
            foreach (var (a, b, c, _) in welded)
            {
                if (a == b || b == c || c == a)
                {
                    continue;
                }
                CountEdge(edgeCount, a, b);
                CountEdge(edgeCount, b, c);
                CountEdge(edgeCount, c, a);
            }

            GoreMeshes.Builder back = flesh ? meat : patch;
            int inside = flesh ? InsideSubmesh : -1;
            var deep = new Dictionary<int, int>();
            foreach ((int a, int b, int c, int submesh) in welded)
            {
                if (a == b || b == c || c == a)
                {
                    continue;
                }
                int sub = flesh ? inside : submesh;
                back.Triangle(sub, Deep(a), Deep(c), Deep(b));
                Edge(a, b, submesh);
                Edge(b, c, submesh);
                Edge(c, a, submesh);
            }
            return patch;

            int Deep(int weld)
            {
                // The far side's corners: shared by its triangles, so the far side is one smooth surface.
                if (!deep.TryGetValue(weld, out int at))
                {
                    at = back.Add(weldPositions[weld] - weldNormals[weld] * thickness, -weldNormals[weld], Vector2.zero);
                    deep[weld] = at;
                }
                return at;
            }

            void Edge(int a, int b, int submesh)
            {
                if (a == b || !edgeCount.TryGetValue(Key(a, b), out int seen) || seen != 1)
                {
                    return;
                }
                if (flesh)
                {
                    float fat = thickness * FatShare;
                    Band(meat, FatSubmesh, a, b, 0.0f, fat);
                    Band(meat, MeatSubmesh, a, b, fat, thickness);
                }
                else
                {
                    Band(patch, submesh, a, b, 0.0f, thickness);
                }
            }

            void Band(GoreMeshes.Builder into, int submesh, int a, int b, float from, float to)
            {
                // A strip down the torn edge from a to b, facing out of the patch (away from the triangle that has the edge).
                Vector3 ua = weldPositions[a] - weldNormals[a] * from, ub = weldPositions[b] - weldNormals[b] * from;
                Vector3 la = weldPositions[a] - weldNormals[a] * to, lb = weldPositions[b] - weldNormals[b] * to;
                Vector3 normal = GoreMeshes.Direction(Vector3.Cross(lb - ua, ub - ua), Vector3.up);
                int iua = into.Add(ua, normal, Vector2.zero), iub = into.Add(ub, normal, Vector2.zero);
                int ila = into.Add(la, normal, Vector2.zero), ilb = into.Add(lb, normal, Vector2.zero);
                into.Triangle(submesh, iua, ilb, iub);
                into.Triangle(submesh, iua, ila, ilb);
            }
        }

        private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        private static void CountEdge(Dictionary<(int, int), int> count, int a, int b)
        {
            if (a == b)
            {
                return;
            }
            (int, int) key = Key(a, b);
            count.TryGetValue(key, out int seen);
            count[key] = seen + 1;
        }
    }
}
