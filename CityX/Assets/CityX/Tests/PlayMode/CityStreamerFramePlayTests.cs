using System.Collections;
using System.Collections.Generic;
using System.Text;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Hitches while walking, sprinting or flying the jetpack (2026-10-14): every chunk that streamed in cooked its MeshCollider on the main
    /// thread, about 10 ms of one frame per detailed chunk. A real streamer with its worker threads in the game's own city, a viewer flown
    /// through it at jetpack speed with the main thread held to 60 frames a second (the rest of a real frame is rendering): every frame's
    /// streaming time (CityStreamer.LastUpdateMs) is recorded, with how many chunks went on screen in it. And what cooking off the main thread
    /// must not break: the ring fills in whole, one object per chunk, every chunk solid; a chunk that leaves range while its collider cooks, and
    /// a world rebuilt or a streamer switched off meanwhile, leave nothing behind.
    /// </summary>
    public sealed class CityStreamerFramePlayTests
    {
        private const int Seed = CitySeed.Default;

        private readonly List<GameObject> _made = new List<GameObject>();
        private CityScale _scaleBefore;
        private bool _scaleChanged;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_scaleChanged)
            {
                CityScale.Active = _scaleBefore;
                _scaleChanged = false;
            }
        }

        private Transform Viewer(Vector3 position)
        {
            var go = new GameObject("Viewer");
            _made.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        private CityStreamer Streamer(Transform viewer, int radius, int fullRadius)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);   // set the fields before Awake runs
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = radius;
            streamer.FullDetailRadiusChunks = fullRadius;
            streamer.DetailHysteresisChunks = 1;
            streamer.SpawnPreStreamBudget = 0;   // background generation, as in the game (GeneratorThreads left at its default)
            streamer.Viewer = viewer;
            go.SetActive(true);
            return streamer;
        }

        private static Vector3 Spawn(CityWorld world)
        {
            Assert.IsTrue(world.Plan.TryFindSpawn(out double x, out double z), "the world has no spawn point");
            return new Vector3((float)x, (float)world.Plan.GroundHeight(x, z) + 2.0f, (float)z);
        }

        private static Vector2Int ChunkUnder(Vector3 position)
        {
            return new Vector2Int(Mathf.FloorToInt(position.x / CityLayout.ChunkSize), Mathf.FloorToInt(position.z / CityLayout.ChunkSize));
        }

        /// <summary>Waits, by the clock (a batch-mode frame is under a millisecond), until nothing is being generated, cooked or put on screen.</summary>
        private static IEnumerator Streamed(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 30.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 20)
            {
                quiet = streamer.IsStreaming ? 0 : quiet + 1;
                yield return null;
            }
            Physics.SyncTransforms();
            Assert.IsFalse(streamer.IsStreaming, "the city never finished streaming in");
        }

        /// <summary>Waits, by the clock, until some chunk has been made and is waiting for its collider to be cooked.</summary>
        private static IEnumerator UntilAColliderCooks(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 20.0f;
            while (streamer.PendingSwapCount == 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            Assert.Greater(streamer.PendingSwapCount, 0, "no streamed chunk ever waited for its collider: it is still cooked on the main thread");
        }

        /// <summary>Meshes made for chunks that still exist (a chunk's big mesh is called chunk-x-y): one per loaded chunk, or one has leaked.</summary>
        private static int LiveChunkMeshes()
        {
            int count = 0;
            foreach (Mesh mesh in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                if (mesh.name.StartsWith("chunk-"))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// The ring round <paramref name="at"/> is whole, and what is under the streamer is exactly the loaded chunks: one object each, every one
        /// switched on and solid. A chunk left switched off is one whose collider never came; two of one chunk is a swap that never finished.
        /// </summary>
        private static void AssertWhole(CityStreamer streamer, Vector3 at, int radius)
        {
            Vector2Int centre = ChunkUnder(at);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var key = new Vector2Int(centre.x + dx, centre.y + dy);
                    Assert.IsTrue(streamer.IsChunkLoaded(key), $"chunk {key} never streamed in");
                }
            }
            var names = new HashSet<string>();
            foreach (Transform child in streamer.transform)
            {
                Assert.IsTrue(child.gameObject.activeSelf, $"{child.name} is still switched off: it waited for a collider that never came");
                Assert.IsTrue(names.Add(child.name), $"{child.name} is there twice");
                MeshCollider collider = child.GetComponent<MeshCollider>();
                Assert.IsTrue(collider != null && collider.sharedMesh != null, $"{child.name} has no collider: you would fall through it");
            }
            Assert.AreEqual(streamer.LoadedChunkCount, streamer.transform.childCount, "objects that are not loaded chunks are left under the streamer");
        }

        [UnityTest]
        public IEnumerator ChunksStreamingInHaveTheirCollidersCookedOffTheMainThread()
        {
            // The game's own districts (the tests pin the classic city, whose small buildings are cheap to cook), and its ring scaled down:
            // the far version out to 8 chunks, the full one within 4.
            _scaleBefore = CityScale.Active;
            _scaleChanged = true;
            CityScale.Active = CityScale.Default;
            const int Radius = 8;
            var world = new CityWorld(Seed);
            Vector3 start = Spawn(world);
            Transform viewer = Viewer(start);
            CityStreamer streamer = Streamer(viewer, radius: Radius, fullRadius: 4);
            streamer.StreamAround(start, int.MaxValue);   // settled round the viewer, as the spawn pre-stream leaves it
            yield return Streamed(streamer);
            System.GC.Collect();   // what earlier tests left behind is not this test's to pay for
            yield return null;

            // Six seconds of a boosted jetpack (22 m/s), a little off the axes: a new edge of chunks about every second, and the detail swaps
            // round the viewer (the far version ahead becoming the full one, the full one behind going far).
            const float dt = 1.0f / 60.0f;
            const float speed = 22.0f;
            Vector3 heading = new Vector3(1.0f, 0.0f, 0.35f).normalized;
            var frames = new List<double>();
            var lines = new StringBuilder();
            double worst = 0.0;
            int worstFrame = -1;
            double worstSwapIn = 0.0;
            int worstSwapInFrame = -1;
            int mostWaiting = 0;
            int busyFrames = 0;
            int collectedFrames = 0;
            int streamedBefore = streamer.ChunksStreamedIn;
            long arraysMadeBefore = ChunkArrayPool.MadeBytes;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            const int Frames = 360;
            for (int frame = 0; frame < Frames; frame++)
            {
                double frameStart = clock.Elapsed.TotalMilliseconds;
                viewer.position = start + heading * (speed * dt * (frame + 1));
                int streamedAtStart = streamer.ChunksStreamedIn;
                int collectionsAtStart = System.GC.CollectionCount(0);
                yield return null;

                double ms = streamer.LastUpdateMs;
                int streamed = streamer.ChunksStreamedIn - streamedAtStart;
                // A garbage collection stops every thread wherever it is, for anything up to a few hundred milliseconds in the editor with a
                // city's worth of chunk buffers about: it is counted, but a frame it fell in says nothing about the streamer's own work.
                bool collected = System.GC.CollectionCount(0) != collectionsAtStart;
                frames.Add(ms);
                if (streamed > 0)
                {
                    busyFrames++;
                }
                if (collected)
                {
                    collectedFrames++;
                }
                // The first two frames carry one-time costs (code run for the first time): judged apart.
                if (frame >= 2 && ms > worst)
                {
                    worst = ms;
                    worstFrame = frame;
                }
                if (frame >= 2 && !collected && streamer.LastSwapInMs > worstSwapIn)
                {
                    worstSwapIn = streamer.LastSwapInMs;
                    worstSwapInFrame = frame;
                }
                mostWaiting = Mathf.Max(mostWaiting, streamer.PendingSwapCount);
                if (ms > 3.0)
                {
                    lines.AppendLine($"  frame {frame,3}: streamer {ms,5:0.0} ms (switching cooked chunks on {streamer.LastSwapInMs,4:0.0} ms)  " +
                                     $"chunks on screen {streamed}  waiting for a collider {streamer.PendingSwapCount}{(collected ? "  (a garbage collection)" : "")}");
                }

                // Hold the frame to 1/60 s: the rest of a real frame is rendering, which the main thread spends not streaming.
                while (clock.Elapsed.TotalMilliseconds - frameStart < dt * 1000.0)
                {
                    System.Threading.Thread.Sleep(0);
                }
            }
            int streamedIn = streamer.ChunksStreamedIn - streamedBefore;
            double arraysMadeMb = (ChunkArrayPool.MadeBytes - arraysMadeBefore) / (1024.0 * 1024.0);

            var sorted = new List<double>(frames);
            sorted.Sort();
            double median = sorted[sorted.Count / 2];
            double p95 = sorted[Mathf.Clamp(Mathf.RoundToInt(sorted.Count * 0.95f), 0, sorted.Count - 1)];
            double p99 = sorted[Mathf.Clamp(Mathf.RoundToInt(sorted.Count * 0.99f), 0, sorted.Count - 1)];
            int over4 = 0, over8 = 0;
            double total = 0.0;
            foreach (double ms in frames)
            {
                if (ms > 4.0) over4++;
                if (ms > 8.0) over8++;
                total += ms;
            }
            string report = $"CityX streaming frames: {streamedIn} chunks on screen in {Frames} frames ({busyFrames} frames with some), " +
                            $"streamer per frame: median {median:0.00} ms, 95th percentile {p95:0.00} ms, 99th {p99:0.00} ms, " +
                            $"worst after the first two {worst:0.0} ms (frame {worstFrame}); frames over 4 ms {over4}, over 8 ms {over8}; " +
                            $"main thread spent streaming {total:0} ms in all; switching cooked chunks on: worst {worstSwapIn:0.00} ms (frame {worstSwapInFrame}); " +
                            $"most chunks waiting for a collider at once {mostWaiting}; frames with a garbage collection {collectedFrames}; " +
                            $"chunk mesh arrays made new {arraysMadeMb:0} MB (the rest used again), held for reuse {ChunkArrayPool.PooledBytes / (1024.0 * 1024.0):0} MB\n{lines}";
            Debug.Log(report);
            TestContext.WriteLine(report);
            Assert.Greater(streamedIn, 40, "the flight hardly streamed anything in: this measured nothing");

            // Everything it flew past is there, whole and solid; and every chunk's storage (payload, buffers, arrays) is back for the next ones.
            yield return Streamed(streamer);
            yield return null;
            AssertWhole(streamer, viewer.position, Radius);
            Assert.AreEqual(0, streamer.PayloadsOut, "a chunk's payload was never given back after the flight: its arrays are lost to the chunks after it");

            // THE POINT: no collider is cooked on the main thread any more. Before, every chunk that streamed in had its collider cooked there in
            // the frame it landed: ~10 ms for an ordinary chunk, 80-110 ms for a block of towers (2026-10-14: frames of 90-150 ms while flying).
            // Now a chunk waits switched off until a worker has cooked it, and switching it on takes the cooked data as it is (under 1.5 ms for
            // the frame's swaps when measured). The frame's whole streaming time is reported, not judged: it carries a powered block's room
            // lights and small parts and the garbage collections (PERFORMANCE.md). Making a block of towers' mesh, 20-46 ms of it until the
            // mesh came laid out from the worker, is 2-3 ms now (ChunkMeshData; ChunkMeshDataTests judges that).
            Assert.Greater(mostWaiting, 0, "no chunk ever waited for its collider: they are still cooked on the main thread");
            Assert.Less(worstSwapIn, 5.0, $"switching cooked chunks on took {worstSwapIn:0.0} ms of one frame (frame {worstSwapInFrame}): a collider is still cooked on the main thread");
        }

        [UnityTest]
        public IEnumerator AChunkThatLeavesRangeWhileItsColliderCooksIsDropped()
        {
            var world = new CityWorld(Seed);
            Vector3 start = Spawn(world);
            Transform viewer = Viewer(start);
            int meshesBefore = LiveChunkMeshes();
            CityStreamer streamer = Streamer(viewer, radius: 3, fullRadius: 0);   // nothing far: every chunk that lands is a new one

            // As soon as a chunk is waiting for its collider, the viewer is somewhere else entirely (a respawn, a teleport).
            yield return UntilAColliderCooks(streamer);
            Vector3 away = start + new Vector3(CityLayout.ChunkSize * 40.0f, 0.0f, 0.0f);
            viewer.position = away;

            yield return Streamed(streamer);
            yield return null;
            AssertWhole(streamer, away, 3);
            Assert.AreEqual(streamer.LoadedChunkCount, LiveChunkMeshes() - meshesBefore, "a chunk dropped while its collider cooked left its mesh behind");
        }

        [UnityTest]
        public IEnumerator RebuildingOrSwitchingOffWhileCollidersCookLeavesNothingBehind()
        {
            var world = new CityWorld(Seed);
            Vector3 start = Spawn(world);
            Transform viewer = Viewer(start);
            int meshesBefore = LiveChunkMeshes();
            CityStreamer streamer = Streamer(viewer, radius: 3, fullRadius: 0);

            // The world rebuilt while colliders cook: the cooking finishes (a worker must never be left with a mesh that is gone), then all goes.
            yield return UntilAColliderCooks(streamer);
            streamer.Rebuild();
            Assert.AreEqual(0, streamer.PendingSwapCount, "a chunk of the old world is still waiting to go on screen");
            Assert.AreEqual(0, streamer.LoadedChunkCount);

            // Switched off while colliders cook (leaving the scene), then on again: it carries on and fills the ring.
            yield return UntilAColliderCooks(streamer);
            streamer.gameObject.SetActive(false);
            Assert.AreEqual(0, streamer.PendingSwapCount, "switching off left chunks waiting for their colliders");
            yield return null;
            streamer.gameObject.SetActive(true);

            yield return Streamed(streamer);
            yield return null;
            AssertWhole(streamer, start, 3);
            Assert.AreEqual(streamer.LoadedChunkCount, LiveChunkMeshes() - meshesBefore, "a chunk thrown away while its collider cooked left its mesh behind");
        }
    }
}
