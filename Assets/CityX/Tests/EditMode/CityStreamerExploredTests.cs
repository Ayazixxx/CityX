using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The actual wiring point: does streaming around a position mark it
    /// explored on the real component, not just on the pure class in
    /// isolation (see ExploredChunksTests for that). RadiusChunks is kept at
    /// 0 throughout so each call builds exactly one real chunk mesh --
    /// ChunkBuildCostTests puts that around 0.8ms, cheap enough for a test.
    /// </summary>
    public sealed class CityStreamerExploredTests
    {
        private GameObject _object;
        private CityStreamer _streamer;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Streamer");
            _streamer = _object.AddComponent<CityStreamer>();
            _streamer.WorldSeed = 20260810;
            _streamer.RadiusChunks = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void StreamingAroundAPositionMarksItsChunkExplored()
        {
            _streamer.StreamAround(new Vector3(12.0f, 0.0f, 12.0f), budget: 1);

            Vector2Int chunk = new Vector2Int(
                Mathf.FloorToInt(12.0f / CityX.World.CityLayout.ChunkSize),
                Mathf.FloorToInt(12.0f / CityX.World.CityLayout.ChunkSize));
            Assert.IsTrue(_streamer.IsChunkExplored(chunk));
        }

        [Test]
        public void FarAwayChunksAreNotExplored()
        {
            _streamer.StreamAround(Vector3.zero, budget: 1);

            Assert.IsFalse(_streamer.IsChunkExplored(new Vector2Int(500, 500)));
        }

        [Test]
        public void ExplorationSurvivesTheViewerMovingOnAndChunksStreamingOut()
        {
            _streamer.StreamAround(Vector3.zero, budget: 1);
            Vector2Int origin = Vector2Int.zero;

            // Far enough that origin drops out of the loaded set entirely --
            // RadiusChunks is 0, so anywhere past a couple of chunks does it.
            _streamer.StreamAround(new Vector3(1000.0f, 0.0f, 1000.0f), budget: 1);

            Assert.IsTrue(_streamer.IsChunkExplored(origin),
                "explored-ness was lost when the chunk streamed back out, but it should be permanent");
        }

        [Test]
        public void GetExploredChunksIncludesEverythingMarkedSoFar()
        {
            _streamer.StreamAround(Vector3.zero, budget: 1);
            _streamer.StreamAround(new Vector3(1000.0f, 0.0f, 1000.0f), budget: 1);

            Vector2Int[] explored = _streamer.GetExploredChunks();

            Assert.Contains(Vector2Int.zero, explored);
        }
    }
}
