using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A part burst into pieces of itself (GoreShatter, docs/DISMEMBERMENT.md), on a tube like an arm's skin: every triangle lands in one piece,
    /// every piece is closed (skin outside, a line of fat and then meat round its torn edge, meat inside), cloth over skin gets a thin back of
    /// its own and the skin the meat, cloth with no skin under it is the flesh, the pieces are of a size, and the same seed makes the same
    /// pieces.
    /// </summary>
    public sealed class GoreShatterTests
    {
        private const float Radius = 0.12f;
        private readonly List<Mesh> _made = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (Mesh mesh in _made)
            {
                Object.DestroyImmediate(mesh);
            }
            _made.Clear();
        }

        /// <summary>
        /// An open tube up the Y axis, <paramref name="rings"/> rings of <paramref name="sides"/>, facing out, with a texture seam (its first
        /// column repeated at the end, as a real mesh's seam repeats its corners).
        /// </summary>
        private static GoreShatter.Surface Tube(float radius, int rings, int sides, GoreShatter.Layer layer, float length = 0.5f)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int k = 0; k <= rings; k++)
            {
                for (int i = 0; i <= sides; i++)
                {
                    float angle = Mathf.PI * 2.0f * i / sides;
                    var outward = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                    positions.Add(outward * radius + Vector3.up * length * k / rings);
                    normals.Add(outward);
                    uvs.Add(new Vector2(i / (float)sides, k / (float)rings));
                }
            }
            var triangles = new List<int>();
            int row = sides + 1;
            for (int k = 0; k < rings; k++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int a = k * row + i, b = a + 1, c = a + row, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            return new GoreShatter.Surface
            {
                Positions = positions.ToArray(),
                Normals = normals.ToArray(),
                Uvs = uvs.ToArray(),
                Triangles = new[] { triangles.ToArray() },
                Layer = layer,
            };
        }

        private List<GoreShatter.Chunk> Shatter(int count, int seed, params GoreShatter.Surface[] surfaces)
        {
            List<GoreShatter.Chunk> chunks = GoreShatter.Shatter(surfaces, count, 1.0f, new System.Random(seed));
            foreach (GoreShatter.Chunk chunk in chunks)
            {
                foreach (GoreShatter.Patch patch in chunk.Patches)
                {
                    _made.Add(patch.Mesh);
                }
                if (chunk.Meat != null)
                {
                    _made.Add(chunk.Meat);
                }
            }
            return chunks;
        }

        /// <summary>How many edges of the meshes (corners welded by position) are not shared by exactly two triangles: 0 for a closed piece.</summary>
        internal static int OpenEdges(IEnumerable<Mesh> meshes)
        {
            var count = new Dictionary<(Vector3Int, Vector3Int), int>();
            foreach (Mesh mesh in meshes)
            {
                Vector3[] corners = mesh.vertices;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] triangles = mesh.GetTriangles(s);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Count(count, corners[triangles[t]], corners[triangles[t + 1]]);
                        Count(count, corners[triangles[t + 1]], corners[triangles[t + 2]]);
                        Count(count, corners[triangles[t + 2]], corners[triangles[t]]);
                    }
                }
            }
            int open = 0;
            foreach (int seen in count.Values)
            {
                if (seen != 2)
                {
                    open++;
                }
            }
            return open;
        }

        private static void Count(Dictionary<(Vector3Int, Vector3Int), int> count, Vector3 a, Vector3 b)
        {
            Vector3Int ka = Weld(a), kb = Weld(b);
            if (ka == kb)
            {
                return;
            }
            bool less = ka.x != kb.x ? ka.x < kb.x : ka.y != kb.y ? ka.y < kb.y : ka.z < kb.z;
            (Vector3Int, Vector3Int) key = less ? (ka, kb) : (kb, ka);
            count.TryGetValue(key, out int seen);
            count[key] = seen + 1;
        }

        private static Vector3Int Weld(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));

        /// <summary>How far a corner of a piece (in its own space) is from the tube's axis.</summary>
        private static float FromAxis(GoreShatter.Chunk chunk, Vector3 local)
        {
            Vector3 world = chunk.Centre + local;
            return new Vector2(world.x, world.z).magnitude;
        }

        /// <summary>
        /// Corners of <paramref name="mesh"/> with a position that is not a number, or a normal that is not one long: lit, either comes out as an infinite brightness that
        /// the bloom spreads into a flash (owner, 2026-11-05: "flashing lights coming from them ... like a flashlight type brightness").
        /// </summary>
        public static int BadCorners(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int bad = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                bool finite = !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
                bool unit = i < normals.Length && Mathf.Abs(normals[i].magnitude - 1.0f) < 0.01f;
                bad += finite && unit ? 0 : 1;
            }
            return bad;
        }

        [Test]
        public void EveryPiecesCornersAreNumbersAndItsNormalsOneLong()
        {
            GoreShatter.Surface skin = Tube(Radius, 10, 16, GoreShatter.Layer.Flesh);
            foreach (GoreShatter.Chunk chunk in Shatter(5, 7, skin))
            {
                foreach (GoreShatter.Patch patch in chunk.Patches)
                {
                    Assert.AreEqual(0, BadCorners(patch.Mesh), "a piece's outside has a broken corner");
                }
                Assert.AreEqual(0, BadCorners(chunk.Meat), "a piece's meat has a broken corner");
            }
        }

        /// <summary>
        /// A body's mesh has slivers, and Vector3.normalized answers zero for anything shorter than 1e-5, which their face normals are: a corner
        /// with no normal draws as a pixel that is not a number, and the bloom makes that a glowing dot, there for the frames a pixel lands on it
        /// (owner, 2026-11-08: "the little flickers of light are still happening on dismembered body parts", "they flicker as i move my mouse").
        /// </summary>
        [Test]
        public void EvenAPieceOfTinyTrianglesHasNormalsOneLong()
        {
            GoreShatter.Surface fine = Tube(0.012f, 12, 12, GoreShatter.Layer.Flesh, 0.003f);
            foreach (GoreShatter.Chunk chunk in Shatter(4, 3, fine))
            {
                foreach (GoreShatter.Patch patch in chunk.Patches)
                {
                    Assert.AreEqual(0, BadCorners(patch.Mesh), "a tiny piece's outside has a corner with no normal");
                }
                Assert.AreEqual(0, BadCorners(chunk.Meat), "a tiny piece's meat has a corner with no normal");
            }
        }

        [Test]
        public void EveryTriangleLandsInOnePieceAndEveryPieceIsClosed()
        {
            GoreShatter.Surface skin = Tube(Radius, 10, 16, GoreShatter.Layer.Flesh);
            List<GoreShatter.Chunk> chunks = Shatter(5, 7, skin);
            Assert.AreEqual(5, chunks.Count);
            int triangles = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                GoreShatter.Chunk chunk = chunks[i];
                Assert.AreEqual(1, chunk.Patches.Count, $"piece {i}: one surface, one patch");
                triangles += chunk.Patches[0].Mesh.triangles.Length / 3;
                Assert.IsNotNull(chunk.Meat, $"piece {i}: skin with no meat behind it");
                Assert.Greater(chunk.Meat.GetTriangles(GoreShatter.FatSubmesh).Length, 0, $"piece {i}: no line of fat round its torn edge");
                Assert.Greater(chunk.Meat.GetTriangles(GoreShatter.MeatSubmesh).Length, 0, $"piece {i}: no meat round its torn edge");
                Assert.Greater(chunk.Meat.GetTriangles(GoreShatter.InsideSubmesh).Length, 0, $"piece {i}: no meat inside");
                Assert.AreEqual(0, OpenEdges(new[] { chunk.Patches[0].Mesh, chunk.Meat }), $"piece {i} is not closed: it would show a hole");
            }
            Assert.AreEqual(skin.Triangles[0].Length / 3, triangles, "every triangle of the skin is in exactly one piece");
        }

        [Test]
        public void TheMeatIsInsideTheSkinAndTheFatLinesItsEdge()
        {
            foreach (GoreShatter.Chunk chunk in Shatter(4, 3, Tube(Radius, 8, 16, GoreShatter.Layer.Flesh)))
            {
                Vector3[] corners = chunk.Meat.vertices;
                foreach (int index in chunk.Meat.GetTriangles(GoreShatter.InsideSubmesh))
                {
                    Assert.Less(FromAxis(chunk, corners[index]), Radius - chunk.Thickness * 0.5f, "the inside is not under the skin");
                }
                foreach (int index in chunk.Meat.GetTriangles(GoreShatter.FatSubmesh))
                {
                    Assert.GreaterOrEqual(FromAxis(chunk, corners[index]), Radius - chunk.Thickness * GoreShatter.FatShare - 1e-4f,
                        "the fat is not just under the skin");
                }
                int[] inside = chunk.Meat.GetTriangles(GoreShatter.InsideSubmesh);
                for (int t = 0; t < inside.Length; t += 3)
                {
                    Vector3 a = corners[inside[t]], b = corners[inside[t + 1]], c = corners[inside[t + 2]];
                    Vector3 facing = Vector3.Cross(b - a, c - a);
                    Vector3 middle = chunk.Centre + (a + b + c) / 3.0f;
                    Assert.Less(Vector3.Dot(facing, new Vector3(middle.x, 0.0f, middle.z)), 0.0f, "the inside faces out of the body, not into the piece");
                }
                Assert.That(chunk.Thickness, Is.InRange(GoreShatter.MinMeat, GoreShatter.MaxMeat));
            }
        }

        [Test]
        public void ClothOverSkinGetsABackOfItsOwnAndTheSkinTheMeat()
        {
            GoreShatter.Surface skin = Tube(Radius, 8, 16, GoreShatter.Layer.Flesh);
            GoreShatter.Surface shirt = Tube(Radius + 0.01f, 8, 16, GoreShatter.Layer.Cloth);
            foreach (GoreShatter.Chunk chunk in Shatter(4, 5, skin, shirt))
            {
                Assert.IsNotNull(chunk.Meat, "a piece of a clothed arm has no meat");
                foreach (Vector3 corner in chunk.Meat.vertices)
                {
                    Assert.LessOrEqual(FromAxis(chunk, corner), Radius + 1e-4f, "the meat is behind the shirt, not behind the skin");
                }
                var skinAndMeat = new List<Mesh> { chunk.Meat };
                foreach (GoreShatter.Patch patch in chunk.Patches)
                {
                    if (patch.Surface == 1)
                    {
                        Assert.AreEqual(0, OpenEdges(new[] { patch.Mesh }), "the shirt's patch is not closed by its own back");
                    }
                    else
                    {
                        skinAndMeat.Add(patch.Mesh);
                    }
                }
                Assert.AreEqual(0, OpenEdges(skinAndMeat), "the skin and its meat are not closed");
            }
        }

        [Test]
        public void ClothWithNoSkinUnderItIsTheFlesh()
        {
            foreach (GoreShatter.Chunk chunk in Shatter(4, 5, Tube(Radius, 8, 16, GoreShatter.Layer.Cloth)))
            {
                Assert.IsNotNull(chunk.Meat, "a garment over a body with no skin under it (WornBodyCover took it) bursts as cloth only");
            }
        }

        [Test]
        public void ThePiecesAreOfASize()
        {
            List<GoreShatter.Chunk> chunks = Shatter(6, 21, Tube(Radius, 20, 24, GoreShatter.Layer.Flesh, 0.8f));
            float smallest = float.MaxValue, largest = 0.0f;
            foreach (GoreShatter.Chunk chunk in chunks)
            {
                smallest = Mathf.Min(smallest, chunk.Area);
                largest = Mathf.Max(largest, chunk.Area);
            }
            Assert.Less(largest / smallest, 3.5f, $"pieces from {smallest:F4} to {largest:F4} m2: one crumb and one slab");
        }

        [Test]
        public void TheSameSeedMakesTheSamePieces()
        {
            GoreShatter.Surface skin = Tube(Radius, 8, 16, GoreShatter.Layer.Flesh);
            List<GoreShatter.Chunk> first = Shatter(5, 11, skin);
            List<GoreShatter.Chunk> again = Shatter(5, 11, skin);
            List<GoreShatter.Chunk> other = Shatter(5, 12, skin);
            Assert.AreEqual(first.Count, again.Count);
            bool differs = false;
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Centre, again[i].Centre, "the same seed broke the skin differently");
                differs |= i >= other.Count || Vector3.Distance(first[i].Centre, other[i].Centre) > 1e-3f;
            }
            Assert.IsTrue(differs, "another seed broke the skin the same way");
        }
    }
}
