using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    public static partial class CitySurfaceBuilder
    {
        // ---------------------------------------------------------------- the expressways (World/Plan/ExpresswayPlanner)

        /// <summary>The parapet along a deck's edges, and its thickness.</summary>
        public const float DeckParapet = 1.1f;
        public const float DeckParapetThickness = 0.3f;

        /// <summary>The concrete barrier down the middle of a main line, between the two directions.</summary>
        public const float MedianHeight = 0.95f;
        public const float MedianHalfWidth = 0.3f;

        /// <summary>Under a deck less than this over the ground it is an embankment (its sides go down to the ground), not a viaduct.</summary>
        public const float EmbankmentBelow = 1.6f;

        /// <summary>A main line's shoulder: the white edge line is this far in from the deck's edge.</summary>
        public const double Shoulder = 1.4;

        private static Color32 DeckConcrete => SurfaceLook.Pack(new Color32(198, 196, 190, 255), (int)WallMaterial.Concrete, 0.6f);

        /// <summary>An expressway's carriageway: clean, dark, a little different from one deck to the next.</summary>
        private static Color32 ExpresswayAsphalt(int id)
        {
            float h = LatticeHash(id, 23, 5);
            float tone = 0.74f + 0.14f * h;
            return SurfaceLook.Pack(new Color(tone, tone, tone), SurfaceLook.Asphalt, 0.10f + 0.12f * h);
        }

        /// <summary>
        /// This chunk's part of the expressways: every deck segment whose footprint touches it (clipped to the square, so neighbours meet exactly),
        /// the piers, signs and barriers that stand in it. A distant chunk builds the decks and piers only: no paint, parapets or signs.
        /// </summary>
        private static void BuildExpressways(Context ctx, int chunkX, int chunkY, MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures,
            MeshBuffer yellow, MeshBuffer white, MeshBuffer guardRail, bool far)
        {
            PlanWorld plan = ctx.Plan;
            ExpresswayNetwork xw = plan.Data.Expressways;
            foreach (int id in plan.ViaductSegmentsIn(chunkX, chunkY))
            {
                (int pathId, int seg) = plan.ViaductSegments[id];
                BuildDeckSegment(ctx, xw.Paths[pathId], seg, ground, pavement, far ? null : guardRail, far ? null : yellow, far ? null : white);
            }
            structures.Look = DeckConcrete;
            foreach (int i in plan.PiersIn(chunkX, chunkY))
            {
                BuildPier(ctx, xw.Piers[i], structures);
            }
            if (far) return;
            foreach (int i in plan.SignsIn(chunkX, chunkY))
            {
                ExpresswaySign sign = xw.Signs[i];
                Vector3 at = ctx.Local(sign.P.X, 0.0, sign.P.Y);
                CityProps.AddGantrySign(structures, at.x, at.z, ctx.LocalY(sign.Top), (float)sign.Across.X, (float)sign.Across.Y,
                    (float)sign.HalfWidth, (float)sign.Offset, sign.Seed);
            }
            foreach (int i in plan.BarriersIn(chunkX, chunkY))
            {
                ExpresswayBarrier row = xw.Barriers[i];
                int count = Math.Max(1, (int)((2.0 * row.HalfWidth - 1.0) / 3.2));
                for (int k = 0; k < count; k++)
                {
                    Vec2 pos = row.P + row.Across * (-row.HalfWidth + 1.0 + 1.6 + k * 3.2);
                    Vector3 at = ctx.Local(pos.X, 0.0, pos.Y);
                    CityProps.AddBarrier(structures, at.x, at.z, ctx.LocalY(row.Top), (float)row.Across.X, (float)row.Across.Y);
                }
            }
        }

        /// <summary>The height on the plane through a triangle's three corners, anywhere (outside it too): so a clipped piece lies exactly in its triangle's plane.</summary>
        private static Func<double, double, double> PlaneThrough(Vec2 a, Vec2 b, Vec2 c, double ha, double hb, double hc)
        {
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < 1e-12)
            {
                double mean = (ha + hb + hc) / 3.0;
                return (x, z) => mean;
            }
            return (x, z) =>
            {
                double wa = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (z - c.Y)) / det;
                double wb = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (z - c.Y)) / det;
                return wa * ha + wb * hb + (1.0 - wa - wb) * hc;
            };
        }

        /// <summary>
        /// One segment of a deck, sample <paramref name="i"/> to the next: the running surface as two planar triangles (so a curved deck's pieces
        /// share their edges exactly), the slab's underside and sides on a viaduct -- or sides straight down to the ground on an embankment -- a
        /// parapet on each edge that has one, and the paint.
        /// </summary>
        private static void BuildDeckSegment(Context ctx, ViaductPath path, int i, MeshBuffer ground, MeshBuffer pavement, MeshBuffer guardRail,
            MeshBuffer yellow, MeshBuffer white)
        {
            Vec2 l0 = path.Left(i), r0 = path.Right(i), l1 = path.Left(i + 1), r1 = path.Right(i + 1);
            var quad = new List<Vec2> { l0, r0, r1, l1 };
            Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
            if (!ctx.Rect.Overlaps(mx - 1.5, mz - 1.5, Mx + 1.5, Mz + 1.5)) return;
            double h0 = path.Top[i], h1 = path.Top[i + 1];
            float slab = (float)path.Slab;
            Vec2 mid = (path.P[i] + path.P[i + 1]) * 0.5;
            double under = Math.Min(h0, h1) - slab - ctx.Plan.GroundHeight(mid.X, mid.Y);
            bool viaduct = under > EmbankmentBelow;

            ground.Look = ExpresswayAsphalt(path.Id);
            pavement.Look = DeckConcrete;
            var triangles = new[]
            {
                (new List<Vec2> { l0, r0, r1 }, PlaneThrough(l0, r0, r1, h0, h0, h1)),
                (new List<Vec2> { l0, r1, l1 }, PlaneThrough(l0, r1, l1, h0, h1, h1)),
            };
            foreach ((List<Vec2> tri, Func<double, double, double> plane) in triangles)
            {
                // The running surface winds counter-clockwise seen from above; flip a triangle the other way round.
                if (PlanMath.SignedArea(tri) < 0.0) tri.Reverse();
                Bounds(tri, out double tx, out double tz, out double Tx, out double Tz);
                if (!ctx.Rect.Overlaps(tx, tz, Tx, Tz)) continue;
                List<Vec2> clipped = Clip(tri, ctx.Rect);
                Fan(ctx, ground, clipped, plane);
                if (viaduct)
                {
                    Fan(ctx, pavement, clipped, (x, z) => plane(x, z) - slab, up: false);
                }
            }

            // The two edges: the slab's side face, and a parapet where nothing abuts it.
            Vec2 nl = (path.N[i] + path.N[i + 1]).Normalized;
            Func<double, double, double> leftHeight = AlongEdge(l0, l1, h0, h1);
            Func<double, double, double> rightHeight = AlongEdge(r0, r1, h0, h1);
            AddSlabEdge(ctx, pavement, path.ParapetLeft[i] ? guardRail : null, l0, l1, new Vector3((float)nl.X, 0.0f, (float)nl.Y),
                leftHeight, slab, DeckParapet, DeckParapetThickness, !viaduct);
            AddSlabEdge(ctx, pavement, path.ParapetRight[i] ? guardRail : null, r0, r1, new Vector3((float)-nl.X, 0.0f, (float)-nl.Y),
                rightHeight, slab, DeckParapet, DeckParapetThickness, !viaduct);

            if (white == null && yellow == null) return;
            Func<Vec2, double> top = p => path.TopAt(i, p) + PaintLift;
            if (path.Kind == ViaductKind.Mainline)
            {
                double w = Math.Min(path.HalfLeft[i], path.HalfRight[i]);
                // The median barrier down the middle, a yellow line either side of it, white lane lines, a white edge line in from each edge --
                // dashed where a ramp runs alongside (the lane it leaves from, or joins).
                if (guardRail != null)
                {
                    AddMedian(ctx, guardRail, path.P[i], path.P[i + 1], AlongEdge(path.P[i], path.P[i + 1], h0, h1));
                }
                double lanes = (w - Shoulder - MedianHalfWidth - 0.5) / 3.0;
                foreach (int side in new[] { 1, -1 })
                {
                    StripeOnPath(ctx, yellow, path, i, side * (MedianHalfWidth + 0.35), 0.08, top, false);
                    for (int k = 1; k <= 2; k++)
                    {
                        StripeOnPath(ctx, white, path, i, side * (MedianHalfWidth + 0.5 + lanes * k), LineWidth * 0.5, top, true);
                    }
                    bool open = side > 0 ? !path.ParapetLeft[i] : !path.ParapetRight[i];
                    StripeOnPath(ctx, white, path, i, side * (w - Shoulder), open ? 0.18 : 0.1, top, open);
                }
            }
            else
            {
                // A ramp's one lane: a white line along its outer edge, following a taper in to its tip; and along its inner edge where it has
                // left its main line (a parapet there) or a gore lies between them -- not where the lane is still part of the main carriageway.
                double outer0 = path.HalfRight[i] - 0.5, outer1 = path.HalfRight[i + 1] - 0.5;
                if (outer0 > 0.3 && outer1 > 0.3)
                {
                    StripeOnPath(ctx, white, path, i, -outer0, -outer1, 0.08, top, false);
                }
                bool gore = path.HalfLeft[i] > path.HalfRight[i] + 0.3 || path.HalfLeft[i + 1] > path.HalfRight[i + 1] + 0.3;
                if ((path.ParapetLeft[i] || gore) && outer0 > 0.3 && outer1 > 0.3)
                {
                    StripeOnPath(ctx, white, path, i, outer0, outer1, 0.08, top, false);
                }
            }
        }

        /// <summary>The height along a straight edge from one end's to the other's, at the point of the edge nearest any point.</summary>
        private static Func<double, double, double> AlongEdge(Vec2 a, Vec2 b, double ha, double hb)
        {
            Vec2 d = b - a;
            double len2 = Math.Max(1e-12, d.SqrLength);
            return (x, z) =>
            {
                double t = ((x - a.X) * d.X + (z - a.Y) * d.Y) / len2;
                t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
                return ha + (hb - ha) * t;
            };
        }

        /// <summary>
        /// A stripe of paint along a deck segment at an offset from its centreline (positive: to the left), solid or dashed. Dashes are laid by the
        /// distance along the whole path, so they run on unbroken from one segment and one chunk to the next.
        /// </summary>
        private static void StripeOnPath(Context ctx, MeshBuffer buffer, ViaductPath path, int i, double offset, double halfWidth, Func<Vec2, double> top, bool dashed)
        {
            StripeOnPath(ctx, buffer, path, i, offset, offset, halfWidth, top, dashed);
        }

        /// <summary>...with the offset changing along the segment, from <paramref name="offset0"/> at its start to <paramref name="offset1"/> at its end (a taper's edge).</summary>
        private static void StripeOnPath(Context ctx, MeshBuffer buffer, ViaductPath path, int i, double offset0, double offset1, double halfWidth, Func<Vec2, double> top, bool dashed)
        {
            if (buffer == null) return;
            Vec2 a = path.P[i] + path.N[i] * offset0, b = path.P[i + 1] + path.N[i + 1] * offset1;
            double s0 = path.S[i], s1 = path.S[i + 1];
            if (s1 - s0 < 1e-6) return;
            Vec2 perp = (path.N[i] + path.N[i + 1]).Normalized;
            if (!dashed)
            {
                StripeAlong(ctx, buffer, a, b, perp, halfWidth, (x, z) => top(new Vec2(x, z)));
                return;
            }
            double period = DashLength + DashGap;
            for (double d = Math.Floor(s0 / period) * period; d < s1; d += period)
            {
                double u0 = Math.Max(s0, d), u1 = Math.Min(s1, d + DashLength);
                if (u1 - u0 < 0.05) continue;
                Vec2 pa = a + (b - a) * ((u0 - s0) / (s1 - s0));
                Vec2 pb = a + (b - a) * ((u1 - s0) / (s1 - s0));
                StripeAlong(ctx, buffer, pa, pb, perp, halfWidth, (x, z) => top(new Vec2(x, z)));
            }
        }

        /// <summary>A concrete barrier down a main line's centre: two faces and a top, clipped to the chunk.</summary>
        private static void AddMedian(Context ctx, MeshBuffer buffer, Vec2 a, Vec2 b, Func<double, double, double> height)
        {
            if (!ClipSegment(a, b, ctx.Rect, out Vec2 ka, out Vec2 kb) || (kb - ka).Length < 1e-6) return;
            Vec2 d = (kb - ka).Normalized;
            Vec2 n = d.Perp * MedianHalfWidth;
            double ya = height(ka.X, ka.Y), yb = height(kb.X, kb.Y);
            float len = (float)(kb - ka).Length;
            Color32 saved = buffer.Look;
            buffer.Look = DeckConcrete;
            foreach (int side in new[] { 1, -1 })
            {
                Vec2 pa = ka + n * side, pb = kb + n * side;
                var outward = new Vector3((float)(d.Perp.X * side), 0.0f, (float)(d.Perp.Y * side));
                buffer.AddQuad(ctx.Local(pa.X, ya, pa.Y), ctx.Local(pb.X, yb, pb.Y), ctx.Local(pb.X, yb + MedianHeight, pb.Y), ctx.Local(pa.X, ya + MedianHeight, pa.Y),
                    outward, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, MedianHeight), new Vector2(0, MedianHeight));
            }
            buffer.AddQuad(ctx.Local((ka + n).X, ya + MedianHeight, (ka + n).Y), ctx.Local((kb + n).X, yb + MedianHeight, (kb + n).Y),
                ctx.Local((kb - n).X, yb + MedianHeight, (kb - n).Y), ctx.Local((ka - n).X, ya + MedianHeight, (ka - n).Y), Vector3.up,
                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
            buffer.Look = saved;
        }

        /// <summary>
        /// A pier, built whole by the chunk it stands in: a column (or two, under a wide main line) from below the ground up to a cap that carries
        /// the slab across its width.
        /// </summary>
        private static void BuildPier(Context ctx, ViaductPier pier, MeshBuffer structures)
        {
            structures.SetFrame(ctx.Local(pier.P.X, 0.0, pier.P.Y), pier.Along.X, pier.Along.Y);
            float underside = ctx.LocalY(pier.Underside);
            float groundY = ctx.LocalY(pier.Ground);
            bool main = pier.HalfSpan > 6.0;
            float capDepth = main ? 1.3f : 0.6f;
            float capBottom = underside - capDepth;
            float col = main ? 1.0f : 0.6f;
            if (pier.Twin)
            {
                foreach (float z in new[] { (float)pier.ColumnLeft, -(float)pier.ColumnRight })
                {
                    structures.AddBox(new Vector3(-col, groundY - 0.6f, z - col), new Vector3(col, capBottom, z + col));
                }
            }
            else
            {
                structures.AddBox(new Vector3(-col, groundY - 0.6f, -col), new Vector3(col, capBottom, col));
            }
            float half = (float)pier.HalfSpan;
            structures.AddBox(new Vector3(-col * 0.9f, capBottom, -half), new Vector3(col * 0.9f, underside + 0.02f, half), includeBottom: true);
            structures.ClearFrame();
        }

        // ---------------------------------------------------------------- kerb cuts: where a ramp meets a street

        /// <summary>The kerb cuts that touch this chunk, as polygons.</summary>
        private static List<Vec2[]> KerbCuts(Context ctx, int chunkX, int chunkY)
        {
            List<int> ids = ctx.Plan.KerbCutsIn(chunkX, chunkY);
            var cuts = new List<Vec2[]>(ids.Count);
            foreach (int id in ids) cuts.Add(ctx.Plan.Data.Expressways.KerbCuts[id].Poly);
            return cuts;
        }

        /// <summary>
        /// A convex piece of pavement with every kerb cut taken out of it: the pieces left, each convex. Each cut is convex: what lies outside it is
        /// the union of the parts beyond each of its edges, taken one edge at a time from what is still inside the ones before.
        /// </summary>
        private static List<List<Vec2>> Subtract(List<Vec2> piece, List<Vec2[]> cuts)
        {
            var pieces = new List<List<Vec2>> { piece };
            if (cuts == null || cuts.Count == 0) return pieces;
            foreach (Vec2[] cut in cuts)
            {
                var next = new List<List<Vec2>>();
                foreach (List<Vec2> p in pieces)
                {
                    Bounds(p, out double px, out double pz, out double Px, out double Pz);
                    Bounds(cut, out double cx, out double cz, out double Cx, out double Cz);
                    if (Px < cx || px > Cx || Pz < cz || pz > Cz)
                    {
                        next.Add(p);
                        continue;
                    }
                    bool ccw = PlanMath.SignedArea(cut) > 0.0;
                    List<Vec2> remaining = p;
                    for (int e = 0; e < cut.Length && remaining.Count >= 3; e++)
                    {
                        Vec2 a = cut[e], b = cut[(e + 1) % cut.Length];
                        Vec2 inward = (b - a).Perp * (ccw ? 1.0 : -1.0);   // toward the inside of the cut
                        List<Vec2> outside = ClipHalf(remaining, a, -inward);
                        if (outside.Count >= 3 && Math.Abs(PlanMath.SignedArea(outside)) > SliverArea) next.Add(outside);
                        remaining = ClipHalf(remaining, a, inward);
                    }
                    // What is left inside every edge is inside the cut: dropped.
                }
                pieces = next;
            }
            return pieces;
        }

        /// <summary>The part of a convex polygon on the side of the line through <paramref name="a"/> that <paramref name="toward"/> points to.</summary>
        private static List<Vec2> ClipHalf(List<Vec2> poly, Vec2 a, Vec2 toward)
        {
            var result = new List<Vec2>(poly.Count + 2);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 p = poly[i], q = poly[(i + 1) % n];
                double dp = Vec2.Dot(p - a, toward), dq = Vec2.Dot(q - a, toward);
                if (dp >= 0.0) result.Add(p);
                if ((dp >= 0.0) != (dq >= 0.0))
                {
                    double t = dp / (dp - dq);
                    result.Add(p + (q - p) * t);
                }
            }
            return result;
        }

        /// <summary>The parts of a segment outside every cut (each cut convex, so it takes out at most one stretch).</summary>
        private static List<(Vec2 a, Vec2 b)> OutsideCuts(Vec2 a, Vec2 b, List<Vec2[]> cuts)
        {
            var parts = new List<(Vec2 a, Vec2 b)> { (a, b) };
            if (cuts == null) return parts;
            foreach (Vec2[] cut in cuts)
            {
                var next = new List<(Vec2 a, Vec2 b)>();
                foreach ((Vec2 pa, Vec2 pb) in parts)
                {
                    // The stretch of pa-pb inside the cut: clip the parameter range by each edge's half plane.
                    bool ccw = PlanMath.SignedArea(cut) > 0.0;
                    double t0 = 0.0, t1 = 1.0;
                    Vec2 d = pb - pa;
                    bool empty = false;
                    for (int e = 0; e < cut.Length && !empty; e++)
                    {
                        Vec2 ea = cut[e], eb = cut[(e + 1) % cut.Length];
                        Vec2 inward = (eb - ea).Perp * (ccw ? 1.0 : -1.0);
                        double num = Vec2.Dot(pa - ea, inward), den = Vec2.Dot(d, inward);
                        if (Math.Abs(den) < 1e-12)
                        {
                            if (num < 0.0) empty = true;
                            continue;
                        }
                        double t = -num / den;
                        if (den > 0.0) t0 = Math.Max(t0, t); else t1 = Math.Min(t1, t);
                        if (t0 >= t1) empty = true;
                    }
                    if (empty)
                    {
                        next.Add((pa, pb));
                        continue;
                    }
                    if (t0 > 1e-6) next.Add((pa, pa + d * t0));
                    if (t1 < 1.0 - 1e-6) next.Add((pa + d * t1, pb));
                }
                parts = next;
            }
            return parts;
        }

        /// <summary>Whether a point lies in any kerb cut.</summary>
        private static bool InCut(Vec2 p, List<Vec2[]> cuts)
        {
            if (cuts == null) return false;
            foreach (Vec2[] cut in cuts)
            {
                if (PlanMath.PointInPolygon(p, cut)) return true;
            }
            return false;
        }
    }
}
