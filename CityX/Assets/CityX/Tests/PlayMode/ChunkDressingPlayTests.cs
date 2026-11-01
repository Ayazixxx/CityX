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
    /// A chunk streamed in on the frame budget is dressed over the frames its collider cooks (docs/PERFORMANCE.md, 2026-10-31): its doors,
    /// lamps, interiors and the rest arrive a step a frame, so when it goes on screen it is the same chunk the synchronous path makes in one go,
    /// and every payload the dressing read is given back once.
    /// </summary>
    public sealed class ChunkDressingPlayTests
    {
        private const int Seed = 81565117;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private CityStreamer Streamer(string name, Transform viewer)
        {
            var go = new GameObject(name);
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            streamer.PlaceAtSpawn = false;
            streamer.Viewer = viewer;
            go.SetActive(true);
            return streamer;
        }

        /// <summary>What a chunk is made of, by what the dressing adds: its name against the count of each kind of part.</summary>
        private static Dictionary<string, string> Census(CityStreamer streamer)
        {
            var census = new Dictionary<string, string>();
            foreach (Transform chunk in streamer.transform)
            {
                census[chunk.name] = $"renderers {chunk.GetComponentsInChildren<MeshRenderer>(true).Length} doors {chunk.GetComponentsInChildren<BuildingDoor>(true).Length} " +
                                     $"interiors {chunk.GetComponentsInChildren<BuildingInteriorVolume>(true).Length} lights {chunk.GetComponentsInChildren<Light>(true).Length} " +
                                     $"children {chunk.childCount}";
            }
            return census;
        }

        [UnityTest]
        public IEnumerator AChunkDressedOverSeveralFramesIsTheSameChunkMadeInOneAndGivesEveryPayloadBack()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz), "no spawn on the seed");
            var viewer = new GameObject("Viewer");
            _made.Add(viewer);
            viewer.transform.position = new Vector3((float)sx, 30.0f, (float)sz);

            // The reference: every chunk round the spawn made in one go, solid at once (the spawn pre-stream, the captures).
            CityStreamer atOnce = Streamer("AtOnce", null);
            atOnce.StreamAround(viewer.transform.position, int.MaxValue);
            yield return null;
            Dictionary<string, string> reference = Census(atOnce);
            Assert.Greater(reference.Count, 10, "the reference streamed in too few chunks to compare");
            atOnce.gameObject.SetActive(false);

            // The game's way: streamed on the frame budget, each chunk's dressing a step a frame while its collider cooks.
            CityStreamer budgeted = Streamer("Budgeted", viewer.transform);
            int settledFor = 0;
            for (int frame = 0; frame < 4000 && settledFor < 10; frame++)
            {
                yield return null;
                bool settled = !budgeted.IsStreaming && budgeted.PendingSwapCount == 0 && budgeted.transform.childCount >= reference.Count;
                settledFor = settled ? settledFor + 1 : 0;
            }
            Assert.AreEqual(10, settledFor, $"the budgeted streamer never settled (streaming {budgeted.IsStreaming}, swaps pending {budgeted.PendingSwapCount}, chunks {budgeted.transform.childCount} of {reference.Count})");
            Assert.AreEqual(0, budgeted.PayloadsOut, "a payload was never given back after its chunk was dressed");

            Dictionary<string, string> dressed = Census(budgeted);
            foreach (KeyValuePair<string, string> chunk in reference)
            {
                Assert.IsTrue(dressed.TryGetValue(chunk.Key, out string made), $"{chunk.Key} never streamed in on the budget");
                Assert.AreEqual(chunk.Value, made, $"{chunk.Key} dressed over several frames differs from the one made at once");
            }
        }
    }
}
