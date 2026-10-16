using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>What a deck is: an expressway's main carriageway, a connector between two expressways, or a ramp to a street.</summary>
    public enum ViaductKind
    {
        Mainline = 0,
        Connector = 1,
        Ramp = 2,
    }

    /// <summary>
    /// One running surface of the expressway system -- a main line, a connector or a ramp -- as a centreline sampled every few metres, with
    /// the top of the carriageway at every sample and its width each side. Every kind is drawn by the same code (CitySurfaceBuilder), so a
    /// ramp curves and slopes exactly as smoothly as its samples do.
    ///
    /// Between two samples the surface is two triangles (left-front, right-front, right-back and left-front, right-back, left-back), each a
    /// plane through its corners' heights: a curved deck's pieces then share their edges exactly, with no step at a joint (<see cref="TopAt"/>).
    /// </summary>
    public sealed class ViaductPath
    {
        public int Id;
        public ViaductKind Kind;

        /// <summary>The main line's own route; for a connector or a ramp, the route it leaves or joins at its high end.</summary>
        public int Route = -1;

        /// <summary>The interchange it belongs to (-1 for a main line).</summary>
        public int Interchange = -1;

        public Vec2[] P;
        /// <summary>The unit normal to the LEFT of the direction of the samples (Vec2.Perp of the tangent).</summary>
        public Vec2[] N;
        public double[] S;
        public double[] Top;
        public double[] HalfLeft;
        public double[] HalfRight;

        /// <summary>The depth of the slab under the running surface.</summary>
        public double Slab;

        /// <summary>Per segment (sample i to i+1): whether that edge has a parapet. Off where another deck abuts it (a ramp's taper).</summary>
        public bool[] ParapetLeft;
        public bool[] ParapetRight;

        /// <summary>A ring: the last sample is the first one again.</summary>
        public bool Closed;

        // ---- ramps ----

        /// <summary>An off-ramp (traffic leaves the expressway on it) rather than an on-ramp. Decides the exit sign.</summary>
        public bool Leaving;

        /// <summary>The street a ramp's low end meets (a road of the graph), -1 for anything else.</summary>
        public int StreetEdge = -1;

        /// <summary>Whether the street end is the LAST sample (an off-ramp) or the first (an on-ramp).</summary>
        public bool StreetAtEnd = true;

        /// <summary>How the ramp or connector is shaped, for the harness and the tests: diamond, slip, loop, outer, inner.</summary>
        public string Shape = "";

        public int Count => P.Length;

        public int Segments => P.Length - 1;

        /// <summary>The left and right edge points of a sample.</summary>
        public Vec2 Left(int i) => P[i] + N[i] * HalfLeft[i];
        public Vec2 Right(int i) => P[i] - N[i] * HalfRight[i];

        /// <summary>The unit direction of a segment.</summary>
        public Vec2 SegmentDir(int i) => (P[i + 1] - P[i]).Normalized;

        /// <summary>
        /// The top of the running surface at a point over segment <paramref name="i"/>, on the plane of whichever of its two triangles the point
        /// lies over (the nearer when it is just outside both: paint at the very edge).
        /// </summary>
        public double TopAt(int i, Vec2 p)
        {
            Vec2 l0 = Left(i), r0 = Right(i), l1 = Left(i + 1), r1 = Right(i + 1);
            double h0 = Top[i], h1 = Top[i + 1];
            double first = Plane(l0, r0, r1, h0, h0, h1, p, out double inside1);
            double second = Plane(l0, r1, l1, h0, h1, h1, p, out double inside2);
            return inside1 >= inside2 ? first : second;
        }

        /// <summary>The height on the plane through three corners at a point, and how far inside the triangle the point is (its least barycentric weight).</summary>
        private static double Plane(Vec2 a, Vec2 b, Vec2 c, double ha, double hb, double hc, Vec2 p, out double inside)
        {
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < 1e-12)
            {
                inside = double.NegativeInfinity;
                return (ha + hb + hc) / 3.0;
            }
            double wa = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
            double wb = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
            double wc = 1.0 - wa - wb;
            inside = Math.Min(wa, Math.Min(wb, wc));
            return wa * ha + wb * hb + wc * hc;
        }

        /// <summary>The footprint of a segment, grown by a margin: a quadrilateral (left-front, left-back, right-back, right-front).</summary>
        public Vec2[] Footprint(int i, double margin)
        {
            return new[]
            {
                P[i] + N[i] * (HalfLeft[i] + margin),
                P[i + 1] + N[i + 1] * (HalfLeft[i + 1] + margin),
                P[i + 1] - N[i + 1] * (HalfRight[i + 1] + margin),
                P[i] - N[i] * (HalfRight[i] + margin),
            };
        }

        /// <summary>The sample nearest a distance along the path.</summary>
        public int IndexAt(double s)
        {
            int lo = 0, hi = S.Length - 1;
            if (s <= S[0]) return 0;
            if (s >= S[hi]) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (S[mid] <= s) lo = mid; else hi = mid;
            }
            return s - S[lo] < S[hi] - s ? lo : hi;
        }

        /// <summary>The centreline point, the unit tangent and the top this far along (linear between samples).</summary>
        public void At(double s, out Vec2 p, out Vec2 t, out double top)
        {
            int n = S.Length;
            if (s <= S[0])
            {
                p = P[0];
                t = SegmentDir(0);
                top = Top[0];
                return;
            }
            if (s >= S[n - 1])
            {
                p = P[n - 1];
                t = SegmentDir(n - 2);
                top = Top[n - 1];
                return;
            }
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (S[mid] <= s) lo = mid; else hi = mid;
            }
            double f = (s - S[lo]) / Math.Max(1e-9, S[hi] - S[lo]);
            p = P[lo] + (P[hi] - P[lo]) * f;
            t = SegmentDir(lo);
            top = Top[lo] + (Top[hi] - Top[lo]) * f;
        }

        public double Length => S[S.Length - 1];
    }

    /// <summary>A pier under a deck: one column (a ramp's, or a narrow main line's hammerhead) or two under a wide one, with a cap carrying the slab.</summary>
    public sealed class ViaductPier
    {
        public int Path;
        public Vec2 P;
        /// <summary>The deck's direction at the pier.</summary>
        public Vec2 Along;
        /// <summary>The underside of the slab it carries, and the ground it stands on.</summary>
        public double Underside;
        public double Ground;
        /// <summary>Half the width of the cap across the deck.</summary>
        public double HalfSpan;
        /// <summary>Two columns (a wide main line) rather than one.</summary>
        public bool Twin;
        /// <summary>A twin's columns stand this far either side of the centreline (to the left of <see cref="Along"/> and to the right).</summary>
        public double ColumnLeft, ColumnRight;
    }

    /// <summary>A gantry over a main line where an exit leaves it: the sign hangs over the lanes on the exit's side.</summary>
    public sealed class ExpresswaySign
    {
        public Vec2 P;
        public Vec2 Across;
        public double Top;
        public double HalfWidth;
        /// <summary>Where across the deck the sign panel hangs, metres from the centre toward <see cref="Across"/>.</summary>
        public double Offset;
        public int Seed;
    }

    /// <summary>Where a ramp meets a street: the kerb and the pavement are cut away under it so it runs down onto the carriageway.</summary>
    public sealed class KerbCut
    {
        public Vec2[] Poly;
        public int StreetEdge;
    }

    /// <summary>A street the lots keep clear of (see <see cref="ExpresswayNetwork.HeldStreets"/>): its centreline and how far either side is kept.</summary>
    public struct HeldStreet
    {
        public Vec2 A;
        public Vec2 B;
        public double Clear;
    }

    /// <summary>A row of barriers across a carriageway: where an expressway leaves the region, closed since the evacuation.</summary>
    public sealed class ExpresswayBarrier
    {
        public Vec2 P;
        public Vec2 Across;
        public double Top;
        public double HalfWidth;
    }

    /// <summary>Where expressways meet each other (a SYSTEM interchange: one passes over the other, joined by connectors) or a street (an exit).</summary>
    public sealed class ExpresswayInterchange
    {
        public int Id;
        public bool System;
        public Vec2 At;
        public int RouteA = -1, RouteB = -1;
        /// <summary>An exit's cross street (a road of the graph).</summary>
        public int StreetEdge = -1;
        public readonly List<int> Paths = new List<int>();
    }

    /// <summary>One expressway: a ring round the centre, or a route across the whole region from edge to edge.</summary>
    public sealed class ExpresswayRoute
    {
        public int Id;
        public bool Ring;
        public int Path;
    }

    /// <summary>
    /// The expressway system of a region: its routes, every deck (main lines, connectors, ramps), the piers, the signs, the kerb cuts where ramps
    /// meet streets, and the land kept clear of lots under and between them. Planned by <see cref="ExpresswayPlanner"/>; pure data.
    /// </summary>
    public sealed class ExpresswayNetwork
    {
        public readonly List<ExpresswayRoute> Routes = new List<ExpresswayRoute>();
        public readonly List<ViaductPath> Paths = new List<ViaductPath>();
        public readonly List<ViaductPier> Piers = new List<ViaductPier>();
        public readonly List<ExpresswaySign> Signs = new List<ExpresswaySign>();
        public readonly List<KerbCut> KerbCuts = new List<KerbCut>();
        public readonly List<ExpresswayBarrier> Barriers = new List<ExpresswayBarrier>();
        public readonly List<ExpresswayInterchange> Interchanges = new List<ExpresswayInterchange>();

        /// <summary>Land between a ramp and its main line, inside a loop, inside an interchange: kept clear of lots.</summary>
        public readonly List<Vec2[]> Infields = new List<Vec2[]>();

        /// <summary>
        /// Streets inside a block that bound no block but carry a ramp's foot or an exit (a dead end a ramp runs down onto): the lots are cut back
        /// from them as from a deck, so nothing is built across them. Filled by <see cref="BlockExtractor.RetireRoadsInsideBlocks"/>.
        /// </summary>
        public readonly List<HeldStreet> HeldStreets = new List<HeldStreet>();

        /// <summary>No lot is cut within this of a deck's edge (TxDOT: keep 15-20 ft between the structure and buildings).</summary>
        public const double LotMargin = 5.0;

        // ---- a grid of every segment's footprint, for the queries below ----
        private const double Cell = 64.0;
        private readonly Dictionary<long, List<long>> _segments = new Dictionary<long, List<long>>();
        private readonly Dictionary<long, List<int>> _infields = new Dictionary<long, List<int>>();
        private bool _indexed;

        private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;
        private static int CellOf(double v) => (int)Math.Floor(v / Cell);
        private static long Ref(int path, int seg) => ((long)path << 32) | (uint)seg;

        /// <summary>(Re)builds the footprint grid; call after the paths and infields are final.</summary>
        public void Index()
        {
            _segments.Clear();
            _infields.Clear();
            foreach (ViaductPath path in Paths)
            {
                for (int i = 0; i < path.Segments; i++)
                {
                    AddBox(path.Footprint(i, LotMargin), Ref(path.Id, i));
                }
            }
            for (int k = 0; k < Infields.Count; k++)
            {
                Bounds(Infields[k], out double x0, out double y0, out double x1, out double y1);
                for (int cy = CellOf(y0); cy <= CellOf(y1); cy++)
                {
                    for (int cx = CellOf(x0); cx <= CellOf(x1); cx++)
                    {
                        long key = Key(cx, cy);
                        if (!_infields.TryGetValue(key, out List<int> list)) _infields[key] = list = new List<int>(2);
                        list.Add(k);
                    }
                }
            }
            _indexed = true;
        }

        private void AddBox(Vec2[] poly, long reference)
        {
            Bounds(poly, out double x0, out double y0, out double x1, out double y1);
            for (int cy = CellOf(y0); cy <= CellOf(y1); cy++)
            {
                for (int cx = CellOf(x0); cx <= CellOf(x1); cx++)
                {
                    long key = Key(cx, cy);
                    if (!_segments.TryGetValue(key, out List<long> list)) _segments[key] = list = new List<long>(4);
                    list.Add(reference);
                }
            }
        }

        private static void Bounds(IList<Vec2> poly, out double x0, out double y0, out double x1, out double y1)
        {
            x0 = y0 = double.MaxValue;
            x1 = y1 = double.MinValue;
            foreach (Vec2 p in poly)
            {
                x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X);
                y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y);
            }
        }

        /// <summary>Every (path, segment) whose footprint (grown by the lot margin) may touch the box.</summary>
        public void SegmentsNear(double x0, double y0, double x1, double y1, List<(int path, int seg)> result)
        {
            if (!_indexed) Index();
            result.Clear();
            var seen = new HashSet<long>();
            for (int cy = CellOf(y0); cy <= CellOf(y1); cy++)
            {
                for (int cx = CellOf(x0); cx <= CellOf(x1); cx++)
                {
                    if (!_segments.TryGetValue(Key(cx, cy), out List<long> list)) continue;
                    foreach (long r in list)
                    {
                        if (seen.Add(r)) result.Add(((int)(r >> 32), (int)(uint)r));
                    }
                }
            }
        }

        [ThreadStatic] private static List<(int path, int seg)> _scratch;

        /// <summary>Whether a polygon (a lot, a block) touches any deck's footprint grown by <see cref="LotMargin"/>, or any infield.</summary>
        public bool Touches(IList<Vec2> poly)
        {
            if (!_indexed) Index();
            Bounds(poly, out double x0, out double y0, out double x1, out double y1);
            List<(int path, int seg)> near = _scratch ?? (_scratch = new List<(int path, int seg)>());
            SegmentsNear(x0, y0, x1, y1, near);
            foreach ((int pathIndex, int seg) in near)
            {
                if (PlanMath.PolygonsIntersect(poly, Paths[pathIndex].Footprint(seg, LotMargin))) return true;
            }
            return TouchesAnInfield(poly);
        }

        private bool TouchesAnInfield(IList<Vec2> poly)
        {
            Bounds(poly, out double x0, out double y0, out double x1, out double y1);
            var seenInfields = new HashSet<int>();
            for (int cy = CellOf(y0); cy <= CellOf(y1); cy++)
            {
                for (int cx = CellOf(x0); cx <= CellOf(x1); cx++)
                {
                    if (!_infields.TryGetValue(Key(cx, cy), out List<int> list)) continue;
                    foreach (int k in list)
                    {
                        if (seenInfields.Add(k) && PlanMath.PolygonsIntersect(poly, Infields[k])) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A lot cut back from the decks beside it rather than lost: wherever a deck or ramp, grown by <see cref="LotMargin"/>, reaches into the
        /// lot, the lot is cut along that segment's edge and keeps the side its middle is on (a curved deck is followed by a cut at every segment
        /// that reaches in). The edges the cuts make face the expressway, not a street (their <paramref name="front"/> is false). The city then
        /// comes right up to the motorway, as it does along Shanghai's and Tokyo's elevated roads, instead of a strip of empty blocks either side.
        /// False when the lot is in an interchange's infield, nothing is left, or what is left no longer fronts a street. Untouched lots come back
        /// as the very lists given.
        /// </summary>
        public bool CutBack(List<Vec2> poly, List<bool> front, out List<Vec2> cutPoly, out List<bool> cutFront)
        {
            if (!_indexed) Index();
            cutPoly = poly;
            cutFront = front;
            Bounds(poly, out double x0, out double y0, out double x1, out double y1);
            // An infield (convex) reaching in: the lot is cut along whichever of its sides leaves the lot the most.
            var seenInfields = new HashSet<int>();
            for (int cy = CellOf(y0); cy <= CellOf(y1); cy++)
            {
                for (int cx = CellOf(x0); cx <= CellOf(x1); cx++)
                {
                    if (!_infields.TryGetValue(Key(cx, cy), out List<int> list)) continue;
                    foreach (int k in list)
                    {
                        if (!seenInfields.Add(k) || !PlanMath.PolygonsIntersect(cutPoly, Infields[k])) continue;
                        Vec2[] field = Infields[k];
                        Vec2 middle = PlanMath.Centroid(field);
                        List<Vec2> bestPoly = null;
                        List<bool> bestFront = null;
                        double bestArea = 0.0;
                        for (int e = 0; e < field.Length; e++)
                        {
                            Vec2 a = field[e], b = field[(e + 1) % field.Length];
                            if ((b - a).Length < 1e-6) continue;
                            Vec2 away = (b - a).Normalized.Perp;
                            if (Vec2.Dot(middle - a, away) > 0.0) away = -away;
                            var tryPoly = new List<Vec2>(cutPoly);
                            var tryFront = new List<bool>(cutFront);
                            KeepBeyond(tryPoly, tryFront, a, away, LotMargin);
                            if (tryPoly.Count < 3) continue;
                            double area = Math.Abs(PlanMath.SignedArea(tryPoly));
                            if (area <= bestArea) continue;
                            bestArea = area;
                            bestPoly = tryPoly;
                            bestFront = tryFront;
                        }
                        if (bestPoly == null) return false;
                        cutPoly = bestPoly;
                        cutFront = bestFront;
                    }
                }
            }
            List<(int path, int seg)> near = _scratch ?? (_scratch = new List<(int path, int seg)>());
            SegmentsNear(x0, y0, x1, y1, near);
            // One pass is enough: a cut only takes land away, so a segment clear of the lot now stays clear after the cuts that follow it.
            foreach ((int pathIndex, int seg) in near)
            {
                ViaductPath v = Paths[pathIndex];
                if (!PlanMath.PolygonsIntersect(cutPoly, v.Footprint(seg, LotMargin))) continue;
                Vec2 a = v.P[seg], b = v.P[seg + 1];
                Vec2 nrm = (b - a).Normalized.Perp;
                double side = Vec2.Dot(PlanMath.Centroid(cutPoly) - (a + b) * 0.5, nrm) >= 0.0 ? 1.0 : -1.0;
                double half = side > 0.0 ? Math.Max(v.HalfLeft[seg], v.HalfLeft[seg + 1]) : Math.Max(v.HalfRight[seg], v.HalfRight[seg + 1]);
                if (ReferenceEquals(cutPoly, poly))
                {
                    cutPoly = new List<Vec2>(poly);
                    cutFront = new List<bool>(front);
                }
                // 0.3 m more than the margin: the footprint's corners stand out along the samples' own normals, a hair off the segment's.
                KeepBeyond(cutPoly, cutFront, a, nrm * side, half + LotMargin + 0.3);
                if (cutPoly.Count < 3) return false;
            }
            // A held street inside the block: cut back to its pavement's width from its carriageway, on the lot's own side.
            foreach (HeldStreet s in HeldStreets)
            {
                Vec2 d = s.B - s.A;
                if (d.Length < 1e-6) continue;
                Vec2 nrm = d.Normalized.Perp;
                var foot = new[] { s.A + nrm * s.Clear, s.B + nrm * s.Clear, s.B - nrm * s.Clear, s.A - nrm * s.Clear };
                if (!PlanMath.PolygonsIntersect(cutPoly, foot)) continue;
                double side = Vec2.Dot(PlanMath.Centroid(cutPoly) - (s.A + s.B) * 0.5, nrm) >= 0.0 ? 1.0 : -1.0;
                if (ReferenceEquals(cutPoly, poly))
                {
                    cutPoly = new List<Vec2>(poly);
                    cutFront = new List<bool>(front);
                }
                KeepBeyond(cutPoly, cutFront, s.A, nrm * side, s.Clear);
                if (cutPoly.Count < 3) return false;
            }
            if (ReferenceEquals(cutPoly, poly)) return true;
            if (Math.Abs(PlanMath.SignedArea(cutPoly)) < 1.0) return false;
            foreach (bool f in cutFront) if (f) return true;
            return false;   // the street side went: a lot reached only from under a motorway is no lot
        }

        /// <summary>
        /// Clips a polygon to the points at least <paramref name="distance"/> from the line through <paramref name="a"/> on the side of the unit
        /// normal <paramref name="n"/> (Sutherland-Hodgman, one plane), carrying each edge's flag; the edge the cut makes is flagged false.
        /// </summary>
        private static void KeepBeyond(List<Vec2> poly, List<bool> flags, Vec2 a, Vec2 n, double distance)
        {
            int count = poly.Count;
            var outPoly = new List<Vec2>(count + 2);
            var outFlags = new List<bool>(count + 2);
            for (int i = 0; i < count; i++)
            {
                Vec2 p = poly[i], q = poly[(i + 1) % count];
                double hp = Vec2.Dot(p - a, n) - distance, hq = Vec2.Dot(q - a, n) - distance;
                bool pIn = hp >= 0.0, qIn = hq >= 0.0;
                if (pIn) outPoly.Add(p);
                if (pIn) outFlags.Add(flags[i]);
                if (pIn == qIn) continue;
                Vec2 x = p + (q - p) * (hp / (hp - hq));
                outPoly.Add(x);
                outFlags.Add(pIn ? false : flags[i]);   // leaving: along the cut to where it comes back in; entering: on along the old edge
            }
            poly.Clear();
            poly.AddRange(outPoly);
            flags.Clear();
            flags.AddRange(outFlags);
        }

        /// <summary>
        /// The highest deck over a point (the top of its running surface), or NaN when no deck covers it within <paramref name="margin"/> of
        /// its edges. <paramref name="pathIndex"/> is which path that deck is.
        /// </summary>
        public double DeckOver(Vec2 p, double margin, out int pathIndex)
        {
            pathIndex = -1;
            if (!_indexed) Index();
            List<(int path, int seg)> near = _scratch ?? (_scratch = new List<(int path, int seg)>());
            SegmentsNear(p.X - 1.0, p.Y - 1.0, p.X + 1.0, p.Y + 1.0, near);
            double best = double.NaN;
            foreach ((int path, int seg) in near)
            {
                ViaductPath v = Paths[path];
                if (!PlanMath.PointInPolygon(p, v.Footprint(seg, margin))) continue;
                double top = v.TopAt(seg, p);
                if (double.IsNaN(best) || top > best)
                {
                    best = top;
                    pathIndex = path;
                }
            }
            return best;
        }

        /// <summary>Whether a point lies under (or on) any deck or in any infield, within a margin of their edges.</summary>
        public bool Covers(Vec2 p, double margin)
        {
            if (!double.IsNaN(DeckOver(p, margin, out _))) return true;
            for (int k = 0; k < Infields.Count; k++)
            {
                if (PlanMath.PointInPolygon(p, Infields[k])) return true;
            }
            return false;
        }
    }
}
