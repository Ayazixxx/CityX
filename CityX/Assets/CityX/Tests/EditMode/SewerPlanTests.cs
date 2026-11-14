using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sewers under the city (docs/SEWERS.md; owner, 2026-11-09: "one world", "make the manhole ladder entrances", "make the flood tunnel
    /// entrances too", "variety is good"): a tunnel under every road sized by the road, a room under every junction with a ladder to a
    /// manhole, a mouth where a bridge crosses a dry channel, all of it worked out from the plan alone.
    /// </summary>
    public sealed class SewerPlanTests
    {
        private const int Seed = CitySeed.Default;

        /// <summary>Worlds tried, in turn, for something the first may not have near enough (a bridge with a mouth under it).</summary>
        private static readonly int[] Seeds = { Seed, 45678679, 91736402, 58203917, 63428175 };

        private static CityWorld _world;

        private static CityWorld World => _world ??= new CityWorld(Seed);

        private static PlanWorld Plan => World.Plan;

        private static SewerPlan Sewers => SewerPlan.For(Plan);

        private static double Ground(Vec2 p) => Plan.GroundHeight(p.X, p.Y);

        [Test]
        public void EveryRoadButAStepStreetOrABridgeHasATunnelOfItsOwnSize()
        {
            int tunnels = 0, without = 0;
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                if (!edge.Alive) continue;
                SewerTube tube = Sewers.Tube(edge.Id);
                if (edge.Bridge || StepStreets.Is(Plan, edge))
                {
                    Assert.IsNull(tube, $"road {edge.Id} is a bridge or steps and has a tunnel under it");
                    continue;
                }
                if (tube == null)
                {
                    without++;
                    continue;
                }
                tunnels++;
                Assert.AreEqual(SewerPlan.KindOf(edge.Class), tube.Section.Kind, $"road {edge.Id}");
                Assert.Greater(tube.End - tube.Start, SewerPlan.ShortestTube - 1e-6, $"road {edge.Id}: no length to it");
                for (int i = 0; i < tube.Stations.Length; i++)
                {
                    if (i > 0) Assert.Greater(tube.Stations[i], tube.Stations[i - 1], $"road {edge.Id}: its rings are out of order");
                    Assert.AreEqual(Ground(tube.At(tube.Stations[i])) - SewerPlan.Depth, tube.Floors[i], 1e-6, $"road {edge.Id}: ring {i} is not under its street");
                }
            }
            Assert.Greater(tunnels, 500);
            Assert.Less(without, tunnels / 10, "more than a tenth of the roads are too short for a tunnel");
        }

        [Test]
        public void TheSectionsAreBigEnoughForTheCameraAndHaveAChannelAndTwoWalkways()
        {
            foreach (SewerKind kind in Enum.GetValues(typeof(SewerKind)))
            {
                foreach (SewerStyle style in Enum.GetValues(typeof(SewerStyle)))
                {
                    SewerSection section = SewerSection.For(kind, style);
                    string what = $"{kind} {style}";
                    Assert.LessOrEqual(section.HalfWidth, SewerSection.HalfWidthOf(kind) + 1e-6, what + ": wider than the plan lays out");
                    Assert.GreaterOrEqual(section.FloorHalfWidth * 2.0, 4.6, what + ": too narrow a floor for the camera");
                    Assert.GreaterOrEqual(section.Crown, 4.5, what + ": too low for the camera");
                    Assert.Greater(section.FloorHalfWidth - section.ChannelHalfWidth, 1.2, what + ": no walkway to speak of");
                    Assert.AreEqual(section.Outline.Length, section.Surfaces.Length, what);
                    // The outline goes round anticlockwise (its inside on the left), and the opening is convex, as the wall cutter needs.
                    Assert.Greater(PlanMath.SignedArea(section.Outline), 0.0, what + ": its outline goes round the wrong way");
                    Vec2[] o = section.Opening;
                    for (int i = 0; i < o.Length; i++)
                    {
                        Vec2 a = o[i], b = o[(i + 1) % o.Length], c = o[(i + 2) % o.Length];
                        Assert.GreaterOrEqual(Vec2.Cross(b - a, c - b), -1e-9, what + $": its opening bends in at point {i}");
                    }
                }
            }
        }

        [Test]
        public void EveryTunnelIntoARoomEndsExactlyAtItsMouthInTheRoomsWall()
        {
            int mouths = 0;
            foreach (SewerRoom room in Sewers.Rooms)
            {
                int n = room.Plate.Length;
                for (int i = 0; i < n; i++)
                {
                    if (room.Opening[i] == null) continue;
                    mouths++;
                    Vec2 middle = (room.Plate[i] + room.Plate[(i + 1) % n]) * 0.5;
                    Assert.AreEqual(room.Floor[i], room.Floor[(i + 1) % n], 1e-9, "a mouth's two feet are at different heights");
                    Assert.AreEqual(Ground(middle) - SewerPlan.Depth, room.Floor[i], 1e-6);
                    Assert.AreEqual(SewerSection.HalfWidthOf(room.Opening[i].Kind) * 2.0, (room.Plate[i] - room.Plate[(i + 1) % n]).Length, 1e-6,
                        "the wall a tunnel opens in is not the tunnel's width");
                    Assert.IsTrue(SomethingEndsAt(room, middle), $"room {room.Node.Id}: side {i} is open and nothing ends there");
                    Assert.Greater(room.Ceiling[i] - room.Floor[i], room.Opening[i].Crown + 0.5, "the room is lower than the tunnel into it");
                }
            }
            Assert.Greater(mouths, 1000);
        }

        private static bool SomethingEndsAt(SewerRoom room, Vec2 point)
        {
            foreach (RoadEdge edge in room.Node.Edges)
            {
                SewerTube tube = Sewers.Tube(edge.Id);
                if (tube != null && ((tube.At(tube.Start) - point).Length < 1e-6 || (tube.At(tube.End) - point).Length < 1e-6)) return true;
                foreach (SewerOutfall outfall in Sewers.Outfalls(edge.Id))
                {
                    if ((outfall.Tube.At(0.0) - point).Length < 1e-6) return true;
                }
            }
            return false;
        }

        [Test]
        public void ARoomWithSpaceHasALadderFromItsFloorToTheStreetAndSomeLidsAreOff()
        {
            int rooms = 0, shafts = 0, open = 0;
            foreach (SewerRoom room in Sewers.Rooms)
            {
                rooms++;
                SewerShaft shaft = room.Shaft;
                if (shaft == null) continue;
                shafts++;
                if (shaft.Open) open++;
                foreach (Vec2 corner in shaft.Ring)
                {
                    Assert.IsTrue(PlanMath.PointInPolygon(corner, room.Plate), $"room {room.Node.Id}: its shaft comes down outside it");
                }
                Ladder ladder = Ladders.Of(shaft);
                Assert.AreEqual(room.CentreFloor, ladder.Foot.y, 1e-3);
                Assert.AreEqual(SewerPlan.Depth, ladder.Height, 0.4, $"room {room.Node.Id}: the ladder does not reach the street");
                Assert.AreEqual(1.0f, ladder.Normal.magnitude, 1e-4f);
                Assert.AreEqual(0.0f, ladder.Normal.y, 1e-6f);
                // Whoever stands at the top stands on the junction's road, clear of the hole.
                var stand = new Vec2(ladder.StandAtTop.x, ladder.StandAtTop.z);
                Assert.Greater((stand - shaft.Centre).Length, SewerPlan.NeckHalfWidth + 0.3);
                Assert.IsTrue(PlanMath.PointInPolygon(stand, Plan.Geometry.Junctions[room.Node.Id].Plate), $"room {room.Node.Id}: the top of its ladder is off the junction");
                Assert.AreEqual(Ground(stand) + PlanGeometry.RoadLift, ladder.Top.y, 0.05);
            }
            Debug.Log($"sewers of seed {Seed}: {rooms} rooms, {shafts} with a shaft, {open} of them open");
            Assert.Greater(shafts, rooms * 4 / 10, "fewer than four rooms in ten have a way up");
            Assert.That((double)open / shafts, Is.InRange(SewerPlan.OpenShare - 0.08, SewerPlan.OpenShare + 0.08));
        }

        [Test]
        public void AMouthStandsWhereTheBankHasFallenToItsCrownAndItsFloorRunsOutOntoTheBank()
        {
            int mouths = 0;
            var gates = new int[3];
            foreach (int seed in Seeds)
            {
                PlanWorld plan = new CityWorld(seed).Plan;
                SewerPlan sewers = SewerPlan.For(plan);
                foreach (SewerOutfall outfall in sewers.AllOutfalls)
                {
                    mouths++;
                    gates[(int)outfall.Gate]++;
                    SewerTube tube = outfall.Tube;
                    string what = $"seed {seed}, bridge {outfall.Bridge.Id}";
                    Assert.IsTrue(outfall.Bridge.Bridge, what);
                    Assert.GreaterOrEqual(outfall.Portal, SewerPlan.NearestPortal, what);
                    Assert.Greater(outfall.End, outfall.Portal, what);
                    double crown = outfall.Floor + tube.Section.Crown;
                    for (double s = 0.0; s < outfall.Portal - 1.0; s += 1.0)
                    {
                        Vec2 p = tube.At(s);
                        Assert.Greater(plan.GroundHeight(p.X, p.Y), crown + SewerPlan.PortalCover - 1e-6, what + $": the buried part shows through the bank {s:0} m along");
                    }
                    Vec2 at = tube.At(outfall.Portal);
                    Assert.LessOrEqual(plan.GroundHeight(at.X, at.Y), crown + SewerPlan.PortalCover + 1e-6, what);
                    at = tube.At(outfall.End);
                    Assert.LessOrEqual(plan.GroundHeight(at.X, at.Y), outfall.Floor + 0.1 + 1e-6, what + ": the floor ends in the bank");
                    Assert.IsNotNull(sewers.Room(outfall.From.Id), what + ": it comes out of no room");
                }
                if (mouths >= 4) break;
            }
            Debug.Log($"mouths: {mouths}; open {gates[0]}, a door ajar {gates[1]}, bars rusted through {gates[2]}");
            Assert.Greater(mouths, 0, "no bridge in any of these worlds has a tunnel's mouth under it");
        }

        [Test]
        public void ItIsDarkInATunnelAndInARoomAndNotOnTheStreetOverThem()
        {
            int checkedRooms = 0;
            foreach (SewerRoom room in Sewers.Rooms)
            {
                Vec2 c = room.Node.P;
                // Off the middle, where a shaft's neck would be.
                Vec2 inRoom = c + (room.Plate[0] - c) * 0.5;
                Assert.AreEqual(1.0, Sewers.DarknessAt(inRoom.X, room.CentreFloor + 1.5, inRoom.Y), 1e-9, $"room {room.Node.Id}");
                Assert.AreEqual(0.0, Sewers.DarknessAt(inRoom.X, Ground(inRoom) + 1.5, inRoom.Y), 1e-9, $"the street over room {room.Node.Id}");
                if (room.Shaft != null)
                {
                    double top = Sewers.DarknessAt(c.X, room.Shaft.Top - 0.05, c.Y), low = Sewers.DarknessAt(c.X, room.CentreCeiling + 0.05, c.Y);
                    Assert.Less(top, 0.15, "the top of a shaft's neck is dark");
                    Assert.Greater(low, 0.85, "the bottom of a shaft's neck is light");
                }
                if (++checkedRooms >= 200) break;
            }
            int tubes = 0;
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                SewerTube tube = Sewers.Tube(edge.Id);
                if (tube == null) continue;
                double middle = (tube.Start + tube.End) * 0.5;
                Vec2 p = tube.At(middle);
                Assert.AreEqual(1.0, Sewers.DarknessAt(p.X, tube.FloorAt(middle) + 1.5, p.Y), 1e-9, $"the tunnel under road {edge.Id}");
                Assert.AreEqual(0.0, Sewers.DarknessAt(p.X, Ground(p) + 1.5, p.Y), 1e-9, $"road {edge.Id} itself");
                if (++tubes >= 300) break;
            }
        }

        [Test]
        public void EveryMachineLaysTheSameSewersAndTheyAreNotAllAlike()
        {
            PlanWorld one = PlanWorld.Create(Seed, PlanWorld.DefaultConfig()), other = PlanWorld.Create(Seed, PlanWorld.DefaultConfig());
            SewerPlan a = SewerPlan.For(one), b = SewerPlan.For(other);
            var styles = new HashSet<SewerStyle>();
            int same = 0;
            foreach (RoadEdge edge in one.Data.Edges)
            {
                SewerTube ta = a.Tube(edge.Id), tb = b.Tube(edge.Id);
                Assert.AreEqual(ta == null, tb == null, $"road {edge.Id}");
                if (ta == null) continue;
                same++;
                styles.Add(ta.Section.Style);
                Assert.AreEqual(ta.Section.Style, tb.Section.Style);
                Assert.AreEqual(ta.Stations.Length, tb.Stations.Length);
                Assert.AreEqual(ta.Floors[0], tb.Floors[0], 0.0);
            }
            foreach (SewerRoom room in a.Rooms)
            {
                SewerRoom twin = b.Room(room.Node.Id);
                Assert.IsNotNull(twin);
                Assert.AreEqual(room.Shaft == null, twin.Shaft == null);
                if (room.Shaft != null) Assert.AreEqual(room.Shaft.Open, twin.Shaft.Open, $"room {room.Node.Id}: its lid is off on one machine and on on another");
            }
            Assert.Greater(same, 500);
            Assert.GreaterOrEqual(styles.Count, 4, "the tunnels are of fewer than four kinds");
        }
    }
}
