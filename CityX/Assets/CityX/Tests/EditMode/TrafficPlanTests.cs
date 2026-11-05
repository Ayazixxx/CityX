using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>Which of the real city's junctions get lights, STOP signs or nothing, and how the lights are timed (docs/TRAFFIC_CONTROL.md).</summary>
    public sealed class TrafficPlanTests
    {
        private const int Seed = 95998845;

        private static PlanWorld _plan;

        private static PlanWorld Plan => _plan ??= new CityWorld(Seed).Plan;

        private static IEnumerable<TrafficJunction> Junctions()
        {
            foreach (TrafficJunction junction in Plan.Traffic.Junctions)
            {
                if (junction != null) yield return junction;
            }
        }

        private static IEnumerable<TrafficJunction> Signals()
        {
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.Control == JunctionControl.Signal) yield return junction;
            }
        }

        private static int Driven(TrafficJunction junction)
        {
            int count = 0;
            foreach (bool drives in junction.Drives) if (drives) count++;
            return count;
        }

        private static bool AtABridge(TrafficJunction junction)
        {
            for (int i = 0; i < junction.Legs.Length; i++)
            {
                if (junction.Drives[i] && junction.Legs[i].Bridge) return true;
            }
            return false;
        }

        private static double Population(TrafficJunction junction) => Plan.Fields.Population(junction.Node.P.X, junction.Node.P.Y);

        // ---------------------------------------------------------------- which junction gets what

        [Test]
        public void WhereTwoMainRoadsMeetThereAreLights()
        {
            int met = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.ClassOf(false) < RoadClass.Arterial) continue;
                met++;
                Assert.AreEqual(JunctionControl.Signal, junction.Control, $"node {junction.Node.Id}: {junction.ClassOf(true)} meets {junction.ClassOf(false)} without lights");
            }
            Assert.Greater(met, 20, "the city has hardly any main roads meeting");
        }

        [Test]
        public void LightsOnASideStreetsCrossingKeepTheirDistanceFromOtherLights()
        {
            var signals = new List<TrafficJunction>(Signals());
            foreach (TrafficJunction a in signals)
            {
                if (a.ClassOf(false) != RoadClass.Street) continue;
                double least = a.ClassOf(true) == RoadClass.Street ? TrafficPlan.CoreStreetSignalSpacing : TrafficPlan.CoreSignalSpacing;
                foreach (TrafficJunction b in signals)
                {
                    if (a == b) continue;
                    double apart = (a.Node.P - b.Node.P).Length;
                    Assert.GreaterOrEqual(apart, Math.Min(least, TrafficPlan.CoreStreetSignalSpacing) - 1e-6, $"lights at nodes {a.Node.Id} and {b.Node.Id} are {apart:F0} m apart");
                }
            }
        }

        [Test]
        public void TheMainRoadNeverStopsForASideStreet()
        {
            int checkedCount = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.Control != JunctionControl.StopMinor) continue;
                int stopping = 0, free = 0;
                for (int i = 0; i < junction.Legs.Length; i++)
                {
                    if (!junction.Drives[i]) continue;
                    if (junction.Stops(i))
                    {
                        stopping++;
                        Assert.AreEqual(RoadClass.Street, junction.Legs[i].Class, $"node {junction.Node.Id}: a {junction.Legs[i].Class} stops for a side street");
                    }
                    else
                    {
                        free++;
                    }
                }
                Assert.AreEqual(2, free, $"node {junction.Node.Id}: the street that runs free has {free} ends");
                Assert.GreaterOrEqual(stopping, 1, $"node {junction.Node.Id}: a stop with nobody stopping");
                checkedCount++;
            }
            Assert.Greater(checkedCount, 500);
        }

        [Test]
        public void AStreetThatEndsAtAnotherIsTheOneThatStops()
        {
            int ends = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.Control != JunctionControl.StopMinor || Driven(junction) != 3) continue;
                int a = -1, b = -1;
                for (int i = 0; i < junction.Legs.Length; i++)
                {
                    if (!junction.Drives[i] || junction.Stops(i)) continue;
                    if (a < 0) a = i; else b = i;
                }
                // Where the junction has a street running straight through, that is the one left free.
                double straightest = 1.0;
                for (int i = 0; i < junction.Legs.Length; i++)
                {
                    for (int j = i + 1; j < junction.Legs.Length; j++)
                    {
                        if (junction.Drives[i] && junction.Drives[j] && junction.Legs[i].Class == junction.Legs[j].Class)
                        {
                            straightest = Math.Min(straightest, Vec2.Dot(junction.Dirs[i], junction.Dirs[j]));
                        }
                    }
                }
                if (straightest > -0.95 || junction.ClassOf(true) != RoadClass.Street) continue;
                ends++;
                Assert.Less(Vec2.Dot(junction.Dirs[a], junction.Dirs[b]), -0.9, $"node {junction.Node.Id}: the street running through stops, and the one that ends does not");
            }
            Assert.Greater(ends, 100);
        }

        [Test]
        public void AnAllWayStopStopsEveryoneAndStandsOnlyWhereSideStreetsCross()
        {
            int count = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.Control != JunctionControl.AllWayStop) continue;
                count++;
                Assert.AreEqual(RoadClass.Street, junction.ClassOf(true), $"node {junction.Node.Id}: an all-way stop on a main road");
                Assert.GreaterOrEqual(Driven(junction), 4);
                for (int i = 0; i < junction.Legs.Length; i++)
                {
                    Assert.AreEqual(junction.Drives[i], junction.Stops(i), $"node {junction.Node.Id}: not everyone stops");
                }
            }
            Assert.Greater(count, 30);
        }

        [Test]
        public void TheBusierThePlaceTheMoreItsSideStreetsCrossingsAreGoverned()
        {
            foreach (TrafficJunction junction in Junctions())
            {
                if (junction.ClassOf(true) != RoadClass.Street || Driven(junction) < 4) continue;
                double population = Population(junction);
                switch (junction.Control)
                {
                    case JunctionControl.Signal: Assert.GreaterOrEqual(population, TrafficPlan.CorePopulation); break;
                    case JunctionControl.AllWayStop: Assert.GreaterOrEqual(population, TrafficPlan.BusyPopulation); break;
                    case JunctionControl.StopMinor: Assert.GreaterOrEqual(population, TrafficPlan.QuietPopulation); break;
                    default: Assert.Less(population, TrafficPlan.QuietPopulation); break;
                }
            }
        }

        [Test]
        public void ACityHasFarMoreStopSignsThanLights()
        {
            int lights = 0, stops = 0, all = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                all++;
                if (junction.Control == JunctionControl.Signal) lights++;
                if (junction.Control == JunctionControl.StopMinor || junction.Control == JunctionControl.AllWayStop) stops++;
            }
            Plan.TryFindSpawn(out double sx, out double sz);
            TrafficJunction atSpawn = null;
            foreach (TrafficJunction junction in Junctions())
            {
                if (atSpawn == null || (junction.Node.P - new Vec2(sx, sz)).Length < (atSpawn.Node.P - new Vec2(sx, sz)).Length) atSpawn = junction;
            }
            TestContext.WriteLine($"{Plan.Data.Nodes.Count} nodes, {all} junctions: {lights} with lights, {stops} with STOP signs; scale {CityScale.Active.Name}; " +
                $"spawn {sx:F0},{sz:F0}, nearest junction node {atSpawn.Node.Id} ({atSpawn.Control}, {atSpawn.ClassOf(true)} x {atSpawn.ClassOf(false)})");
            // American cities light roughly a tenth to a quarter of their junctions.
            Assert.Greater(lights, all * 0.08, $"{lights} of {all} junctions have lights");
            Assert.Less(lights, all * 0.25, $"{lights} of {all} junctions have lights");
            Assert.Greater(stops, lights * 3, $"{stops} stops to {lights} lights");
        }

        [Test]
        public void WhichStreetStopsAlternatesFromCornerToCorner()
        {
            // Along a side street, between two neighbouring four-way crossings each with a stop on one street: the street stops at some and runs free at others.
            int same = 0, alternate = 0;
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                if (!edge.Alive || edge.Class != RoadClass.Street) continue;
                TrafficJunction a = Plan.Traffic.At(edge.A), b = Plan.Traffic.At(edge.B);
                if (a == null || b == null || a.Control != JunctionControl.StopMinor || b.Control != JunctionControl.StopMinor) continue;
                if (Driven(a) != 4 || Driven(b) != 4 || a.ClassOf(true) != RoadClass.Street || b.ClassOf(true) != RoadClass.Street) continue;
                bool stopsAtA = a.Stops(a.LegOf(edge)), stopsAtB = b.Stops(b.LegOf(edge));
                if (stopsAtA == stopsAtB) same++; else alternate++;
            }
            Assert.Greater(alternate + same, 5, "no two such crossings are neighbours in this city");
            Assert.Greater(alternate, (alternate + same) * 0.6, $"{alternate} of {alternate + same} neighbouring pairs alternate");
        }

        [Test]
        public void AJunctionAtABridgesEndIsGovernedLikeAnyOther()
        {
            // (What cannot stand there, over the drop, is left out piece by piece when the chunks are built: TrafficHardwareTests.)
            int governed = 0, bridges = 0;
            foreach (TrafficJunction junction in Junctions())
            {
                if (!AtABridge(junction)) continue;
                bridges++;
                if (junction.Control != JunctionControl.None) governed++;
                if (junction.ClassOf(false) >= RoadClass.Arterial) Assert.AreEqual(JunctionControl.Signal, junction.Control, $"node {junction.Node.Id}");
            }
            Assert.Greater(bridges, 0, "no junction at a bridge's end in this city");
            Assert.Greater(governed, bridges / 2, $"{governed} of {bridges} junctions at a bridge's end have a sign or lights");
        }

        [Test]
        public void TheSamePlanGivesTheSameLightsEveryTime()
        {
            TrafficPlan again = TrafficPlan.Build(Plan);
            for (int i = 0; i < again.Junctions.Length; i++)
            {
                TrafficJunction a = Plan.Traffic.Junctions[i], b = again.Junctions[i];
                Assert.AreEqual(a == null, b == null);
                if (a == null) continue;
                Assert.AreEqual(a.Control, b.Control, $"node {i}");
                if (a.Timing != null)
                {
                    Assert.AreEqual(a.Timing.Offset, b.Timing.Offset, 1e-9);
                    Assert.AreEqual(a.Timing.GreenMajor, b.Timing.GreenMajor, 1e-9);
                }
            }
        }

        // ---------------------------------------------------------------- how the lights are timed

        [Test]
        public void EveryLightsCycleAddsUpAndDividesAnHour()
        {
            foreach (TrafficJunction junction in Signals())
            {
                SignalTiming t = junction.Timing;
                Assert.IsNotNull(t, $"node {junction.Node.Id} has lights and no timing");
                Assert.AreEqual(t.Cycle, t.GreenMajor + t.YellowMajor + t.AllRedMajor + t.GreenMinor + t.YellowMinor + t.AllRedMinor, 1e-9);
                Assert.AreEqual(0.0, SignalTiming.ClockWrap % t.Cycle, 1e-9, "a cycle that does not divide the hour jumps when the clock wraps");
                Assert.GreaterOrEqual(t.Cycle, 60.0);
                Assert.LessOrEqual(t.Cycle, 120.0);
                Assert.GreaterOrEqual(t.Offset, 0.0);
                Assert.Less(t.Offset, t.Cycle);
            }
        }

        [Test]
        public void YellowsAndAllRedsAreTheStandards()
        {
            foreach (TrafficJunction junction in Signals())
            {
                SignalTiming t = junction.Timing;
                foreach (double yellow in new[] { t.YellowMajor, t.YellowMinor })
                {
                    Assert.GreaterOrEqual(yellow, 3.0, "MUTCD 4D.26: 3 to 6 s");
                    Assert.LessOrEqual(yellow, 6.0);
                }
                foreach (double allRed in new[] { t.AllRedMajor, t.AllRedMinor })
                {
                    Assert.GreaterOrEqual(allRed, 1.0);
                    Assert.LessOrEqual(allRed, 2.0);
                }
                Assert.GreaterOrEqual(SignalTiming.YellowFor(RoadClass.Boulevard), SignalTiming.YellowFor(RoadClass.Street), "a faster road's yellow is no shorter");
            }
        }

        [Test]
        public void TheTwoStreetsAreNeverGoingAtOnceAndEachGetsItsTurn()
        {
            foreach (TrafficJunction junction in Signals())
            {
                SignalTiming t = junction.Timing;
                bool mainWent = false, sideWent = false, allRed = false;
                for (double clock = 0.0; clock < t.Cycle; clock += 0.25)
                {
                    SignalLamp main = t.VehicleAt(true, clock), side = t.VehicleAt(false, clock);
                    Assert.IsTrue(main == SignalLamp.Red || side == SignalLamp.Red, $"node {junction.Node.Id} at {clock:F2} s: {main} for the main street and {side} for the side");
                    mainWent |= main == SignalLamp.Green;
                    sideWent |= side == SignalLamp.Green;
                    allRed |= main == SignalLamp.Red && side == SignalLamp.Red;
                }
                Assert.IsTrue(mainWent && sideWent, $"node {junction.Node.Id}: a street never gets a green");
                Assert.IsTrue(allRed, $"node {junction.Node.Id}: no all-red between the greens");
                Assert.GreaterOrEqual(t.GreenMajor, t.GreenMinor - 1e-9, "the main street's green is no shorter than the side street's");
            }
        }

        [Test]
        public void AGreenIsFollowedByYellowThenRed()
        {
            foreach (TrafficJunction junction in Signals())
            {
                SignalTiming t = junction.Timing;
                foreach (bool major in new[] { true, false })
                {
                    SignalLamp last = t.VehicleAt(major, 0.0);
                    for (double clock = 0.05; clock <= t.Cycle * 2.0; clock += 0.05)
                    {
                        SignalLamp now = t.VehicleAt(major, clock);
                        if (now != last)
                        {
                            bool lawful = (last == SignalLamp.Green && now == SignalLamp.Yellow) || (last == SignalLamp.Yellow && now == SignalLamp.Red)
                                || (last == SignalLamp.Red && now == SignalLamp.Green);
                            Assert.IsTrue(lawful, $"node {junction.Node.Id}: {last} went to {now}");
                        }
                        last = now;
                    }
                }
            }
        }

        [Test]
        public void WalkersGetSevenSecondsOfWalkAndTimeToFinishCrossing()
        {
            foreach (TrafficJunction junction in Signals())
            {
                SignalTiming t = junction.Timing;
                foreach (bool crossesMain in new[] { true, false })
                {
                    double walk = t.Pedestrian(crossesMain, WalkLamp.Walk).Length;
                    double clear = t.Pedestrian(crossesMain, WalkLamp.FlashingHand).Length;
                    double width = junction.WidthOf(crossesMain);
                    Assert.GreaterOrEqual(walk, SignalTiming.WalkSeconds - 1e-9, $"node {junction.Node.Id}: {walk:F1} s of WALK (MUTCD 4E.06: 7)");
                    // The flashing hand, with the yellow and all-red after it, covers the crossing at 3.5 ft/s.
                    double buffer = crossesMain ? t.YellowMinor + t.AllRedMinor : t.YellowMajor + t.AllRedMajor;
                    Assert.GreaterOrEqual(clear + buffer, width / SignalTiming.CrossingSpeed - 1e-9, $"node {junction.Node.Id}: {clear:F0} s to cross {width:F0} m");
                }
                for (double clock = 0.0; clock < t.Cycle; clock += 0.25)
                {
                    // WALK only while the traffic across the walker's path is stopped.
                    if (t.PedestrianAt(true, clock) != WalkLamp.Hand)
                    {
                        Assert.AreEqual(SignalLamp.Red, t.VehicleAt(true, clock), $"node {junction.Node.Id} at {clock:F2} s: walking across the main street's green");
                    }
                    if (t.PedestrianAt(false, clock) != WalkLamp.Hand)
                    {
                        Assert.AreEqual(SignalLamp.Red, t.VehicleAt(false, clock), $"node {junction.Node.Id} at {clock:F2} s: walking across the side street's green");
                    }
                }
            }
        }

        [Test]
        public void TheGreenWaveRunsAlongAMainRoadAtTheRoadsSpeed()
        {
            // Two lights joined by one piece of a main road that is the main street at both: their greens start the drive between them apart.
            int pairs = 0;
            foreach (RoadEdge edge in Plan.Data.Edges)
            {
                if (!edge.Alive || edge.Class < RoadClass.Arterial) continue;
                TrafficJunction a = Plan.Traffic.At(edge.A), b = Plan.Traffic.At(edge.B);
                if (a?.Timing == null || b?.Timing == null) continue;
                int la = a.LegOf(edge), lb = b.LegOf(edge);
                if (!a.Major[la] || !b.Major[lb] || a.ClassOf(true) != edge.Class || b.ClassOf(true) != edge.Class) continue;
                if (Math.Abs(a.Timing.Cycle - b.Timing.Cycle) > 1e-9) continue;
                double cycle = a.Timing.Cycle;
                double drive = edge.Length / SignalTiming.ProgressionSpeed(edge.Class);
                // One way down the road or the other: the wave has one direction, and which is the road's own business.
                double later = SignalTiming.Mod(a.Timing.Offset - b.Timing.Offset, cycle);
                double off = Math.Min(Circular(later - drive, cycle), Circular(later + drive, cycle));
                Assert.Less(off, 0.05, $"nodes {a.Node.Id} and {b.Node.Id}: greens {later:F1} s apart over a {drive:F1} s drive");
                pairs++;
            }
            Assert.Greater(pairs, 5, "no two lights stand on one piece of main road");
        }

        private static double Circular(double seconds, double cycle) => Math.Abs(SignalTiming.Mod(seconds + cycle * 0.5, cycle) - cycle * 0.5);

        [Test]
        public void OnlyASideStreetsLightsFlashLateAtNight()
        {
            foreach (TrafficJunction junction in Signals())
            {
                Assert.AreEqual(junction.ClassOf(false) == RoadClass.Street, junction.Timing.NightFlash, $"node {junction.Node.Id}");
            }
            Assert.IsTrue(SignalTiming.FlashLit(0.1));
            Assert.IsFalse(SignalTiming.FlashLit(0.6));
        }

        [Test]
        public void TheClockIsTheSessionsWhenThereIsOneAndThisMachinesAlone()
        {
            try
            {
                SignalClock.Local = 12.0;
                SignalClock.Shared = null;
                Assert.AreEqual(12.0, SignalClock.Seconds);
                SignalClock.Shared = () => 345.0;
                Assert.AreEqual(345.0, SignalClock.Seconds);
            }
            finally
            {
                SignalClock.Shared = null;
            }
        }
    }
}
