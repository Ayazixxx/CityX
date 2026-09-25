using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Fog of war for the (not yet built) map panel. What matters here is not
    /// the map's own drawing, which needs a screen, but the bookkeeping
    /// behind "has the player been near here" -- the ring around a mark, and
    /// standing still not re-marking the same chunks.
    /// </summary>
    public sealed class ExploredChunksTests
    {
        [Test]
        public void NothingIsExploredBeforeTheFirstMark()
        {
            var explored = new ExploredChunks();

            Assert.IsFalse(explored.Contains(new Vector2Int(0, 0)));
            Assert.AreEqual(0, explored.Count);
        }

        [Test]
        public void MarkingACentreExploresTheWholeRingAroundIt()
        {
            var explored = new ExploredChunks();

            explored.Mark(new Vector2Int(5, 5));

            for (int dz = -ExploredChunks.Radius; dz <= ExploredChunks.Radius; dz++)
            {
                for (int dx = -ExploredChunks.Radius; dx <= ExploredChunks.Radius; dx++)
                {
                    var coord = new Vector2Int(5 + dx, 5 + dz);
                    Assert.IsTrue(explored.Contains(coord), $"{coord} inside the ring was not marked explored");
                }
            }
        }

        [Test]
        public void OutsideTheRingIsNotExplored()
        {
            var explored = new ExploredChunks();

            explored.Mark(new Vector2Int(0, 0));

            Assert.IsFalse(explored.Contains(new Vector2Int(ExploredChunks.Radius + 1, 0)));
            Assert.IsFalse(explored.Contains(new Vector2Int(0, ExploredChunks.Radius + 1)));
        }

        [Test]
        public void ExplorationAccumulatesRatherThanReplacing()
        {
            var explored = new ExploredChunks();

            explored.Mark(new Vector2Int(0, 0));
            explored.Mark(new Vector2Int(20, 20));

            Assert.IsTrue(explored.Contains(new Vector2Int(0, 0)), "the first place visited was forgotten");
            Assert.IsTrue(explored.Contains(new Vector2Int(20, 20)));
        }

        /// <summary>
        /// The whole reason Mark is a no-op on a repeated centre: a moving
        /// player calls this every frame, and re-adding the same handful of
        /// keys sixty times a second while standing still is pure waste.
        /// Observed here through Count rather than timing, which a real test
        /// can actually assert on.
        /// </summary>
        [Test]
        public void MarkingTheSameCentreTwiceDoesNothingExtra()
        {
            var explored = new ExploredChunks();
            explored.Mark(new Vector2Int(3, 3));
            int countAfterFirst = explored.Count;

            explored.Mark(new Vector2Int(3, 3));

            Assert.AreEqual(countAfterFirst, explored.Count);
        }

        [Test]
        public void ClearingForgetsEverything()
        {
            var explored = new ExploredChunks();
            explored.Mark(new Vector2Int(0, 0));

            explored.Clear();

            Assert.AreEqual(0, explored.Count);
            Assert.IsFalse(explored.Contains(new Vector2Int(0, 0)));
        }

        /// <summary>
        /// Clearing also resets the "same centre" memory -- otherwise marking
        /// the very spot that was just cleared would be silently swallowed as
        /// a repeat of a centre nothing remembers marking any more.
        /// </summary>
        [Test]
        public void MarkingAgainAfterClearingWorks()
        {
            var explored = new ExploredChunks();
            explored.Mark(new Vector2Int(7, 7));
            explored.Clear();

            explored.Mark(new Vector2Int(7, 7));

            Assert.IsTrue(explored.Contains(new Vector2Int(7, 7)));
        }

        [Test]
        public void ToArrayIsASnapshotNotALiveView()
        {
            var explored = new ExploredChunks();
            explored.Mark(new Vector2Int(0, 0));

            Vector2Int[] snapshot = explored.ToArray();
            explored.Mark(new Vector2Int(50, 50));

            Assert.Less(snapshot.Length, explored.Count,
                "the earlier snapshot grew when more was explored afterwards");
        }
    }
}
