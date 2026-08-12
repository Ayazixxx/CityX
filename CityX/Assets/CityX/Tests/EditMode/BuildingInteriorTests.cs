using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Where a building's floors and staircases land.
    ///
    /// Every one of these is a property the Godot build got wrong at least
    /// once: a flight floating in the middle of the room, four flights on the
    /// same wall, a hole that did not line up with the stairs under it, a
    /// riser a metre tall. They are checked here rather than looked at,
    /// because a staircase that is slightly wrong still looks like a
    /// staircase from across the street.
    /// </summary>
    public class BuildingInteriorTests
    {
        private const float HalfX = 7.0f;
        private const float HalfZ = 5.0f;

        private static float Run() => BuildingInterior.RunFor(HalfX, HalfZ);

        [Test]
        public void FloorsFitTheHeightAndAreCapped()
        {
            float story = CellLayout.NominalFloorMetres;

            Assert.AreEqual(1, BuildingInterior.FloorCount(story, story));
            Assert.AreEqual(3, BuildingInterior.FloorCount(story * 3.0f, story));
            Assert.AreEqual(BuildingInterior.MaxFloors,
                BuildingInterior.FloorCount(story * 40.0f, story),
                "a tall tower should stop at the cap, not fill every storey");

            // Never zero: a building shorter than one storey still needs a
            // floor to stand on.
            Assert.AreEqual(1, BuildingInterior.FloorCount(0.5f, story));
            Assert.AreEqual(1, BuildingInterior.FloorCount(story, 0.0f),
                "a zero storey height should not divide by zero");
        }

        /// <summary>
        /// EVERY FLIGHT FLUSH AGAINST TWO REAL WALLS. A flight in the middle of
        /// the room reads as a freestanding block, which is what centring it
        /// looked like in the Godot build.
        /// </summary>
        [Test]
        public void EveryFlightHugsARealCorner()
        {
            float run = Run();

            for (int floor = 0; floor < 8; floor++)
            {
                BuildingInterior.Stairwell well =
                    BuildingInterior.StairwellFor(floor, HalfX, HalfZ, run);

                // It starts one margin off the wall it climbs away from...
                float alongRun = Mathf.Abs(Vector3.Dot(well.WallPoint, well.RunDirection));
                float nearExtent = Mathf.Abs(well.RunDirection.x) > 0.5f ? HalfX : HalfZ;
                Assert.AreEqual(nearExtent - BuildingInterior.StairMargin, alongRun, 0.001f,
                    $"floor {floor} does not start a margin off its wall");

                // ...and hugs the side wall, its outer edge a hair off it.
                float acrossRun = Mathf.Abs(Vector3.Dot(well.WallPoint, well.WidthDirection));
                float hugExtent = Mathf.Abs(well.WidthDirection.x) > 0.5f ? HalfX : HalfZ;
                Assert.AreEqual(
                    hugExtent - BuildingInterior.StairSideMargin - BuildingInterior.StairWidth * 0.5f,
                    acrossRun, 0.001f, $"floor {floor} is not flush against a side wall");

                Assert.Greater(BuildingInterior.StairSideMargin, 0.0f,
                    "a zero side margin makes the stair coplanar with the wall, which z-fights");
            }
        }

        [Test]
        public void TheFlightClimbsAwayFromItsOwnWallAndStaysInTheRoom()
        {
            float run = Run();

            for (int floor = 0; floor < 8; floor++)
            {
                BuildingInterior.Stairwell well =
                    BuildingInterior.StairwellFor(floor, HalfX, HalfZ, run);

                Assert.AreEqual(1.0f, well.RunDirection.magnitude, 0.001f);
                Assert.AreEqual(0.0f, Vector3.Dot(well.RunDirection, well.WidthDirection), 0.001f,
                    "the run and the width should be at right angles");
                Assert.AreEqual(0.0f, well.RunDirection.y, "a flight should not climb through Y");

                // Both ends inside the room.
                foreach (Vector3 end in new[] { well.WallPoint, well.TopOf(run) })
                {
                    Assert.LessOrEqual(Mathf.Abs(end.x), HalfX + 0.001f,
                        $"floor {floor} leaves the room in X");
                    Assert.LessOrEqual(Mathf.Abs(end.z), HalfZ + 0.001f,
                        $"floor {floor} leaves the room in Z");
                }
            }
        }

        /// <summary>
        /// CONSECUTIVE FLIGHTS DO NOT MEET, and that is what Godot shipped.
        ///
        /// _stairwell_for_floor's own comment claims the next flight starts
        /// along the wall the previous one topped out at -- a walk along one
        /// wall rather than a crossing. Its code does not do that: with the
        /// corner table it actually has, floor 0 hugs the -X wall and tops out
        /// mid-way along it, while floor 1 starts against +X. In a 14 by 10
        /// room they end up 12.7 apart, against a room diagonal of 8.6.
        ///
        /// Measured and pinned rather than asserted as an intention, because
        /// this is the behaviour the Godot build actually has and the port is
        /// meant to match it. If someone later makes the flights genuinely
        /// meet, this fails and says so, which is the right prompt to check
        /// whether that was on purpose.
        /// </summary>
        [Test]
        public void ConsecutiveFlightsLandOnOppositeSidesJustAsGodotsDo()
        {
            float run = Run();

            Vector3 top = BuildingInterior.StairwellFor(0, HalfX, HalfZ, run).TopOf(run);
            Vector3 next = BuildingInterior.StairwellFor(1, HalfX, HalfZ, run).WallPoint;

            Assert.AreEqual(-6.05f, top.x, 0.01f, "floor 0 tops out somewhere new");
            Assert.AreEqual(0.0f, top.z, 0.01f);
            Assert.AreEqual(6.0f, next.x, 0.01f, "floor 1 starts somewhere new");
            Assert.AreEqual(-4.05f, next.z, 0.01f);
        }

        [Test]
        public void FourFloorsUseFourDifferentCorners()
        {
            float run = Run();
            var seen = new System.Collections.Generic.HashSet<Vector3>();
            for (int floor = 0; floor < 4; floor++)
            {
                seen.Add(BuildingInterior.StairwellFor(floor, HalfX, HalfZ, run).WallPoint);
            }
            Assert.AreEqual(4, seen.Count, "four floors in a row reused a corner");

            // And it repeats after four, so the pattern is a rotation.
            Assert.AreEqual(BuildingInterior.StairwellFor(0, HalfX, HalfZ, run).WallPoint,
                BuildingInterior.StairwellFor(4, HalfX, HalfZ, run).WallPoint);
        }

        /// <summary>
        /// THE HOLE IS THE STAIRS' OWN FOOTPRINT. A wider one left strips of
        /// floor hanging over open air, which read as an unintentional pit
        /// rather than a stairwell.
        /// </summary>
        [Test]
        public void TheHoleIsExactlyTheStairsFootprint()
        {
            float run = Run();

            for (int floor = 0; floor < 4; floor++)
            {
                BuildingInterior.Stairwell well =
                    BuildingInterior.StairwellFor(floor, HalfX, HalfZ, run);
                Rect hole = well.Hole;

                Assert.Greater(hole.width, 0.0f, $"floor {floor}'s hole has no width");
                Assert.Greater(hole.height, 0.0f, $"floor {floor}'s hole has no depth");

                // One stair wide across, and exactly the run long. The half
                // width added at one end and subtracted at the other cancels
                // along the run, which is what makes the hole the footprint
                // rather than the footprint plus a margin.
                float shorter = Mathf.Min(hole.width, hole.height);
                float longer = Mathf.Max(hole.width, hole.height);
                Assert.AreEqual(BuildingInterior.StairWidth, shorter, 0.001f,
                    $"floor {floor}'s hole is not one stair wide");
                Assert.AreEqual(run, longer, 0.001f,
                    $"floor {floor}'s hole is not exactly the length of the run");

                // Both ends of the flight are inside it -- ON the edge counts.
                // The hole IS the footprint, so the centre line of the stairs
                // runs from one edge to the other and both ends land exactly
                // on the boundary. Rect.Contains is half-open and would call
                // that a miss; its second argument is allowInverse, not
                // inclusive, which is an easy thing to read wrong.
                Inside(hole, well.WallPoint, $"floor {floor}'s hole misses the foot of its stairs");
                Inside(hole, well.TopOf(run), $"floor {floor}'s hole misses the top of its stairs");

                // And it stays inside the room, or it would cut the outer wall.
                Assert.GreaterOrEqual(hole.xMin, -HalfX - 0.001f);
                Assert.LessOrEqual(hole.xMax, HalfX + 0.001f);
                Assert.GreaterOrEqual(hole.yMin, -HalfZ - 0.001f);
                Assert.LessOrEqual(hole.yMax, HalfZ + 0.001f);
            }
        }

        private static void Inside(Rect hole, Vector3 point, string message)
        {
            const float edge = 0.001f;
            Assert.GreaterOrEqual(point.x, hole.xMin - edge, message);
            Assert.LessOrEqual(point.x, hole.xMax + edge, message);
            Assert.GreaterOrEqual(point.z, hole.yMin - edge, message);
            Assert.LessOrEqual(point.z, hole.yMax + edge, message);
        }

        /// <summary>
        /// Steps come from the rise, not a fixed count. A fixed count at a tall
        /// storey height gave risers over a metre high -- giant against a 1.8m
        /// person.
        /// </summary>
        [Test]
        public void StepsAreARealHeightWhateverTheRise()
        {
            foreach (float rise in new[] { 2.0f, 3.2f, 4.5f, 6.5f, 12.0f })
            {
                int steps = BuildingInterior.StepsFor(rise);
                float riser = rise / steps;
                Assert.Less(riser, 0.45f, $"a {rise}m flight has {riser:0.00}m risers -- too tall "
                                          + "to climb and far taller than a real step");
                Assert.Greater(steps, 1, "a flight should be steps, not a ramp");
            }

            Assert.AreEqual(2, BuildingInterior.StepsFor(0.05f),
                "even a tiny rise should be two steps rather than none");
        }

        /// <summary>
        /// The run reaches the room's own centre. Stopping short of it read as
        /// a stubby staircase rather than a real flight.
        /// </summary>
        [Test]
        public void TheRunReachesTheMiddleOfTheRoom()
        {
            Assert.AreEqual(Mathf.Min(HalfX, HalfZ) - BuildingInterior.StairMargin,
                BuildingInterior.RunFor(HalfX, HalfZ), 0.001f);

            // A room barely wider than the margin still gets a positive run
            // rather than a backwards one.
            Assert.Greater(BuildingInterior.RunFor(0.5f, 0.5f), 0.0f,
                "a tiny room produced a negative run, which inverts the stairs");
        }
    }
}
