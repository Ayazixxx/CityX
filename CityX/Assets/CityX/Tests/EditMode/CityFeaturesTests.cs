using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The city's life (docs/HIGHWAYS.md, docs/CITY_LIFE.md): EXPRESSWAYS laid the way real cities lay them -- a ring round the centre and routes
    /// right across, in their own corridors, up over the streets they cross, never meeting another on the flat (a cloverleaf where two cross),
    /// with exits whose ramps curve and slope down to meet a street; parking lots and garages on a few per cent of the lots; malls, very rare
    /// and far apart; pedestrian plazas among the dense streets. And the meshes they turn into.
    /// </summary>
    public sealed class CityFeaturesTests
    {
        private const int Seed = 61839025;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));
        private static PlanWorld Plan => World.Plan;
        private static ExpresswayNetwork Xw => Plan.Data.Expressways;

        // ---- expressways: where they go ----

        [Test]
        public void TheRoadLiftIsTheSameInThePlanAndTheMesh()
        {
            Assert.AreEqual(PlanGeometry.RoadLift, CitySurfaceBuilder.RoadLift, 1e-9);
        }

        [Test]
        public void ThereIsARingRoundTheCentreAndRoutesRightAcrossTheRegion()
        {
            PlanConfig cfg = Plan.Data.Config;
            int rings = 0, through = 0;
            foreach (ExpresswayRoute route in Xw.Routes)
            {
                ViaductPath m = Xw.Paths[route.Path];
                Assert.AreEqual(ViaductKind.Mainline, m.Kind);
                if (route.Ring)
                {
                    rings++;
                    Assert.IsTrue(m.Closed, "the ring is not closed");
                    Assert.Less((m.P[0] - m.P[m.Count - 1]).Length, 1e-6, "the ring does not end where it starts");
                    Assert.IsTrue(PlanMath.PointInPolygon(new Vec2(0.0, 0.0), m.P), "the ring does not go round the centre");
                    double mean = 0.0;
                    foreach (Vec2 p in m.P) mean += p.Length;
                    mean /= m.Count;
                    Assert.That(mean, Is.InRange(cfg.RingRadius * 0.75, cfg.RingRadius * 1.25), "the ring is not at its radius");
                }
                else
                {
                    // A route across runs out of the region at both ends: an expressway does not stop in the middle of the city.
                    through++;
                    foreach (Vec2 end in new[] { m.P[0], m.P[m.Count - 1] })
                    {
                        double edge = Math.Max(Math.Abs(end.X), Math.Abs(end.Y));
                        Assert.AreEqual(cfg.Half, edge, 1.0, $"route {route.Id} ends at ({end.X:F0},{end.Y:F0}), inside the region");
                    }
                }
            }
            Assert.AreEqual(1, rings, "no ring road");
            Assert.GreaterOrEqual(through, 2, "fewer than two routes across the region");
        }

        /// <summary>
        /// Owner (2026-10-15): "the highways as they are now feel pointless they just go nowhere ... make them function the same to connect our
        /// cities". Every outlying centre (a satellite town beyond the ring's downtown) has a route across passing beside it and an exit near it.
        /// </summary>
        [Test]
        public void InTheGamesOwnCityEveryOutlyingCentreHasAnExpresswayAndAnExitNearIt()
        {
            // The game's own varied city (the classic test city is too small for satellite towns beyond its ring). A lone centre nearer in than
            // a bypass allows gets no route of its own, so a route may be as far as the pairing lets it: every centre still gets a main line
            // within twice the reach, and an exit within twice the exit reach.
            CityScale before = CityScale.Active;
            try
            {
                CityScale.Active = CityScale.Default;
                PlanWorld plan = PlanWorld.For(Seed);
                ExpresswayNetwork xw = plan.Data.Expressways;
                PlanConfig cfg = plan.Data.Config;
                double r = cfg.ExpresswayHalfWidth / 12.0;
                int outlying = 0;
                foreach (Vec2 c in plan.Data.Centres)
                {
                    if (c.Length <= ExpresswayPlanner.SatelliteFromRing * cfg.RingRadius) continue;
                    outlying++;
                    double nearestLine = double.MaxValue;
                    foreach (ExpresswayRoute route in xw.Routes)
                    {
                        ViaductPath m = xw.Paths[route.Path];
                        for (int i = 0; i + 1 < m.Count; i++)
                        {
                            nearestLine = Math.Min(nearestLine, PlanMath.DistanceToSegment(c, m.P[i], m.P[i + 1], out _));
                        }
                    }
                    Assert.LessOrEqual(nearestLine, 2.0 * ExpresswayPlanner.SatelliteReach * r, $"the centre at ({c.X:F0},{c.Y:F0}) has no expressway near it ({nearestLine:F0} m to the nearest)");
                    double nearestExit = double.MaxValue;
                    foreach (ExpresswayInterchange ic in xw.Interchanges)
                    {
                        if (!ic.System) nearestExit = Math.Min(nearestExit, (ic.At - c).Length);
                    }
                    Assert.LessOrEqual(nearestExit, 2.0 * ExpresswayPlanner.SatelliteExitReach * r, $"the centre at ({c.X:F0},{c.Y:F0}) has no exit near it ({nearestExit:F0} m to the nearest)");
                }
                Assert.GreaterOrEqual(outlying, 1, "the game's city has no outlying centre to check");
            }
            finally
            {
                CityScale.Active = before;
            }
        }

        [Test]
        public void NoTwoExpresswaysMeetOnTheFlatAndEveryCrossingIsACloverleaf()
        {
            int crossings = 0;
            for (int a = 0; a < Xw.Routes.Count; a++)
            {
                for (int b = a + 1; b < Xw.Routes.Count; b++)
                {
                    ViaductPath la = Xw.Paths[Xw.Routes[a].Path], lb = Xw.Paths[Xw.Routes[b].Path];
                    for (int i = 0; i < la.Segments; i++)
                    {
                        for (int j = 0; j < lb.Segments; j++)
                        {
                            if (!PlanMath.SegmentIntersect(la.P[i], la.P[i + 1], lb.P[j], lb.P[j + 1], out double t, out double u)) continue;
                            crossings++;
                            double ta = la.Top[i] + (la.Top[i + 1] - la.Top[i]) * t, tb = lb.Top[j] + (lb.Top[j + 1] - lb.Top[j]) * u;
                            double gap = ta > tb ? ta - la.Slab - tb : tb - lb.Slab - ta;
                            Assert.GreaterOrEqual(gap, ExpresswayPlanner.LevelGap - 0.05, $"routes {a} and {b} cross with only {gap:F1} m between their decks");
                        }
                    }
                }
            }
            Assert.Greater(crossings, 0, "the expressways never cross");
            int systems = 0;
            foreach (ExpresswayInterchange ic in Xw.Interchanges)
            {
                if (!ic.System) continue;
                systems++;
                int loops = 0, outer = 0;
                foreach (int id in ic.Paths)
                {
                    if (Xw.Paths[id].Shape == "loop") loops++;
                    if (Xw.Paths[id].Shape == "outer") outer++;
                }
                Assert.GreaterOrEqual(loops + outer, 6, $"the interchange at ({ic.At.X:F0},{ic.At.Y:F0}) has {loops} loops and {outer} curves of 8");
                Assert.GreaterOrEqual(loops, 3, $"the interchange at ({ic.At.X:F0},{ic.At.Y:F0}) is no cloverleaf");
            }
            Assert.AreEqual(crossings, systems, "a crossing of two expressways without its interchange");
        }

        [Test]
        public void MainDecksClearEveryRoadTheyCrossAndAreHigherOverOneTheyRunAlong()
        {
            PlanConfig cfg = Plan.Data.Config;
            int over = 0, along = 0, samples = 0;
            foreach (ExpresswayRoute route in Xw.Routes)
            {
                ViaductPath m = Xw.Paths[route.Path];
                for (int i = 0; i < m.Count; i++)
                {
                    samples++;
                    Vec2 tangent = -m.N[i].Perp;
                    foreach (EdgeGeometry g in Plan.Geometry.Edges)
                    {
                        if (g == null) continue;
                        if (Math.Abs(g.A.X - m.P[i].X) > 600.0 && Math.Abs(g.B.X - m.P[i].X) > 600.0) continue;
                        bool under = false;
                        for (int k = -2; k <= 2 && !under; k++)
                        {
                            Vec2 q = m.P[i] + m.N[i] * (k * 0.5 * m.HalfLeft[i]);
                            under = PlanMath.DistanceToSegment(q, g.A, g.B, out _) <= g.HalfWidth + cfg.SidewalkWidth - 0.5;
                        }
                        if (!under) continue;
                        over++;
                        bool runsAlong = Math.Abs(Vec2.Dot(g.Dir, tangent)) > 0.95;
                        if (runsAlong) along++;
                        double clearance = m.Top[i] - m.Slab - Plan.GroundHeight(m.P[i].X, m.P[i].Y);
                        double needed = (runsAlong ? ExpresswayPlanner.ParallelClearance : ExpresswayPlanner.StreetClearance) - 0.6;
                        Assert.GreaterOrEqual(clearance, needed, $"route {route.Id} at ({m.P[i].X:F0},{m.P[i].Y:F0}) is {clearance:F1} m over road {g.Edge.Id}");
                    }
                }
            }
            Assert.Greater(over, 0, "the expressways pass over no road at all");
            // Owner (2026-10-13): highways above parallel streets "doesnt happen that often in real life". The old decks rode over a street the whole way.
            Assert.Less(along / (double)samples, 0.15, $"{100.0 * along / samples:F0}% of the main decks run along a street");
        }

        [Test]
        public void MainDecksClimbGentlyAndStayOffTheGround()
        {
            foreach (ExpresswayRoute route in Xw.Routes)
            {
                ViaductPath m = Xw.Paths[route.Path];
                for (int i = 0; i < m.Segments; i++)
                {
                    double grade = Math.Abs(m.Top[i + 1] - m.Top[i]) / Math.Max(1e-6, m.S[i + 1] - m.S[i]);
                    Assert.LessOrEqual(grade, ExpresswayPlanner.MainlineGrade + 0.002, $"route {route.Id} climbs at {grade * 100.0:F1}% at sample {i}");
                }
                for (int i = 0; i < m.Count; i++)
                {
                    Assert.GreaterOrEqual(m.Top[i], Plan.GroundHeight(m.P[i].X, m.P[i].Y) + 0.3, $"route {route.Id} sinks into the ground at sample {i}");
                }
            }
        }

        // ---- exits: ramps that come from and lead to a street ----

        [Test]
        public void EveryRampMeetsAStreetAtOneEndAndItsExpresswayAtTheOther()
        {
            PlanConfig cfg = Plan.Data.Config;
            int ramps = 0;
            foreach (ViaductPath r in Xw.Paths)
            {
                if (r.Kind != ViaductKind.Ramp) continue;
                ramps++;
                Assert.GreaterOrEqual(r.StreetEdge, 0, $"ramp {r.Id} meets no street");
                RoadEdge street = Plan.Data.Edges[r.StreetEdge];
                Assert.IsTrue(street.Alive);
                int low = r.StreetAtEnd ? r.Count - 1 : 0, high = r.StreetAtEnd ? 0 : r.Count - 1;
                // The low end: on the street's kerb line, at the street's level.
                Vec2 foot = r.P[low];
                double fromCentre = PlanMath.DistanceToSegment(foot, street.A.P, street.B.P, out _);
                Assert.AreEqual(CityPlan.HalfWidth(cfg, street.Class), fromCentre, 0.5, $"ramp {r.Id} stops {fromCentre:F1} m from its street's centre, not at the kerb");
                Assert.AreEqual(Plan.GroundHeight(foot.X, foot.Y) + PlanGeometry.RoadLift, r.Top[low], 0.1, $"ramp {r.Id} does not come down to the street");
                // The high end: its inner edge on its main line's edge, at its height, running the same way.
                ViaductPath main = Xw.Paths[Xw.Routes[r.Route].Path];
                double best = double.MaxValue, bestTop = 0.0;
                Vec2 bestDir = default;
                for (int i = 0; i < main.Segments; i++)
                {
                    double d = PlanMath.DistanceToSegment(r.Left(high), main.P[i], main.P[i + 1], out double t);
                    if (d < best)
                    {
                        best = d;
                        bestTop = main.Top[i] + (main.Top[i + 1] - main.Top[i]) * t;
                        bestDir = main.SegmentDir(i);
                    }
                }
                Assert.AreEqual(main.HalfLeft[0], best, 0.3, $"ramp {r.Id} does not start right on its main line's edge");
                Assert.AreEqual(bestTop, r.Top[high], 0.08, $"ramp {r.Id} is not level with its main line where it joins it");
                // Its inner edge runs along the main line's (the taper widens outward from it).
                Vec2 edgeDir = high == 0 ? (r.Left(1) - r.Left(0)).Normalized : (r.Left(r.Count - 1) - r.Left(r.Count - 2)).Normalized;
                Assert.Greater(Math.Abs(Vec2.Dot(edgeDir, bestDir)), 0.995, $"ramp {r.Id} does not run alongside its main line where it joins it");
            }
            Assert.GreaterOrEqual(ramps, 6, "too few ramps");
        }

        /// <summary>
        /// Owner (2026-10-13): ramps were "clearly cut off instead of transitioning into the major freeway" -- each started or ended at its full
        /// width, a square end standing against the main line. A ramp's lane now grows out of its main line's edge from a tip, as a real taper
        /// does: next to nothing wide where it meets the main line, widening smoothly to a full lane.
        /// </summary>
        [Test]
        public void EveryRampAndConnectorGrowsOutOfItsMainLineFromATaperTip()
        {
            PlanConfig cfg = Plan.Data.Config;
            double full = 2.0 * cfg.RampHalfWidth;
            int ends = 0;
            foreach (ViaductPath r in Xw.Paths)
            {
                if (r.Kind == ViaductKind.Mainline) continue;
                var tips = new List<int>();
                if (r.Kind == ViaductKind.Connector) { tips.Add(0); tips.Add(r.Count - 1); }
                else tips.Add(r.StreetAtEnd ? 0 : r.Count - 1);
                foreach (int tip in tips)
                {
                    ends++;
                    int step = tip == 0 ? 1 : -1;
                    double width = r.HalfLeft[tip] + r.HalfRight[tip];
                    Assert.Less(width, 0.6, $"{r.Shape} {r.Id} meets its main line {width:F1} m wide: a square end, not a taper");
                    // Widening without a jump, to the whole lane within the taper's length.
                    double last = width, walked = 0.0;
                    bool whole = false;
                    for (int i = tip + step; i >= 0 && i < r.Count; i += step)
                    {
                        walked += (r.P[i] - r.P[i - step]).Length;
                        double w = 2.0 * r.HalfRight[i];
                        Assert.Less(w - last, 2.5, $"{r.Shape} {r.Id} widens by {w - last:F1} m in one step at sample {i}");
                        last = w;
                        if (w > full - 0.05)
                        {
                            whole = true;
                            break;
                        }
                    }
                    Assert.IsTrue(whole && walked < ExpresswayPlanner.Taper + 8.0, $"{r.Shape} {r.Id} takes {walked:F0} m to become a whole lane");
                }
            }
            Assert.Greater(ends, 20);
        }

        [Test]
        public void RampsCurveAndSlopeSmoothlyAndComeInManyShapes()
        {
            int curved = 0, straight = 0, loops = 0, viaduct = 0, embankment = 0;
            var shapes = new HashSet<string>();
            foreach (ViaductPath r in Xw.Paths)
            {
                if (r.Kind == ViaductKind.Mainline) continue;
                double turn = 0.0;
                for (int i = 1; i < r.Segments; i++)
                {
                    double bend = Math.Abs(DMath.Turn(r.SegmentDir(i - 1), r.SegmentDir(i)));
                    Assert.Less(bend, 0.15, $"{r.Shape} {r.Id} kinks by {bend * 57.3:F0} degrees at sample {i}");
                    turn += bend;
                }
                for (int i = 0, j = 0; i < r.Count; i++)
                {
                    while (j < r.Count - 1 && r.S[j] - r.S[i] < 12.0) j++;
                    if (r.S[j] - r.S[i] < 12.0) continue;
                    double grade = Math.Abs(r.Top[j] - r.Top[i]) / (r.S[j] - r.S[i]);
                    Assert.LessOrEqual(grade, ExpresswayPlanner.RampGrade + 0.015, $"{r.Shape} {r.Id} climbs at {grade * 100.0:F1}%");
                }
                if (r.Kind != ViaductKind.Ramp) continue;
                if (turn > 1.0) curved++;
                if (turn < 0.35) straight++;
                shapes.Add(r.Shape);
                if (r.Shape == "loop")
                {
                    // An exit's loop turns most of the way round, to run along its street (a left turn made by turning right).
                    loops++;
                    Assert.Greater(turn, 3.8, $"loop ramp {r.Id} turns only {turn * 57.3:F0} degrees");
                }
                bool high = false, low = false;
                for (int i = 0; i < r.Count; i++)
                {
                    double under = r.Top[i] - r.Slab - Plan.GroundHeight(r.P[i].X, r.P[i].Y);
                    high |= under > 4.0;
                    low |= under < 1.5;
                }
                if (high) viaduct++;
                if (low) embankment++;
            }
            Assert.Greater(curved, 0, "no ramp curves");
            Assert.Greater(viaduct, 0, "no ramp stands on a viaduct");
            Assert.Greater(embankment, 0, "no ramp comes down on an embankment");
            // Owner: exits are "sometimes curved sometimes straight" -- not one shape everywhere.
            Assert.GreaterOrEqual(shapes.Count, 2, "every exit ramp is a " + string.Join("/", shapes));
            Assert.Greater(loops, 0, "no exit has a loop ramp");
            int connectorsLoops = 0;
            foreach (ViaductPath r in Xw.Paths) if (r.Kind == ViaductKind.Connector && r.Shape == "loop") connectorsLoops++;
            Assert.Greater(connectorsLoops + loops, 0, "no loop anywhere");
        }

        [Test]
        public void RampsNeverRunLowOverAStreetNorThroughAnotherDeck()
        {
            PlanConfig cfg = Plan.Data.Config;
            var nearby = new List<(int path, int seg)>();
            foreach (ViaductPath r in Xw.Paths)
            {
                if (r.Kind == ViaductKind.Mainline) continue;
                double length = r.Length;
                for (int i = 0; i < r.Count; i++)
                {
                    Vec2 p = r.P[i];
                    double under = r.Top[i] - r.Slab - Plan.GroundHeight(p.X, p.Y);
                    bool nearFoot = r.StreetEdge >= 0 && (r.StreetAtEnd ? length - r.S[i] : r.S[i]) < ExpresswayPlanner.Landing * 1.5 + 20.0;
                    if (under < ExpresswayPlanner.RampClearance - 0.1 && !nearFoot)
                    {
                        foreach (EdgeGeometry g in Plan.Geometry.Edges)
                        {
                            if (g == null) continue;
                            double d = PlanMath.DistanceToSegment(p, g.A, g.B, out _);
                            Assert.GreaterOrEqual(d, r.HalfRight[i] + g.HalfWidth + cfg.SidewalkWidth - 0.6,
                                $"{r.Shape} {r.Id} runs {under:F1} m over road {g.Edge.Id} at ({p.X:F0},{p.Y:F0})");
                        }
                    }
                    // Every other deck over or under it is well clear of it (beside its own main line it runs level, by design).
                    Xw.SegmentsNear(p.X - 1.0, p.Y - 1.0, p.X + 1.0, p.Y + 1.0, nearby);
                    foreach ((int other, int seg) in nearby)
                    {
                        if (other == r.Id) continue;
                        ViaductPath o = Xw.Paths[other];
                        if (!PlanMath.PointInPolygon(p, o.Footprint(seg, -0.5))) continue;
                        double top = o.TopAt(seg, p);
                        if (Math.Abs(top - r.Top[i]) < 0.6) continue;
                        double gap = top > r.Top[i] ? top - o.Slab - r.Top[i] : r.Top[i] - r.Slab - top;
                        Assert.GreaterOrEqual(gap, ExpresswayPlanner.RampClearance - 0.1, $"{r.Shape} {r.Id} and deck {other} pass {gap:F1} m apart at ({p.X:F0},{p.Y:F0})");
                    }
                }
            }
        }

        [Test]
        public void ExitsAreSpacedLikeACitysAndClearOfTheCloverleafs()
        {
            var exits = new List<ExpresswayInterchange>();
            var systems = new List<ExpresswayInterchange>();
            foreach (ExpresswayInterchange ic in Xw.Interchanges) (ic.System ? systems : exits).Add(ic);
            Assert.GreaterOrEqual(exits.Count, 3, "too few exits");
            double scale = Plan.Data.Config.ExpresswayHalfWidth / 12.0;
            foreach (ExpresswayInterchange e in exits)
            {
                Assert.GreaterOrEqual(e.Paths.Count, 2, $"the exit at ({e.At.X:F0},{e.At.Y:F0}) has {e.Paths.Count} ramp");
                foreach (ExpresswayInterchange s in systems)
                {
                    Assert.GreaterOrEqual((e.At - s.At).Length, ExpresswayPlanner.SystemSpacing * scale - 1.0, "an exit crowds a system interchange");
                }
                foreach (ExpresswayInterchange o in exits)
                {
                    if (o == e || o.RouteA != e.RouteA) continue;
                    Assert.GreaterOrEqual((e.At - o.At).Length, ExpresswayPlanner.ExitSpacing * scale * 0.5, "two exits on one route are too close");
                }
                // ...and its ramps leave and join the main line clear of the cloverleafs' connectors.
                foreach (int id in e.Paths)
                {
                    ViaductPath r = Xw.Paths[id];
                    Vec2 onMain = r.StreetAtEnd ? r.P[0] : r.P[r.Count - 1];
                    foreach (ExpresswayInterchange s in systems)
                    {
                        Assert.GreaterOrEqual((onMain - s.At).Length, 0.35 * ExpresswayPlanner.SystemSpacing * scale - 1.0, $"ramp {r.Id} joins its main line inside a cloverleaf");
                    }
                }
            }
        }

        /// <summary>
        /// Owner (2026-10-13): "it seems like the highway entrances/exits only connect to other highways. you should mostly be entering the
        /// highway from a street and exiting onto one as well". In the game's own city (the varied districts; the other tests here pin the classic
        /// one) the ramps to and from streets far outnumber the connectors between expressways.
        /// </summary>
        [Test]
        public void InTheGamesOwnCityMostRampsComeFromAndLeadToStreets()
        {
            CityScale before = CityScale.Active;
            try
            {
                CityScale.Active = CityScale.Default;
                ExpresswayNetwork xw = PlanWorld.For(Seed).Data.Expressways;
                int streetRamps = 0, connectors = 0;
                foreach (ViaductPath p in xw.Paths)
                {
                    if (p.Kind == ViaductKind.Ramp) streetRamps++;
                    else if (p.Kind == ViaductKind.Connector) connectors++;
                }
                Assert.Greater(connectors, 0, "no cloverleaf at all");
                Assert.GreaterOrEqual(streetRamps, 1.6 * connectors, $"{streetRamps} ramps to streets against {connectors} connectors between expressways");
            }
            finally
            {
                CityScale.Active = before;
            }
        }

        [Test]
        public void NoLotIsCutUnderAnExpresswayAndNoPierStandsInARoad()
        {
            PlanConfig cfg = Plan.Data.Config;
            var near = new List<(int path, int seg)>();
            int beside = 0;
            foreach (PlanLot lot in Plan.Data.Lots)
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
                foreach (Vec2 v in lot.Lot)
                {
                    x0 = Math.Min(x0, v.X); x1 = Math.Max(x1, v.X); y0 = Math.Min(y0, v.Y); y1 = Math.Max(y1, v.Y);
                }
                Xw.SegmentsNear(x0 - 2.0, y0 - 2.0, x1 + 2.0, y1 + 2.0, near);
                bool besideADeck = false;
                foreach ((int path, int seg) in near)
                {
                    Assert.IsFalse(PlanMath.PolygonsIntersect(lot.Lot, Xw.Paths[path].Footprint(seg, 0.0)), $"lot {lot.Id} lies under deck {path}");
                    besideADeck |= Xw.Paths[path].Kind == ViaductKind.Mainline && PlanMath.PolygonsIntersect(lot.Lot, Xw.Paths[path].Footprint(seg, ExpresswayNetwork.LotMargin + 1.0));
                }
                if (besideADeck) beside++;
                foreach (Vec2[] infield in Xw.Infields)
                {
                    Assert.IsFalse(PlanMath.PolygonsIntersect(lot.Lot, infield), $"lot {lot.Id} lies in an interchange's land");
                }
            }
            // A lot by an expressway is cut back to the margin, not lost: the city lines the motorway rather than leaving a strip of empty blocks.
            Assert.Greater(beside, 20, "hardly any lot comes up to an expressway");
            Assert.Greater(Xw.Piers.Count, 50, "hardly any piers");
            foreach (ViaductPier pier in Xw.Piers)
            {
                var columns = new List<Vec2>();
                Vec2 n = pier.Along.Perp;
                if (pier.Twin) { columns.Add(pier.P + n * pier.ColumnLeft); columns.Add(pier.P - n * pier.ColumnRight); }
                else columns.Add(pier.P);
                foreach (Vec2 c in columns)
                {
                    foreach (EdgeGeometry g in Plan.Geometry.Edges)
                    {
                        if (g == null) continue;
                        double d = PlanMath.DistanceToSegment(c, g.A, g.B, out _);
                        Assert.GreaterOrEqual(d, g.HalfWidth + cfg.SidewalkWidth + 0.5, $"a pier of deck {pier.Path} stands in road {g.Edge.Id}");
                    }
                }
                Assert.Greater(pier.Underside - pier.Ground, ExpresswayPlanner.PierMinimumHeight - 0.01, "a pier under nothing");
            }
        }

        [Test]
        public void TheSpawnIsOutInTheOpenNotUnderAnExpressway()
        {
            Assert.IsTrue(Plan.TryFindSpawn(out double sx, out double sz));
            Assert.IsFalse(Xw.Covers(new Vec2(sx, sz), 20.0), "the spawn is under an expressway or on its ramps' land");
        }

        [Test]
        public void TheExpresswaysAreThePlansOwnFunction()
        {
            // Same seed, same city: every player computes the expressways for themselves (DMath, not Math.Sin, for exactly this).
            PlanConfig cfg = Plan.Data.Config;
            CityPlanData again = CityPlan.Generate(Seed, cfg);
            Assert.AreEqual(Xw.Paths.Count, again.Expressways.Paths.Count);
            for (int k = 0; k < Xw.Paths.Count; k += 3)
            {
                ViaductPath a = Xw.Paths[k], b = again.Expressways.Paths[k];
                Assert.AreEqual(a.Count, b.Count);
                for (int i = 0; i < a.Count; i += 7)
                {
                    Assert.AreEqual(a.P[i].X, b.P[i].X, 0.0);
                    Assert.AreEqual(a.Top[i], b.Top[i], 0.0);
                }
            }
        }

        // ---- parking, malls, plazas ----

        [Test]
        public void ParkingTakesAFewPerCentOfTheLotsAndNeverTwiceInABlock()
        {
            int lots = 0, garages = 0, open = 0;
            var garageBlocks = new HashSet<int>();
            var openBlocks = new HashSet<int>();
            foreach (PlanLot lot in Plan.Data.Lots)
            {
                if (lot.Use == LotUse.Mall) continue;
                lots++;
                if (lot.Use == LotUse.ParkingGarage)
                {
                    garages++;
                    Assert.IsTrue(garageBlocks.Add(lot.BlockIndex), $"block {lot.BlockIndex} has two parking garages");
                }
                else if (lot.Use == LotUse.ParkingLot)
                {
                    open++;
                    Assert.IsTrue(openBlocks.Add(lot.BlockIndex), $"block {lot.BlockIndex} has two parking lots");
                }
            }
            double share = (garages + open) / (double)lots;
            Assert.Greater(garages, 5, "no parking garages");
            Assert.Greater(open, 5, "no parking lots");
            Assert.That(share, Is.InRange(0.005, 0.08), $"parking is {share * 100.0:F1}% of the lots: a city is a few per cent");
        }

        [Test]
        public void ParkingLotsStandAsBuildingsOfTheirOwnKind()
        {
            int garages = 0, open = 0;
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsParkingGarage)
                {
                    garages++;
                    Assert.IsFalse(b.OnRoof || b.Wing || b.Shell, $"garage #{b.Id} is a wing or stacked volume");
                    Assert.GreaterOrEqual(b.Params.Height, Amenities.MinGarageLevels * Amenities.GarageFloorHeight - 0.01);
                    Assert.LessOrEqual(b.Params.Height, Amenities.MaxGarageLevels * Amenities.GarageFloorHeight + 0.01);
                }
                else if (b.IsOpenGround)
                {
                    open++;
                    Assert.Less(b.Params.Height, 1.0, $"car park #{b.Id} has a height");
                    Assert.IsFalse(b.OnRoof || b.Wing || b.Shell);
                }
            }
            Assert.Greater(garages, 5);
            Assert.Greater(open, 5);
        }

        [Test]
        public void MallsAreVeryRareAndAsFarApartAsRealOnes()
        {
            List<int> blocks = Plan.Data.MallBlocks;
            Assert.That(blocks.Count, Is.InRange(1, Amenities.MaxMalls), $"{blocks.Count} malls");
            for (int i = 0; i < blocks.Count; i++)
            {
                BuildingSite[] complex = Plan.MallSites(blocks[i]);
                int spines = 0;
                foreach (BuildingSite m in complex)
                {
                    Assert.IsFalse(m.OnRoof || m.Shell, $"mall piece #{m.Id} is stacked");
                    if (m.IsMall)
                    {
                        Assert.GreaterOrEqual(m.Params.Height, 2.0 * Amenities.MallLevelHeight - 0.01, "a mall corridor of one level");
                        Assert.GreaterOrEqual(m.Params.Depth, 40.0, $"corridor #{m.Id} is only {m.Params.Depth:F0} m across");
                        if (!m.Wing) spines++;
                    }
                }
                Assert.AreEqual(1, spines, $"mall block {blocks[i]} has {spines} main corridors");
                Vec2 a = PlanMath.Centroid(Plan.Data.Blocks[blocks[i]].Outer);
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    double d = (a - PlanMath.Centroid(Plan.Data.Blocks[blocks[j]].Outer)).Length;
                    Assert.GreaterOrEqual(d, Amenities.MallSeparation * 0.95, $"malls on blocks {blocks[i]} and {blocks[j]} are {d / 1000.0:F1} km apart");
                }
            }
        }

        [Test]
        public void EveryPieceOfAMallOpensIntoTheRest()
        {
            // A corridor or an anchor that is a wing of the mall touches another piece (a WallContact, where the builder opens a passage).
            int pieces = 0;
            foreach (int block in Plan.Data.MallBlocks)
            {
                foreach (BuildingSite m in Plan.MallSites(block))
                {
                    if (!(m.IsMall || m.IsMallAnchor) || !m.Wing) continue;
                    pieces++;
                    Assert.IsNotNull(m.Contacts, $"mall piece #{m.Id} stands on its own, joined to nothing");
                    Assert.Greater(m.Contacts.Length, 0, $"mall piece #{m.Id} joins nothing");
                }
            }
            Assert.Greater(pieces, 0, "no mall has more than one piece");
        }

        [Test]
        public void MallsComeInManyShapesWithFewBigStoresAndManySmall()
        {
            // The layout over many seeds on a good-sized block: branches and anchors vary, nothing overlaps, everything stays in its lot.
            int withBranch = 0, multiAnchor = 0, runs = 300;
            var shapes = new HashSet<string>();
            for (uint h = 1; h <= runs; h++)
            {
                const double width = 300.0, depth = 230.0;
                List<Amenities.MallPiece> pieces = Amenities.MallLayout.Compose(width, depth, 1.0, 13.0, PlanNoise.Mix(h, 0xBEEFU), MallParking.OpenLot);
                var volumes = new List<MassVolume>();
                int corridors = 0, anchors = 0;
                foreach (Amenities.MallPiece p in pieces)
                {
                    volumes.Add(p.Volume);
                    if (p.Part == Amenities.MallPart.Concourse) corridors++;
                    if (p.Part == Amenities.MallPart.Anchor) anchors++;
                    Assert.AreNotEqual(Amenities.MallPart.Garage, p.Part, "a garage on an open-lot mall");
                }
                Assert.AreEqual(Amenities.MallPart.Concourse, pieces[0].Part, "the first piece is not the main corridor");
                Assert.IsTrue(PlanBuildings.InsideLot(volumes, width, depth), $"seed {h}: a piece outside its lot");
                for (int i = 0; i < volumes.Count; i++)
                {
                    for (int j = i + 1; j < volumes.Count; j++)
                    {
                        Assert.IsFalse(Overlaps(volumes[i], volumes[j]), $"seed {h}: pieces {i} and {j} overlap");
                    }
                }
                if (corridors > 1) withBranch++;
                if (anchors > 1) multiAnchor++;
                shapes.Add($"{corridors}/{anchors}");
            }
            Assert.Greater(withBranch, runs / 4, "too few malls with side corridors");
            Assert.Greater(multiAnchor, runs / 2, "too few malls with more than one anchor");
            Assert.GreaterOrEqual(shapes.Count, 6, $"only {shapes.Count} kinds of mall");
        }

        [Test]
        public void AParkingGarageIsTheRareExtra()
        {
            Assert.That(Amenities.MallGarageChanceAt(Amenities.MallMinPopulation), Is.EqualTo(Amenities.MallGarageChanceEdge).Within(1e-9));
            Assert.That(Amenities.MallGarageChanceAt(Amenities.MallMaxPopulation), Is.EqualTo(Amenities.MallGarageChanceDense).Within(1e-9));
            Assert.Less(Amenities.MallGarageChanceDense, 0.5, "a garage at most malls");
            int garages = 0;
            for (uint h = 1; h <= 100; h++)
            {
                foreach (Amenities.MallPiece p in Amenities.MallLayout.Compose(300.0, 230.0, 1.0, 13.0, PlanNoise.Mix(h, 0xCAFEU), MallParking.GarageAndLot))
                {
                    if (p.Part == Amenities.MallPart.Garage) garages++;
                }
            }
            Assert.Greater(garages, 80, "a mall that has a garage mostly gets it placed");
        }

        [Test]
        public void TheMallsCarParkIsPaintedRoundTheMallAndClearOfIt()
        {
            int block = Plan.Data.MallBlocks[0];
            BuildingSite[] complex = Plan.MallSites(block);
            List<Vec2> ring = Plan.Data.Blocks[block].Inner;
            Bounds(ring, out double x0, out double z0, out double x1, out double z1);
            int stripes = 0, lamps = 0;
            for (int cx = PlanWorld.ChunkOf(x0); cx <= PlanWorld.ChunkOf(x1); cx += 2)
            {
                for (int cy = PlanWorld.ChunkOf(z0); cy <= PlanWorld.ChunkOf(z1); cy += 2)
                {
                    MeshBuffer[] near = Surface(cx, cy, false);
                    foreach (Vector3 v in near[4].Vertices)   // the white paint (the lane-divider buffer draws white)
                    {
                        // Every bit of paint in the block stands clear of the mall's buildings.
                        if (!PlanMath.PointInPolygon(new Vec2(v.x, v.z), ring)) continue;
                        stripes++;
                        foreach (BuildingSite m in complex)
                        {
                            m.WorldToLocal(v.x, v.z, out double lx, out double lz);
                            Assert.IsFalse(Math.Abs(lx) < m.Params.Width * 0.5 + 2.0 && Math.Abs(lz) < m.Params.Depth * 0.5 + 2.0,
                                $"a parking stripe at ({v.x:F0}, {v.z:F0}) runs into mall piece #{m.Id}");
                        }
                    }
                    if (HasLook(near[2], CityProps.DarkMetal)) lamps++;
                }
            }
            // Stripes sampled from every other chunk: a 3-4 ha mall's car park still gives a couple of hundred (the old bar of 400 was one big mall's).
            Assert.Greater(stripes, 150, "the mall's block has hardly any parking painted on it");
            Assert.Greater(lamps, 0, "no lamp posts in the mall's car park");
        }

        private static bool Overlaps(MassVolume a, MassVolume b)
        {
            var ax = new double[4];
            var az = new double[4];
            var bx = new double[4];
            var bz = new double[4];
            a.Corners(ax, az);
            b.Corners(bx, bz);
            double a0 = Math.Min(Math.Min(ax[0], ax[1]), Math.Min(ax[2], ax[3])), a1 = Math.Max(Math.Max(ax[0], ax[1]), Math.Max(ax[2], ax[3]));
            double c0 = Math.Min(Math.Min(az[0], az[1]), Math.Min(az[2], az[3])), c1 = Math.Max(Math.Max(az[0], az[1]), Math.Max(az[2], az[3]));
            double b0 = Math.Min(Math.Min(bx[0], bx[1]), Math.Min(bx[2], bx[3])), b1 = Math.Max(Math.Max(bx[0], bx[1]), Math.Max(bx[2], bx[3]));
            double d0 = Math.Min(Math.Min(bz[0], bz[1]), Math.Min(bz[2], bz[3])), d1 = Math.Max(Math.Max(bz[0], bz[1]), Math.Max(bz[2], bz[3]));
            const double eps = 0.01;
            return a0 < b1 - eps && a1 > b0 + eps && c0 < d1 - eps && c1 > d0 + eps;
        }

        private static void Bounds(List<Vec2> ring, out double x0, out double z0, out double x1, out double z1)
        {
            x0 = z0 = double.MaxValue;
            x1 = z1 = double.MinValue;
            foreach (Vec2 p in ring)
            {
                x0 = Math.Min(x0, p.X);
                x1 = Math.Max(x1, p.X);
                z0 = Math.Min(z0, p.Y);
                z1 = Math.Max(z1, p.Y);
            }
        }

        [Test]
        public void PlazasAreOpenBlocksWithNothingBuiltOnThem()
        {
            CityPlanData data = Plan.Data;
            Assert.GreaterOrEqual(data.PlazaBlocks.Count, 10, "too few plazas");
            var plazaIndex = new HashSet<int>(data.PlazaBlocks);
            foreach (PlanLot lot in data.Lots)
            {
                Assert.IsFalse(plazaIndex.Contains(lot.BlockIndex), $"lot {lot.Id} was cut from plaza block {lot.BlockIndex}");
            }
            foreach (int index in data.PlazaBlocks)
            {
                PlanBlock block = data.Blocks[index];
                Assert.IsTrue(block.Plaza);
                Assert.IsNotNull(block.Inner, "a plaza with no kerb ring");
                Assert.That(block.Area, Is.InRange(Amenities.PlazaMinArea * 0.99, Amenities.PlazaMaxArea * 16.0 * 1.01));
                double minX = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxZ = double.MinValue;
                foreach (Vec2 v in block.Inner)
                {
                    minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
                    minZ = Math.Min(minZ, v.Y); maxZ = Math.Max(maxZ, v.Y);
                }
                foreach (BuildingSite b in Plan.Buildings)
                {
                    if (b.CentreX < minX || b.CentreX > maxX || b.CentreZ < minZ || b.CentreZ > maxZ) continue;
                    Assert.IsFalse(PlanMath.PointInPolygon(new Vec2(b.CentreX, b.CentreZ), block.Inner), $"building #{b.Id} stands in plaza block {index}");
                }
            }
        }

        // ---- the meshes ----

        [Test]
        public void TheDeckChunkHasACarriagewayInTheAirWithParapetsAndAMedian()
        {
            (ViaductPath m, int i) = AHighMainSample();
            Vec2 p = m.P[i];
            int cx = PlanWorld.ChunkOf(p.X), cy = PlanWorld.ChunkOf(p.Y);
            MeshBuffer[] near = Surface(cx, cy, false);
            MeshBuffer[] far = Surface(cx, cy, true);
            float ground = (float)Plan.GroundHeight(p.X, p.Y);
            float deck = (float)m.Top[i];
            // Road surface at the deck's height right there (another deck may pass higher still through the same chunk, at an interchange).
            bool atDeck = false;
            foreach (Vector3 v in near[0].Vertices)
            {
                if (new Vec2(v.x - p.X, v.z - p.Y).Length < m.HalfLeft[i] + 6.0 && Mathf.Abs(v.y - deck) < 0.4f) { atDeck = true; break; }
            }
            Assert.IsTrue(atDeck, "the chunk's road surface does not reach the deck's height");
            Assert.Greater(deck - ground, ExpresswayPlanner.StreetClearance, "picked a sample that is not up in the air");
            Assert.Greater(near[5].Triangles.Count, 0, "no parapet or median on the deck");
            Assert.AreEqual(0, far[5].Triangles.Count, "a far chunk builds parapets");
            int down = 0;
            foreach (Vector3 n in near[1].Normals) if (n.y < -0.9f) down++;
            Assert.Greater(down, 0, "the deck has no underside");
            foreach (MeshBuffer b in near) AssertWound(b, $"deck chunk ({cx},{cy})");
            foreach (MeshBuffer b in far) AssertWound(b, $"far deck chunk ({cx},{cy})");
            int nearTriangles = 0, farTriangles = 0;
            foreach (MeshBuffer b in near) nearTriangles += b.Triangles.Count;
            foreach (MeshBuffer b in far) farTriangles += b.Triangles.Count;
            Assert.Less(farTriangles, nearTriangles, "the far deck is not lighter");
        }

        [Test]
        public void APierStandsFromTheGroundToTheDeckInItsChunk()
        {
            ViaductPier pier = null;
            foreach (ViaductPier candidate in Xw.Piers)
            {
                if (candidate.Underside - candidate.Ground > 8.0) { pier = candidate; break; }
            }
            Assert.IsNotNull(pier, "no tall pier");
            MeshBuffer[] near = Surface(PlanWorld.ChunkOf(pier.P.X), PlanWorld.ChunkOf(pier.P.Y), false);
            bool low = false, high = false;
            foreach (Vector3 v in near[2].Vertices)
            {
                if (new Vec2(v.x - pier.P.X, v.z - pier.P.Y).Length > Math.Max(3.0, pier.HalfSpan + 1.0)) continue;
                low |= v.y < pier.Ground + 0.1;
                high |= Mathf.Abs(v.y - (float)pier.Underside) < 0.1f;
            }
            Assert.IsTrue(low && high, "the pier does not reach from the ground to the deck");
        }

        [Test]
        public void AnExitHasItsSignItsSlopeAndAKerbCutWhereItMeetsTheStreet()
        {
            ViaductPath r = null;
            foreach (ViaductPath candidate in Xw.Paths)
            {
                if (candidate.Kind == ViaductKind.Ramp && candidate.Leaving) { r = candidate; break; }
            }
            Assert.IsNotNull(r, "no off-ramp");
            // A sign over the main line before the exit.
            bool sign = false;
            foreach (ExpresswaySign s in Xw.Signs)
            {
                if ((s.P - r.P[0]).Length > 120.0) continue;
                MeshBuffer[] at = Surface(PlanWorld.ChunkOf(s.P.X), PlanWorld.ChunkOf(s.P.Y), false);
                sign |= HasLook(at[2], CityProps.SignGreen);
            }
            Assert.IsTrue(sign, "no exit sign before the off-ramp");
            // Halfway down: the ramp's surface at its height in that chunk.
            int half = r.Count / 2;
            Vec2 mid = r.P[half];
            MeshBuffer[] slope = Surface(PlanWorld.ChunkOf(mid.X), PlanWorld.ChunkOf(mid.Y), false);
            bool found = false;
            foreach (Vector3 v in slope[0].Vertices)
            {
                if (Mathf.Abs(v.y - (float)r.Top[half]) < 0.3f && new Vec2(v.x - mid.X, v.z - mid.Y).Length < 12.0) { found = true; break; }
            }
            Assert.IsTrue(found, "the ramp's slope is not in the chunk's road surface");
            // The foot: no pavement left standing at kerb height under the ramp where it crosses onto the street.
            Vec2 foot = r.P[r.Count - 1];
            KerbCut cut = null;
            foreach (KerbCut k in Xw.KerbCuts)
            {
                if (PlanMath.PointInPolygon(foot + (r.P[r.Count - 3] - foot).Normalized * 1.5, k.Poly)) cut = k;
            }
            Assert.IsNotNull(cut, "no kerb cut where the ramp meets its street");
            MeshBuffer[] street = Surface(PlanWorld.ChunkOf(foot.X), PlanWorld.ChunkOf(foot.Y), false);
            Vec2 inside = foot + (r.P[r.Count - 3] - foot).Normalized * 2.0;
            float kerbTop = (float)Plan.GroundHeight(inside.X, inside.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight;
            for (int t = 0; t + 2 < street[1].Triangles.Count; t += 3)
            {
                Vector3 a = street[1].Vertices[street[1].Triangles[t]], b = street[1].Vertices[street[1].Triangles[t + 1]], c = street[1].Vertices[street[1].Triangles[t + 2]];
                if (Mathf.Abs(a.y - kerbTop) > 0.08f) continue;   // the paving, not the bare ground 16 cm under it
                var tri = new[] { new Vec2(a.x, a.z), new Vec2(b.x, b.z), new Vec2(c.x, c.z) };
                Assert.IsFalse(PlanMath.PointInPolygon(inside, tri), "the pavement still stands under the ramp's foot");
            }
            foreach (MeshBuffer b in slope) AssertWound(b, "ramp chunk");
            foreach (MeshBuffer b in street) AssertWound(b, "street chunk");
        }

        [Test]
        public void SetPiecesHaveOpenEntrancesNotADoorWithALamp()
        {
            // A garage, a car park, a mall's corridors and anchors: no hinged door hangs in their openings, so no door lamp either.
            int checkedSites = 0;
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (!b.Params.IsSpecial) continue;
                checkedSites++;
                Assert.IsFalse(EntranceLighting.Has(b), $"set piece #{b.Id} ({b.Params.SpecialType}) has a door lamp");
            }
            Assert.Greater(checkedSites, 10);
        }

        [Test]
        public void ThereAreNoCars()
        {
            // The owner took the abandoned cars out (2026-10-12): nothing on the decks, the streets or the car parks. A car was the only prop
            // standing on wheels 0.84 m tall at the road's height; the structures of a deck chunk now hold only piers, signs and barricades.
            Assert.IsNull(typeof(CityProps).GetMethod("AddCar"), "the car is back in the prop library");
        }

        [Test]
        public void APlazaIsLaidInSettsWithItsBenches()
        {
            Assert.Greater(Plan.Data.PlazaBlocks.Count, 0);
            bool setts = false, bench = false;
            foreach (int index in Plan.Data.PlazaBlocks)
            {
                PlanBlock block = Plan.Data.Blocks[index];
                Vec2 c = PlanMath.Centroid(block.Inner);
                MeshBuffer[] near = Surface(PlanWorld.ChunkOf(c.X), PlanWorld.ChunkOf(c.Y), false);
                foreach (Color32 look in near[1].Colors)
                {
                    SurfaceLook.Unpack(look, out _, out int slice, out _);
                    if (slice == SurfaceLook.Setts) { setts = true; break; }
                }
                bench |= HasLook(near[2], CityProps.Wood);
                if (setts && bench) return;
            }
            Assert.IsTrue(setts, "no plaza is paved in setts");
            Assert.IsTrue(bench, "no plaza has a bench");
        }

        [Test]
        public void AGarageACarParkAndAMallBuildNearAndFar()
        {
            BuildingSite garage = default, open = default, mall = default, anchor = default;
            bool hasGarage = false, hasOpen = false, hasMall = false, hasAnchor = false;
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (!hasGarage && b.IsParkingGarage) { garage = b; hasGarage = true; }
                if (!hasOpen && b.IsOpenGround) { open = b; hasOpen = true; }
                if (!hasMall && b.IsMall && !b.Wing) { mall = b; hasMall = true; }
                if (!hasAnchor && b.IsMallAnchor) { anchor = b; hasAnchor = true; }
            }
            Assert.IsTrue(hasGarage, "no parking garage");
            Assert.IsTrue(hasOpen, "no car park");
            Assert.IsTrue(hasMall, "no mall");
            Assert.IsTrue(hasAnchor, "no mall anchor store");
            foreach ((string name, BuildingSite site) in new[] { ("garage", garage), ("car park", open), ("mall", mall), ("anchor", anchor) })
            {
                MeshBuffer[] near = Site(site, false);
                MeshBuffer[] far = Site(site, true);
                int nearCount = 0, farCount = 0;
                foreach (MeshBuffer b in near) { nearCount += b.Triangles.Count; AssertWound(b, name); }
                foreach (MeshBuffer b in far) { farCount += b.Triangles.Count; AssertWound(b, name + " (far)"); }
                Assert.Greater(nearCount, 50, $"the {name} is empty");
                Assert.Less(farCount, nearCount, $"the far {name} is not lighter");
                foreach (MeshBuffer b in near)
                {
                    foreach (Vector3 v in b.Vertices)
                    {
                        Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), $"a NaN vertex in the {name}");
                    }
                }
            }
            // The mall has an inside: vertices well within its walls, between its floor and its roof (the stores, the galleries, the food court).
            MeshBuffer[] mallNear = Site(mall, false);
            int inside = 0;
            float halfW = mall.Params.Width * 0.5f - 3.0f, halfD = mall.Params.Depth * 0.5f - 3.0f;
            foreach (Vector3 v in mallNear[0].Vertices)
            {
                if (Mathf.Abs(v.x) < halfW && Mathf.Abs(v.z) < halfD && v.y > mall.BaseHeight + 0.5f && v.y < mall.Top - 1.0f) inside++;
            }
            Assert.Greater(inside, 400, "the mall has nothing inside it");
            // The garage's decks: structure at several distinct heights between the floor and the roof.
            MeshBuffer[] garageNear = Site(garage, false);
            var heights = new HashSet<int>();
            foreach (Vector3 v in garageNear[1].Vertices)
            {
                if (v.y > garage.BaseHeight + 1.0f && v.y < garage.Top - 0.5f) heights.Add(Mathf.RoundToInt(v.y));
            }
            Assert.GreaterOrEqual(heights.Count, 3, "the garage has no decks");
        }

        [Test]
        public void TheChunksAroundAnExitAndAMallAreWoundAndFinite()
        {
            var seeds = new List<Vec2>();
            foreach (ViaductPath r in Xw.Paths)
            {
                if (r.Kind == ViaductKind.Ramp) { seeds.Add(r.P[r.StreetAtEnd ? r.Count - 1 : 0]); break; }
            }
            foreach (ExpresswayInterchange ic in Xw.Interchanges)
            {
                if (ic.System) { seeds.Add(ic.At); break; }
            }
            foreach (BuildingSite b in Plan.Buildings)
            {
                if (b.IsMall) { seeds.Add(new Vec2(b.CentreX, b.CentreZ)); break; }
            }
            foreach (Vec2 p in seeds)
            {
                int cx = PlanWorld.ChunkOf(p.X), cy = PlanWorld.ChunkOf(p.Y);
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        foreach (MeshBuffer b in Chunk(cx + dx, cy + dy))
                        {
                            AssertWound(b, $"chunk ({cx + dx},{cy + dy})");
                            foreach (Vector3 v in b.Vertices)
                            {
                                Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.y), $"a bad vertex in chunk ({cx + dx},{cy + dy})");
                            }
                        }
                    }
                }
            }
        }

        // ---- helpers ----

        /// <summary>The main-line sample furthest above the ground (but not over a river trench, where the ground is the trench's floor).</summary>
        private static (ViaductPath, int) AHighMainSample()
        {
            ViaductPath best = null;
            int bestIndex = -1;
            double bestRise = 0.0;
            foreach (ExpresswayRoute route in Xw.Routes)
            {
                ViaductPath m = Xw.Paths[route.Path];
                for (int i = 0; i < m.Count; i += 3)
                {
                    if (Plan.Fields.IsWater(m.P[i])) continue;
                    double rise = m.Top[i] - Plan.GroundHeight(m.P[i].X, m.P[i].Y);
                    if (rise > bestRise && rise < 30.0) { bestRise = rise; best = m; bestIndex = i; }
                }
            }
            Assert.IsNotNull(best, "no elevated expressway");
            return (best, bestIndex);
        }

        /// <summary>One chunk's surface only (CitySurfaceBuilder): ground, pavement, structures (the props), markings, lane dividers, guard rail.</summary>
        private static MeshBuffer[] Surface(int cx, int cy, bool far)
        {
            var buffers = new MeshBuffer[6];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3((float)(cx * PlanWorld.ChunkSize), 0.0f, (float)(cy * PlanWorld.ChunkSize));
            CitySurfaceBuilder.Build(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], far ? null : buffers[3], far ? null : buffers[4], far ? null : buffers[5], far);
            // Chunk-local to world, so the asserts can read heights off the plan.
            foreach (MeshBuffer b in buffers)
            {
                for (int i = 0; i < b.Vertices.Count; i++) b.Vertices[i] += origin;
            }
            return buffers;
        }

        /// <summary>One building alone, in its own frame about its centre (x, z local; y absolute): structures, concrete, lit glass, dark glass.</summary>
        private static MeshBuffer[] Site(BuildingSite site, bool far)
        {
            var buffers = new MeshBuffer[4];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
            CityMeshBuilder.BuildSite(World, site, origin, buffers[0], buffers[1], buffers[2], buffers[3], null, null, null, null, far);
            // Back into the builder's unturned frame (points and normals both), so the asserts can use the site's width and depth.
            float cos = (float)site.AxisX, sin = (float)site.AxisZ;
            foreach (MeshBuffer b in buffers)
            {
                for (int i = 0; i < b.Vertices.Count; i++)
                {
                    Vector3 v = b.Vertices[i];
                    site.WorldToLocal(v.x + site.CentreX, v.z + site.CentreZ, out double lx, out double lz);
                    b.Vertices[i] = new Vector3((float)lx, v.y, (float)lz);
                    Vector3 n = b.Normals[i];
                    b.Normals[i] = new Vector3(n.x * cos + n.z * sin, n.y, -n.x * sin + n.z * cos);
                }
            }
            return buffers;
        }

        private static MeshBuffer[] Chunk(int cx, int cy)
        {
            var buffers = new MeshBuffer[5];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
            CityMeshBuilder.BuildChunk(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3]);
            return buffers;
        }

        private static bool HasLook(MeshBuffer buffer, Color32 look)
        {
            foreach (Color32 c in buffer.Colors)
            {
                if (c.r == look.r && c.g == look.g && c.b == look.b && c.a == look.a) return true;
            }
            return false;
        }

        private static void AssertWound(MeshBuffer buffer, string what)
        {
            for (int i = 0; i + 2 < buffer.Triangles.Count; i += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[i]];
                Vector3 b = buffer.Vertices[buffer.Triangles[i + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[i + 2]];
                Vector3 n = buffer.Normals[buffer.Triangles[i]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude < 1e-12f) continue;
                Assert.GreaterOrEqual(Vector3.Dot(face, n), -1e-4f * face.magnitude, $"{what}: triangle {i / 3} is wound against its normal");
            }
        }
    }
}
