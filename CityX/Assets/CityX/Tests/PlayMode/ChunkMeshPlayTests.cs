using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A chunk's main mesh is copied in from what its worker laid out (ChunkMeshData, 2026-10-14) instead of being built through the
    /// classic Mesh API on the main thread. What that must keep, on screen and underfoot, among the game's own towers: each submesh draws
    /// with its own material -- the walls' first, then the dark glass, then the lit -- and every chunk is solid, both made at once (the
    /// spawn pre-stream, captures) and with its collider cooked on a worker (streaming in).
    /// </summary>
    public sealed class ChunkMeshPlayTests
    {
        private const int Seed = 39271846;

        private readonly List<Object> _made = new List<Object>();
        private CityScale _scaleBefore;

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own districts, where the towers are
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
            CityScale.Active = _scaleBefore;
        }

        private Material Named(string name)
        {
            var material = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = name };
            _made.Add(material);
            return material;
        }

        private CityStreamer Streamer(int threads, Transform viewer, Material ground, Material structure, Material dark, Material lit)
        {
            var go = new GameObject(threads > 0 ? "Streamed" : "AtOnce");
            _made.Add(go);
            go.SetActive(false);   // set the fields before Awake runs
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.FullDetailRadiusChunks = 0;
            streamer.GeneratorThreads = threads;
            streamer.SpawnPreStreamBudget = 0;
            streamer.Viewer = viewer;
            streamer.GroundMaterial = ground;
            streamer.StructureMaterial = structure;
            streamer.DarkGlassMaterial = dark;
            streamer.LitGlassMaterial = lit;
            go.SetActive(true);
            return streamer;
        }

        /// <summary>The chunk round the spawn that builds the tallest building: a block of towers, with walls and both kinds of glass.</summary>
        private static Vector3 TallestNearTheSpawn(CityWorld world)
        {
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz), "the world has no spawn point");
            int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
            var best = new Vector2Int(cx0, cy0);
            float tallest = -1.0f;
            for (int dy = -6; dy <= 6; dy++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    foreach (int id in world.BuildingsOwnedBy(cx0 + dx, cy0 + dy))
                    {
                        float height = (float)world.Building(id).Params.Height;
                        if (height > tallest)
                        {
                            tallest = height;
                            best = new Vector2Int(cx0 + dx, cy0 + dy);
                        }
                    }
                }
            }
            return new Vector3((best.x + 0.5f) * CityLayout.ChunkSize, 0.0f, (best.y + 0.5f) * CityLayout.ChunkSize);
        }

        private static IEnumerator Streamed(CityStreamer streamer, int chunks)
        {
            float until = Time.realtimeSinceStartup + 60.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 20)
            {
                quiet = streamer.IsStreaming || streamer.LoadedChunkCount < chunks ? 0 : quiet + 1;
                yield return null;
            }
            Assert.AreEqual(chunks, streamer.LoadedChunkCount, "the ring never filled in");
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Every chunk under the streamer: a material per submesh, the walls' first and the glass after it in order (dark, then lit), and
        /// solid (AssertSolid). How many chunks had glass comes back.
        /// </summary>
        private static int AssertDrawnAndSolid(CityStreamer streamer, Material ground, Material structure, Material dark, Material lit)
        {
            int withGlass = 0;
            foreach (Transform chunk in streamer.transform)
            {
                Mesh mesh = chunk.GetComponent<MeshFilter>().sharedMesh;
                Material[] materials = chunk.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.AreEqual(mesh.subMeshCount, materials.Length, $"{chunk.name}: {mesh.subMeshCount} submeshes, {materials.Length} materials");
                if (mesh.GetIndexCount(0) == 0)
                {
                    Assert.AreSame(ground, materials[0], $"{chunk.name}, empty, is not drawn with the ground's material");
                    continue;
                }
                Assert.AreSame(structure, materials[0], $"{chunk.name}: the ground, pavement and walls are drawn with {materials[0].name}");
                var glass = new List<Material>();
                for (int i = 1; i < materials.Length; i++) glass.Add(materials[i]);
                Assert.LessOrEqual(glass.Count, 2, $"{chunk.name}: more submeshes than walls and two kinds of glass");
                if (glass.Count == 2)
                {
                    CollectionAssert.AreEqual(new[] { dark, lit }, glass, $"{chunk.name}: the glass is drawn with the wrong materials");
                }
                else if (glass.Count == 1)
                {
                    Assert.IsTrue(glass[0] == dark || glass[0] == lit, $"{chunk.name}: its glass is drawn with {glass[0].name}");
                }
                if (glass.Count > 0)
                {
                    withGlass++;
                }

                AssertSolid(chunk, mesh);
            }
            return withGlass;
        }

        /// <summary>
        /// The chunk's collider is its mesh: a ray at a dozen of its faces, spread through it, from just in front of each, lands on the
        /// collider every time.
        /// </summary>
        private static void AssertSolid(Transform chunk, Mesh mesh)
        {
            MeshCollider collider = chunk.GetComponent<MeshCollider>();
            Assert.IsTrue(collider != null && collider.enabled && collider.sharedMesh == mesh, $"{chunk.name} has no collider of its own mesh");
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            int faces = triangles.Length / 3, tried = 0;
            for (int f = 0; f < faces && tried < 12; f += Mathf.Max(1, faces / 12))
            {
                Vector3 a = chunk.TransformPoint(vertices[triangles[3 * f]]);
                Vector3 b = chunk.TransformPoint(vertices[triangles[3 * f + 1]]);
                Vector3 c = chunk.TransformPoint(vertices[triangles[3 * f + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.magnitude * 0.5f < 0.05f)
                {
                    continue;   // a sliver: cooking may weld it away
                }
                Vector3 front = cross.normalized;
                Vector3 centre = (a + b + c) / 3.0f;
                Assert.IsTrue(collider.Raycast(new Ray(centre + front * 0.5f, -front), out _, 1.0f),
                    $"{chunk.name}: a ray at its face {f} (at {centre}) goes straight through");
                tried++;
            }
            Assert.Greater(tried, 0, $"{chunk.name}: no face to aim at");
        }

        [UnityTest]
        public IEnumerator EverySubmeshDrawsWithItsMaterialAndEveryChunkIsSolid()
        {
            Material ground = Named("Ground"), structure = Named("Structure"), dark = Named("DarkGlass"), lit = Named("LitGlass");
            Vector3 towers = TallestNearTheSpawn(new CityWorld(Seed));
            var viewer = new GameObject("Viewer");
            _made.Add(viewer);
            viewer.transform.position = towers;

            // Made at once, as the spawn pre-stream and the captures make them: the collider is cooked as the chunk is made.
            CityStreamer atOnce = Streamer(0, viewer.transform, ground, structure, dark, lit);
            atOnce.StreamAround(towers, int.MaxValue);
            Assert.AreEqual(25, atOnce.LoadedChunkCount);
            Physics.SyncTransforms();
            Assert.Greater(AssertDrawnAndSolid(atOnce, ground, structure, dark, lit), 0, "no chunk round the towers has glass");
            Object.Destroy(atOnce.gameObject);
            yield return null;

            // Streamed in on workers, its collider cooked there before the chunk is switched on.
            CityStreamer streamed = Streamer(3, viewer.transform, ground, structure, dark, lit);
            yield return Streamed(streamed, 25);
            Assert.Greater(AssertDrawnAndSolid(streamed, ground, structure, dark, lit), 0, "no streamed chunk round the towers has glass");
        }
    }
}
