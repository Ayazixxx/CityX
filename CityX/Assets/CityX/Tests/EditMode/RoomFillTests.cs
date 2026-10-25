using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A lit room's bounced light (World.RoomLighting.FillFor, View.RoomFill, drawn by the facade shader): owner, 2026-10-22, "it feels like there
    /// are still really dark spots that are close by to the light source". Lamps shining down left the ceiling round their fittings, the tops of the
    /// walls and the undersides of stairs dark; now every surface of a lit room gets the light its walls throw back -- and only that room's:
    /// never the storey above or below, never the corner room or stairwell carved from a hall, which have their own switch.
    /// </summary>
    public sealed class RoomFillTests
    {
        private const int Seed = 23481876;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        /// <summary>A building turned a third of the way round, somewhere in the world: the fill has to follow the building's own frame.</summary>
        private static readonly Vector3 Origin = new Vector3(1234.5f, 17.0f, -987.25f);
        private const float Turn = 0.6f;
        private static readonly float Cos = Mathf.Cos(Turn);
        private static readonly float Sin = Mathf.Sin(Turn);

        /// <summary>A point in the building's frame, in the world (MeshBuffer.FramePoint's turn).</summary>
        private static Vector3 InTheWorld(Vector3 p) => Origin + new Vector3(p.x * Cos - p.z * Sin, p.y, p.x * Sin + p.z * Cos);

        /// <summary>A direction in the building's frame, in the world.</summary>
        private static Vector3 Along(Vector3 d) => new Vector3(d.x * Cos - d.z * Sin, d.y, d.x * Sin + d.z * Cos);

        private static RoomFill FillOf(RoomLightPlan plan) => new RoomFill(Origin, Along(Vector3.right), Along(Vector3.forward), plan, plan.Colour);

        private static IEnumerable<BuildingSite> Interiors(int count)
        {
            int n = 0;
            foreach (BuildingSite s in TestSites.NearSpawn(World, 12))
            {
                if (s.Shell || s.Sides >= 3 || s.Params.IsSpecial)   // a garage, car park or mall has no rooms
                {
                    continue;
                }
                yield return s;
                if (++n >= count)
                {
                    yield break;
                }
            }
        }

        [Test]
        public void ALitRoomsLightFillsItsFloorWallsAndCeilingAndNeitherTheStoreyAboveNorBelow()
        {
            int rooms = 0;
            foreach (BuildingSite site in Interiors(40))
            {
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    if (plan.LightPoints.Count == 0) continue;
                    rooms++;
                    RoomFill fill = FillOf(plan);
                    Vector3 lamp = plan.LightPoints[0];   // under a fitting: in the room, never in what is cut from it
                    var onFloor = new Vector3(lamp.x, plan.FloorY, lamp.z);
                    var onCeiling = new Vector3(lamp.x, plan.CeilingY, lamp.z);
                    Assert.IsTrue(fill.Covers(InTheWorld(onFloor), Vector3.up), $"{plan.Key}: its own floor");
                    Assert.IsTrue(fill.Covers(InTheWorld(onCeiling), Vector3.down), $"{plan.Key}: its own ceiling");
                    Assert.IsTrue(fill.Covers(InTheWorld(lamp), Vector3.down), $"{plan.Key}: the underside of its fitting");
                    Assert.IsFalse(fill.Covers(InTheWorld(onCeiling + Vector3.up * BuildingInterior.SlabThickness), Vector3.up),
                        $"{plan.Key}: the floor of the storey above");
                    Assert.IsFalse(fill.Covers(InTheWorld(onFloor - Vector3.up * BuildingInterior.SlabThickness), Vector3.down),
                        $"{plan.Key}: the ceiling of the storey below");

                    // The tops and bottoms of its walls, beside the lamp.
                    Rect area = plan.Area;
                    var wallTop = new Vector3(area.xMax, plan.CeilingY - 0.1f, lamp.z);
                    var wallFoot = new Vector3(area.xMax, plan.FloorY + 0.1f, lamp.z);
                    if (!InAnyCut(plan, new Vector2(area.xMax - 0.1f, lamp.z)))
                    {
                        Assert.IsTrue(fill.Covers(InTheWorld(wallTop), Vector3.left), $"{plan.Key}: the top of its wall");
                        Assert.IsTrue(fill.Covers(InTheWorld(wallFoot), Vector3.left), $"{plan.Key}: the foot of its wall");
                        Assert.IsFalse(fill.Covers(InTheWorld(wallTop + Vector3.right * BuildingInterior.WallThickness), Vector3.right),
                            $"{plan.Key}: the outside of its wall");
                    }
                }
            }
            Assert.Greater(rooms, 100, "need a real sample");
        }

        [Test]
        public void NoLampHangsInARoomCarvedFromItsOwnNorOverAStairHole()
        {
            // A lamp goes under the middle of the fittings in its part of the room; fittings either side of an enclosed stairwell in the middle
            // of a hall put it inside the stairwell (seed 19077151's building 7343), lighting a room with its own switch and leaving its own
            // part of the hall dark. Over a stair hole it would shine down into the storey below.
            int lamps = 0;
            foreach (BuildingSite site in Interiors(200))
            {
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    StairPlan up = stairs.Floors[plan.Key.Floor];
                    StairPlan down = plan.Key.Floor > 0 ? stairs.Floors[plan.Key.Floor - 1] : stairs.Basement;
                    foreach (Vector3 lamp in plan.LightPoints)
                    {
                        lamps++;
                        var at = new Vector2(lamp.x, lamp.z);
                        foreach (Rect cut in plan.Cuts)
                        {
                            Assert.IsFalse(cut.Contains(at), $"{plan.Key}: a lamp at {lamp} hangs inside {cut}, a room of its own");
                        }
                        Assert.IsFalse(up != null && up.Hole.Contains(at), $"{plan.Key}: a lamp at {lamp} hangs in the hole the stairs go up through");
                        // (An enclosed stairwell is one shaft through every floor: its light going on down it is what a real stairwell's does.)
                        Assert.IsFalse(plan.Key.Room != RoomLighting.StairwellRoom && down != null && down.Hole.Contains(at),
                            $"{plan.Key}: a lamp at {lamp} hangs over the hole down to the storey below");
                    }
                }
            }
            Assert.Greater(lamps, 300, "need a real sample");
        }

        private static bool InAnyCut(RoomLightPlan plan, Vector2 p)
        {
            foreach (Rect cut in plan.Cuts)
            {
                if (cut.xMin - 0.1f <= p.x && p.x <= cut.xMax + 0.1f && cut.yMin - 0.1f <= p.y && p.y <= cut.yMax + 0.1f) return true;
            }
            return false;
        }

        [Test]
        public void APartitionsTwoFacesBelongToTheRoomsEitherSideOfIt()
        {
            // The corner room's wall to the hall is one sheet seen from both sides (CityMeshBuilder.AddRoomPartition): the hall's side takes the
            // hall's light, the room's side the room's, and neither takes the other's.
            int partitions = 0;
            foreach (BuildingSite site in Interiors(60))
            {
                List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
                foreach (RoomLightPlan hall in plans)
                {
                    if (hall.Key.Room != 0) continue;
                    RoomLightPlan corner = plans.Find(p => p.Key.Floor == hall.Key.Floor && p.Key.Room == 1);
                    if (corner == null) continue;
                    partitions++;
                    CollectionAssert.Contains(hall.Cuts, corner.Area, $"{hall.Key}: the corner room is not cut from the hall");
                    RoomFill hallFill = FillOf(hall);
                    RoomFill cornerFill = FillOf(corner);
                    Rect room = corner.Area;
                    // Its solid inner side, at the room's far edge from the back wall (BuildingInterior.RoomIn), halfway up.
                    var onPartition = new Vector3(room.center.x, (hall.FloorY + hall.CeilingY) * 0.5f, room.yMax);
                    Assert.IsTrue(hallFill.Covers(InTheWorld(onPartition), Along(Vector3.forward)), $"{hall.Key}: the hall's side of the partition is dark");
                    Assert.IsFalse(cornerFill.Covers(InTheWorld(onPartition), Along(Vector3.forward)), $"{hall.Key}: the corner room lights the hall's side");
                    Assert.IsTrue(cornerFill.Covers(InTheWorld(onPartition), Along(Vector3.back)), $"{hall.Key}: the room's side of the partition is dark");
                    Assert.IsFalse(hallFill.Covers(InTheWorld(onPartition), Along(Vector3.back)), $"{hall.Key}: the hall lights the corner room");
                    Assert.IsFalse(hallFill.Covers(InTheWorld(new Vector3(room.center.x, hall.FloorY, room.center.y)), Vector3.up),
                        $"{hall.Key}: the hall lights the corner room's floor");
                }
            }
            Assert.Greater(partitions, 10, "need some corner rooms");
        }

        [Test]
        public void AHallsLightReachesItsWindowRevealsButNotIntoTheStairwellCarvedFromIt()
        {
            int halls = 0, shafts = 0;
            foreach (BuildingSite site in Interiors(200))
            {
                foreach (RoomLightPlan hall in RoomLighting.PlansFor(site))
                {
                    if (hall.Key.Room != 0) continue;
                    halls++;
                    float toGlass = BuildingInterior.WallThickness - BuildingInterior.GlassSetBack;
                    Assert.AreEqual(hall.Area.xMax + toGlass, hall.FillArea.xMax, 1e-4f, $"{hall.Key}: its light stops short of the window glass");
                    Assert.AreEqual(hall.Area.yMin - toGlass, hall.FillArea.yMin, 1e-4f, $"{hall.Key}: its light stops short of the window glass");
                    foreach (Rect cut in hall.Cuts)
                    {
                        if (Mathf.Approximately(cut.xMin, -site.Params.Width * 0.5f) || Mathf.Approximately(cut.xMax, site.Params.Width * 0.5f)) continue;   // the corner room
                        shafts++;
                        RoomFill fill = FillOf(hall);
                        var inShaft = new Vector3(cut.center.x, hall.FloorY, cut.center.y);
                        Assert.IsFalse(fill.Covers(InTheWorld(inShaft), Vector3.up), $"{hall.Key}: the hall lights its enclosed stairwell");
                    }
                }
            }
            Assert.Greater(halls, 100, "need a real sample");
            Assert.Greater(shafts, 5, "need some enclosed stairwells");
        }

        [Test]
        public void TheBouncedLightIsLessThanUnderALampAndASmallRoomIsBrighterThanABigHall()
        {
            foreach (BuildingSite site in Interiors(40))
            {
                foreach (RoomLightPlan plan in RoomLighting.PlansFor(site))
                {
                    if (plan.LightPoints.Count == 0) continue;
                    Assert.Greater(plan.FillLight, 0.05f * RoomLighting.FloorLight, $"{plan.Key}: next to no bounced light");
                    Assert.LessOrEqual(plan.FillLight, RoomLighting.MaxFillShare * RoomLighting.FloorLight + 1e-5f,
                        $"{plan.Key}: lit flat, the walls giving back as much as lands under a lamp");
                }
            }

            RoomLightPlan Room(float width, float depth)
            {
                var plan = new RoomLightPlan { Area = new Rect(0, 0, width, depth), FloorY = 0.0f, CeilingY = 4.0f, LightIntensity = 20.0f };
                plan.LightPoints.Add(Vector3.up * 3.9f);
                return plan;
            }
            Assert.Greater(RoomLighting.FillFor(Room(5, 5)), RoomLighting.FillFor(Room(20, 20)), "a small room's walls give back less than a big hall's");
            RoomLightPlan cut = Room(20, 20);
            cut.Cuts.Add(new Rect(0, 0, 10, 10));
            Assert.Greater(RoomLighting.FillFor(cut), RoomLighting.FillFor(Room(20, 20)), "the room carved from a hall counted as the hall's");
        }

        [Test]
        public void TheShaderKnowsHowARoomIsPacked()
        {
            // The facade shader reads the rooms with its own constants (CityXRoomFill); they have to say what RoomFill and RoomLightDirector say.
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "CityX/View/Shaders/FacadeForwardPass.hlsl"));
            float Define(string name) => float.Parse(Regex.Match(text, $@"#define {name} ([0-9.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.AreEqual(RoomLightDirector.MaxFilledRooms, Define("CITYX_MAX_ROOM_FILLS"));
            Assert.AreEqual(RoomFill.Stride, Define("CITYX_ROOM_FILL_STRIDE"));
            Assert.AreEqual(RoomFill.Nudge, Define("CITYX_ROOM_FILL_NUDGE"), 1e-6f);

            // And the packing itself: the room's origin, axes, plan and light where the shader looks for them.
            RoomLightPlan plan = null;
            foreach (BuildingSite site in Interiors(5))
            {
                plan = RoomLighting.PlansFor(site).Find(p => p.LightPoints.Count > 0);
                if (plan != null) break;
            }
            Assert.IsNotNull(plan);
            RoomFill fill = FillOf(plan);
            var packed = new Vector4[RoomFill.Stride * 2];
            fill.Pack(packed, 1);
            Assert.AreEqual(Vector4.zero, packed[0], "room 1 wrote over room 0");
            Assert.AreEqual(Origin.x, packed[RoomFill.Stride].x, 1e-3f);
            Assert.AreEqual(Origin.y + plan.FloorY, packed[RoomFill.Stride].z, 1e-3f);
            Assert.AreEqual(plan.CeilingY - plan.FloorY, packed[RoomFill.Stride].w, 1e-3f);
            Assert.AreEqual(Cos, packed[RoomFill.Stride + 1].x, 1e-4f);
            Assert.AreEqual(plan.FillArea.xMax, packed[RoomFill.Stride + 2].z, 1e-4f);
            Assert.AreEqual(fill.Light.g, packed[RoomFill.Stride + 5].y, 1e-6f);
        }
    }
}
