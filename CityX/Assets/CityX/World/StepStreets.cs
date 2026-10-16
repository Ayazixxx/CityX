using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// STEP STREETS (owner, 2026-10-16: "san francisco has a lot of randoms staircases that go up hills and stuff in intricate ways study how they
    /// do it"). Where San Francisco's grid ran up a slope too steep to drive, the street was built as steps instead, kerb to kerb: the Vallejo,
    /// Filbert and Greenwich Street steps on Russian and Telegraph Hills, the Lyon Street Steps' paired flights with a planted middle, Pittsburgh's
    /// "paper streets" that are flights of city steps on the map. The city stopped building drivable streets at about 31 %; our car stalls at
    /// about 21 % (Vehicles/Car: twelve degrees).
    ///
    /// Here an ordinary street (not an arterial, a boulevard or a bridge) whose ground climbs <see cref="Grade"/> or more from one junction to the
    /// other, over <see cref="MinLength"/> or more, that no expressway ramp or exit lands on and whose stair keeps within <see cref="MaxStray"/> of
    /// the road and meets it at both junctions (a street steeper than any flight climbs, a river bank, stays a road), is a step street: from the junction plate at one
    /// end to the plate at the other its carriageway is concrete steps following the ground (<see cref="Profile"/>: the stairways' treads,
    /// landings every flight), with a level pad at each end where a row of bollards closes it to cars; a wide one is split into two flights by a
    /// planter down its middle (the Lyon Street Steps), each flight with a handrail down it. Its sidewalks, as steep, are steps of their own
    /// (SteppedSidewalks). No garage or car park is put on one (Amenities.ParkingPlanner). It is a pure rule of the plan: the plan stores nothing.
    /// </summary>
    public static class StepStreets
    {
        /// <summary>A street climbing this much from junction to junction (rise over run) is steps.</summary>
        public const double Grade = 0.21;

        /// <summary>A street shorter than this keeps its carriageway, metres.</summary>
        public const double MinLength = 15.0;

        /// <summary>The level pad at each end, where the bollards stand, metres (short: a pad climbs nothing, and the flights must make it up).</summary>
        public const double EndPad = 1.0;

        /// <summary>
        /// The most a step street's treads may stray from the road they replace, under it or over it, metres: steeper than its flights can climb (a
        /// river bank on seed 45678679), the stair sank 13 m under the road in the middle, below its own stepped sidewalks; such a street stays a road.
        /// </summary>
        public const double MaxStray = 0.75;

        /// <summary>A carriageway this wide (half, metres) or more is two flights with a planter down the middle, this wide.</summary>
        public const double MedianFrom = 4.0;

        public const double MedianWidth = 1.4;

        /// <summary>The bollards across each end: how far in from the trim, and how far apart, metres.</summary>
        public const double BollardInset = 0.5;

        public const double BollardSpacing = 1.6;

        private static readonly ConditionalWeakTable<CityPlanData, HashSet<int>> Landed = new ConditionalWeakTable<CityPlanData, HashSet<int>>();

        /// <summary>Whether a street of this class between these two junctions is steep enough to be steps (the ground alone).</summary>
        public static bool Steep(PlanFields fields, Vec2 a, Vec2 b, RoadClass roadClass)
        {
            if (roadClass != RoadClass.Street) return false;
            double length = (b - a).Length;
            return length >= MinLength && Math.Abs(fields.Height(b.X, b.Y) - fields.Height(a.X, a.Y)) / length >= Grade;
        }

        /// <summary>Whether a road is one of the step streets.</summary>
        public static bool Is(PlanWorld plan, RoadEdge edge)
        {
            if (edge == null || !edge.Alive || edge.Bridge) return false;
            if (!Steep(plan.Fields, edge.A.P, edge.B.P, edge.Class)) return false;
            if (Landed.GetValue(plan.Data, RampStreets).Contains(edge.Id)) return false;
            // Its stair must meet the road at both junctions: a street steeper than any flight can climb (a few in a region, over 55 %) stays a
            // road rather than end in a ledge under its top junction.
            Dictionary<int, bool> fits = FitCache.GetValue(plan, _ => new Dictionary<int, bool>());
            lock (fits)
            {
                if (fits.TryGetValue(edge.Id, out bool known)) return known;
            }
            bool fit = Fits(plan, plan.Geometry.Edges[edge.Id]);
            lock (fits)
            {
                fits[edge.Id] = fit;
            }
            return fit;
        }

        private static readonly ConditionalWeakTable<PlanWorld, Dictionary<int, bool>> FitCache = new ConditionalWeakTable<PlanWorld, Dictionary<int, bool>>();

        /// <summary>
        /// Whether a street's stair (<see cref="Profile"/>) ends within three quarters of a riser of the road at its high junction and keeps within
        /// <see cref="MaxStray"/> of the road all the way.
        /// </summary>
        private static bool Fits(PlanWorld plan, EdgeGeometry g)
        {
            if (g == null || g.Length - g.TrimA - g.TrimB < 2.0) return false;
            List<StairTread> treads = Profile(plan, g, out bool fromB);
            Vec2 top = g.A + g.Dir * (fromB ? g.TrimA : g.Length - g.TrimB);
            return Math.Abs(plan.GroundHeight(top.X, top.Y) + PlanGeometry.RoadLift - treads[treads.Count - 1].Top) <= StairLanes.Riser * 0.75
                && StairLanes.WorstStray(treads, Road(plan, g, fromB)) <= MaxStray;
        }

        /// <summary>The streets an expressway ramp, a ramp's kerb cut or an exit lands on: they stay roads.</summary>
        private static HashSet<int> RampStreets(CityPlanData data)
        {
            var set = new HashSet<int>();
            foreach (ViaductPath p in data.Expressways.Paths) if (p.StreetEdge >= 0) set.Add(p.StreetEdge);
            foreach (KerbCut c in data.Expressways.KerbCuts) if (c.StreetEdge >= 0) set.Add(c.StreetEdge);
            foreach (ExpresswayInterchange i in data.Expressways.Interchanges) if (i.StreetEdge >= 0) set.Add(i.StreetEdge);
            return set;
        }

        /// <summary>
        /// A step street's treads between its trims (where the junction plates end), from its LOW end: <paramref name="fromB"/> when that is the
        /// edge's B end. Each tread's S0 and S1 are metres from the low end's trim; the surface followed is the road's (the ground plus RoadLift).
        /// </summary>
        public static List<StairTread> Profile(PlanWorld plan, EdgeGeometry g, out bool fromB)
        {
            double t0 = g.TrimA, t1 = g.Length - g.TrimB;
            Vec2 a = g.A + g.Dir * t0, b = g.A + g.Dir * t1;
            fromB = plan.GroundHeight(b.X, b.Y) < plan.GroundHeight(a.X, a.Y);
            double length = Math.Max(0.0, t1 - t0);
            return StairLanes.Profile(Road(plan, g, fromB), length, EndPad);
        }

        /// <summary>The road's surface along a step street's middle, s metres from its low end's trim.</summary>
        private static Func<double, double> Road(PlanWorld plan, EdgeGeometry g, bool fromB)
        {
            double t0 = g.TrimA, t1 = g.Length - g.TrimB, length = Math.Max(0.0, t1 - t0);
            Vec2 low = g.A + g.Dir * (fromB ? t1 : t0), dir = fromB ? -g.Dir : g.Dir;
            return s =>
            {
                Vec2 p = low + dir * Math.Max(0.0, Math.Min(length, s));
                return plan.GroundHeight(p.X, p.Y) + PlanGeometry.RoadLift;
            };
        }
    }
}
