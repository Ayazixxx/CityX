using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Reported: "frame drops when spamming shots with the pistol at a window". Every bullet hole used to tear down and rebuild the whole chunk the
    /// window is in -- its big mesh and its collider -- on the spot. Another hole in an already cracked pane changes only the cracked glass and its
    /// decals, so only those are swapped; and with worker threads, a rebuild happens in the background while the old chunk stays on screen.
    /// </summary>
    public sealed class WindowHitRefreshPlayTests
    {
        private const int Seed = 38461927;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private static bool FindPane(CityWorld world, out PaneKey key, out Vector3 centre)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.OnRoof || site.Shell || site.Sides >= 3 || site.Params.Width < 8.0f) continue;
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int row = 1; row < Mathf.Min(rows.Count, 4); row++)
                {
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row)) continue;
                        centre = TestSites.At(site, site.MinX + (column + 0.5f) * columns.Size, site.BaseHeight + (row + 0.5f) * rows.Size, site.MaxZ);
                        if (GlassDamage.TryPaneAt(world, site, centre + CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North) * 0.02f, out key))
                        {
                            return true;
                        }
                    }
                }
            }
            key = default;
            centre = default;
            return false;
        }

        private CityStreamer Streamer(int threads, Vector3 at)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 1;
            streamer.GeneratorThreads = threads;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(at, int.MaxValue);
            var viewer = new GameObject("Viewer");
            _made.Add(viewer);
            viewer.transform.position = at;
            streamer.Viewer = viewer.transform;
            return streamer;
        }

        private static Transform ChunkOf(CityStreamer streamer, CityWorld world, PaneKey key)
        {
            BuildingSite site = world.Building(key.Building);
            return streamer.transform.Find($"Chunk {site.OwnerChunkX},{site.OwnerChunkY}");
        }

        private static int DecalVertices(Transform chunk)
        {
            // The cracked pane itself (its holes are cut into it): the decals need a material a bare test streamer does not have.
            Transform decals = chunk != null ? chunk.Find("CrackedGlass") : null;
            return decals != null ? decals.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;
        }

        [UnityTest]
        public IEnumerator AnotherHoleInACrackedPaneLeavesTheChunkItselfAlone()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindPane(world, out PaneKey key, out Vector3 centre), "no window near the spawn");
            CityStreamer streamer = Streamer(0, centre);
            yield return null;

            streamer.Damage.Shoot(key, new Vector2(0.3f, 0.6f), out _);   // intact -> cracked: the pane leaves the chunk's mesh, a full rebuild
            yield return null;
            Transform chunk = ChunkOf(streamer, world, key);
            Assert.IsNotNull(chunk, "the window's chunk is not loaded");
            Mesh body = chunk.GetComponent<MeshFilter>().sharedMesh;
            int decals = DecalVertices(chunk);
            Assert.Greater(decals, 0, "the first hole left no cracked pane");

            streamer.Damage.Shoot(key, new Vector2(0.65f, 0.4f), out _);   // another hole: only the glass changes
            yield return null;
            Transform after = ChunkOf(streamer, world, key);
            Assert.AreSame(chunk, after, "the whole chunk was rebuilt for one more bullet hole");
            Assert.AreSame(body, after.GetComponent<MeshFilter>().sharedMesh, "the chunk's mesh (and collider) were rebuilt for one more bullet hole");
            Assert.Greater(DecalVertices(after), decals, "the second hole never appeared");
        }

        [UnityTest]
        public IEnumerator WithWorkersAHitIsRebuiltInTheBackgroundAndTheOldChunkStaysMeanwhile()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindPane(world, out PaneKey key, out Vector3 centre), "no window near the spawn");
            CityStreamer streamer = Streamer(2, centre);
            yield return null;
            Transform before = ChunkOf(streamer, world, key);
            Assert.IsNotNull(before);

            streamer.Damage.Shoot(key, new Vector2(0.3f, 0.6f), out _);
            Assert.IsTrue(ChunkOf(streamer, world, key) == before, "the chunk was torn down at once instead of being rebuilt in the background");

            int decals = 0;
            for (int i = 0; i < 300 && decals == 0; i++)
            {
                // (the streamer's own Update assembles what the workers finish, round the Viewer)
                yield return null;
                decals = DecalVertices(ChunkOf(streamer, world, key));
            }
            Assert.Greater(decals, 0, "the hole never appeared");
        }
    }
}
