using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The per-frame streaming budget is a TIME as well as a count: chunk
    /// cost grows with building height and the zoned city puts its tall
    /// buildings together, so six chunks a frame is a few milliseconds in the
    /// suburbs and most of a frame in a downtown core.
    /// </summary>
    public sealed class CityStreamerTimeBudgetTests
    {
        private GameObject _object;
        private CityStreamer _streamer;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Streamer");
            _streamer = _object.AddComponent<CityStreamer>();
            _streamer.WorldSeed = 20260810;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        /// <summary>However tight the limit, a frame makes progress -- otherwise streaming could stall forever.</summary>
        [Test]
        public void AZeroTimeBudgetStillBuildsExactlyOneChunk()
        {
            _streamer.RadiusChunks = 3;
            _streamer.StreamAround(Vector3.zero, budget: 100, timeBudgetMs: 0.0f);

            Assert.AreEqual(1, _streamer.LoadedChunkCount);
        }

        [Test]
        public void ATightTimeBudgetLimitsAFrameButEveryChunkStillArrivesInTime()
        {
            _streamer.RadiusChunks = 6;   // 13 x 13 = 169 chunks
            _streamer.StreamAround(Vector3.zero, budget: 1000, timeBudgetMs: 2.0f);

            Assert.GreaterOrEqual(_streamer.LoadedChunkCount, 1);
            Assert.Less(_streamer.LoadedChunkCount, 169, "a 2ms budget built the whole ring in one frame");

            for (int frame = 0; frame < 500 && _streamer.LoadedChunkCount < 169; frame++)
            {
                _streamer.StreamAround(Vector3.zero, budget: 1000, timeBudgetMs: 2.0f);
            }
            Assert.AreEqual(169, _streamer.LoadedChunkCount, "the ring never finished loading under a per-frame time budget");
        }

        [Test]
        public void NoTimeBudgetBuildsEverythingTheCountAllows()
        {
            _streamer.RadiusChunks = 1;
            _streamer.StreamAround(Vector3.zero, budget: 1000);

            Assert.AreEqual(9, _streamer.LoadedChunkCount);
        }

        [Test]
        public void TheCountLimitStillApplies()
        {
            _streamer.RadiusChunks = 4;
            _streamer.StreamAround(Vector3.zero, budget: 5, timeBudgetMs: 100000.0f);

            Assert.AreEqual(5, _streamer.LoadedChunkCount);
        }

        [Test]
        public void TheShippedDefaultLeavesRoomInAFrame()
        {
            Assert.Greater(_streamer.ChunkBuildTimeBudgetMs, 0.0f);
            Assert.LessOrEqual(_streamer.ChunkBuildTimeBudgetMs, 8.0f, "a frame's streaming budget should stay a fraction of a 16ms frame");
        }
    }
}
