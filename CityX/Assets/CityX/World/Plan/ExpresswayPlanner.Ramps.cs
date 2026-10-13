using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    public sealed partial class ExpresswayPlanner
    {
        // ================================================================== shared pieces

        /// <summary>
        /// A sample of a ramp or connector being laid: where, which way, how high (NaN until worked out), and how much narrower than a full lane
        /// it is (0: full width; toward 1 at the tip of a taper, where the ramp's lane begins or ends against its main line's edge).
        /// </summary>
        private struct Sample
        {
            public Vec2 P;
            public Vec2 Dir;
            public double Top;
            public double Narrow;
        }

        /// <summary>The narrowest a taper's tip is, as a share of the full lane: next to nothing, but never a zero-width deck.</summary>
        private const double TipNarrow = 0.97;

        /// <summary>A distance along a main line, wrapped round a ring.</summary>
        private static double Wrap(ViaductPath m, double s)
        {
            if (!m.Closed) return s;
            double length = m.Length;
            double r = s - length * Math.Floor(s / length);
            return r >= length ? 0.0 : r;
        }

        /// <summary>A main line at a distance along it, for traffic going one way (+1 with the samples, -1 against): the centre, the direction of travel, the right of it, the top.</summary>
        private static void MainPose(ViaductPath m, double s, int sigma, out Vec2 p, out Vec2 d, out Vec2 right, out double top)
        {
            m.At(Wrap(m, s), out p, out Vec2 t, out top);
            d = t * sigma;
            right = -d.Perp;
        }

        /// <summary>Whether a distance along a main line lies on it (a ring has no ends).</summary>
        private static bool OnLine(ViaductPath m, double s, double margin) => m.Closed || (s >= margin && s <= m.Length - margin);

        /// <summary>
        /// The nearest point of a main line to a point, searching only the stretch within <paramref name="window"/> of a distance along it (the
        /// ramps of one interchange never stray further): its distance along and how far away.
        /// </summary>
        private static void NearestLocal(ViaductPath m, Vec2 p, double hint, double window, out double along, out double distance)
        {
            along = hint;
            distance = double.MaxValue;
            int count = m.Segments;
            int centre = m.IndexAt(Wrap(m, hint));
            int span = (int)Math.Ceiling(window / SampleStep) + 1;
            for (int k = -span; k <= span; k++)
            {
                int i = centre + k;
                if (m.Closed) i = ((i % count) + count) % count;
                else if (i < 0 || i >= count) continue;
                double d = PlanMath.DistanceToSegment(p, m.P[i], m.P[i + 1], out double t);
                if (d < distance)
                {
                    distance = d;
                    along = m.S[i] + (m.S[i + 1] - m.S[i]) * t;
                }
            }
        }

        private const double Window = 700.0;

        /// <summary>
        /// The stretch where a ramp runs alongside a main line's edge, its inner edge on the main line's (the deceleration or acceleration lane):
        /// samples from <paramref name="s0"/> for <paramref name="length"/> in the direction of travel, with the main line's own top. It is a
        /// TAPER, as a real one is: the lane grows out of the main line's edge from nothing at its tip -- the first sample when
        /// <paramref name="tip"/> is +1 (a ramp leaving), the last when -1 (a ramp joining) -- to its full width where it curves away, so the
        /// carriageway widens and narrows instead of a ramp's square end standing against the main line (owner, 2026-10-13: "clearly cut off
        /// instead of transitioning into the major freeway").
        /// </summary>
        private void TaperSamples(ViaductPath m, double s0, int sigma, double length, List<Sample> into, int tip)
        {
            int k = Math.Max(2, (int)Math.Ceiling(length / SampleStep));
            for (int i = 0; i <= k; i++)
            {
                MainPose(m, s0 + sigma * length * i / k, sigma, out Vec2 p, out Vec2 d, out Vec2 right, out double top);
                double fromTip = tip == 0 ? 1.0 : (tip > 0 ? i / (double)k : (k - i) / (double)k);   // 0 at the tip, 1 where the lane is whole
                double widened = fromTip * (2.0 - fromTip);   // eased: the outer edge comes in parallel where the lane is whole
                double narrow = TipNarrow * (1.0 - widened);
                double half = _wr * (1.0 - narrow);
                into.Add(new Sample { P = p + right * (m.HalfLeft[0] + half), Dir = d, Top = top, Narrow = narrow });
            }
        }

        /// <summary>The samples of a Dubins path between two poses (the ends left out: the pieces either side already have them).</summary>
        private static void DubinsSamples(DubinsPath path, List<Sample> into)
        {
            List<Pose> poses = path.Sample(SampleStep);
            for (int i = 1; i < poses.Count - 1; i++)
            {
                into.Add(new Sample { P = poses[i].P, Dir = poses[i].Dir, Top = double.NaN });
            }
        }

        /// <summary>A straight run between two points, its ends included (the first only if asked).</summary>
        private static void LineSamples(Vec2 a, Vec2 b, List<Sample> into, bool includeStart)
        {
            Vec2 d = (b - a).Normalized;
            int k = Math.Max(1, (int)Math.Ceiling((b - a).Length / SampleStep));
            for (int i = includeStart ? 0 : 1; i <= k; i++)
            {
                into.Add(new Sample { P = a + (b - a) * (i / (double)k), Dir = d, Top = double.NaN });
            }
        }

        /// <summary>
        /// The grade shape of a ramp's slope: no grade at either end, rising to a steady grade over a vertical curve <paramref name="a"/> of the
        /// length at each end. The fraction of the height covered at a fraction <paramref name="u"/> of the length; the steepest grade is
        /// height / (length * (1 - a)).
        /// </summary>
        private static double SlopeShape(double u, double a)
        {
            u = Math.Max(0.0, Math.Min(1.0, u));
            if (a <= 1e-9) return u;
            double area = 1.0 - a;
            double v;
            if (u <= a) v = u * u / (2.0 * a);
            else if (u <= 1.0 - a) v = a * 0.5 + (u - a);
            else v = area - (1.0 - u) * (1.0 - u) / (2.0 * a);
            return v / area;
        }

        /// <summary>The vertical curve at each end of a slope of this length: 30 m, or a third of a short one.</summary>
        private static double CurveShare(double length) => Math.Min(0.3, 30.0 / Math.Max(1.0, length));

        /// <summary>
        /// Gives the samples from <paramref name="from"/> to <paramref name="to"/> (inclusive) a slope on <see cref="SlopeShape"/> between two
        /// tops. False when that is steeper than <paramref name="grade"/>.
        /// </summary>
        private static bool Slope(List<Sample> samples, int from, int to, double startTop, double endTop, double grade)
        {
            double length = 0.0;
            for (int i = from + 1; i <= to; i++) length += (samples[i].P - samples[i - 1].P).Length;
            if (length < 1e-6) return Math.Abs(endTop - startTop) < 0.05;
            double a = CurveShare(length);
            if (Math.Abs(endTop - startTop) / (length * (1.0 - a)) > grade) return false;
            double walked = 0.0;
            for (int i = from; i <= to; i++)
            {
                if (i > from) walked += (samples[i].P - samples[i - 1].P).Length;
                SetTop(samples, i, startTop + (endTop - startTop) * SlopeShape(walked / length, a));
            }
            return true;
        }

        private static void SetTop(List<Sample> samples, int i, double top)
        {
            Sample s = samples[i];
            s.Top = top;
            samples[i] = s;
        }

        /// <summary>
        /// Lifts the sloping stretch of a ramp or connector (samples <paramref name="from"/> to <paramref name="to"/>) where its even slope would
        /// pass too low over a road, or through a deck laid before it at about its own height: the deck stays up, <see cref="RampClearance"/> under
        /// its slab, over the road or the other deck (a flyover, as connectors weave over each other in a real interchange) and comes down after it
        /// at no more than the ramp grade, both ends of the stretch where they were. Its own street, within <paramref name="footZone"/> of the foot
        /// (the last sample when <paramref name="footAtEnd"/>, else the first), is no obstacle: it lands on it; nor are the main lines it runs level
        /// beside (<paramref name="lineA"/>, <paramref name="lineB"/>). A deck well above it is left to <see cref="Clear"/>: rising cannot pass
        /// under one. False if it cannot get back down in time.
        /// </summary>
        private bool LiftOverRoads(List<Sample> samples, int from, int to, int footStreet, double footZone, bool footAtEnd, ViaductPath lineA, ViaductPath lineB)
        {
            int lo = Math.Min(from, to), hi = Math.Max(from, to);
            int n = hi - lo + 1;
            if (n < 3) return true;
            var walked = new double[samples.Count];
            for (int i = 1; i < samples.Count; i++) walked[i] = walked[i - 1] + (samples[i].P - samples[i - 1].P).Length;
            double total = walked[samples.Count - 1];
            var s = new double[n];
            var orig = new double[n];
            var cone = new double[n];   // the lowest a deck may be, coming off the heights the roads under it need at a little under the ramp grade
            bool raised = false;
            for (int k = 0; k < n; k++)
            {
                int i = lo + k;
                s[k] = walked[i] - walked[lo];
                orig[k] = samples[i].Top;
                cone[k] = double.NegativeInfinity;
                double needed = double.NegativeInfinity;
                double clear = Ground(samples[i].P) + RampClearance + RampSlab + 0.4;
                if (orig[k] < clear)
                {
                    bool nearFoot = footStreet >= 0 && (footAtEnd ? total - walked[i] : walked[i]) < footZone;
                    if (RoadUnder(samples[i].P, _wr + 1.0, nearFoot ? footStreet : -1)) needed = clear;
                }
                DecksAt(samples[i].P, _wr + 0.5, _liftDecks);
                foreach ((int pathId, int _, double top) in _liftDecks)
                {
                    ViaductPath o = _out.Paths[pathId];
                    if ((o == lineA || o == lineB) && Math.Abs(orig[k] - top) < 0.6) continue;   // beside its own main line, level with it
                    if (top > orig[k] + 2.0) continue;
                    needed = Math.Max(needed, top + RampClearance + RampSlab + 0.4);
                }
                if (needed <= orig[k]) continue;
                cone[k] = needed;
                raised = true;
            }
            if (!raised) return true;
            double grade = RampGrade * 0.9;
            for (int k = 1; k < n; k++) cone[k] = Math.Max(cone[k], cone[k - 1] - grade * (s[k] - s[k - 1]));
            for (int k = n - 2; k >= 0; k--) cone[k] = Math.Max(cone[k], cone[k + 1] - grade * (s[k + 1] - s[k]));
            var need = new double[n];
            for (int k = 0; k < n; k++) need[k] = Math.Max(0.0, cone[k] - orig[k]);
            // The lift rounded (only the lift: an untouched stretch, and the ends, keep their own heights exactly), never far under the need.
            double[] lift = need;
            const int window = 3;
            for (int round = 0; round < 2; round++)
            {
                var blurred = new double[n];
                for (int k = 0; k < n; k++)
                {
                    double sum = 0.0;
                    int count = 0;
                    for (int j = Math.Max(0, k - window); j <= Math.Min(n - 1, k + window); j++)
                    {
                        sum += lift[j];
                        count++;
                    }
                    blurred[k] = Math.Max(sum / count, need[k] - 0.1);
                }
                lift = blurred;
            }
            if (lift[0] > 0.05 || lift[n - 1] > 0.05) return false;
            for (int k = 0; k < n; k++) SetTop(samples, lo + k, orig[k] + lift[k]);
            return true;
        }

        private readonly List<(int path, int seg, double top)> _liftDecks = new List<(int path, int seg, double top)>();

        /// <summary>Whether a road (other than <paramref name="ignoreEdge"/>) runs under a point of a deck this wide (each side).</summary>
        private bool RoadUnder(Vec2 p, double half, int ignoreEdge)
        {
            _net.EdgesNear(p, p, half + _cfg.BoulevardHalfWidth + _cfg.SidewalkWidth + 1.0, _edgeScratch);
            foreach (RoadEdge e in _edgeScratch)
            {
                if (e.Id == ignoreEdge) continue;
                if (PlanMath.DistanceToSegment(p, e.A.P, e.B.P, out _) < half + CityPlan.HalfWidth(_cfg, e.Class) + _cfg.SidewalkWidth) return true;
            }
            return false;
        }

        /// <summary>
        /// The first sample, from <paramref name="from"/> toward <paramref name="to"/>, that has pulled <paramref name="gap"/> clear of a main
        /// line's edge: where the ramp's own edge begins (the nose of the gore).
        /// </summary>
        private int Nose(List<Sample> samples, ViaductPath m, double hint, int from, int to, double gap)
        {
            int step = to >= from ? 1 : -1;
            for (int i = from; i != to + step; i += step)
            {
                NearestLocal(m, samples[i].P, hint, Window, out _, out double distance);
                if (distance - m.HalfLeft[0] - _wr >= gap) return i;
            }
            return to;
        }

        /// <summary>Gives a sample the top of the main line it is beside.</summary>
        private static void Follow(List<Sample> samples, int i, ViaductPath line, double hint)
        {
            NearestLocal(line, samples[i].P, hint, Window, out double s, out _);
            line.At(Wrap(line, s), out _, out _, out double top);
            SetTop(samples, i, top);
        }

        /// <summary>
        /// A ramp or connector from its samples: the deck (samples closer than half a metre merged), the terrain never poking through it, the
        /// gore paved where it is still beside one of its main lines (its inner edge pushed out to meet the main line's, no parapet there).
        /// </summary>
        private ViaductPath Assemble(List<Sample> samples, ViaductKind kind, string shape, ViaductPath fromLine, double fromHint, ViaductPath toLine, double toHint)
        {
            var pts = new List<Vec2>(samples.Count);
            var dirs = new List<Vec2>(samples.Count);
            var tops = new List<double>(samples.Count);
            var halves = new List<double>(samples.Count);
            foreach (Sample s in samples)
            {
                if (double.IsNaN(s.Top)) return null;
                if (pts.Count > 0 && (s.P - pts[pts.Count - 1]).Length < 0.5) continue;
                pts.Add(s.P);
                dirs.Add(s.Dir);
                tops.Add(s.Top);
                halves.Add(_wr * (1.0 - s.Narrow));
            }
            if (pts.Count < 3) return null;
            ViaductPath path = MakePath(kind, pts, dirs, _wr, _wr, RampSlab, false);
            path.Shape = shape;
            for (int i = 0; i < pts.Count; i++)
            {
                // The terrain never pokes through: a ramp over a rise is lifted onto it (an embankment), never sunk into it.
                path.Top[i] = Math.Max(tops[i], Ground(pts[i]) + RoadLift + 0.05);
                path.HalfLeft[i] = path.HalfRight[i] = halves[i];   // a taper's lane narrows to its tip
            }
            Gore(path, fromLine, fromHint);
            Gore(path, toLine, toHint);
            return path;
        }

        private void Gore(ViaductPath path, ViaductPath line, double hint)
        {
            if (line == null) return;
            for (int i = 0; i < path.Count; i++)
            {
                NearestLocal(line, path.P[i], hint, Window, out double s, out double distance);
                double lane = path.HalfRight[i];   // the lane's own half width (only the inner side is ever widened)
                double gap = distance - line.HalfLeft[0] - lane;
                if (gap >= GoreWidth) continue;
                line.At(Wrap(line, s), out _, out _, out double lineTop);
                if (Math.Abs(path.Top[i] - lineTop) > 0.5) continue;
                path.HalfLeft[i] = Math.Max(path.HalfLeft[i], lane + Math.Max(0.0, gap));
                if (i < path.Segments) path.ParapetLeft[i] = false;
                if (i > 0) path.ParapetLeft[i - 1] = false;
            }
        }

        /// <summary>
        /// Turns off a main line's parapet along the stretch where an accepted ramp or connector runs beside it, its paved inner edge on the main
        /// line's edge -- but not at a taper's very tip, where the lane is still too narrow to drive: the main line's parapet runs on until the
        /// ramp's own outer parapet has peeled away from it.
        /// </summary>
        private void OpenParapet(ViaductPath line, ViaductPath ramp, double hint)
        {
            for (int i = 0; i < ramp.Count; i++)
            {
                if (ramp.HalfRight[i] < 0.6) continue;
                NearestLocal(line, ramp.P[i], hint, Window, out double s, out double distance);
                if (distance - line.HalfLeft[0] - ramp.HalfLeft[i] > 0.6) continue;
                int k = line.IndexAt(Wrap(line, s));
                bool left = Vec2.Dot(ramp.P[i] - line.P[k], line.N[k]) > 0.0;
                for (int q = Math.Max(0, k - 1); q <= Math.Min(line.Segments - 1, k); q++)
                {
                    if (left) line.ParapetLeft[q] = false; else line.ParapetRight[q] = false;
                }
            }
        }

        /// <summary>
        /// Whether a new ramp or connector clears everything: it stays in the region; where it is low it is over no road (bar its own street at
        /// its foot) and not down in a river; and wherever it passes over or under another deck there is <see cref="RampClearance"/> between
        /// them -- except beside the main lines it leaves and joins, where it runs level with them by design. And never too steep.
        /// </summary>
        private bool Clear(ViaductPath c, ViaductPath fromLine, ViaductPath toLine, int streetEdge, double footZone)
        {
            var decks = new List<(int path, int seg, double top)>();
            double length = c.Length;
            for (int i = 0; i < c.Count; i++)
            {
                Vec2 p = c.P[i];
                if (!InRegion(p, 20.0))
                {
                    Count("ramp: leaves the region");
                    return false;
                }
                double half = Math.Max(c.HalfLeft[i], c.HalfRight[i]);
                double under = c.Top[i] - c.Slab - Ground(p);
                bool nearFoot = streetEdge >= 0 && (c.StreetAtEnd ? length - c.S[i] : c.S[i]) < footZone;
                if (under < RampClearance)
                {
                    _net.EdgesNear(p, p, half + _cfg.BoulevardHalfWidth + _cfg.SidewalkWidth + 1.0, _edgeScratch);
                    foreach (RoadEdge e in _edgeScratch)
                    {
                        if (nearFoot && e.Id == streetEdge) continue;
                        double reach = half + CityPlan.HalfWidth(_cfg, e.Class) + _cfg.SidewalkWidth;
                        if (PlanMath.DistanceToSegment(p, e.A.P, e.B.P, out _) < reach)
                        {
                            Count("ramp: low over a road");
                            return false;
                        }
                    }
                    if (under < 2.0 && _fields.IsWater(p))
                    {
                        Count("ramp: down in a river");
                        return false;
                    }
                }
                DecksAt(p, half + 0.5, decks);
                foreach ((int pathId, int seg, double top) in decks)
                {
                    ViaductPath o = _out.Paths[pathId];
                    if ((o == fromLine || o == toLine) && Math.Abs(c.Top[i] - top) < 0.6) continue;   // beside its own main line, level with it
                    double gap = c.Top[i] > top ? c.Top[i] - c.Slab - top : top - o.Slab - c.Top[i];
                    if (gap < RampClearance)
                    {
                        Count(o == fromLine || o == toLine ? "ramp: through its own main line" : "ramp: clashes with a deck");
                        return false;
                    }
                }
            }
            // Never steeper than a ramp may be, over any 12 m.
            for (int i = 0, j = 0; i < c.Count; i++)
            {
                while (j < c.Count - 1 && c.S[j] - c.S[i] < 12.0) j++;
                if (c.S[j] - c.S[i] >= 12.0 && Math.Abs(c.Top[j] - c.Top[i]) / (c.S[j] - c.S[i]) > RampGrade + 0.015)
                {
                    Count("ramp: too steep");
                    return false;
                }
            }
            return true;
        }

        /// <summary>Takes the last-added paths off the working grid and the list again (an exit that did not get enough ramps).</summary>
        private void RemoveLast(ViaductPath path)
        {
            long hi = (long)path.Id << 32;
            foreach (List<long> list in _grid.Values)
            {
                list.RemoveAll(r => (r & unchecked((long)0xFFFFFFFF00000000UL)) == hi);
            }
            _out.Paths.RemoveAt(path.Id);
        }

        // ================================================================== system interchanges

        /// <summary>
        /// Where two routes cross: the later passes over the earlier (LayProfiles made it), and a CLOVERLEAF joins them -- for each of the four
        /// ways in, a right turn by an outer connector curving round the quadrant and a left turn by a 270-degree loop inside it (Caltrans HDM
        /// 502.3: every movement served). A connector that cannot be laid without a clash is left out; the crossing stays a clean flyover.
        /// </summary>
        private void SystemInterchanges()
        {
            for (int b = 0; b < _out.Routes.Count; b++)
            {
                for (int a = 0; a < b; a++)
                {
                    ViaductPath la = _out.Paths[_out.Routes[a].Path];
                    ViaductPath lb = _out.Paths[_out.Routes[b].Path];
                    foreach ((double sa, double sb, Vec2 x) in Crossings(la, lb))
                    {
                        var ic = new ExpresswayInterchange { Id = _out.Interchanges.Count, System = true, At = x, RouteA = a, RouteB = b };
                        Cloverleaf(ic, la, sa, lb, sb);
                        _out.Interchanges.Add(ic);
                        Count("system interchanges");
                    }
                }
            }
        }

        /// <summary>Where two main lines' centrelines cross: the distance along each and the point; crossings closer than 100 m are one.</summary>
        private static List<(double sa, double sb, Vec2 x)> Crossings(ViaductPath la, ViaductPath lb)
        {
            var found = new List<(double sa, double sb, Vec2 x)>();
            for (int j = 0; j < lb.Segments; j++)
            {
                Vec2 c = lb.P[j], d = lb.P[j + 1];
                double bx0 = Math.Min(c.X, d.X), bx1 = Math.Max(c.X, d.X), by0 = Math.Min(c.Y, d.Y), by1 = Math.Max(c.Y, d.Y);
                for (int i = 0; i < la.Segments; i++)
                {
                    Vec2 a = la.P[i], b = la.P[i + 1];
                    if (Math.Max(a.X, b.X) < bx0 || Math.Min(a.X, b.X) > bx1 || Math.Max(a.Y, b.Y) < by0 || Math.Min(a.Y, b.Y) > by1) continue;
                    if (!PlanMath.SegmentIntersect(a, b, c, d, out double t, out double u)) continue;
                    Vec2 x = a + (b - a) * t;
                    bool dup = false;
                    foreach (var f in found) dup |= (f.x - x).Length < 100.0;
                    if (!dup) found.Add((la.S[i] + (la.S[i + 1] - la.S[i]) * t, lb.S[j] + (lb.S[j + 1] - lb.S[j]) * u, x));
                }
            }
            return found;
        }

        private void Cloverleaf(ExpresswayInterchange ic, ViaductPath la, double sa, ViaductPath lb, double sb)
        {
            la.At(Wrap(la, sa), out _, out Vec2 ta, out _);
            lb.At(Wrap(lb, sb), out _, out Vec2 tb, out _);
            // The four ways in; for each, its right turn (an outer connector) and its left turn (a loop).
            var ways = new List<(ViaductPath u, double su, int sigma, ViaductPath v, double sv, Vec2 tv)>
            {
                (la, sa, 1, lb, sb, tb), (la, sa, -1, lb, sb, tb), (lb, sb, 1, la, sa, ta), (lb, sb, -1, la, sa, ta),
            };
            double[] loopRadii = { 58.0, 72.0, 88.0 };
            // Each loop by the corner it sits in (the two rays out from the crossing that bound it), for the outer connector that goes round it.
            var loopsByCorner = new List<(Vec2 rayA, Vec2 rayB, ViaductPath loop)>();
            // Loops first (they sit tight in the corners), then the outer connectors round them.
            foreach (bool loop in new[] { true, false })
            {
                foreach (var w in ways)
                {
                    w.u.At(Wrap(w.u, w.su), out _, out Vec2 tu, out _);
                    Vec2 du = tu * w.sigma;
                    int rightSign = Vec2.Cross(du, w.tv) < 0.0 ? 1 : -1;   // the way along v that is a right turn from du
                    Vec2 dvRight = w.tv * rightSign;
                    int targetSign = loop ? -rightSign : rightSign;
                    // The corner each sits in, and its angle: a loop leaves along du and comes back in along v against its traffic (the ray
                    // dvRight); an outer connector comes in along u (the ray -du) and leaves along dvRight. A skew crossing has two sharp
                    // corners and two blunt ones, and a curve only fits a sharp one well out from the crossing.
                    double corner = Math.Abs(DMath.Turn(loop ? du : -du, dvRight));
                    double au = w.u.HalfLeft[0] + _wr, av = w.v.HalfLeft[0] + _wr;
                    double sinCross = Math.Max(0.3, Math.Abs(Vec2.Cross(tu, w.tv)));
                    double edgeU = w.v.HalfLeft[0] / sinCross + _wr + 4.0 * _r, edgeV = w.u.HalfLeft[0] / sinCross + _wr + 4.0 * _r;   // past the other deck
                    double[] radii = loop ? loopRadii : new[] { 150.0, 190.0, 240.0 };
                    ViaductPath made = null;
                    string roundWhy = "no loop in the corner";
                    if (!loop)
                    {
                        // The loop in this corner, if it was laid: then the outer connector swings wide round it, through a point just beyond it.
                        ViaductPath inCorner = null;
                        foreach (var entry in loopsByCorner)
                        {
                            bool same = (Vec2.Dot(entry.rayA, -du) > 0.98 && Vec2.Dot(entry.rayB, dvRight) > 0.98) || (Vec2.Dot(entry.rayB, -du) > 0.98 && Vec2.Dot(entry.rayA, dvRight) > 0.98);
                            if (same) inCorner = entry.loop;
                        }
                        if (inCorner != null)
                        {
                            made = RoundTheLoop(ic, w.u, w.su, w.sigma, du, w.v, w.sv, targetSign, dvRight, inCorner);
                            roundWhy = _lastReason;
                            if (made != null)
                            {
                                made.Interchange = ic.Id;
                                Add(made);
                                OpenParapet(w.u, made, w.su);
                                OpenParapet(w.v, made, w.sv);
                                ic.Paths.Add(made.Id);
                                NearestLocal(w.u, made.P[0], w.su, 2.0 * Window, out double divergeAt, out _);
                                AddSign(w.u, divergeAt - w.sigma * 25.0, w.sigma, ic.Id * 16 + ic.Paths.Count);
                                Count("connector: outer");
                            }
                        }
                    }
                    foreach (double radiusClassic in radii)
                    {
                        if (made != null) break;
                        double r = radiusClassic * _r;
                        // Where the curve, tangent to both main lines' edges, begins on each (a sweep beyond it: a curved route bends away from
                        // its tangent). An outer connector stays beyond the loop that shares its corner, with room to weave between them.
                        double curveU = TangentReach(corner, au, av, r), curveV = TangentReach(corner, av, au, r);
                        if (!loop)
                        {
                            double loopU = TangentReach(corner, au, av, loopRadii[loopRadii.Length - 1] * _r);
                            double loopV = TangentReach(corner, av, au, loopRadii[loopRadii.Length - 1] * _r);
                            curveU = Math.Max(curveU, loopU + Taper + 30.0 * _r);
                            curveV = Math.Max(curveV, loopV + Taper + 30.0 * _r);
                        }
                        foreach (double grow in loop ? new[] { 0.0, 8.0, 18.0, 32.0 } : new[] { 0.0, 30.0, 70.0, 130.0 })
                        {
                            double g = grow * _r;
                            // A loop's taper runs out from the crossing and its curve begins where the taper ends; an outer connector's taper runs
                            // in toward the crossing, its curve beginning at the taper's far end -- so the distances are measured to where each
                            // taper meets its curve.
                            made = loop
                                ? Connector(w.u, w.su + w.sigma * Math.Max(edgeU, curveU + g - Taper), w.sigma,
                                    w.v, w.sv - targetSign * Math.Max(edgeV, curveV + g - Taper), targetSign, r, "loop", 1.85 * DMath.Pi)
                                : Connector(w.u, w.su - w.sigma * (curveU + g + Taper), w.sigma,
                                    w.v, w.sv + targetSign * (curveV + g + Taper), targetSign, r, "outer", 0.95 * DMath.Pi);
                            if (made == null) continue;
                            made.Interchange = ic.Id;
                            Add(made);
                            OpenParapet(w.u, made, w.su);
                            OpenParapet(w.v, made, w.sv);
                            ic.Paths.Add(made.Id);
                            double divergeAt = loop ? w.su + w.sigma * Math.Max(edgeU, curveU + g - Taper) : w.su - w.sigma * (curveU + g + Taper);
                            AddSign(w.u, divergeAt - w.sigma * 25.0, w.sigma, ic.Id * 16 + ic.Paths.Count);
                            Count(loop ? "connector: loop" : "connector: outer");
                            if (loop) loopsByCorner.Add((du, dvRight, made));
                            break;
                        }
                        if (made != null) break;
                    }
                    if (made == null)
                    {
                        string why = _lastReason;
                        Count(loop ? "connector: loop lost" : "connector: outer lost");
                        if (Environment.GetEnvironmentVariable("CITYX_XW_DEBUG") == "1")
                        {
                            Console.WriteLine($"    {(loop ? "loop" : "outer")} lost at ({ic.At.X:F0},{ic.At.Y:F0}) from route {w.u.Route} sigma {w.sigma}: corner {corner * 57.2958:F0} deg, last: {why}" +
                                (loop ? "" : $"; round the loop: {roundWhy}"));
                        }
                    }
                }
            }
            // The land inside the interchange is the interchange's: no lot under or between its curves.
            var hull = new List<Vec2>();
            foreach (int id in ic.Paths)
            {
                ViaductPath c = _out.Paths[id];
                for (int i = 0; i < c.Count; i += 3) hull.Add(c.P[i]);
            }
            if (hull.Count >= 3) _out.Infields.Add(PlanMath.ConvexHull(hull));
        }

        /// <summary>
        /// An outer connector round the loop in its corner, as a real cloverleaf's is: it leaves its main line beyond the loop's reach, swings
        /// out through a point just past the loop's far side, and comes in to the other main line beyond the loop's reach there -- the loop
        /// wholly inside the land it encloses. (A single curve tangent to the same two edges as the loop can only clear it with a radius several
        /// times the loop's -- six times at a square crossing, fourteen at a blunt corner -- so it would take a square kilometre.)
        /// </summary>
        private ViaductPath RoundTheLoop(ExpresswayInterchange ic, ViaductPath u, double su, int sigma, Vec2 du, ViaductPath v, double sv, int targetSign,
            Vec2 dvRight, ViaductPath loop)
        {
            Vec2 x = ic.At;
            // The loop's centre and size, and how far it reaches out along each main line from the crossing.
            Vec2 centre = new Vec2(0.0, 0.0);
            foreach (Vec2 p in loop.P) centre += p;
            centre /= loop.Count;
            double radius = 0.0, alongU = 0.0, alongV = 0.0;
            foreach (Vec2 p in loop.P)
            {
                radius = Math.Max(radius, (p - centre).Length);
                alongU = Math.Max(alongU, Vec2.Dot(p - x, -du));
                alongV = Math.Max(alongV, Vec2.Dot(p - x, dvRight));
            }
            Vec2 outward = (dvRight - du).Normalized;   // into the corner, away from the crossing
            Vec2 across = (du + dvRight).Normalized;     // the way the connector runs past the loop: from u's side to v's
            Vec2 intoU = -du.Perp;                       // u's right: into the corner from u's side
            Vec2 intoV = dvRight.Perp;                   // ... and from v's side (left of v's outward ray)
            if (Vec2.Dot(intoV, outward) < 0.0) intoV = -intoV;
            double au = u.HalfLeft[0] + _wr, av = v.HalfLeft[0] + _wr;
            foreach (double gap in new[] { 12.0, 24.0, 40.0 })
            {
                Vec2 viaPoint = centre + outward * (radius + 2.0 * _wr + gap * _r);
                var via = new Pose(viaPoint, across);
                foreach (double legRadius in new[] { 120.0, 170.0 })
                {
                    double leg = legRadius * _r;
                    // How far out along each main line the connector must leave it to reach the waypoint gently: the waypoint lies "drop" off the
                    // line's edge, and a turn of theta covers drop / tan(theta) going forward, plus room for the two bends of the S.
                    double dropU = Vec2.Dot(viaPoint - x, intoU) - au, dropV = Vec2.Dot(viaPoint - x, intoV) - av;
                    double thetaU = Math.Abs(DMath.Turn(du, across)), thetaV = Math.Abs(DMath.Turn(across, dvRight));
                    if (dropU <= 0.0 || dropV <= 0.0 || thetaU < 0.15 || thetaV < 0.15) continue;
                    double forwardU = dropU / Math.Max(0.15, DMath.Sin(thetaU) / DMath.Cos(thetaU)) + 2.0 * leg * DMath.Sin(thetaU * 0.5) / DMath.Cos(thetaU * 0.5);
                    double forwardV = dropV / Math.Max(0.15, DMath.Sin(thetaV) / DMath.Cos(thetaV)) + 2.0 * leg * DMath.Sin(thetaV * 0.5) / DMath.Cos(thetaV * 0.5);
                    double viaU = Vec2.Dot(viaPoint - x, -du), viaV = Vec2.Dot(viaPoint - x, dvRight);
                    foreach (double margin in new[] { 15.0, 60.0 })
                    {
                        double startU = Math.Max(viaU + forwardU, alongU) + margin * _r;
                        double startV = Math.Max(viaV + forwardV, alongV) + margin * _r;
                        ViaductPath made = Connector(u, su - sigma * (startU + Taper), sigma, v, sv + targetSign * (startV + Taper), targetSign,
                            leg, "outer", 0.95 * DMath.Pi, via);
                        if (made != null) return made;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Where a curve of radius <paramref name="r"/> sitting in a corner of angle <paramref name="corner"/> between two main lines touches one
        /// of them: its distance along that line from the crossing, the curve's centreline kept <paramref name="aSelf"/> off that line and
        /// <paramref name="aOther"/> off the other (each main line's half width and the connector's).
        /// </summary>
        private static double TangentReach(double corner, double aSelf, double aOther, double r)
        {
            double sin = Math.Max(0.2, DMath.Sin(corner)), cos = DMath.Cos(corner);
            return ((aOther + r) + (aSelf + r) * cos) / sin;
        }

        /// <summary>
        /// A connector from one main line to another: a taper beside the first (leaving at <paramref name="sFrom"/> in the direction of travel), a
        /// Dubins curve turning only right (a loop, or an outer curve), a taper beside the second ending at <paramref name="sTo"/>. Heights: level
        /// with each main line until it has pulled clear of it, climbing or falling between. Null if it cannot be laid.
        /// </summary>
        private ViaductPath Connector(ViaductPath from, double sFrom, int sigmaFrom, ViaductPath to, double sTo, int sigmaTo, double radius, string shape, double maxTurn,
            Pose? via = null)
        {
            if (!OnLine(from, sFrom, 5.0) || !OnLine(from, sFrom + sigmaFrom * Taper, 5.0) || !OnLine(to, sTo, 5.0) || !OnLine(to, sTo - sigmaTo * Taper, 5.0))
            {
                Count("connector: off the end of a line");
                return null;
            }
            var samples = new List<Sample>();
            TaperSamples(from, sFrom, sigmaFrom, Taper, samples, +1);
            int taperEnd = samples.Count - 1;
            var merge = new List<Sample>();
            TaperSamples(to, sTo - sigmaTo * Taper, sigmaTo, Taper, merge, -1);
            var start = new Pose(samples[taperEnd].P, samples[taperEnd].Dir);
            var end = new Pose(merge[0].P, merge[0].Dir);
            if (via.HasValue)
            {
                // Two curves through the waypoint: each may wind either way (an S to swing out, the turn round the loop), neither by much.
                DubinsPath first = DubinsPath.Shortest(start, via.Value, radius, 0.8 * DMath.Pi);
                DubinsPath second = first == null ? null : DubinsPath.Shortest(via.Value, end, radius, 0.8 * DMath.Pi);
                if (first == null || second == null)
                {
                    Count("connector: no curve round the loop");
                    return null;
                }
                double riseVia = Math.Abs(merge[0].Top - samples[taperEnd].Top);
                double lengthVia = first.Length + second.Length;
                if (riseVia / Math.Max(1.0, lengthVia * (1.0 - CurveShare(lengthVia))) > RampGrade)
                {
                    Count("connector: too steep");
                    return null;
                }
                DubinsSamples(first, samples);
                samples.Add(new Sample { P = via.Value.P, Dir = via.Value.Dir, Top = double.NaN });
                DubinsSamples(second, samples);
                return Finish(samples, taperEnd, merge, from, sFrom, to, sTo, shape);
            }
            DubinsPath curve = DubinsPath.Shortest(start, end, radius, maxTurn, DubinsPath.Word.RSR);
            if (curve == null)
            {
                DubinsPath any = DubinsPath.Solve(new Pose(samples[taperEnd].P, samples[taperEnd].Dir), new Pose(merge[0].P, merge[0].Dir), radius, DubinsPath.Word.RSR);
                Count(any == null ? "connector: no curve" : $"connector: turns too far ({any.TotalTurn / DMath.Pi:F2} pi)");
                if (Environment.GetEnvironmentVariable("CITYX_XW_DEBUG") == "1")
                {
                    Console.WriteLine($"    {shape} from ({samples[taperEnd].P.X:F0},{samples[taperEnd].P.Y:F0}) dir ({samples[taperEnd].Dir.X:F2},{samples[taperEnd].Dir.Y:F2}) to ({merge[0].P.X:F0},{merge[0].P.Y:F0}) dir ({merge[0].Dir.X:F2},{merge[0].Dir.Y:F2}) r {radius:F0}; sFrom {sFrom:F0} sTo {sTo:F0} sigma {sigmaFrom}/{sigmaTo}");
                }
                return null;
            }
            double rise = Math.Abs(merge[0].Top - samples[taperEnd].Top);
            if (rise / Math.Max(1.0, curve.Length * (1.0 - CurveShare(curve.Length))) > RampGrade)
            {
                Count("connector: too steep");
                return null;
            }
            DubinsSamples(curve, samples);
            return Finish(samples, taperEnd, merge, from, sFrom, to, sTo, shape);
        }

        /// <summary>A connector's samples from the end of its first taper on: the merge taper added, the heights (level with each main line to its nose, the slope between), checked.</summary>
        private ViaductPath Finish(List<Sample> samples, int taperEnd, List<Sample> merge, ViaductPath from, double sFrom, ViaductPath to, double sTo, string shape)
        {
            int curveEnd = samples.Count;
            samples.AddRange(merge);
            int noseOut = Nose(samples, from, sFrom, taperEnd, curveEnd - 1, GoreWidth);
            int noseIn = Nose(samples, to, sTo, curveEnd, taperEnd, GoreWidth);
            if (noseIn <= noseOut)
            {
                Count("connector: no room between the gores");
                return null;
            }
            for (int i = taperEnd + 1; i < noseOut; i++) Follow(samples, i, from, sFrom);
            for (int i = noseIn + 1; i < curveEnd; i++) Follow(samples, i, to, sTo);
            if (!Slope(samples, noseOut - 1, noseIn + 1, samples[noseOut - 1].Top, samples[noseIn + 1].Top, RampGrade))
            {
                Count("connector: too steep");
                return null;
            }
            if (!LiftOverRoads(samples, noseOut - 1, noseIn + 1, -1, 0.0, false, from, to))
            {
                Count("connector: cannot rise over a road");
                return null;
            }
            ViaductPath path = Assemble(samples, ViaductKind.Connector, shape, from, sFrom, to, sTo);
            if (path == null) return null;
            path.Route = from.Route;
            return Clear(path, from, to, -1, 0.0) ? path : null;
        }

        private void AddSign(ViaductPath line, double s, int sigma, int seed)
        {
            if (!OnLine(line, s, 2.0)) return;
            MainPose(line, s, sigma, out Vec2 p, out _, out Vec2 right, out double top);
            _out.Signs.Add(new ExpresswaySign
            {
                P = p,
                Across = right,
                Top = top,
                HalfWidth = line.HalfLeft[0],
                Offset = line.HalfLeft[0] * 0.5,
                Seed = seed,
            });
        }

        // ================================================================== exits

        private struct ExitCandidate
        {
            public int Route;
            public double S;
            public Vec2 X;
            public RoadEdge Street;
            public int Rank;
            public double Order;
        }

        /// <summary>
        /// The exits: where a road passes under a route at a good angle in built-up country, best roads first (a boulevard, then an arterial, then
        /// a street), no two closer along a route than <see cref="ExitSpacing"/> and none near a system interchange. Each gets its ramps
        /// (<see cref="BuildExit"/>); one that gets fewer than two is undone.
        /// </summary>
        private void Exits()
        {
            var candidates = new List<ExitCandidate>();
            foreach (ExpresswayRoute route in _out.Routes)
            {
                ViaductPath m = _out.Paths[route.Path];
                for (int i = 0; i < m.Segments; i++)
                {
                    _net.EdgesNear(m.P[i], m.P[i + 1], 0.01, _edgeScratch);
                    foreach (RoadEdge e in _edgeScratch)
                    {
                        if (e.Bridge) continue;
                        if (!PlanMath.SegmentIntersect(m.P[i], m.P[i + 1], e.A.P, e.B.P, out double t, out _)) continue;
                        Vec2 x = m.P[i] + (m.P[i + 1] - m.P[i]) * t;
                        double cos = Math.Abs(Vec2.Dot((e.B.P - e.A.P).Normalized, m.SegmentDir(i)));
                        if (cos > 0.75) continue;   // under 40 degrees: too oblique for a ramp to meet it square
                        var crossing = new ExitCandidate
                        {
                            Route = route.Id,
                            S = m.S[i] + (m.S[i + 1] - m.S[i]) * t,
                            X = x,
                            Street = e,
                            Rank = e.Class == RoadClass.Boulevard ? 0 : (e.Class == RoadClass.Arterial ? 1 : 2),
                            Order = Rand(0x65786974U, e.Id * 31 + route.Id),
                        };
                        if (!_crossings.TryGetValue(route.Id, out List<ExitCandidate> list)) _crossings[route.Id] = list = new List<ExitCandidate>();
                        list.Add(crossing);
                        if (cos > 0.64) continue;   // an exit's own street: at least 50 degrees
                        if (_fields.Population(x) < ExitPopulation) continue;
                        candidates.Add(crossing);
                    }
                }
            }
            candidates.Sort((p, q) =>
            {
                int c = p.Rank.CompareTo(q.Rank);
                if (c != 0) return c;
                c = p.Order.CompareTo(q.Order);
                return c != 0 ? c : p.Street.Id.CompareTo(q.Street.Id);
            });

            var accepted = new List<ExitCandidate>();
            foreach (ExitCandidate cand in candidates)
            {
                ViaductPath m = _out.Paths[_out.Routes[cand.Route].Path];
                bool ok = true;
                foreach (ExitCandidate other in accepted)
                {
                    double apart = (other.X - cand.X).Length;
                    if (other.Route == cand.Route)
                    {
                        double along = Math.Abs(other.S - cand.S);
                        if (m.Closed) along = Math.Min(along, m.Length - along);
                        apart = Math.Min(apart, along);
                    }
                    if (apart < ExitSpacing * _r * (other.Route == cand.Route ? 1.0 : 0.55)) ok = false;
                }
                foreach (ExpresswayInterchange ic in _out.Interchanges)
                {
                    if (ic.System && (ic.At - cand.X).Length < SystemSpacing * _r) ok = false;
                }
                if (!ok)
                {
                    Count("exit: too close");
                    continue;
                }
                if (BuildExit(m, cand))
                {
                    accepted.Add(cand);
                }
            }
        }

        /// <summary>
        /// The ramps of one exit: for each direction of travel an off-ramp before the cross street and an on-ramp after it, all meeting that one
        /// street. Shapes, chosen by a hash and falling back in turn: a DIAMOND (down across the corner of land to meet the street square), a
        /// SLIP (down onto a street running alongside the expressway, the way ramps feed a frontage road), or LOOPS (a partial cloverleaf: the
        /// off-ramp leaves after the street and turns right about 250 degrees round the corner beyond it to run back along the street, merging into
        /// it; the on-ramp peels off the lanes heading away from the expressway and loops up into it before the street).
        /// </summary>
        private bool BuildExit(ViaductPath m, ExitCandidate cand)
        {
            var ic = new ExpresswayInterchange { Id = _out.Interchanges.Count, System = false, At = cand.X, RouteA = cand.Route, StreetEdge = cand.Street.Id };
            var built = new List<(ViaductPath ramp, int sigma, int cuts)>();
            int[] directions = Rand(0x7369676EU, cand.Street.Id) < 0.5 ? new[] { 1, -1 } : new[] { -1, 1 };
            foreach (int sigma in directions)
            {
                double pick = Rand(0x73686170U, cand.Street.Id * 2 + (sigma > 0 ? 1 : 0));
                string[] order = pick < 0.45 ? new[] { "diamond", "slip", "loop" } : (pick < 0.62 ? new[] { "slip", "loop", "diamond" } : new[] { "loop", "diamond", "slip" });
                foreach (bool leaving in new[] { true, false })
                {
                    ViaductPath ramp = null;
                    int cutsBefore = _out.KerbCuts.Count;
                    // The exit's own street first, in every shape; then, as a SPLIT diamond does in a tight grid (Caltrans L-3), the next streets
                    // that pass under the expressway before it (an off-ramp) or after it (an on-ramp), nearest first, meeting them square.
                    foreach (ExitCandidate landing in Landings(m, cand, sigma, leaving))
                    {
                        foreach (string shape in landing.Street == cand.Street ? order : new[] { "diamond" })
                        {
                            _tallyPrefix = "[" + shape + "] ";
                            ramp = Ramp(m, landing, sigma, leaving, shape);
                            _tallyPrefix = null;
                            if (ramp != null) break;
                        }
                        if (ramp != null) break;
                    }
                    if (ramp == null)
                    {
                        Count(leaving ? "exit: off-ramp lost" : "exit: on-ramp lost");
                        continue;
                    }
                    ramp.Interchange = ic.Id;
                    ramp.Leaving = leaving;
                    Add(ramp);
                    built.Add((ramp, sigma, _out.KerbCuts.Count - cutsBefore));
                    Count("ramp: " + ramp.Shape);
                }
            }
            if (built.Count < 2)
            {
                for (int i = built.Count - 1; i >= 0; i--)
                {
                    RemoveLast(built[i].ramp);
                    _out.KerbCuts.RemoveRange(_out.KerbCuts.Count - built[i].cuts, built[i].cuts);
                }
                Count("exit: undone");
                return false;
            }
            foreach ((ViaductPath ramp, int sigma, int _) in built)
            {
                OpenParapet(m, ramp, cand.S);
                ic.Paths.Add(ramp.Id);
                if (ramp.Leaving)
                {
                    NearestLocal(m, ramp.P[0], cand.S, Window, out double s, out _);
                    AddSign(m, s - sigma * 25.0, sigma, ic.Id * 16 + ic.Paths.Count);
                }
                _out.Infields.Add(Infield(m, ramp, cand));
            }
            _out.Interchanges.Add(ic);
            Count("exits");
            return true;
        }

        /// <summary>Every road crossing under each route at 40 degrees or more, by route: the streets an exit's ramps may meet.</summary>
        private readonly Dictionary<int, List<ExitCandidate>> _crossings = new Dictionary<int, List<ExitCandidate>>();

        /// <summary>Most a split diamond's ramp may land from its exit's own street, along the expressway.</summary>
        public const double SplitReach = 450.0;

        /// <summary>The streets one ramp of an exit may meet: the exit's own, then those crossing under the route on the ramp's side of it, nearest first.</summary>
        private List<ExitCandidate> Landings(ViaductPath m, ExitCandidate cand, int sigma, bool leaving)
        {
            var result = new List<ExitCandidate> { cand };
            if (!_crossings.TryGetValue(cand.Route, out List<ExitCandidate> all)) return result;
            var others = new List<(double distance, ExitCandidate c)>();
            foreach (ExitCandidate c in all)
            {
                if (c.Street == cand.Street) continue;
                double ds = c.S - cand.S;
                if (m.Closed) ds -= m.Length * Math.Round(ds / m.Length);
                double ahead = sigma * ds;   // how far after the exit's street, in the direction of travel
                if (leaving ? (ahead > -20.0 || ahead < -SplitReach * _r) : (ahead < 20.0 || ahead > SplitReach * _r)) continue;
                others.Add((Math.Abs(ds), c));
            }
            others.Sort((p, q) =>
            {
                int k = p.distance.CompareTo(q.distance);
                return k != 0 ? k : p.c.Street.Id.CompareTo(q.c.Street.Id);
            });
            foreach (var o in others) result.Add(o.c);
            return result;
        }

        /// <summary>The land a ramp cuts off against its main line and its street: kept clear of lots (the hull of the ramp and the stretch of main line beside it).</summary>
        private Vec2[] Infield(ViaductPath m, ViaductPath ramp, ExitCandidate cand)
        {
            var points = new List<Vec2>();
            for (int i = 0; i < ramp.Count; i += 2) points.Add(ramp.P[i]);
            NearestLocal(m, ramp.P[0], cand.S, Window, out double s0, out _);
            NearestLocal(m, ramp.P[ramp.Count - 1], cand.S, Window, out double s1, out _);
            if (m.Closed)
            {
                // The short way round a ring: across its start, s1 - s0 is nearly the whole ring the other way (and the hull was all of it).
                double length = m.Length;
                double ds = s1 - s0;
                ds -= length * Math.Round(ds / length);
                s1 = s0 + ds;
            }
            for (int k = 0; k <= 8; k++)
            {
                m.At(Wrap(m, s0 + (s1 - s0) * k / 8.0), out Vec2 p, out _, out _);
                points.Add(p);
            }
            return PlanMath.ConvexHull(points);
        }

        /// <summary>A point on a street at a distance from where it passes under the expressway, toward one side, walking on along the street through a straight junction if it must. Keeps clear of junctions.</summary>
        private bool StreetPoint(RoadEdge street, Vec2 x, Vec2 side, double distance, out RoadEdge edge, out Vec2 point, out Vec2 along)
        {
            edge = street;
            RoadNode far = Vec2.Dot(street.A.P - x, side) > 0.0 ? street.A : street.B;
            Vec2 start = x;
            double left = distance;
            double clearEnd = FootClearOfJunction * _r;
            for (int guard = 0; guard < 3; guard++)
            {
                double length = (far.P - start).Length;
                along = (far.P - start).Normalized;
                if (left <= length - clearEnd)
                {
                    point = start + along * left;
                    if (edge != street && (point - start).Length < clearEnd) break;   // too near the junction it walked through
                    return true;
                }
                RoadEdge next = null;
                foreach (RoadEdge e in far.Edges)
                {
                    if (e == edge || !e.Alive || e.Bridge) continue;
                    if (Vec2.Dot((e.Other(far).P - far.P).Normalized, along) > 0.94) next = e;
                }
                if (next == null) break;
                left -= length;
                start = far.P;
                edge = next;
                far = next.Other(far);
            }
            point = default;
            along = default;
            return false;
        }

        /// <summary>
        /// One ramp of an exit, of one shape, for one direction of travel; laid in the order traffic drives it (an off-ramp from the expressway
        /// to the street, an on-ramp from the street to the expressway). Null if that shape cannot be laid here.
        /// </summary>
        private ViaductPath Ramp(ViaductPath m, ExitCandidate cand, int sigma, bool leaving, string shape)
        {
            MainPose(m, cand.S, sigma, out _, out Vec2 d, out Vec2 right, out _);
            var feet = new List<(RoadEdge edge, Vec2 kerb, Vec2 inward)>();
            if (shape == "slip")
            {
                if (ParallelStreet(m, cand, sigma, leaving, out RoadEdge edge, out Vec2 kerb, out Vec2 inward)) feet.Add((edge, kerb, inward));
            }
            else if (shape == "loop")
            {
                // A loop serves a LEFT turn by turning right three quarters of a circle, as a cloverleaf's do: an off-ramp in the corner beyond the
                // street, coming round to run back along it toward the expressway and merging into the lanes on its own side; an on-ramp in the
                // corner before the street, peeling off the lanes heading away from the expressway. So it meets the street running along it, at
                // 20 degrees, a loop's width out from the expressway.
                double[] distances = { 85.0, 110.0, 140.0 };
                double c = DMath.Cos(0.35), sn = DMath.Sin(0.35);
                foreach (double distance in distances)
                {
                    if (!StreetPoint(cand.Street, cand.X, right, m.HalfLeft[0] + distance * _r, out RoadEdge edge, out Vec2 centre, out Vec2 along)) continue;
                    Vec2 toCorner = along.Perp;
                    if (Vec2.Dot(toCorner, leaving ? d : -d) < 0.0) toCorner = -toCorner;
                    Vec2 travel = (leaving ? -(along * c) - toCorner * sn : along * c + toCorner * sn).Normalized;   // the way its traffic goes at the kerb
                    feet.Add((edge, centre + toCorner * CityPlan.HalfWidth(_cfg, edge.Class), leaving ? travel : -travel));
                }
            }
            else
            {
                double[] distances = { 34.0, 58.0, 86.0 };
                int first = (int)(Rand(0x666F6F74U, cand.Street.Id * 4 + (sigma > 0 ? 2 : 0) + (leaving ? 1 : 0)) * distances.Length) % distances.Length;
                for (int k = 0; k < distances.Length; k++)
                {
                    double df = m.HalfLeft[0] + distances[(first + k) % distances.Length] * _r;
                    if (!StreetPoint(cand.Street, cand.X, right, df, out RoadEdge edge, out Vec2 centre, out Vec2 along)) continue;
                    // A diamond's off-ramp comes onto the street square from the corner BEFORE it (where its traffic came from), its on-ramp leaves
                    // into the corner AFTER it.
                    Vec2 toCorner = along.Perp;
                    if (Vec2.Dot(toCorner, leaving ? -d : d) < 0.0) toCorner = -toCorner;
                    feet.Add((edge, centre + toCorner * CityPlan.HalfWidth(_cfg, edge.Class), -toCorner));
                }
            }
            if (feet.Count == 0)
            {
                Count(shape == "slip" ? "slip: no street alongside" : "ramp: no room on the street");
                return null;
            }

            foreach ((RoadEdge footEdge, Vec2 kerb, Vec2 inward) in feet)
            {
                if (_net.AnyNodeNear(kerb, FootClearOfJunction * _r * 0.8))
                {
                    Count("ramp: foot on a junction");
                    continue;
                }
                // The landing: the last stretch, straight across the pavement to the kerb, on the street's level.
                double landingLength = Landing * _r + _cfg.SidewalkWidth;
                Vec2 landing = kerb - inward * landingLength;
                double footTop = Ground(kerb) + RoadLift;
                double[] reaches = { 210.0, 270.0, 340.0, 430.0 };
                double[] radii = shape == "loop" ? new[] { 55.0, 70.0 } : new[] { 70.0, 120.0 };
                foreach (double reach in reaches)
                {
                    foreach (double radius in radii)
                    {
                        ViaductPath ramp = LayRamp(m, cand, sigma, leaving, shape, reach * _r, radius * _r, kerb, inward, landing, footTop, footEdge);
                        if (ramp == null) continue;
                        _out.KerbCuts.Add(new KerbCut { Poly = KerbCutPoly(kerb, inward, landingLength), StreetEdge = footEdge.Id });
                        ramp.StreetEdge = footEdge.Id;
                        return ramp;
                    }
                }
            }
            return null;
        }

        /// <summary>The pavement cut away under a ramp's landing: from just into the street back past the kerb, a little wider than the ramp.</summary>
        private Vec2[] KerbCutPoly(Vec2 kerb, Vec2 inward, double landingLength)
        {
            Vec2 across = inward.Perp * (_wr + 0.2);
            Vec2 a = kerb + inward * 0.3, b = kerb - inward * (landingLength + 0.5);
            return new[] { a + across, b + across, b - across, a - across };
        }

        /// <summary>
        /// Lays the ramp itself for one reach along the expressway and one curve radius: the taper beside the main line, a Dubins curve, the
        /// landing; level with the main line to the nose, then the slope to the street; checked against everything. Null if anything fails.
        /// </summary>
        private ViaductPath LayRamp(ViaductPath m, ExitCandidate cand, int sigma, bool leaving, string shape, double reach, double radius,
            Vec2 kerb, Vec2 inward, Vec2 landing, double footTop, RoadEdge footEdge)
        {
            // Where the taper is: an off-ramp leaves before the street (a loop's after it); an on-ramp joins after it (a loop's before it).
            bool loop = shape == "loop";
            double clearOfStreet = CityPlan.HalfWidth(_cfg, cand.Street.Class) + _cfg.SidewalkWidth + _wr + 6.0 * _r;
            double s0, s1;   // the taper runs from s0 to s1 in the direction of travel
            if (leaving)
            {
                s0 = loop ? cand.S + sigma * clearOfStreet : cand.S - sigma * reach;
                s1 = s0 + sigma * Taper;
            }
            else
            {
                s1 = loop ? cand.S - sigma * clearOfStreet : cand.S + sigma * reach;
                s0 = s1 - sigma * Taper;
            }
            if (loop && reach > 211.0 * _r) return null;   // a loop's taper is pinned beside the street: only the first reach means anything
            if (!OnLine(m, s0, 5.0) || !OnLine(m, s1, 5.0))
            {
                Count("ramp: off the end of its line");
                return null;
            }
            // Not in among a cloverleaf's connectors: an exit's ramps leave and join the main line well clear of a system interchange.
            m.At(Wrap(m, s0), out Vec2 taperFrom, out _, out _);
            m.At(Wrap(m, s1), out Vec2 taperTo, out _, out _);
            foreach (ExpresswayInterchange ic in _out.Interchanges)
            {
                if (!ic.System) continue;
                double keep = 0.35 * SystemSpacing * _r;
                if ((ic.At - taperFrom).Length < keep || (ic.At - taperTo).Length < keep)
                {
                    Count("ramp: in a system interchange");
                    return null;
                }
            }
            var taper = new List<Sample>();
            TaperSamples(m, s0, sigma, Taper, taper, leaving ? +1 : -1);
            double maxTurn = loop ? 1.85 * DMath.Pi : 1.05 * DMath.Pi;
            DubinsPath.Word[] words = loop ? new[] { DubinsPath.Word.RSR } : null;
            Pose high = leaving ? new Pose(taper[taper.Count - 1].P, taper[taper.Count - 1].Dir) : new Pose(taper[0].P, taper[0].Dir);
            DubinsPath curve = leaving
                ? DubinsPath.Shortest(high, new Pose(landing, inward), radius, maxTurn, words)
                : DubinsPath.Shortest(new Pose(landing, -inward), high, radius, maxTurn, words);
            if (curve == null)
            {
                Count("ramp: no curve to the street");
                return null;
            }
            // Long enough to come down at the ramp's grade at all (checked before anything is sampled: most tries fail here).
            double drop = Math.Abs((leaving ? taper[taper.Count - 1].Top : taper[0].Top) - footTop);
            if (drop / Math.Max(1.0, curve.Length * (1.0 - CurveShare(curve.Length))) > RampGrade)
            {
                Count("ramp: too short for its drop");
                return null;
            }

            var samples = new List<Sample>();
            int highEnd, lowEnd;
            double footZone = Landing * _r + _cfg.SidewalkWidth + CityPlan.HalfWidth(_cfg, footEdge.Class) + 6.0;
            if (leaving)
            {
                samples.AddRange(taper);
                int taperEnd = samples.Count - 1;
                DubinsSamples(curve, samples);
                lowEnd = samples.Count;
                LineSamples(landing, kerb, samples, true);
                highEnd = Nose(samples, m, cand.S, taperEnd, lowEnd - 1, GoreWidth);
                for (int i = taperEnd + 1; i < highEnd; i++) Follow(samples, i, m, cand.S);
                for (int i = lowEnd; i < samples.Count; i++) SetTop(samples, i, footTop);
                if (!Slope(samples, highEnd - 1, lowEnd, samples[highEnd - 1].Top, footTop, RampGrade))
                {
                    Count("ramp: too steep");
                    return null;
                }
                if (!LiftOverRoads(samples, highEnd - 1, lowEnd, footEdge.Id, footZone, true, m, null))
                {
                    Count("ramp: cannot rise over a road");
                    return null;
                }
            }
            else
            {
                LineSamples(kerb, landing, samples, true);
                int landingEnd = samples.Count - 1;
                DubinsSamples(curve, samples);
                int taperStart = samples.Count;
                samples.AddRange(taper);
                for (int i = 0; i <= landingEnd; i++) SetTop(samples, i, footTop);
                lowEnd = landingEnd;
                highEnd = Nose(samples, m, cand.S, taperStart, landingEnd, GoreWidth);
                for (int i = highEnd + 1; i < taperStart; i++) Follow(samples, i, m, cand.S);
                if (!Slope(samples, lowEnd, highEnd + 1, footTop, samples[highEnd + 1].Top, RampGrade))
                {
                    Count("ramp: too steep");
                    return null;
                }
                if (!LiftOverRoads(samples, lowEnd, highEnd + 1, footEdge.Id, footZone, false, m, null))
                {
                    Count("ramp: cannot rise over a road");
                    return null;
                }
            }
            ViaductPath ramp = Assemble(samples, ViaductKind.Ramp, shape, leaving ? m : null, cand.S, leaving ? null : m, cand.S);
            if (ramp == null) return null;
            ramp.Route = m.Route;
            ramp.StreetAtEnd = leaving;
            return Clear(ramp, leaving ? m : null, leaving ? null : m, footEdge.Id, footZone) ? ramp : null;
        }

        /// <summary>
        /// For a slip ramp: a street running alongside the expressway on the ramp's side, a little way off (the way a frontage road does), clear of
        /// junctions where the ramp meets it. An off-ramp comes onto its near kerb at 20 degrees going the way the traffic goes; an on-ramp leaves
        /// it the same way.
        /// </summary>
        private bool ParallelStreet(ViaductPath m, ExitCandidate cand, int sigma, bool leaving, out RoadEdge edge, out Vec2 kerb, out Vec2 inward)
        {
            edge = null;
            kerb = default;
            inward = default;
            MainPose(m, cand.S, sigma, out Vec2 x, out Vec2 d, out Vec2 right, out _);
            double sign = leaving ? -1.0 : 1.0;   // before the crossing for an off-ramp, after it for an on-ramp
            Vec2 probe = x + right * (m.HalfLeft[0] + 45.0 * _r) + d * (sign * 90.0 * _r);
            _net.EdgesNear(probe, probe, 75.0 * _r, _edgeScratch);
            double best = double.MaxValue;
            foreach (RoadEdge e in _edgeScratch)
            {
                if (e == cand.Street || e.Bridge) continue;
                Vec2 ed = (e.B.P - e.A.P).Normalized;
                if (Math.Abs(Vec2.Dot(ed, d)) < 0.94) continue;
                double eh = CityPlan.HalfWidth(_cfg, e.Class);
                double lateral = Vec2.Dot((e.A.P + e.B.P) * 0.5 - x, right);
                if (lateral < m.HalfLeft[0] + eh + _cfg.SidewalkWidth + 14.0 * _r || lateral > m.HalfLeft[0] + 95.0 * _r) continue;
                Vec2 target = x + d * (sign * 60.0 * _r);
                PlanMath.DistanceToSegment(target, e.A.P, e.B.P, out double t);
                Vec2 centre = e.A.P + (e.B.P - e.A.P) * t;
                if ((centre - e.A.P).Length < FootClearOfJunction * _r || (centre - e.B.P).Length < FootClearOfJunction * _r) continue;
                double score = Math.Abs(lateral - (m.HalfLeft[0] + 40.0 * _r));
                if (score >= best) continue;
                best = score;
                edge = e;
                kerb = centre - right * eh;   // the kerb on the expressway's side
                double c = DMath.Cos(0.35), s = DMath.Sin(0.35);
                // Across the kerb into the street: an off-ramp heads on and to the right, into it; an on-ramp leaves it heading on and to the left,
                // so the way into the street is the reverse of that.
                inward = leaving ? (d * c + right * s).Normalized : -(d * c - right * s).Normalized;
            }
            return edge != null;
        }
    }
}
