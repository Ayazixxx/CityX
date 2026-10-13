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
    /// The city streams in on worker threads: the geometry of each chunk is generated off the main thread and
    /// the main thread only makes the objects. These check that it fills the ring, that what a worker makes is
    /// exactly what the main thread would have made (the generation is a pure function of the chunk's
    /// coordinates), that moving drops and loads the right chunks, and that a rebuilt world does not receive
    /// chunks generated for the old one.
    /// </summary>
    public sealed class CityStreamerBackgroundPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private CityStreamer Streamer(string name, int threads, int radius, Transform viewer, int seed = 61470385)
        {
            var go = new GameObject(name);
            _made.Add(go);
            go.SetActive(false);   // set the fields before Awake runs
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = seed;
            streamer.RadiusChunks = radius;
            streamer.GeneratorThreads = threads;
            streamer.SpawnPreStreamBudget = 0;
            streamer.Viewer = viewer;
            go.SetActive(true);
            return streamer;
        }

        private Transform Viewer(Vector3 position)
        {
            var go = new GameObject("Viewer");
            _made.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float elapsed = 0.0f;
            while (!condition() && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Each chunk object's own mesh as (vertices, triangle indices), by chunk name.</summary>
        private static Dictionary<string, KeyValuePair<int, int>> Meshes(CityStreamer streamer)
        {
            var result = new Dictionary<string, KeyValuePair<int, int>>();
            foreach (Transform child in streamer.transform)
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                int indices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) indices += (int)mesh.GetIndexCount(i);
                result[child.name] = new KeyValuePair<int, int>(mesh.vertexCount, indices);
            }
            return result;
        }

        [UnityTest]
        public IEnumerator TheRingFillsInOnWorkerThreads()
        {
            CityStreamer streamer = Streamer("Async", threads: 3, radius: 3, Viewer(new Vector3(500.0f, 0.0f, 500.0f)));
            int expected = 7 * 7;

            yield return WaitUntil(() => streamer.LoadedChunkCount >= expected, 20.0f);

            Assert.AreEqual(expected, streamer.LoadedChunkCount, "the ring did not fill in");
            Assert.AreEqual(expected, streamer.transform.childCount, "a chunk was made more than once (or one is missing its object)");
        }

        [UnityTest]
        public IEnumerator AChunkMadeInTheBackgroundIsExactlyTheOneTheMainThreadMakes()
        {
            Vector3 at = new Vector3(700.0f, 0.0f, -300.0f);
            CityStreamer sync = Streamer("Sync", threads: 0, radius: 2, Viewer(at));
            sync.StreamAround(at, int.MaxValue);
            CityStreamer async = Streamer("Async", threads: 3, radius: 2, Viewer(at));

            yield return WaitUntil(() => async.LoadedChunkCount >= 25, 20.0f);
            Assert.AreEqual(25, sync.LoadedChunkCount);
            Assert.AreEqual(25, async.LoadedChunkCount);

            Dictionary<string, KeyValuePair<int, int>> a = Meshes(sync);
            Dictionary<string, KeyValuePair<int, int>> b = Meshes(async);
            Assert.AreEqual(a.Count, b.Count, "the two streamers built a different number of chunk meshes");
            foreach (KeyValuePair<string, KeyValuePair<int, int>> pair in a)
            {
                Assert.IsTrue(b.TryGetValue(pair.Key, out KeyValuePair<int, int> other), $"{pair.Key} is missing from the background build");
                Assert.AreEqual(pair.Value.Key, other.Key, $"{pair.Key}: a different number of vertices");
                Assert.AreEqual(pair.Value.Value, other.Value, $"{pair.Key}: a different number of triangle indices");
            }
        }

        [UnityTest]
        public IEnumerator MovingTheViewerLoadsTheNewEdgeAndDropsTheOldOne()
        {
            Transform viewer = Viewer(new Vector3(0.0f, 0.0f, 0.0f));
            CityStreamer streamer = Streamer("Async", threads: 3, radius: 2, viewer);
            yield return WaitUntil(() => streamer.LoadedChunkCount >= 25, 20.0f);

            viewer.position = new Vector3(CityLayout.ChunkSize * 6.0f + 1.0f, 0.0f, 0.0f);   // six chunks east
            int centreX = 6;
            yield return WaitUntil(() =>
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        if (streamer.transform.Find($"Chunk {centreX + dx},{dy}") == null) return false;
                    }
                }
                return true;
            }, 20.0f);

            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Assert.IsNotNull(streamer.transform.Find($"Chunk {centreX + dx},{dy}"), $"chunk ({centreX + dx},{dy}) never streamed in");
                }
            }
            yield return null;
            yield return null;   // destroyed objects go at the end of the frame
            Assert.IsNull(streamer.transform.Find("Chunk 0,0"), "the chunk the viewer left was never dropped");
            Assert.LessOrEqual(streamer.LoadedChunkCount, 25 + 2 * 5 * 2, "the loaded set grew without bound");
        }

        [UnityTest]
        public IEnumerator RebuildingTheWorldNeverReceivesChunksGeneratedForTheOldOne()
        {
            Vector3 at = new Vector3(100.0f, 0.0f, 100.0f);
            CityStreamer streamer = Streamer("Async", threads: 3, radius: 2, Viewer(at), seed: 11111111);

            // Generation for the first seed is in flight; switch worlds under it.
            yield return null;
            streamer.WorldSeed = 22222222;
            streamer.Rebuild();
            yield return WaitUntil(() => streamer.LoadedChunkCount >= 25, 20.0f);
            yield return null;

            CityStreamer reference = Streamer("Reference", threads: 0, radius: 2, Viewer(at), seed: 22222222);
            reference.StreamAround(at, int.MaxValue);

            Dictionary<string, KeyValuePair<int, int>> got = Meshes(streamer);
            Dictionary<string, KeyValuePair<int, int>> want = Meshes(reference);
            Assert.AreEqual(want.Count, got.Count, "the rebuilt world has a different number of chunks");
            foreach (KeyValuePair<string, KeyValuePair<int, int>> pair in want)
            {
                Assert.IsTrue(got.TryGetValue(pair.Key, out KeyValuePair<int, int> other), $"{pair.Key} missing");
                Assert.AreEqual(pair.Value.Key, other.Key, $"{pair.Key}: built for the wrong world (vertex count differs)");
            }
        }
    }
}
