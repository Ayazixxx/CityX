using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// A public stairway through a steep block (<see cref="StairLanes"/>): from the lot line on the block's low side straight across to the lot
    /// line on its high side, <see cref="Width"/> wide; the block's lots are cut either side of it.
    /// </summary>
    public sealed class StairLane
    {
        public int Id;
        public int Block;

        /// <summary>The low end and the high end, on the block's lot line (the back of the pavement).</summary>
        public Vec2 A, B;

        public double Width;

        /// <summary>How far the paving at <see cref="B"/> stands over the paving at <see cref="A"/>, metres.</summary>
        public double Drop;

        public double Length => (B - A).Length;

        public Vec2 Dir => (B - A).Normalized;
    }

    /// <summary>One tread of a stairway's profile: from <see cref="S0"/> to <see cref="S1"/> metres along it from its low end, its top at <see cref="Top"/>.</summary>
    public readonly struct StairTread
    {
        public readonly double S0, S1, Top;

        public StairTread(double s0, double s1, double top)
        {
            S0 = s0;
            S1 = s1;
            Top = top;
        }
    }

    /// <summary>
    /// STAIRWAYS THROUGH STEEP BLOCKS (owner, 2026-10-16: "make more staircases throughout the city overall ... san francisco has a lot of randoms
    /// staircases that go up hills and stuff in intricate ways study how they do it"). San Francisco has 600-900 public stairways and Pittsburgh some
    /// 700 sets of city steps (Wikipedia's "Step street", KQED's Bay Curious, the Pittsburgh city steps guides): where a street would have been
    /// too steep, or a block too long to walk round, a stair takes its place -- built into a sidewalk, or a flight of its own between two streets
    /// at different levels (Pittsburgh: some 450 on structures, 350 in sidewalks). They climb straight up the slope with landings where it eases,
    /// are concrete or stone with a handrail on posts down the middle, often hedged or gardened at the sides (the Lyon Street Steps' paired
    /// flights and clipped hedges, the Filbert Steps through the Grace Marchant Garden, the mosaic 16th Avenue steps).
    ///
    /// Here: a steep block (its paving climbing <see cref="MinDrop"/> or more straight across it, at a grade of <see cref="MinGrade"/> or more)
    /// gets, with <see cref="Share"/>, one stairway straight across from the lot line on its low side to the lot line on its high side, the
    /// steepest square crossing there is, no two within <see cref="Spacing"/>; the block's lots are cut either side of it
    /// (LotSubdivider), so it runs between the buildings from one street's pavement to the other's. Its steps follow the ground
    /// (<see cref="Profile"/>): a flight where the ground climbs, a level tread where it eases, a landing every <see cref="FlightMax"/> steps.
    /// Pure, from the plan.
    /// </summary>
    public static class StairLanes
    {
        /// <summary>A stairway's width, metres (San Francisco's are 2.4-4.5 m; a 3.4 m one at the game's 1.3x).</summary>
        public const double Width = 4.4;

        /// <summary>A rise across the block worth a stairway, and the least grade (8-12 %: long treads, a step every few strides, a cordonata).</summary>
        public const double MinDrop = 5.0;

        public const double MinGrade = 0.08;

        /// <summary>The steepest a stairway climbs on average (its flights at a riser per tread with landings, about 26 degrees).</summary>
        public const double MaxGrade = 0.48;

        /// <summary>How long a stairway is at the least and the most (the most times the square root of the district's scale), metres.</summary>
        public const double MinLength = 18.0;

        public const double MaxLength = 150.0;

        /// <summary>No two stairways' middles closer than this, metres (times the square root of the district's scale), and the share of the steep blocks that get one.</summary>
        public const double Spacing = 90.0;

        public const double Share = 0.75;

        /// <summary>How far the lots keep from a stairway's sides, metres.</summary>
        public const double LotClear = 0.6;

        /// <summary>A step: its going and its rise (EntranceStairs: 0.32 and 0.22; a little gentler outdoors on a long climb).</summary>
        public const double Tread = 0.36;

        public const double Riser = 0.2;

        /// <summary>
        /// The going of a step where the flight has fallen more than a riser and a half behind the slope (a step street up a 53 % hill): steeper
        /// steps, as San Francisco's are on its steepest hills, so the flight catches up.
        /// </summary>
        public const double CompactTread = 0.28;

        /// <summary>Steps in a flight before a landing (3.6 m of rise; the building code's 12 ft between landings is 4.75 m at the game's 1.3x, and the
        /// bridges' flights are 18 steps too), and the landing's length, metres.</summary>
        public const int FlightMax = 18;

        public const double Landing = 1.8;

        /// <summary>The level pad at each end, on the pavement's line, metres.</summary>
        public const double EndPad = 0.8;

        /// <summary>
        /// How far a corner of the stairway's far end may stand past the lot line there, metres: its end is cut square, so where the far side of the
        /// block runs off square one corner stands out over the pavement and the other short of it (half the width times the slant's tangent;
        /// 0.6 m is about 12 degrees off square).
        /// </summary>
        private const double MaxEndOvershoot = 0.6;

        /// <summary>
        /// Picks the city's stairways (after the malls and plazas, before any lot is cut): the blocks' steepest square crossings, a share of them,
        /// spaced apart, clear of the expressways.
        /// </summary>
        public static void Choose(CityPlanData data, uint seed)
        {
            PlanFields fields = data.Fields;
            var chosen = new List<Vec2>();
            for (int b = 0; b < data.Blocks.Count; b++)
            {
                PlanBlock block = data.Blocks[b];
                if (block.Outer == null || block.Outer.Count < 3 || !block.Paved || block.Encloses || block.Plaza || block.Mall) continue;
                if (PlanNoise.Mix(seed, (uint)b) / 4294967296.0 >= Share) continue;
                if (!SteepestCrossing(block.Outer, fields, out Vec2 a, out Vec2 bEnd, out double drop)) continue;
                Vec2 middle = (a + bEnd) * 0.5;
                double spacing = Spacing * Math.Sqrt(fields.ScaleAt(middle.X, middle.Y));
                bool near = false;
                foreach (Vec2 other in chosen)
                {
                    if ((other - middle).Length < spacing) near = true;
                }
                if (near) continue;
                var lane = new StairLane { Id = data.StairLanes.Count, Block = b, A = a, B = bEnd, Width = Width, Drop = drop };
                if (data.Expressways.Touches(Corridor(lane, LotClear))) continue;
                if (!Fits(lane, fields)) continue;
                data.StairLanes.Add(lane);
                block.StairLane = lane.Id;
                chosen.Add(middle);
            }
        }

        /// <summary>A stairway's ground (and <paramref name="margin"/> more either side), end to end, counter-clockwise.</summary>
        public static Vec2[] Corridor(StairLane lane, double margin = 0.0)
        {
            Vec2 side = lane.Dir.Perp * (lane.Width * 0.5 + margin);
            var poly = new[] { lane.A - side, lane.B - side, lane.B + side, lane.A + side };
            if (PlanMath.SignedArea(poly) < 0.0) Array.Reverse(poly);
            return poly;
        }

        /// <summary>
        /// The steepest straight crossing of a block that a stairway can take: from points along each side of its lot line, square into the block
        /// to where that line first meets another side, which must run near square to it; the whole corridor, its lots' clearance either side
        /// included, inside the block and its two side lines each crossing the lot line only twice (so the lots either side are each one piece).
        /// The one whose ends' paving differs most, long and steep enough. A is its low end.
        /// </summary>
        private static bool SteepestCrossing(List<Vec2> ring, PlanFields fields, out Vec2 low, out Vec2 high, out double drop)
        {
            low = high = default;
            drop = 0.0;
            int n = ring.Count;
            bool ccw = PlanMath.SignedArea(ring) > 0.0;
            double half = Width * 0.5 + LotClear;
            for (int i = 0; i < n; i++)
            {
                Vec2 p0 = ring[i], p1 = ring[(i + 1) % n];
                Vec2 edge = p1 - p0;
                double edgeLength = edge.Length;
                if (edgeLength < 2.0 * half + 6.0) continue;
                Vec2 along = edge / edgeLength;
                Vec2 inward = along.Perp * (ccw ? 1.0 : -1.0);
                foreach (double frac in new[] { 0.5, 0.3, 0.7 })
                {
                    double at = edgeLength * frac;
                    if (at < half + 3.0 || at > edgeLength - half - 3.0) continue;
                    Vec2 start = p0 + along * at;
                    if (!FirstCrossing(ring, i, start, inward, out Vec2 end, out int j)) continue;
                    Vec2 far = ring[(j + 1) % n] - ring[j];
                    double cos = Math.Abs(Vec2.Dot(far.Normalized, along)), sin = Math.Abs(Vec2.Cross(far.Normalized, along));
                    if (cos < 1e-6 || half * sin / cos > MaxEndOvershoot) continue;   // the far side slants too far from square
                    double length = (end - start).Length;
                    Vec2 middle = (start + end) * 0.5;
                    double scale = fields.ScaleAt(middle.X, middle.Y);
                    if (length < MinLength || length > MaxLength * Math.Sqrt(scale)) continue;
                    double d = fields.Height(end.X, end.Y) - fields.Height(start.X, start.Y);
                    double rise = Math.Abs(d);
                    if (rise <= drop || rise < MinDrop) continue;
                    double grade = rise / length;
                    if (grade < MinGrade || grade > MaxGrade) continue;
                    if (!Clear(ring, start, end, inward, half)) continue;
                    drop = rise;
                    low = d >= 0.0 ? start : end;
                    high = d >= 0.0 ? end : start;
                }
            }
            return drop > 0.0;
        }

        /// <summary>Where a ray from a point on side <paramref name="from"/> of a ring first meets another side, and which.</summary>
        private static bool FirstCrossing(List<Vec2> ring, int from, Vec2 start, Vec2 dir, out Vec2 end, out int side)
        {
            end = default;
            side = -1;
            int n = ring.Count;
            double best = double.MaxValue;
            for (int j = 0; j < n; j++)
            {
                if (j == from) continue;
                Vec2 q0 = ring[j], q1 = ring[(j + 1) % n];
                if (PlanMath.SegmentIntersect(start, start + dir * 1000.0, q0, q1, out double t, out double u) && t > 1e-6 && u >= 0.0 && u <= 1.0 && t * 1000.0 < best)
                {
                    best = t * 1000.0;
                    side = j;
                }
            }
            if (side < 0) return false;
            end = start + dir * best;
            return true;
        }

        /// <summary>
        /// Whether a corridor <paramref name="half"/> either side of the line from start to end lies inside the ring (points along both its sides
        /// inside, a little in from the ends), and each of its side lines crosses the ring exactly twice.
        /// </summary>
        private static bool Clear(List<Vec2> ring, Vec2 start, Vec2 end, Vec2 dir, double half)
        {
            Vec2 side = dir.Perp;
            double length = (end - start).Length;
            foreach (double across in new[] { -half, half })
            {
                // Every 2 m from end to end, the last half a metre from the far end whatever the spacing leaves.
                for (double s = 0.5; ; s = Math.Min(s + 2.0, length - 0.5))
                {
                    if (!PlanMath.PointInPolygon(start + dir * s + side * across, ring)) return false;
                    if (s >= length - 0.5) break;
                }
                if (Crossings(ring, start + side * across, dir) != 2) return false;
            }
            return true;
        }

        /// <summary>How many times the infinite line through a point along a direction crosses a ring (sign changes of the side its corners are on).</summary>
        private static int Crossings(List<Vec2> ring, Vec2 point, Vec2 dir)
        {
            int n = ring.Count, count = 0;
            for (int i = 0; i < n; i++)
            {
                bool a = Vec2.Cross(dir, ring[i] - point) < 0.0, b = Vec2.Cross(dir, ring[(i + 1) % n] - point) < 0.0;
                if (a != b) count++;
            }
            return count;
        }

        /// <summary>
        /// The most a stairway's treads may stray from the paving along its middle, under it or over it, metres: a stairway too steep for its
        /// flights in the middle sank into a trench 5 m deep (seed 45678679 at the classic scale) and is not taken.
        /// </summary>
        public const double MaxStray = 1.0;

        /// <summary>
        /// Whether the stairway's steps reach its high end within its length (the ground never runs away from them) and keep within
        /// <see cref="MaxStray"/> of the paving all the way.
        /// </summary>
        private static bool Fits(StairLane lane, PlanFields fields)
        {
            Func<double, double> paving = PavingAlong(lane, fields);
            List<StairTread> steps = Profile(paving, lane.Length, EndPad);
            double top = fields.Height(lane.B.X, lane.B.Y) + PlanBuildings.PavingRise;
            return steps.Count > 0 && Math.Abs(steps[steps.Count - 1].Top - top) < Riser * 0.75 && WorstStray(steps, paving) <= MaxStray;
        }

        /// <summary>How far a run of treads strays from the surface it follows at worst (under it or over it), at each tread's middle, metres.</summary>
        public static double WorstStray(List<StairTread> treads, Func<double, double> surface)
        {
            double worst = 0.0;
            foreach (StairTread t in treads) worst = Math.Max(worst, Math.Abs(surface((t.S0 + t.S1) * 0.5) - t.Top));
            return worst;
        }

        /// <summary>The paving along a stairway's middle, s metres from its low end.</summary>
        private static Func<double, double> PavingAlong(StairLane lane, PlanFields fields)
        {
            double length = lane.Length;
            Vec2 a = lane.A, dir = lane.Dir;
            return s =>
            {
                Vec2 p = a + dir * Math.Max(0.0, Math.Min(length, s));
                return fields.Height(p.X, p.Y) + PlanBuildings.PavingRise;
            };
        }

        /// <summary>
        /// The stairway's treads from its low end to its high end, following the paving along its middle (the general
        /// <see cref="Profile(Func{double, double}, double, double)"/>).
        /// </summary>
        public static List<StairTread> Profile(StairLane lane, PlanFields fields) => Profile(PavingAlong(lane, fields), lane.Length, EndPad);

        /// <summary>
        /// Treads up a run of <paramref name="length"/> metres whose surface stands at <paramref name="surface"/>(s): a level pad at each end
        /// (<paramref name="endPad"/> long, or a quarter of a short run); a step up of <see cref="Riser"/> wherever the surface at a tread's far
        /// edge has climbed half a riser over the tread before, a step down where it has fallen as much, level treads where it is flat; a landing
        /// after every <see cref="FlightMax"/> steps up. Contiguous: each tread starts where the last ends. The stairways and the step streets.
        /// </summary>
        public static List<StairTread> Profile(Func<double, double> surface, double length, double endPad)
        {
            var treads = new List<StairTread>();
            double pad = Math.Min(endPad, length * 0.25);
            double y = surface(0.0);
            treads.Add(new StairTread(0.0, pad, y));
            double s0 = pad;
            int run = 0;
            double end = length - pad;
            while (s0 < end - 1e-6)
            {
                double s1 = Math.Min(end, s0 + Tread);
                // Ahead to the tread's far edge: a step goes up where the ground there has climbed, so the flight keeps up with a steep slope; a
                // shorter going where it has fallen well behind.
                double ground = surface(s1);
                if (ground - y > Riser * 1.5)
                {
                    s1 = Math.Min(end, s0 + CompactTread);
                    ground = surface(s1);
                }
                if (ground - y > Riser * 0.5)
                {
                    if (run >= FlightMax)
                    {
                        // A landing; short of the top pad, whatever room is left (the pass below makes up the rise lower down).
                        double landingEnd = Math.Min(end, s0 + Landing);
                        treads.Add(new StairTread(s0, landingEnd, y));
                        s0 = landingEnd;
                        run = 0;
                        continue;
                    }
                    y += Riser;
                    run++;
                }
                else if (y - ground > Riser * 0.5)
                {
                    y -= Riser;
                    run = 0;
                }
                else
                {
                    run = 0;
                }
                treads.Add(new StairTread(s0, s1, y));
                s0 = s1;
            }
            // The pad on the high end.
            treads.Add(new StairTread(end, length, y));
            // The pads climb nothing, so on a steep run the flights end short of the surface at the top (a 14 m step street at 36 % ended 0.58 m
            // under its junction): the difference is made up from the top down, a level tread turned into a step (or a step into a level tread)
            // at a time, never making a flight longer than FlightMax, until the stair meets the surface at both ends. What still does not meet
            // it, a stairway does not take (Fits reads the shortfall).
            double want = surface(length);
            for (int guard = 0; guard < 64; guard++)
            {
                double gap = want - treads[treads.Count - 1].Top;
                if (Math.Abs(gap) <= Riser * 0.5) break;
                int at = -1;
                for (int i = treads.Count - 2; i >= 1 && at < 0; i--)
                {
                    StairTread t = treads[i];
                    if (t.S1 - t.S0 > Tread + 1e-6) continue;   // a landing stays level
                    double rise = t.Top - treads[i - 1].Top;
                    if (gap > 0.0 ? Math.Abs(rise) < 1e-9 && FlightThrough(treads, i) <= FlightMax : rise > 1e-9) at = i;
                }
                if (at < 0) break;
                double shift = gap > 0.0 ? Riser : -Riser;
                for (int i = at; i < treads.Count; i++) treads[i] = new StairTread(treads[i].S0, treads[i].S1, treads[i].Top + shift);
            }
            return treads;
        }

        /// <summary>How many steps up in a row there would be through tread <paramref name="i"/> if it were a step up.</summary>
        private static int FlightThrough(List<StairTread> treads, int i)
        {
            int run = 1;
            for (int k = i - 1; k >= 1 && treads[k].Top - treads[k - 1].Top > 1e-9; k--) run++;
            for (int k = i + 1; k < treads.Count && treads[k].Top - treads[k - 1].Top > 1e-9; k++) run++;
            return run;
        }
    }
}
