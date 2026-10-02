using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Builds the ground of one chunk: the terrain, the road ribbons and junction plates, the pavement with its kerb, the lane paint
    /// and the bridges -- from the plan, clipped to the chunk's own 24 m square.
    ///
    /// EVERYTHING IS CLIPPED TO THE CHUNK, not assigned to it whole. A road crosses many chunks at any angle; each builds just the part
    /// of it inside its own square, with heights read from the same ground function, so two neighbours meet exactly on their shared
    /// edge with no crack and no overlap.
    ///
    /// SURFACES, in the buffers the streamer already has: the road and its junctions go to ground (the asphalt material); bare
    /// terrain, the pavement and the kerbs go to pavement (the flat concrete material -- the trench a river leaves is concrete too).
    /// </summary>
    public static class CitySurfaceBuilder
    {
        /// <summary>A road floats this far above the terrain under it, so the two never z-fight.</summary>
        public const float RoadLift = 0.07f;

        /// <summary>How far the pavement stands above the road: the kerb height.</summary>
        public const float KerbHeight = 0.16f;

        /// <summary>Lane paint floats this far above the road.</summary>
        public const float PaintLift = 0.03f;

        /// <summary>Terrain is sampled on this grid, metres (coarser for distant chunks).</summary>
        public const float TerrainStep = 4.0f;
        public const float FarTerrainStep = 8.0f;

        /// <summary>Roads and pavement are cut into pieces this long so they follow the ground.</summary>
        public const double PieceLength = 5.0;

        /// <summary>Deck depth of a bridge, and the height of its parapet.</summary>
        public const float BridgeThickness = 0.9f;
        public const float BridgeParapet = 0.95f;
        public const float BridgeParapetThickness = 0.25f;

        public const float LineWidth = 0.15f;
        public const double DashLength = 3.0;
        public const double DashGap = 4.5;

        public static void Build(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures, MeshBuffer markings,
            MeshBuffer laneDividers, MeshBuffer guardRail, bool far)
        {
            PlanWorld plan = world.Plan;
            double size = PlanWorld.ChunkSize;
            var rect = new Rect2(chunkX * size, chunkY * size, (chunkX + 1) * size, (chunkY + 1) * size);
            var ctx = new Context(plan, origin, rect);

            BuildTerrain(ctx, pavement, far ? FarTerrainStep : TerrainStep);

            foreach (int blockIndex in plan.BlocksIn(chunkX, chunkY))
            {
                BuildPavement(ctx, plan.Data.Blocks[blockIndex], pavement);
            }

            foreach (int edgeId in plan.EdgesIn(chunkX, chunkY))
            {
                EdgeGeometry g = plan.Geometry.Edges[edgeId];
                if (g == null) continue;
                BuildRoad(ctx, g, ground);
                if (g.Edge.Bridge)
                {
                    BuildBridgeParts(ctx, g, pavement, structures, guardRail);
                }
                if (!far)
                {
                    BuildPaint(ctx, g, markings, laneDividers);
                }
            }

            foreach (int nodeId in plan.JunctionsIn(chunkX, chunkY))
            {
                JunctionGeometry j = plan.Geometry.Junctions[nodeId];
                if (j == null || j.Plate == null) continue;
                BuildPlate(ctx, j, ground);
            }
        }

        // ---------------------------------------------------------------- context and clipping

        private readonly struct Rect2
        {
            public readonly double X0, Z0, X1, Z1;

            public Rect2(double x0, double z0, double x1, double z1)
            {
                X0 = x0;
                Z0 = z0;
                X1 = x1;
                Z1 = z1;
            }

            public bool Overlaps(double minX, double minZ, double maxX, double maxZ) =>
                maxX >= X0 && minX <= X1 && maxZ >= Z0 && minZ <= Z1;
        }

        private sealed class Context
        {
            public readonly PlanWorld Plan;
            public readonly Vector3 Origin;
            public readonly Rect2 Rect;

            public Context(PlanWorld plan, Vector3 origin, Rect2 rect)
            {
                Plan = plan;
                Origin = origin;
                Rect = rect;
            }

            public float Ground(double x, double z) => (float)Plan.GroundHeight(x, z);

            public Vector3 Local(double x, double y, double z) =>
                new Vector3((float)(x - Origin.x), (float)y - Origin.y, (float)(z - Origin.z));
        }

        /// <summary>Sutherland-Hodgman against the chunk's square. Works for the convex pieces this is fed.</summary>
        private static List<Vec2> Clip(IList<Vec2> poly, Rect2 r)
        {
            var current = new List<Vec2>(poly);
            current = ClipEdge(current, 0, r.X0);
            current = ClipEdge(current, 1, r.X1);
            current = ClipEdge(current, 2, r.Z0);
            current = ClipEdge(current, 3, r.Z1);
            return current;
        }

        private static List<Vec2> ClipEdge(List<Vec2> input, int side, double limit)
        {
            var output = new List<Vec2>(input.Count + 2);
            int n = input.Count;
            if (n == 0) return output;
            for (int i = 0; i < n; i++)
            {
                Vec2 a = input[i];
                Vec2 b = input[(i + 1) % n];
                bool aIn = Inside(a, side, limit);
                bool bIn = Inside(b, side, limit);
                if (aIn)
                {
                    output.Add(a);
                }
                if (aIn != bIn)
                {
                    output.Add(Intersect(a, b, side, limit));
                }
            }
            return output;
        }

        private static bool Inside(Vec2 p, int side, double limit)
        {
            switch (side)
            {
                case 0: return p.X >= limit;
                case 1: return p.X <= limit;
                case 2: return p.Y >= limit;
                default: return p.Y <= limit;
            }
        }

        private static Vec2 Intersect(Vec2 a, Vec2 b, int side, double limit)
        {
            if (side < 2)
            {
                double t = (limit - a.X) / (b.X - a.X);
                return new Vec2(limit, a.Y + (b.Y - a.Y) * t);
            }
            double u = (limit - a.Y) / (b.Y - a.Y);
            return new Vec2(a.X + (b.X - a.X) * u, limit);
        }

        /// <summary>Clips a segment to the square. False if nothing is left.</summary>
        private static bool ClipSegment(Vec2 a, Vec2 b, Rect2 r, out Vec2 ca, out Vec2 cb)
        {
            double t0 = 0.0, t1 = 1.0;
            double dx = b.X - a.X, dz = b.Y - a.Y;
            ca = a;
            cb = b;
            if (!ClipT(-dx, a.X - r.X0, ref t0, ref t1)) return false;
            if (!ClipT(dx, r.X1 - a.X, ref t0, ref t1)) return false;
            if (!ClipT(-dz, a.Y - r.Z0, ref t0, ref t1)) return false;
            if (!ClipT(dz, r.Z1 - a.Y, ref t0, ref t1)) return false;
            ca = new Vec2(a.X + dx * t0, a.Y + dz * t0);
            cb = new Vec2(a.X + dx * t1, a.Y + dz * t1);
            return true;
        }

        private static bool ClipT(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12)
            {
                return q >= 0.0;
            }
            double t = q / p;
            if (p < 0.0)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        private static void Bounds(IList<Vec2> poly, out double minX, out double minZ, out double maxX, out double maxZ)
        {
            minX = minZ = double.MaxValue;
            maxX = maxZ = double.MinValue;
            for (int i = 0; i < poly.Count; i++)
            {
                if (poly[i].X < minX) minX = poly[i].X;
                if (poly[i].X > maxX) maxX = poly[i].X;
                if (poly[i].Y < minZ) minZ = poly[i].Y;
                if (poly[i].Y > maxZ) maxZ = poly[i].Y;
            }
        }

        // ---------------------------------------------------------------- emitting

        /// <summary>
        /// A flat-shaded polygon (already clipped), each vertex at the height <paramref name="heightAt"/> gives, as a fan.
        /// UVs are world metres so a tiling texture reads the same on every chunk.
        /// </summary>
        private static void Fan(Context ctx, MeshBuffer buffer, IList<Vec2> poly, Func<double, double, double> heightAt, bool up = true)
        {
            int n = poly.Count;
            if (n < 3) return;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                pts[i] = ctx.Local(poly[i].X, heightAt(poly[i].X, poly[i].Y), poly[i].Y);
            }
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 normal = Vector3.Cross(pts[i] - pts[0], pts[i + 1] - pts[0]);
                if (normal.sqrMagnitude < 1e-10f) continue;
                normal.Normalize();
                if (up && normal.y < 0.0f) normal = -normal;
                if (!up && normal.y > 0.0f) normal = -normal;
                buffer.AddTriangle(pts[0], pts[i], pts[i + 1], normal,
                    new Vector2((float)poly[0].X, (float)poly[0].Y),
                    new Vector2((float)poly[i].X, (float)poly[i].Y),
                    new Vector2((float)poly[i + 1].X, (float)poly[i + 1].Y));
            }
        }

        // ---------------------------------------------------------------- terrain

        private static void BuildTerrain(Context ctx, MeshBuffer buffer, float step)
        {
            Rect2 r = ctx.Rect;
            int n = Mathf.Max(1, Mathf.RoundToInt((float)(r.X1 - r.X0) / step));
            double s = (r.X1 - r.X0) / n;
            var heights = new double[n + 1, n + 1];
            for (int iz = 0; iz <= n; iz++)
            {
                for (int ix = 0; ix <= n; ix++)
                {
                    heights[ix, iz] = ctx.Plan.GroundHeight(r.X0 + ix * s, r.Z0 + iz * s);
                }
            }
            for (int iz = 0; iz < n; iz++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    double x0 = r.X0 + ix * s, x1 = x0 + s, z0 = r.Z0 + iz * s, z1 = z0 + s;
                    Vector3 a = ctx.Local(x0, heights[ix, iz], z0);
                    Vector3 b = ctx.Local(x0, heights[ix, iz + 1], z1);
                    Vector3 c = ctx.Local(x1, heights[ix + 1, iz + 1], z1);
                    Vector3 d = ctx.Local(x1, heights[ix + 1, iz], z0);
                    Vector3 normal = (Vector3.Cross(b - a, d - a) + Vector3.Cross(c - b, d - b)).normalized;
                    if (normal.y < 0.0f) normal = -normal;
                    buffer.AddQuad(a, b, c, d, normal,
                        new Vector2((float)x0, (float)z0), new Vector2((float)x0, (float)z1),
                        new Vector2((float)x1, (float)z1), new Vector2((float)x1, (float)z0));
                }
            }
        }

        // ---------------------------------------------------------------- roads

        private static Func<double, double, double> RoadHeight(Context ctx, EdgeGeometry g)
        {
            if (!g.Edge.Bridge)
            {
                return (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift;
            }
            double hA = ctx.Plan.GroundHeight(g.A.X, g.A.Y) + RoadLift;
            double hB = ctx.Plan.GroundHeight(g.B.X, g.B.Y) + RoadLift;
            return (x, z) =>
            {
                double t = ((x - g.A.X) * g.Dir.X + (z - g.A.Y) * g.Dir.Y) / g.Length;
                t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
                return hA + (hB - hA) * t;
            };
        }

        private static void BuildRoad(Context ctx, EdgeGeometry g, MeshBuffer buffer)
        {
            double t0 = g.TrimA;
            double t1 = g.Length - g.TrimB;
            if (t1 - t0 < 0.05) return;
            Vec2 perp = g.Dir.Perp * g.HalfWidth;
            Func<double, double, double> height = RoadHeight(ctx, g);

            int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
            for (int i = 0; i < pieces; i++)
            {
                double ta = t0 + (t1 - t0) * i / pieces;
                double tb = t0 + (t1 - t0) * (i + 1) / pieces;
                Vec2 ca = g.A + g.Dir * ta;
                Vec2 cb = g.A + g.Dir * tb;
                var quad = new List<Vec2> { ca + perp, cb + perp, cb - perp, ca - perp };
                Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
                if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) continue;
                List<Vec2> clipped = Clip(quad, ctx.Rect);
                Fan(ctx, buffer, clipped, height);
            }
        }

        private static void BuildPlate(Context ctx, JunctionGeometry j, MeshBuffer buffer)
        {
            Vec2[] plate = j.Plate;
            Bounds(plate, out double mx, out double mz, out double Mx, out double Mz);
            if (!ctx.Rect.Overlaps(mx - 1.0, mz - 1.0, Mx + 1.0, Mz + 1.0)) return;
            Vec2 centre = j.Node.P;
            // The ground the plate sits on is the junction's own: one value for the whole plate, so it is flat and its edges meet the ribbons.
            Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift;
            for (int i = 0; i < plate.Length; i++)
            {
                var tri = new List<Vec2> { centre, plate[i], plate[(i + 1) % plate.Length] };
                List<Vec2> clipped = Clip(tri, ctx.Rect);
                Fan(ctx, buffer, clipped, height);
            }
        }

        // ---------------------------------------------------------------- pavement and kerb

        private static void BuildPavement(Context ctx, PlanBlock block, MeshBuffer buffer)
        {
            if (block.Inner == null || block.Outer == null) return;
            Bounds(block.Poly, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0, bz0, bx1, bz1)) return;

            int n = block.Poly.Count;
            for (int e = 0; e < n; e++)
            {
                int f = (e + 1) % n;
                Vec2 i0 = block.Inner[e], i1 = block.Inner[f];
                Vec2 o0 = block.Outer[e], o1 = block.Outer[f];
                double length = (i1 - i0).Length;
                int pieces = Math.Max(1, (int)Math.Ceiling(length / PieceLength));
                for (int p = 0; p < pieces; p++)
                {
                    double sa = (double)p / pieces, sb = (double)(p + 1) / pieces;
                    Vec2 ia = i0 + (i1 - i0) * sa, ib = i0 + (i1 - i0) * sb;
                    Vec2 oa = o0 + (o1 - o0) * sa, ob = o0 + (o1 - o0) * sb;

                    var quad = new List<Vec2> { ia, ib, ob, oa };
                    Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
                    if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) continue;

                    Fan(ctx, buffer, Clip(quad, ctx.Rect), (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight);

                    // The kerb: a vertical face along the inner edge, from the road up to the pavement, facing the road.
                    if (ClipSegment(ia, ib, ctx.Rect, out Vec2 ka, out Vec2 kb) && (kb - ka).Length > 0.02)
                    {
                        Vec2 d = (ib - ia).Normalized;
                        var normal = new Vector3((float)d.Y, 0.0f, (float)-d.X);   // to the right of the edge: toward the road
                        double ya = ctx.Plan.GroundHeight(ka.X, ka.Y) + RoadLift;
                        double yb = ctx.Plan.GroundHeight(kb.X, kb.Y) + RoadLift;
                        Vector3 b0 = ctx.Local(ka.X, ya, ka.Y);
                        Vector3 b1 = ctx.Local(kb.X, yb, kb.Y);
                        Vector3 t1 = ctx.Local(kb.X, yb + KerbHeight, kb.Y);
                        Vector3 t0 = ctx.Local(ka.X, ya + KerbHeight, ka.Y);
                        float len = (float)(kb - ka).Length;
                        buffer.AddQuad(b0, b1, t1, t0, normal,
                            new Vector2(0.0f, 0.0f), new Vector2(len, 0.0f), new Vector2(len, KerbHeight), new Vector2(0.0f, KerbHeight));
                    }
                }
            }
        }

        // ---------------------------------------------------------------- paint

        private static void BuildPaint(Context ctx, EdgeGeometry g, MeshBuffer yellow, MeshBuffer white)
        {
            double t0 = g.TrimA + 1.5;
            double t1 = g.Length - g.TrimB - 1.5;
            if (t1 - t0 < 3.0) return;
            Func<double, double, double> height = RoadHeight(ctx, g);
            Vec2 perp = g.Dir.Perp;

            switch (g.Edge.Class)
            {
                case RoadClass.Street:
                    if (white != null)
                    {
                        Dashes(ctx, g, white, height, 0.0, t0, t1);
                    }
                    break;
                case RoadClass.Arterial:
                case RoadClass.Highway:
                    if (yellow != null)
                    {
                        Line(ctx, g, yellow, height, 0.13, t0, t1);
                        Line(ctx, g, yellow, height, -0.13, t0, t1);
                    }
                    if (white != null)
                    {
                        Dashes(ctx, g, white, height, g.HalfWidth * 0.5, t0, t1);
                        Dashes(ctx, g, white, height, -g.HalfWidth * 0.5, t0, t1);
                    }
                    break;
            }
        }

        private static void Line(Context ctx, EdgeGeometry g, MeshBuffer buffer, Func<double, double, double> height,
            double offset, double t0, double t1)
        {
            int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
            for (int i = 0; i < pieces; i++)
            {
                double ta = t0 + (t1 - t0) * i / pieces;
                double tb = t0 + (t1 - t0) * (i + 1) / pieces;
                Paint(ctx, g, buffer, height, offset, ta, tb);
            }
        }

        private static void Dashes(Context ctx, EdgeGeometry g, MeshBuffer buffer, Func<double, double, double> height,
            double offset, double t0, double t1)
        {
            for (double t = t0; t + DashLength <= t1; t += DashLength + DashGap)
            {
                Paint(ctx, g, buffer, height, offset, t, t + DashLength);
            }
        }

        private static void Paint(Context ctx, EdgeGeometry g, MeshBuffer buffer, Func<double, double, double> height,
            double offset, double ta, double tb)
        {
            Vec2 perp = g.Dir.Perp;
            double w = LineWidth * 0.5;
            Vec2 pa = g.A + g.Dir * ta + perp * offset;
            Vec2 pb = g.A + g.Dir * tb + perp * offset;
            var quad = new List<Vec2> { pa + perp * w, pb + perp * w, pb - perp * w, pa - perp * w };
            Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
            if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) return;
            Fan(ctx, buffer, Clip(quad, ctx.Rect), (x, z) => height(x, z) + PaintLift);
        }

        // ---------------------------------------------------------------- bridges

        /// <summary>The underside and sides of a bridge deck, its pavement, and a parapet along each side.</summary>
        private static void BuildBridgeParts(Context ctx, EdgeGeometry g, MeshBuffer pavement, MeshBuffer structures, MeshBuffer guardRail)
        {
            Func<double, double, double> height = RoadHeight(ctx, g);
            double t0 = g.TrimA;
            double t1 = g.Length - g.TrimB;
            if (t1 - t0 < 0.1) return;
            Vec2 perp = g.Dir.Perp;
            double edge = g.HalfWidth;
            double outer = g.HalfWidth + 2.2;   // a pavement on each side of the carriageway, to the parapet

            int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
            for (int i = 0; i < pieces; i++)
            {
                double ta = t0 + (t1 - t0) * i / pieces;
                double tb = t0 + (t1 - t0) * (i + 1) / pieces;
                Vec2 ca = g.A + g.Dir * ta;
                Vec2 cb = g.A + g.Dir * tb;

                for (int side = -1; side <= 1; side += 2)
                {
                    // Pavement strip from the carriageway edge out to the parapet.
                    Vec2 ea = ca + perp * (edge * side), eb = cb + perp * (edge * side);
                    Vec2 oa = ca + perp * (outer * side), ob = cb + perp * (outer * side);
                    var strip = new List<Vec2> { ea, eb, ob, oa };
                    Bounds(strip, out double mx, out double mz, out double Mx, out double Mz);
                    if (ctx.Rect.Overlaps(mx, mz, Mx, Mz))
                    {
                        Fan(ctx, pavement, Clip(strip, ctx.Rect), (x, z) => height(x, z) + KerbHeight * 0.5);
                    }

                    // The side of the deck, facing out, and the parapet standing on it.
                    if (ClipSegment(oa, ob, ctx.Rect, out Vec2 ka, out Vec2 kb) && (kb - ka).Length > 0.02)
                    {
                        Vector3 outward = new Vector3((float)(perp.X * side), 0.0f, (float)(perp.Y * side));
                        double ya = height(ka.X, ka.Y), yb = height(kb.X, kb.Y);
                        float len = (float)(kb - ka).Length;
                        // The deck's side.
                        pavement.AddQuad(ctx.Local(ka.X, ya - BridgeThickness, ka.Y), ctx.Local(kb.X, yb - BridgeThickness, kb.Y),
                            ctx.Local(kb.X, yb, kb.Y), ctx.Local(ka.X, ya, ka.Y), outward,
                            new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, BridgeThickness), new Vector2(0, BridgeThickness));
                        // The parapet: an outer face, an inner face and a cap, as a thin wall.
                        if (guardRail != null)
                        {
                            Vec2 inset = new Vec2(perp.X * side, perp.Y * side) * BridgeParapetThickness;
                            Vec2 ia = ka - inset, ib = kb - inset;
                            double pa = ya + KerbHeight * 0.5, pb2 = yb + KerbHeight * 0.5;
                            guardRail.AddQuad(ctx.Local(ka.X, pa, ka.Y), ctx.Local(kb.X, pb2, kb.Y),
                                ctx.Local(kb.X, pb2 + BridgeParapet, kb.Y), ctx.Local(ka.X, pa + BridgeParapet, ka.Y), outward,
                                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, BridgeParapet), new Vector2(0, BridgeParapet));
                            guardRail.AddQuad(ctx.Local(ib.X, pb2, ib.Y), ctx.Local(ia.X, pa, ia.Y),
                                ctx.Local(ia.X, pa + BridgeParapet, ia.Y), ctx.Local(ib.X, pb2 + BridgeParapet, ib.Y), -outward,
                                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, BridgeParapet), new Vector2(0, BridgeParapet));
                            guardRail.AddQuad(ctx.Local(ka.X, pa + BridgeParapet, ka.Y), ctx.Local(kb.X, pb2 + BridgeParapet, kb.Y),
                                ctx.Local(ib.X, pb2 + BridgeParapet, ib.Y), ctx.Local(ia.X, pa + BridgeParapet, ia.Y), Vector3.up,
                                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
                        }
                    }
                }

                // The underside, so the deck is a solid slab from below.
                var under = new List<Vec2> { ca + perp * outer, cb + perp * outer, cb - perp * outer, ca - perp * outer };
                Bounds(under, out double ux, out double uz, out double Ux, out double Uz);
                if (ctx.Rect.Overlaps(ux, uz, Ux, Uz))
                {
                    Fan(ctx, pavement, Clip(under, ctx.Rect), (x, z) => height(x, z) - BridgeThickness, up: false);
                }
            }
        }
    }
}
