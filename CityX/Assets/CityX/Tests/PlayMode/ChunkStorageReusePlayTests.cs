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
    /// The streamer gives every chunk's payload -- its buffers and its main mesh's arrays, used again chunk after chunk (2026-10-14: fresh
    /// ones were ~60 MB of garbage a second flying, a collection every tenth of a second, each stopping every thread) -- back once the chunk
    /// is made or thrown away. One never given back is storage the next chunks cannot use; one used after it was given back would be two
    /// chunks in the same arrays. Streaming in, the detail swaps, chunks dropped behind, window hits rebuilding a chunk (whole and glass
    /// only, some while one is on its way), a world rebuilt while chunks are being generated: once it settles, every payload is back.
    /// </summary>
    public sealed class ChunkStorageReusePlayTests
    {
        private const int Seed = 47360519;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        /// <summary>A glazed pane low on a plain building near the spawn, and where it is.</summary>
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

        /// <summary>
        /// Waits, by the clock, until the streamer has something on screen and has been quiet -- nothing generating, cooking or out -- for
        /// twenty frames running (or 30 s have gone).
        /// </summary>
        private static IEnumerator AllBack(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 30.0f;
            int quiet = 0;
            while (quiet < 20 && Time.realtimeSinceStartup < until)
            {
                yield return null;
                bool busy = streamer.PayloadsOut > 0 || streamer.IsStreaming || streamer.PendingSwapCount > 0 || streamer.LoadedChunkCount == 0;
                quiet = busy ? 0 : quiet + 1;
            }
        }

        [UnityTest]
        public IEnumerator EveryChunksStorageIsGivenBackOnceItIsMadeOrThrownAway()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindPane(world, out PaneKey key, out Vector3 centre), "no window near the spawn");

            var viewerObject = new GameObject("Viewer");
            _made.Add(viewerObject);
            Transform viewer = viewerObject.transform;
            viewer.position = centre;
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);   // set the fields before Awake runs
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 3;
            streamer.FullDetailRadiusChunks = 1;   // the far version beyond one chunk: moving swaps detail both ways
            streamer.DetailHysteresisChunks = 0;
            streamer.GeneratorThreads = 2;
            streamer.SpawnPreStreamBudget = 0;
            streamer.Viewer = viewer;
            go.SetActive(true);

            // Streamed in round the window in the background. Then the window is hit: the first hole rebuilds the whole chunk, the next only
            // its cracked glass, each waited for; then a burst faster than the rebuilds, so some land while one is on its way.
            yield return AllBack(streamer);
            Assert.Greater(streamer.LoadedChunkCount, 0, "nothing streamed in");
            int shots = 0;
            foreach (int burst in new[] { 1, 1, 6 })
            {
                for (int i = 0; i < burst; i++, shots++)
                {
                    streamer.Damage.Shoot(key, new Vector2(0.15f + 0.08f * shots, 0.3f + 0.05f * shots), out _);
                    yield return null;
                }
                float until = Time.realtimeSinceStartup + 20.0f;
                while (streamer.WindowRefreshPending && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }
                Assert.IsFalse(streamer.WindowRefreshPending, "a window's change never reached the screen");
            }

            // Moved on: a new edge streams in, the old one is dropped, chunks change detail both ways.
            for (int step = 1; step <= 4; step++)
            {
                viewer.position = centre + new Vector3(CityLayout.ChunkSize * step, 0.0f, 0.0f);
                for (int frame = 0; frame < 3; frame++) yield return null;
            }

            // The world rebuilt while chunks are being generated and cooked: what comes back from the old world is thrown away.
            streamer.Rebuild();
            yield return AllBack(streamer);
            Assert.IsFalse(streamer.IsStreaming, "the city never settled");
            Assert.AreEqual(0, streamer.PayloadsOut, "a chunk's payload was never given back: its buffers and arrays are lost to the next chunks");
            Assert.Greater(streamer.LoadedChunkCount, 0, "nothing streamed in after the rebuild");
        }
    }
}
