using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Where pedestrians are allowed to walk.
    ///
    /// The single property that matters is that a target is never inside a
    /// building. The Godot original could only approach that by raycasting and
    /// retrying; here it is a claim about arithmetic, so it can be checked
    /// exhaustively instead of hoped for.
    /// </summary>
    public class StreetNetworkTests
    {
        private const int Samples = 4000;

        [Test]
        public void StreetLinesLandOnStreetChunks()
        {
            for (int index = -20; index <= 20; index++)
            {
                float coordinate = StreetNetwork.LineCoordinate(index);
                int chunk = Mathf.FloorToInt(coordinate / CityLayout.ChunkSize);

                Assert.IsTrue(CityLayout.IsStreetChunk(chunk, chunk),
                    $"line {index} at {coordinate}m falls in chunk {chunk}, which is not a street");
            }
        }

        [Test]
        public void LineIndexAndCoordinateAreInverses()
        {
            for (int index = -50; index <= 50; index++)
            {
                Assert.AreEqual(index,
                    StreetNetwork.NearestLineIndex(StreetNetwork.LineCoordinate(index)),
                    $"line {index} does not survive a round trip");
            }
        }

        /// <summary>
        /// THE ONE THAT MATTERS. A pedestrian walking to a target that is
        /// inside a building either walks into a wall forever or walks through
        /// it. This asserts it cannot happen, from four thousand starting
        /// points spread across the city, including deep inside blocks.
        /// </summary>
        [Test]
        public void EveryTargetIsOnAStreet()
        {
            var random = new DeterministicRandom(20260810);

            for (int i = 0; i < Samples; i++)
            {
                var from = new Vector3(
                    (float)random.Range(-900.0, 900.0),
                    0.0f,
                    (float)random.Range(-900.0, 900.0));

                Vector3 target = StreetNetwork.NextTarget(from, random, 6.0f, 26.0f);

                int chunkX = Mathf.FloorToInt(target.x / CityLayout.ChunkSize);
                int chunkY = Mathf.FloorToInt(target.z / CityLayout.ChunkSize);

                Assert.IsTrue(CityLayout.IsStreetChunk(chunkX, chunkY),
                    $"a target at ({target.x:0.0}, {target.z:0.0}) is in chunk " +
                    $"({chunkX}, {chunkY}), which is a building lot");
            }
        }

        /// <summary>
        /// Snapping must also land on a street, including from the very middle
        /// of a block -- that is where a pedestrian shoved off the pavement
        /// ends up, and they have to be able to find their way back.
        /// </summary>
        [Test]
        public void SnappingFromInsideABlockStillReachesAStreet()
        {
            var random = new DeterministicRandom(99);

            for (int i = 0; i < Samples; i++)
            {
                var from = new Vector3(
                    (float)random.Range(-900.0, 900.0),
                    0.0f,
                    (float)random.Range(-900.0, 900.0));

                Vector3 snapped = StreetNetwork.SnapToPavement(from);

                int chunkX = Mathf.FloorToInt(snapped.x / CityLayout.ChunkSize);
                int chunkY = Mathf.FloorToInt(snapped.z / CityLayout.ChunkSize);

                Assert.IsTrue(CityLayout.IsStreetChunk(chunkX, chunkY),
                    $"snapping ({from.x:0.0}, {from.z:0.0}) gave " +
                    $"({snapped.x:0.0}, {snapped.z:0.0}), which is not a street");
            }
        }

        /// <summary>
        /// Pedestrians belong on the pavement, not down the middle of the road.
        /// </summary>
        [Test]
        public void SnappingPutsThemOnThePavementNotTheCentreline()
        {
            var random = new DeterministicRandom(7);

            for (int i = 0; i < 500; i++)
            {
                var from = new Vector3(
                    (float)random.Range(-400.0, 400.0),
                    0.0f,
                    (float)random.Range(-400.0, 400.0));

                Vector3 snapped = StreetNetwork.SnapToPavement(from);

                float offset = StreetNetwork.RunsNorthSouth(from)
                    ? StreetNetwork.DistanceToNearestLine(snapped.x)
                    : StreetNetwork.DistanceToNearestLine(snapped.z);

                Assert.AreEqual(StreetNetwork.PavementOffset, offset, 0.01f,
                    "the pedestrian was put somewhere other than the pavement");
            }
        }

        /// <summary>
        /// Steps have to be short enough to be a walk and long enough to be
        /// worth taking. A zero-length step means a pedestrian that arrives
        /// instantly and stands still forever.
        /// </summary>
        [Test]
        public void StepsAreARealWalkingDistance()
        {
            var random = new DeterministicRandom(4242);
            const float minStep = 6.0f;
            const float maxStep = 26.0f;

            for (int i = 0; i < Samples; i++)
            {
                var from = StreetNetwork.SnapToPavement(new Vector3(
                    (float)random.Range(-500.0, 500.0), 0.0f, (float)random.Range(-500.0, 500.0)));

                Vector3 target = StreetNetwork.NextTarget(from, random, minStep, maxStep);
                float distance = Vector3.Distance(
                    new Vector3(from.x, 0.0f, from.z),
                    new Vector3(target.x, 0.0f, target.z));

                Assert.Greater(distance, 0.5f, "a step of nothing at all");
                // A turn at a junction moves across the road as well as along
                // it, so the ceiling allows for both.
                Assert.Less(distance, maxStep + StreetNetwork.PavementOffset * 2.0f + 1.0f,
                    "a step far longer than the walk it was asked for");
            }
        }

        [Test]
        public void TheSameSeedAlwaysPutsTheCrowdInTheSamePlaces()
        {
            var centre = new Vector3(120.0f, 0.0f, -48.0f);

            for (int index = 0; index < 40; index++)
            {
                Vector3 first = StreetNetwork.SpawnPointNear(centre, index, 20260810, 60.0f);
                Vector3 second = StreetNetwork.SpawnPointNear(centre, index, 20260810, 60.0f);
                Assert.AreEqual(first, second, $"pedestrian {index} moved between two identical calls");

                Vector3 other = StreetNetwork.SpawnPointNear(centre, index, 999, 60.0f);
                if (first != other)
                {
                    return;   // a different world puts them elsewhere, as it must
                }
            }

            Assert.Fail("every spawn point was identical across two different world seeds");
        }

        [Test]
        public void EverySpawnPointIsOnAStreetAndNearby()
        {
            var centre = new Vector3(12.0f, 0.0f, 12.0f);
            const float radius = 60.0f;

            for (int index = 0; index < 400; index++)
            {
                Vector3 spot = StreetNetwork.SpawnPointNear(centre, index, 20260810, radius);

                int chunkX = Mathf.FloorToInt(spot.x / CityLayout.ChunkSize);
                int chunkY = Mathf.FloorToInt(spot.z / CityLayout.ChunkSize);
                Assert.IsTrue(CityLayout.IsStreetChunk(chunkX, chunkY),
                    $"pedestrian {index} spawned inside a building lot");

                float distance = Vector2.Distance(
                    new Vector2(centre.x, centre.z), new Vector2(spot.x, spot.z));
                // Snapping to a pavement can push a point outward, so the
                // ceiling allows for a whole street's width past the radius.
                Assert.Less(distance, radius + CityLayout.ChunkSize,
                    $"pedestrian {index} spawned {distance:0}m away, well outside the crowd");
            }
        }
    }
}
