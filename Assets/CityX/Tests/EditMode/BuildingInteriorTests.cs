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

                // Its top sits one margin off the wall it climbs towards...
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

        /// <summary>
        /// The flight runs from its wall out into the room -- climbing the
        /// other way, towards that wall -- and neither end pokes through it.
        /// </summary>
        [Test]
        public void TheFlightRunsInFromItsWallAndStaysInTheRoom()
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
                foreach (Vector3 end in new[] { well.WallPoint, well.FootOf(run) })
                {
                    Assert.LessOrEqual(Mathf.Abs(end.x), HalfX + 0.001f,
                        $"floor {floor} leaves the room in X");
                    Assert.LessOrEqual(Mathf.Abs(end.z), HalfZ + 0.001f,
                        $"floor {floor} leaves the room in Z");
                }
            }
        }

        /// <summary>
        /// CONSECUTIVE FLIGHTS MEET, which is the whole reason the corner
        /// rotates rather than staying put.
        ///
        /// You arrive on a floor at the TOP of the flight below, which is
        /// against a wall -- see Stairwell.WallPoint, and note that this is
        /// easy to read backwards: the step at the wall is the full rise tall
        /// and they get shorter into the room, so the flight is highest at the
        /// wall and you climb towards it. The next flight's foot should then
        /// be a walk along that wall away, not a march across open floor.
        /// </summary>
        [Test]
        public void ConsecutiveFlightsMeetRatherThanCrossingTheRoom()
        {
            float run = Run();
            float diagonal = new Vector2(HalfX * 2.0f, HalfZ * 2.0f).magnitude;

            for (int floor = 0; floor < 4; floor++)
            {
                // Where you step off, and where you start climbing again.
                Vector3 arrive = BuildingInterior.StairwellFor(floor, HalfX, HalfZ, run).WallPoint;
                Vector3 depart =
                    BuildingInterior.StairwellFor(floor + 1, HalfX, HalfZ, run).FootOf(run);

                Assert.Less(Vector3.Distance(arrive, depart), diagonal * 0.75f,
                    $"stepping off floor {floor} leaves you most of a room away from the next "
                    + "flight -- that is a march across open floor, not a walk along a wall");
            }
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
                Inside(hole, well.WallPoint, $"floor {floor}'s hole misses the top of its stairs");
                Inside(hole, well.FootOf(run), $"floor {floor}'s hole misses the foot of its stairs");

                // And it stays inside the room, or it would cut the outer wall.
                Assert.GreaterOrEqual(hole.xMin, -HalfX - 0.001f);
                Assert.LessOrEqual(hole.xMax, HalfX + 0.001f);
                Assert.GreaterOrEqual(hole.yMin, -HalfZ - 0.001f);
                Assert.LessOrEqual(hole.yMax, HalfZ + 0.001f);
            }
        }

        /// <summary>
        /// The steps make a real, climbable flight: each one standing on the
        /// floor, rising evenly, ending flush with the storey above.
        /// </summary>
        [Test]
        public void TheStepsClimbEvenlyFromTheFloorToTheStoreyAbove()
        {
            float run = Run();
            const float floorY = 3.2f;
            const float rise = 3.2f;

            BuildingInterior.Stairwell well =
                BuildingInterior.StairwellFor(0, HalfX, HalfZ, run);
            BuildingInterior.Step[] steps = BuildingInterior.Steps(well, run, floorY, rise);

            Assert.AreEqual(BuildingInterior.StepsFor(rise), steps.Length);

            float previous = float.MaxValue;
            foreach (BuildingInterior.Step step in steps)
            {
                // EVERY STEP STANDS ON THE FLOOR. Floating treads would be a
                // flight you can fall through and which reads as slats from
                // the side; Godot builds each one as a box down to the slab.
                Assert.AreEqual(floorY, step.Centre.y - step.Size.y * 0.5f, 0.001f,
                    "a step does not reach the floor");
                Assert.Greater(step.Size.y, 0.0f);
                Assert.AreEqual(BuildingInterior.StairWidth, step.Size.x, 0.001f);
                Assert.AreEqual(run / steps.Length, step.Size.z, 0.001f, "uneven tread depth");

                // Tallest at the wall, shortest in the room -- the flight
                // climbs TOWARDS the wall.
                Assert.Less(step.Top, previous, "the steps do not descend into the room");
                previous = step.Top;
            }

            // The first one reaches the floor above exactly.
            Assert.AreEqual(floorY + rise, steps[0].Top, 0.001f,
                "the top step does not meet the storey above -- that is a lip to trip on");

            // And the last is one riser off the ground, not a kerb to vault.
            Assert.AreEqual(rise / steps.Length, steps[steps.Length - 1].Top - floorY, 0.001f);
        }

        /// <summary>
        /// THE CARVED ROOM NEVER EATS THE STAIRWELL. _room_corner_for_floor
        /// claims this and it is the one thing that would be badly broken and
        /// hard to see: a partition wall standing in the stairwell opening
        /// leaves a floor you cannot leave.
        ///
        /// Swept over real footprints rather than one, because the guarantee
        /// is meant to be structural -- the room is capped at half the
        /// footprint so it cannot cross the centre line -- and a single shape
        /// would not tell a structural guarantee from a lucky one.
        /// </summary>
        [Test]
        public void TheCarvedRoomNeverOverlapsTheStairs()
        {
            foreach (float halfWidth in new[] { 3.5f, 5.0f, 7.0f, 12.0f, 30.0f })
            {
                foreach (float halfDepth in new[] { 3.5f, 5.0f, 9.0f, 25.0f })
                {
                    float run = BuildingInterior.RunFor(halfWidth, halfDepth);
                    for (int floor = 0; floor < 8; floor++)
                    {
                        BuildingInterior.Room room =
                            BuildingInterior.RoomFor(floor, halfWidth, halfDepth);
                        if (!room.Exists)
                        {
                            continue;
                        }
                        Rect hole = BuildingInterior.StairwellFor(
                            floor, halfWidth, halfDepth, run).Hole;

                        Assert.IsFalse(room.Plan.Overlaps(hole),
                            $"in a {halfWidth * 2.0f}x{halfDepth * 2.0f}m building, floor "
                            + $"{floor}'s room {room.Plan} sits on its own stairwell {hole} "
                            + "-- that is a floor you cannot leave");

                        // And inside the building, or it is a room hanging in
                        // the street.
                        Assert.GreaterOrEqual(room.Plan.xMin, -halfWidth - 0.001f);
                        Assert.LessOrEqual(room.Plan.xMax, halfWidth + 0.001f);
                        Assert.GreaterOrEqual(room.Plan.yMin, -halfDepth - 0.001f);
                        Assert.LessOrEqual(room.Plan.yMax, halfDepth + 0.001f);
                    }
                }
            }
        }

        /// <summary>
        /// The room has a way in, and it is on the wall facing the rest of the
        /// floor rather than the exterior corner it hugs.
        /// </summary>
        [Test]
        public void TheCarvedRoomHasADoorwayOnItsInnerWall()
        {
            for (int floor = 0; floor < 8; floor++)
            {
                BuildingInterior.Room room = BuildingInterior.RoomFor(floor, HalfX, HalfZ);
                Assert.IsTrue(room.Exists, $"floor {floor} of a {HalfX * 2}x{HalfZ * 2}m "
                                           + "building should be big enough to carve a room from");

                // The doorway is on one of the room's two X edges, and on the
                // one AWAY from the building's own outside wall.
                bool onLeftEdge = Mathf.Abs(room.DoorWallX - room.Plan.xMin) < 0.001f;
                bool onRightEdge = Mathf.Abs(room.DoorWallX - room.Plan.xMax) < 0.001f;
                Assert.IsTrue(onLeftEdge || onRightEdge,
                    $"floor {floor}'s doorway wall is not an edge of the room");
                Assert.Less(Mathf.Abs(room.DoorWallX), HalfX - 0.001f,
                    $"floor {floor} put its doorway in the building's own outside wall");

                Assert.AreEqual(BuildingInterior.DoorWidth, room.DoorZ1 - room.DoorZ0, 0.001f);
                Assert.GreaterOrEqual(room.DoorZ0, room.Plan.yMin - 0.001f,
                    $"floor {floor}'s doorway runs off the end of its own wall");
                Assert.LessOrEqual(room.DoorZ1, room.Plan.yMax + 0.001f);

                // Against the back wall, never the one the entrance is in.
                Assert.AreEqual(-HalfZ, room.Plan.yMin, 0.001f,
                    $"floor {floor}'s room left the back wall");
            }

            // A building too small to give up a corner gets no room at all,
            // rather than one that swallows the floor.
            Assert.IsFalse(BuildingInterior.RoomFor(0, 2.0f, 8.0f).Exists);
            Assert.IsFalse(BuildingInterior.RoomFor(0, 8.0f, 2.0f).Exists);
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
