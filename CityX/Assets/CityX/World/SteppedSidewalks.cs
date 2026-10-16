using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// SIDEWALKS THAT ARE STAIRS (owner, 2026-10-16: "make more staircases throughout the city overall ... san francisco has a lot of randoms
    /// staircases that go up hills and stuff in intricate ways study how they do it"). Where a street climbs too steeply for a comfortable
    /// pavement, San Francisco and Pittsburgh build the pavement as steps: 22nd Street at Vicksburg (a 31.5 % block) has a stairway for a
    /// sidewalk, and some 350 of Pittsburgh's city steps are set into sidewalks. Here a block's sidewalk along one of its streets is steps where
    /// the kerb climbs <see cref="Grade"/> or more over <see cref="MinLength"/> or more: the strip from the kerb to the lot line laid as treads that
    /// follow the slope (<see cref="Treads"/>), each a <see cref="Riser"/> above or below the last -- long where the street is gentle, short where
    /// it is steep. Not along a pedestrian street (its setts lie flush, no kerb), and not where a garage's drive or an expressway ramp's kerb cut
    /// crosses the strip (they lay their own graded surface across it). A pure function of the plan, worked out once per plan, so every chunk
    /// agrees about every strip.
    /// </summary>
    public static class SteppedSidewalks
    {
        /// <summary>A sidewalk climbing this much along its kerb (rise over run) is steps.</summary>
        public const double Grade = 0.14;

        /// <summary>The rise of one step, metres.</summary>
        public const double Riser = 0.2;

        /// <summary>A sidewalk shorter than this is left as it is, metres.</summary>
        public const double MinLength = 6.0;

        /// <summary>How far the strip reaches past the kerb line over the road, so the kerb along it is not drawn (the treads' faces are the kerb).</summary>
        public const double PastKerb = 0.05;

        private static readonly ConditionalWeakTable<PlanWorld, HashSet<long>> Cache = new ConditionalWeakTable<PlanWorld, HashSet<long>>();

        private static long Key(int block, int edge) => ((long)block << 16) | (uint)edge;

        /// <summary>Whether block <paramref name="block"/>'s sidewalk along its edge <paramref name="edge"/> (Poly[edge] to the next) is steps.</summary>
        public static bool Is(PlanWorld plan, int block, int edge) => Cache.GetValue(plan, Compute).Contains(Key(block, edge));

        /// <summary>How many sidewalks in the plan are steps.</summary>
        public static int Count(PlanWorld plan) => Cache.GetValue(plan, Compute).Count;

        /// <summary>The strip of a stepped sidewalk: kerb to lot line along the edge, <see cref="PastKerb"/> out over the road; convex, counter-clockwise.</summary>
        public static Vec2[] Strip(PlanBlock block, int edge)
        {
            int f = (edge + 1) % block.Inner.Count;
            Vec2 i0 = block.Inner[edge], i1 = block.Inner[f], o0 = block.Outer[edge], o1 = block.Outer[f];
            Vec2 toRoad = (i0 + i1 - o0 - o1).Normalized * PastKerb;
            var poly = new[] { i0 + toRoad, i1 + toRoad, o1, o0 };
            if (PlanMath.SignedArea(poly) < 0.0) System.Array.Reverse(poly);
            return poly;
        }

        /// <summary>
        /// The treads of a stepped sidewalk from its start (Inner[edge]) to its end, as fractions along it and each one's top: a new tread wherever
        /// the paving along the strip's middle has climbed (or fallen) half a riser from the tread before, its top a riser up (or down).
        /// </summary>
        public static List<(double t0, double t1, double top)> Treads(PlanWorld plan, PlanBlock block, int edge)
        {
            int f = (edge + 1) % block.Inner.Count;
            Vec2 i0 = block.Inner[edge], i1 = block.Inner[f], o0 = block.Outer[edge], o1 = block.Outer[f];
            double length = (i1 - i0).Length;
            double Paving(double t)
            {
                Vec2 m = (i0 + (i1 - i0) * t + o0 + (o1 - o0) * t) * 0.5;
                return plan.GroundHeight(m.X, m.Y) + PlanBuildings.PavingRise;
            }
            var treads = new List<(double, double, double)>();
            double y = Paving(0.0), start = 0.0;
            int samples = System.Math.Max(2, (int)System.Math.Ceiling(length / 0.1));
            for (int k = 1; k < samples; k++)
            {
                double t = (double)k / samples;
                double g = Paving(t);
                if (System.Math.Abs(g - y) <= Riser * 0.5) continue;
                treads.Add((start, t, y));
                y += g > y ? Riser : -Riser;
                start = t;
            }
            treads.Add((start, 1.0, y));
            return treads;
        }

        private static HashSet<long> Compute(PlanWorld plan)
        {
            var set = new HashSet<long>();
            List<PlanBlock> blocks = plan.Data.Blocks;
            for (int b = 0; b < blocks.Count; b++)
            {
                PlanBlock block = blocks[b];
                if (block.Inner == null || block.Outer == null) continue;
                int n = block.Inner.Count;
                for (int e = 0; e < n; e++)
                {
                    int f = (e + 1) % n;
                    Vec2 i0 = block.Inner[e], i1 = block.Inner[f];
                    double length = (i1 - i0).Length;
                    if (length < MinLength || (block.Outer[f] - block.Outer[e]).Length < 1.0) continue;
                    if (System.Math.Abs(plan.GroundHeight(i1.X, i1.Y) - plan.GroundHeight(i0.X, i0.Y)) / length < Grade) continue;
                    if (PedestrianStreets.Along(plan, block.Poly[e], block.Poly[f])) continue;
                    set.Add(Key(b, e));
                }
            }
            if (set.Count == 0) return set;
            // What crosses a strip with a surface of its own: a garage's drives and an expressway ramp's kerb cuts.
            foreach (BuildingSite site in plan.Buildings)
            {
                if (!site.IsParkingGarage || !site.Exists) continue;
                foreach (GarageEntrance entrance in GarageEntrances.For(plan, site))
                {
                    if (entrance.HasDriveway) Unmark(plan, set, GarageEntrances.Driveway(site, entrance, 0.5f));
                }
            }
            foreach (KerbCut cut in plan.Data.Expressways.KerbCuts) Unmark(plan, set, cut.Poly);
            return set;
        }

        private static void Unmark(PlanWorld plan, HashSet<long> set, Vec2[] poly)
        {
            double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
            foreach (Vec2 p in poly)
            {
                x0 = System.Math.Min(x0, p.X);
                z0 = System.Math.Min(z0, p.Y);
                x1 = System.Math.Max(x1, p.X);
                z1 = System.Math.Max(z1, p.Y);
            }
            var seen = new HashSet<int>();
            for (int cy = PlanWorld.ChunkOf(z0); cy <= PlanWorld.ChunkOf(z1); cy++)
            {
                for (int cx = PlanWorld.ChunkOf(x0); cx <= PlanWorld.ChunkOf(x1); cx++)
                {
                    foreach (int b in plan.BlocksIn(cx, cy))
                    {
                        if (!seen.Add(b)) continue;
                        PlanBlock block = plan.Data.Blocks[b];
                        if (block.Inner == null || block.Outer == null) continue;
                        for (int e = 0; e < block.Inner.Count; e++)
                        {
                            if (set.Contains(Key(b, e)) && PlanMath.PolygonsIntersect(Strip(block, e), poly)) set.Remove(Key(b, e));
                        }
                    }
                }
            }
        }
    }
}
