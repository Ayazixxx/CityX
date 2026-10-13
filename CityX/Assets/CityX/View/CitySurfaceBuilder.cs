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

        /// <summary>
        /// Lane paint floats this far above the road (not lower: it would vanish into the road where two ribbons overlap). 2 cm until 2026-10-09; the
        /// hillier spawn of seed 61839025 buried a tenth of its paint at 2 cm (RoadPaintTests), so 2.5.
        /// </summary>
        public const float PaintLift = 0.025f;

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

        /// <summary>The expressway deck's parapet, and its piers' spacing (real viaduct spans are 20-55 m).</summary>
        public const float DeckParapet = 1.1f;
        public const double PierSpacing = 32.0;

        /// <summary>An interchange ramp: the depth of its slab, how often it is held up, and how close to the ground it stops being a viaduct and becomes an embankment.</summary>
        public const float RampSlab = 0.9f;
        public const double RampPierSpacing = 15.0;
        public const float RampEmbankmentClearance = 1.0f;

        public static void Build(CityWorld world, int chunkX, int chunkY, Vector3 origin,
            MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures, MeshBuffer markings,
            MeshBuffer laneDividers, MeshBuffer guardRail, bool far)
        {
            PlanWorld plan = world.Plan;
            double size = PlanWorld.ChunkSize;
            var rect = new Rect2(chunkX * size, chunkY * size, (chunkX + 1) * size, (chunkY + 1) * size);
            var ctx = new Context(plan, origin, rect);

            float step = far ? FarTerrainStep : TerrainStep;
            BuildTerrain(ctx, pavement, step, plan.BlocksIn(chunkX, chunkY));

            foreach (int blockIndex in plan.BlocksIn(chunkX, chunkY))
            {
                PlanBlock block = plan.Data.Blocks[blockIndex];
                BuildPavement(ctx, block, blockIndex, pavement, far);
                BuildBlockPaving(ctx, block, blockIndex, pavement, step);
                if (block.Plaza && !far)
                {
                    BuildPlazaFurniture(ctx, block, blockIndex, structures);
                }
                if (block.Mall && !far)
                {
                    BuildMallGrounds(ctx, block, blockIndex, structures, laneDividers);   // white paint (the buffer named for the lane dividers draws white, "markings" yellow)
                }
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
                if (g.Elevated)
                {
                    BuildDeck(ctx, g, ground, pavement, structures, markings, laneDividers, far ? null : guardRail, far);
                }
                else if (!far)
                {
                    AddStreetBarricades(ctx, g, structures);
                }
            }

            foreach (int nodeId in plan.JunctionsIn(chunkX, chunkY))
            {
                JunctionGeometry j = plan.Geometry.Junctions[nodeId];
                if (j == null) continue;
                if (j.Plate != null)
                {
                    BuildPlate(ctx, j, ground);
                }
                if (j.DeckPlate != null && j.Elevated)
                {
                    BuildDeckPlate(ctx, j, ground, pavement);
                }
            }

            foreach (int rampIndex in plan.RampsIn(chunkX, chunkY))
            {
                RampGeometry ramp = plan.Geometry.Ramps[rampIndex];
                BuildRamp(ctx, ramp, ground, pavement, structures, far ? null : guardRail, laneDividers, far);   // white edge lines
                if (!far)
                {
                    BuildInterchangeLand(ctx, ramp, rampIndex, pavement, structures, laneDividers);   // white stalls
                }
            }

            // The buildings that follow stamp their own looks; none of the ground's is left on a buffer for them to inherit.
            ground.Look = SurfaceLook.Default;
            pavement.Look = SurfaceLook.Default;
            structures.Look = SurfaceLook.Default;
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

            /// <summary>An absolute height as a chunk-local one (the props in CityProps take local coordinates).</summary>
            public float LocalY(double y) => (float)y - Origin.y;

            /// <summary>Whether a point is in this chunk's square: a thing standing at a point is built by the one chunk that has it.</summary>
            public bool Contains(double x, double z) => x >= Rect.X0 && x < Rect.X1 && z >= Rect.Z0 && z < Rect.Z1;

            public bool Contains(Vec2 p) => Contains(p.X, p.Y);
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

        // ---------------------------------------------------------------- looks of the ground

        /// <summary>Smooth noise 0..1 on a lattice of cells `cell` metres across: the same at the same place in every chunk.</summary>
        private static float PatchNoise(double x, double z, double cell, int salt)
        {
            double fx = x / cell, fz = z / cell;
            int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
            float tx = (float)(fx - ix), tz = (float)(fz - iz);
            tx = tx * tx * (3.0f - 2.0f * tx);
            tz = tz * tz * (3.0f - 2.0f * tz);
            float a = LatticeHash(ix, iz, salt), b = LatticeHash(ix + 1, iz, salt), c = LatticeHash(ix, iz + 1, salt), d = LatticeHash(ix + 1, iz + 1, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private static float LatticeHash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(z * 668265263) + (uint)(salt * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFu) / 65536.0f;
            }
        }

        /// <summary>
        /// The ground at a point. Inside a block (between the pavements) it is hardstanding -- concrete apron, a tarmac yard, setts -- and
        /// only in a block with nothing built on it, sometimes bare earth: a dense city is not standing in a field. Outside every block it is
        /// earth.
        /// </summary>
        private static Color32 GroundLook(Context ctx, double x, double z, List<int> blockIndices)
        {
            for (int i = 0; i < blockIndices.Count; i++)
            {
                PlanBlock block = ctx.Plan.Data.Blocks[blockIndices[i]];
                if (block.Outer == null || !PlanMath.PointInPolygon(new Vec2(x, z), block.Outer))
                {
                    continue;
                }
                bool built = ctx.Plan.BuildingAt(x, z, 5.0) >= 0;
                float pick = LatticeHash(blockIndices[i], 5, 19);
                float tone = 0.72f + 0.28f * PatchNoise(x, z, 17.0, 23);
                float wear = 0.35f + 0.5f * PatchNoise(x, z, 29.0, 29);
                if (built)
                {
                    // Around a building: concrete apron, tarmac, or setts.
                    int slice = pick < 0.55f ? SurfaceLook.Paving : (pick < 0.85f ? SurfaceLook.Asphalt : SurfaceLook.Setts);
                    return SurfaceLook.Pack(new Color(tone, tone, tone * 0.97f), slice, wear);
                }
                // A block with nothing on it: a tarmac yard, a paved square, or waste ground.
                int vacant = pick < 0.40f ? SurfaceLook.Asphalt : (pick < 0.65f ? SurfaceLook.Paving : (pick < 0.75f ? SurfaceLook.Setts : SurfaceLook.Earth));
                return SurfaceLook.Pack(new Color(tone, tone * 0.99f, tone * 0.95f), vacant, wear);
            }
            return EarthLook(x, z);
        }

        /// <summary>Bare ground between the streets: patchy, a little different from one stretch to the next.</summary>
        private static Color32 EarthLook(double x, double z)
        {
            float patch = PatchNoise(x, z, 22.0, 3);
            float tone = 0.70f + 0.30f * patch;
            float wear = 0.25f + 0.55f * PatchNoise(x, z, 41.0, 5);
            return SurfaceLook.Pack(new Color(tone, tone * 0.98f, tone * 0.93f), SurfaceLook.Earth, wear);
        }

        /// <summary>A road: asphalt, newer and cleaner on a highway, more worn on a street, a little different from one road to the next.</summary>
        private static Color32 AsphaltLook(RoadClass roadClass, int id)
        {
            float h = LatticeHash(id, 17, 7);
            float tone = 0.80f + 0.20f * h;
            float wear = roadClass == RoadClass.Highway ? 0.15f + 0.25f * h : (roadClass == RoadClass.Arterial ? 0.30f + 0.30f * h : 0.40f + 0.45f * h);
            return SurfaceLook.Pack(new Color(tone, tone, tone), SurfaceLook.Asphalt, wear);
        }

        /// <summary>The pavement beside a road: concrete slabs, or granite setts in some streets.</summary>
        private static Color32 PavingLook(RoadClass roadClass, int salt)
        {
            float h = LatticeHash(salt, (int)roadClass, 11);
            float tone = 0.82f + 0.18f * h;
            float wear = 0.25f + 0.5f * LatticeHash(salt, 3, 13);
            return SurfaceLook.Pack(new Color(tone, tone * 0.99f, tone * 0.96f), SurfaceLook.Paving, wear);
        }

        // ---------------------------------------------------------------- terrain

        private static void BuildTerrain(Context ctx, MeshBuffer buffer, float step, List<int> blockIndices)
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
                    buffer.Look = GroundLook(ctx, (x0 + x1) * 0.5, (z0 + z1) * 0.5, blockIndices);
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

            // The ground under an expressway is its service road: an ordinary worn road, the clean asphalt is up on the deck.
            buffer.Look = AsphaltLook(g.Elevated ? RoadClass.Arterial : g.Edge.Class, g.Edge.Id);
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
            buffer.Look = AsphaltLook(RoadClass.Street, j.Node.Id);
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

        /// <summary>The one look of a block's paving: the pavement and everything inside it up to the walls, so they read as one surface. A plaza is laid in setts.</summary>
        private static Color32 BlockPavingLook(PlanBlock block, int blockIndex)
        {
            if (block.Plaza)
            {
                float h = LatticeHash(blockIndex, 9, 23);
                return SurfaceLook.Pack(new Color(0.84f + 0.06f * h, 0.80f + 0.05f * h, 0.74f), SurfaceLook.Setts, 0.35f);
            }
            if (block.Mall)
            {
                // A mall's block is its car park, asphalt kerb to wall (BuildMallGrounds paints it).
                return SurfaceLook.Pack(new Color(0.60f, 0.60f, 0.61f), SurfaceLook.Asphalt, 0.75f);
            }
            return PavingLook(RoadClass.Street, blockIndex);
        }

        /// <summary>
        /// A paved block, kerb to walls, as ONE surface in the pavement's own look and height (PlanBlock.InnerTriangles), cut on the terrain grid so it
        /// follows the ground. Replaces the old patchwork of 4 m ground squares (tarmac, setts, earth) with staircase edges between pavement and buildings.
        /// The floors stand on it (PlanBuildings.PavingRise), so under a building it is hidden.
        /// </summary>
        private static void BuildBlockPaving(Context ctx, PlanBlock block, int blockIndex, MeshBuffer buffer, float step)
        {
            if (!block.Paved) return;
            Bounds(block.Inner, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0, bz0, bx1, bz1)) return;
            Rect2 r = ctx.Rect;
            int n = Mathf.Max(1, Mathf.RoundToInt((float)(r.X1 - r.X0) / step));
            double s = (r.X1 - r.X0) / n;
            buffer.Look = BlockPavingLook(block, blockIndex);
            Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight;
            int[] t = block.InnerTriangles;
            for (int k = 0; k + 2 < t.Length; k += 3)
            {
                var tri = new List<Vec2> { block.Inner[t[k]], block.Inner[t[k + 1]], block.Inner[t[k + 2]] };
                if (PlanMath.SignedArea(tri) < 0.0) tri.Reverse();
                Bounds(tri, out double mx, out double mz, out double Mx, out double Mz);
                if (!r.Overlaps(mx, mz, Mx, Mz)) continue;
                for (int iz = 0; iz < n; iz++)
                {
                    for (int ix = 0; ix < n; ix++)
                    {
                        // The outer cells end exactly on the chunk's own edges, so a neighbour clips at the very same line.
                        var cell = new Rect2(ix == 0 ? r.X0 : r.X0 + ix * s, iz == 0 ? r.Z0 : r.Z0 + iz * s,
                            ix == n - 1 ? r.X1 : r.X0 + (ix + 1) * s, iz == n - 1 ? r.Z1 : r.Z0 + (iz + 1) * s);
                        if (!cell.Overlaps(mx, mz, Mx, Mz)) continue;
                        List<Vec2> piece = Clip(tri, cell);
                        if (piece.Count >= 3 && PlanMath.SignedArea(piece) > SliverArea)
                        {
                            Fan(ctx, buffer, piece, height);   // a sliver where a triangle only grazes a cell is left out on both sides of a seam alike
                        }
                    }
                }
            }
        }

        /// <summary>A clipped piece of paving smaller than this (square metres) is a sliver of float noise, not ground: not drawn.</summary>
        private const double SliverArea = 1e-4;

        private static void BuildPavement(Context ctx, PlanBlock block, int blockIndex, MeshBuffer buffer, bool far)
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

                    if (!block.Paved)
                    {
                        // A paved block's pavement is part of its one paving (BuildBlockPaving); only an unpaved block keeps the ring.
                        buffer.Look = PavingLook(block.EdgeClass[e], e);
                        Fan(ctx, buffer, Clip(quad, ctx.Rect), (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight);
                    }
                    buffer.Look = SurfaceLook.Pack(new Color32(214, 212, 206, 255), (int)WallMaterial.Concrete, 0.45f);

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
                if (!far)
                {
                    AddSidewalkProps(ctx, block, e, buffer);
                }
            }
        }

        // ---------------------------------------------------------------- crossings and street furniture

        /// <summary>
        /// Zebra crossings at the mouth of most side streets: bars along the road, spaced across it, just beyond the junction. Not on a
        /// highway, and not at every one (a city with a crossing at every corner looks like a diagram).
        /// </summary>
        private static void Crossings(Context ctx, EdgeGeometry g, MeshBuffer white, Func<double, double, double> height)
        {
            if (white == null || g.Edge.Class == RoadClass.Highway)
            {
                return;
            }
            for (int end = 0; end < 2; end++)
            {
                double trim = end == 0 ? g.TrimA : g.TrimB;
                if (trim < 1.0 || LatticeHash(g.Edge.Id, end, 41) > 0.7f)
                {
                    continue;
                }
                double ta = end == 0 ? trim + 0.6 : g.Length - trim - 3.9;
                double tb = ta + 3.3;
                if (tb > g.Length - 0.5 || ta < 0.5)
                {
                    continue;
                }
                double span = g.HalfWidth - 0.9;
                for (double s = -span; s + 0.7 <= span; s += 1.4)
                {
                    Stripe(ctx, g, white, height, s + 0.35, 0.35, ta, tb);
                }
            }
        }

        private static readonly Color StreetBin = new Color(0.20f, 0.28f, 0.23f);
        private static readonly Color StreetCabinet = new Color(0.55f, 0.57f, 0.58f);
        private static readonly Color StreetHydrant = new Color(0.72f, 0.13f, 0.10f);

        /// <summary>
        /// The odd hydrant, bin and utility cabinet along a pavement: one candidate about every 36 m, clear of the corners, and not
        /// every candidate used. Sized for the 2.6 m player (everything here is about 1.3x). NO LAMP POSTS: they were tried and removed.
        /// </summary>
        private static void AddSidewalkProps(Context ctx, PlanBlock block, int e, MeshBuffer buffer)
        {
            int n = block.Poly.Count;
            int f = (e + 1) % n;
            Vec2 i0 = block.Inner[e], i1 = block.Inner[f];
            Vec2 o0 = block.Outer[e], o1 = block.Outer[f];
            double length = (i1 - i0).Length;
            if (length < 18.0)
            {
                return;
            }
            Vec2 dir = (i1 - i0).Normalized;
            int candidates = (int)(length / 36.0);
            for (int k = 0; k <= candidates; k++)
            {
                float jitter = LatticeHash(e * 131 + k, block.Poly.Count, 47);
                double along = 8.0 + (length - 16.0) * (candidates == 0 ? jitter : (k + jitter) / (candidates + 1.0));
                if (along < 8.0 || along > length - 8.0)
                {
                    continue;
                }
                float pick = LatticeHash(e * 977 + k, 3, 53);
                if (pick > 0.62f)
                {
                    continue;
                }
                double s = along / length;
                Vec2 kerb = i0 + (i1 - i0) * s;
                Vec2 outer = o0 + (o1 - o0) * s;
                Vec2 at = kerb + (outer - kerb) * 0.30;
                if (at.X < ctx.Rect.X0 || at.X >= ctx.Rect.X1 || at.Y < ctx.Rect.Z0 || at.Y >= ctx.Rect.Z1)
                {
                    continue;
                }
                double y = ctx.Plan.GroundHeight(at.X, at.Y) + RoadLift + KerbHeight;
                Vector3 position = ctx.Local(at.X, y, at.Y);

                // Built in its own frame, turned to the kerb, so it sits square to the pavement.
                buffer.SetFrame(position, dir.X, dir.Y);
                Color32 saved = buffer.Look;
                if (pick < 0.20f)
                {
                    buffer.Look = SurfaceLook.Pack(StreetHydrant, (int)WallMaterial.Metal, 0.25f);
                    buffer.AddBox(new Vector3(-0.2f, 0.0f, -0.2f), new Vector3(0.2f, 1.0f, 0.2f));
                    buffer.AddBox(new Vector3(-0.17f, 1.0f, -0.17f), new Vector3(0.17f, 1.2f, 0.17f));
                    buffer.AddBox(new Vector3(-0.3f, 0.55f, -0.1f), new Vector3(0.3f, 0.75f, 0.1f));
                }
                else if (pick < 0.46f)
                {
                    buffer.Look = SurfaceLook.Pack(StreetBin, (int)WallMaterial.Metal, 0.5f);
                    buffer.AddBox(new Vector3(-0.42f, 0.0f, -0.42f), new Vector3(0.42f, 1.25f, 0.42f));
                    buffer.Look = SurfaceLook.Pack(new Color(0.12f, 0.12f, 0.13f), (int)WallMaterial.Metal, 0.4f);
                    buffer.AddBox(new Vector3(-0.47f, 1.25f, -0.47f), new Vector3(0.47f, 1.37f, 0.47f));
                }
                else
                {
                    buffer.Look = SurfaceLook.Pack(StreetCabinet, (int)WallMaterial.Metal, 0.55f);
                    buffer.AddBox(new Vector3(-0.8f, 0.0f, -0.35f), new Vector3(0.8f, 1.8f, 0.35f));
                    buffer.Look = SurfaceLook.Pack(new Color(0.30f, 0.31f, 0.32f), (int)WallMaterial.Metal, 0.5f);
                    buffer.AddBox(new Vector3(-0.85f, 1.8f, -0.4f), new Vector3(0.85f, 1.9f, 0.4f));
                }
                buffer.Look = saved;
                buffer.ClearFrame();
            }
        }

        // ---------------------------------------------------------------- paint

        private static void BuildPaint(Context ctx, EdgeGeometry g, MeshBuffer yellow, MeshBuffer white)
        {
            Func<double, double, double> height = RoadHeight(ctx, g);
            Crossings(ctx, g, white, height);
            // The service road under a deck is painted as an arterial; the expressway's own paint is on the deck (BuildDeck).
            PaintLanes(ctx, g, yellow, white, height, g.TrimA + 1.5, g.Length - g.TrimB - 1.5, g.Elevated ? RoadClass.Arterial : g.Edge.Class, false);
        }

        /// <summary>The lane paint of one road class between two distances along the road, at the heights a surface gives; an expressway's deck gets edge lines too.</summary>
        private static void PaintLanes(Context ctx, EdgeGeometry g, MeshBuffer yellow, MeshBuffer white, Func<double, double, double> height,
            double t0, double t1, RoadClass cls, bool edgeLines)
        {
            if (t1 - t0 < 3.0) return;
            switch (cls)
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
                        if (edgeLines)
                        {
                            Line(ctx, g, white, height, g.HalfWidth - 0.5, t0, t1);
                            Line(ctx, g, white, height, -(g.HalfWidth - 0.5), t0, t1);
                        }
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
            Stripe(ctx, g, buffer, height, offset, LineWidth * 0.5, ta, tb);
        }

        private static void Stripe(Context ctx, EdgeGeometry g, MeshBuffer buffer, Func<double, double, double> height,
            double offset, double halfWidth, double ta, double tb)
        {
            Vec2 perp = g.Dir.Perp;
            StripeAlong(ctx, buffer, g.A + g.Dir * ta + perp * offset, g.A + g.Dir * tb + perp * offset, perp, halfWidth, height);
        }

        /// <summary>A stripe of paint from <paramref name="pa"/> to <paramref name="pb"/>, <paramref name="halfWidth"/> either side across <paramref name="perp"/>.</summary>
        private static void StripeAlong(Context ctx, MeshBuffer buffer, Vec2 pa, Vec2 pb, Vec2 perp, double halfWidth, Func<double, double, double> height)
        {
            var quad = new List<Vec2> { pa + perp * halfWidth, pb + perp * halfWidth, pb - perp * halfWidth, pa - perp * halfWidth };
            Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
            if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) return;
            Fan(ctx, buffer, Clip(quad, ctx.Rect), (x, z) => height(x, z) + PaintLift);
        }

        // ---------------------------------------------------------------- the expressways

        /// <summary>
        /// The deck's top along an expressway: the straight run between the heights at its two ends (PlanGeometry.DeckTop), but never under the
        /// road on the ground -- where an expressway comes down to its end, the deck lies on the service road.
        /// </summary>
        private static Func<double, double, double> DeckHeight(Context ctx, EdgeGeometry g)
        {
            return (x, z) =>
            {
                double t = ((x - g.A.X) * g.Dir.X + (z - g.A.Y) * g.Dir.Y) / g.Length;
                t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
                double top = g.DeckA + (g.DeckB - g.DeckA) * t;
                return Math.Max(top, ctx.Plan.GroundHeight(x, z) + RoadLift + 0.012);
            };
        }

        private static Color32 DeckConcrete => SurfaceLook.Pack(new Color32(198, 196, 190, 255), (int)WallMaterial.Concrete, 0.6f);

        /// <summary>
        /// THE EXPRESSWAY DECK over its service road: the carriageway on top, the slab's sides and underside, a parapet along each edge, a
        /// hammerhead pier every PierSpacing (a column on the centreline between the service road's two halves, a cross-head carrying the full
        /// width), and its lane paint.
        /// </summary>
        private static void BuildDeck(Context ctx, EdgeGeometry g, MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures,
            MeshBuffer markings, MeshBuffer laneDividers, MeshBuffer guardRail, bool far)
        {
            double t0 = g.DeckTrimA;
            double t1 = g.Length - g.DeckTrimB;
            if (t1 - t0 < 0.1) return;
            float thick = (float)ctx.Plan.Data.Config.DeckThickness;
            Func<double, double, double> height = DeckHeight(ctx, g);
            Vec2 perp = g.Dir.Perp;
            double hw = g.HalfWidth;
            Color32 concrete = DeckConcrete;

            int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
            for (int i = 0; i < pieces; i++)
            {
                double ta = t0 + (t1 - t0) * i / pieces;
                double tb = t0 + (t1 - t0) * (i + 1) / pieces;
                Vec2 ca = g.A + g.Dir * ta;
                Vec2 cb = g.A + g.Dir * tb;
                var quad = new List<Vec2> { ca + perp * hw, cb + perp * hw, cb - perp * hw, ca - perp * hw };
                Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
                if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) continue;
                List<Vec2> clipped = Clip(quad, ctx.Rect);
                ground.Look = AsphaltLook(RoadClass.Highway, g.Edge.Id);
                Fan(ctx, ground, clipped, height);
                pavement.Look = concrete;
                Fan(ctx, pavement, clipped, (x, z) => height(x, z) - thick, up: false);
                for (int side = -1; side <= 1; side += 2)
                {
                    AddSlabEdge(ctx, pavement, guardRail, ca + perp * (hw * side), cb + perp * (hw * side), new Vector3((float)(perp.X * side), 0.0f, (float)(perp.Y * side)),
                        height, thick, DeckParapet, 0.3f, false);
                }
            }

            // Piers.
            structures.Look = concrete;
            for (double t = PierSpacing * 0.5; t < g.Length; t += PierSpacing)
            {
                if (t < t0 + 3.0 || t > t1 - 3.0) continue;
                Vec2 c = g.A + g.Dir * t;
                if (!ctx.Contains(c)) continue;
                float top = (float)height(c.X, c.Y);
                float groundY = ctx.Ground(c.X, c.Y);
                float underside = top - thick;
                if (underside - groundY < 1.5f) continue;   // the deck is down on the ground here: nothing to hold up
                structures.SetFrame(ctx.Local(c.X, 0.0, c.Y), g.Dir.X, g.Dir.Y);
                float beamBottom = underside - 1.3f;
                structures.AddBox(new Vector3(-0.9f, groundY - 0.6f, -0.9f), new Vector3(0.9f, beamBottom, 0.9f));
                structures.AddBox(new Vector3(-0.9f, beamBottom, -(float)hw + 0.4f), new Vector3(0.9f, underside + 0.02f, (float)hw - 0.4f), includeBottom: true);
                structures.ClearFrame();
            }

            if (!far)
            {
                PaintLanes(ctx, g, markings, laneDividers, height, t0 + 1.0, t1 - 1.0, RoadClass.Highway, true);   // yellow ("markings"), white ("laneDividers")
            }
        }

        /// <summary>
        /// One edge of a deck or ramp slab from <paramref name="a"/> to <paramref name="b"/>: the slab's side face (down <paramref name="thick"/>
        /// from the surface, or right down to the ground for an embankment), and a parapet standing on it when there is a guard-rail buffer.
        /// </summary>
        private static void AddSlabEdge(Context ctx, MeshBuffer pavement, MeshBuffer guardRail, Vec2 a, Vec2 b, Vector3 outward,
            Func<double, double, double> height, float thick, float parapet, float parapetThickness, bool toGround)
        {
            // Even a sliver of the edge is drawn: a piece that ends a centimetre past the chunk's border must still put its border vertices in
            // this chunk, or the two chunks' surfaces no longer meet exactly on their shared edge (PlanChunkMeshTests).
            if (!ClipSegment(a, b, ctx.Rect, out Vec2 ka, out Vec2 kb) || (kb - ka).Length < 1e-6) return;
            double ya = height(ka.X, ka.Y), yb = height(kb.X, kb.Y);
            float len = (float)(kb - ka).Length;
            double ba = toGround ? ctx.Ground(ka.X, ka.Y) - 0.4 : ya - thick;
            double bb = toGround ? ctx.Ground(kb.X, kb.Y) - 0.4 : yb - thick;
            pavement.AddQuad(ctx.Local(ka.X, ba, ka.Y), ctx.Local(kb.X, bb, kb.Y), ctx.Local(kb.X, yb, kb.Y), ctx.Local(ka.X, ya, ka.Y), outward,
                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, thick), new Vector2(0, thick));
            if (guardRail == null || parapet <= 0.0f) return;
            var inset = new Vec2(outward.x, outward.z) * parapetThickness;
            Vec2 ia = ka - inset, ib = kb - inset;
            guardRail.AddQuad(ctx.Local(ka.X, ya, ka.Y), ctx.Local(kb.X, yb, kb.Y), ctx.Local(kb.X, yb + parapet, kb.Y), ctx.Local(ka.X, ya + parapet, ka.Y), outward,
                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, parapet), new Vector2(0, parapet));
            guardRail.AddQuad(ctx.Local(ib.X, yb, ib.Y), ctx.Local(ia.X, ya, ia.Y), ctx.Local(ia.X, ya + parapet, ia.Y), ctx.Local(ib.X, yb + parapet, ib.Y), -outward,
                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, parapet), new Vector2(0, parapet));
            guardRail.AddQuad(ctx.Local(ka.X, ya + parapet, ka.Y), ctx.Local(kb.X, yb + parapet, kb.Y), ctx.Local(ib.X, yb + parapet, ib.Y), ctx.Local(ia.X, ya + parapet, ia.Y), Vector3.up,
                new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
        }

        /// <summary>Where two expressway decks meet: the plate between their trimmed ends, up at the deck's height, with its underside and edges.</summary>
        private static void BuildDeckPlate(Context ctx, JunctionGeometry j, MeshBuffer ground, MeshBuffer pavement)
        {
            Vec2[] plate = j.DeckPlate;
            Bounds(plate, out double mx, out double mz, out double Mx, out double Mz);
            if (!ctx.Rect.Overlaps(mx - 1.0, mz - 1.0, Mx + 1.0, Mz + 1.0)) return;
            float thick = (float)ctx.Plan.Data.Config.DeckThickness;
            double top = j.DeckTop;
            Func<double, double, double> height = (x, z) => Math.Max(top, ctx.Plan.GroundHeight(x, z) + RoadLift + 0.012);
            Vec2 centre = j.Node.P;
            ground.Look = AsphaltLook(RoadClass.Highway, j.Node.Id);
            pavement.Look = DeckConcrete;
            for (int i = 0; i < plate.Length; i++)
            {
                var tri = new List<Vec2> { centre, plate[i], plate[(i + 1) % plate.Length] };
                List<Vec2> clipped = Clip(tri, ctx.Rect);
                Fan(ctx, ground, clipped, height);
                Fan(ctx, pavement, clipped, (x, z) => height(x, z) - thick, up: false);
                Vec2 d = plate[(i + 1) % plate.Length] - plate[i];
                if (d.Length < 0.05) continue;
                d = d.Normalized;
                AddSlabEdge(ctx, pavement, null, plate[i], plate[(i + 1) % plate.Length], new Vector3((float)d.Y, 0.0f, (float)-d.X), height, thick, 0.0f, 0.0f, false);
            }
        }

        /// <summary>
        /// An interchange ramp: its two stretches, each a sloped slab -- a viaduct on small piers where it is up in the air, an embankment with
        /// its sides down to the ground where it is low -- with a parapet along both edges and white edge lines.
        /// </summary>
        private static void BuildRamp(Context ctx, RampGeometry r, MeshBuffer ground, MeshBuffer pavement, MeshBuffer structures, MeshBuffer guardRail, MeshBuffer white, bool far)
        {
            double rh = r.HalfWidth;
            Color32 concrete = DeckConcrete;
            for (int stretch = 0; stretch < 2; stretch++)
            {
                Vec2 a = stretch == 0 ? r.Start : r.Mid;
                Vec2 b = stretch == 0 ? r.Mid : r.Foot;
                Vec2 dir = stretch == 0 ? r.DirA : r.DirB;
                Vec2 perpA = stretch == 0 ? r.PerpStart : r.PerpMid;
                Vec2 perpB = stretch == 0 ? r.PerpMid : r.PerpFoot;
                double len = (b - a).Length;
                if (len < 0.1) continue;
                double along0 = stretch == 0 ? 0.0 : r.LengthA;
                Func<double, double, double> height = (x, z) =>
                {
                    double t = ((x - a.X) * dir.X + (z - a.Y) * dir.Y) / len;
                    t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
                    return Math.Max(r.TopAt(along0 + t * len), ctx.Plan.GroundHeight(x, z) + RoadLift + 0.012);
                };
                int pieces = Math.Max(1, (int)Math.Ceiling(len / PieceLength));
                for (int i = 0; i < pieces; i++)
                {
                    double ta = len * i / pieces, tb = len * (i + 1) / pieces;
                    Vec2 ca = a + dir * ta, cb = a + dir * tb;
                    Vec2 pa = perpA * (1.0 - ta / len) + perpB * (ta / len);
                    Vec2 pb = perpA * (1.0 - tb / len) + perpB * (tb / len);
                    var quad = new List<Vec2> { ca + pa * rh, cb + pb * rh, cb - pb * rh, ca - pa * rh };
                    Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
                    if (!ctx.Rect.Overlaps(mx, mz, Mx, Mz)) continue;
                    Vec2 centre = (ca + cb) * 0.5;
                    float top = (float)height(centre.X, centre.Y);
                    bool viaduct = top - RampSlab - ctx.Ground(centre.X, centre.Y) > RampEmbankmentClearance;
                    List<Vec2> clipped = Clip(quad, ctx.Rect);
                    ground.Look = AsphaltLook(RoadClass.Arterial, r.Plan.ExitNode * 4 + stretch);
                    Fan(ctx, ground, clipped, height);
                    pavement.Look = concrete;
                    if (viaduct)
                    {
                        Fan(ctx, pavement, clipped, (x, z) => height(x, z) - RampSlab, up: false);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vec2 ea = ca + pa * (rh * side), eb = cb + pb * (rh * side);
                        Vec2 n = (pa + pb) * (0.5 * side);
                        AddSlabEdge(ctx, pavement, guardRail, ea, eb, new Vector3((float)n.X, 0.0f, (float)n.Y).normalized, height, RampSlab, 1.0f, 0.25f, !viaduct);
                    }
                    // A small pier under a viaduct piece now and then.
                    if (viaduct && (int)(ta / RampPierSpacing) != (int)(tb / RampPierSpacing) && ctx.Contains(centre))
                    {
                        structures.Look = concrete;
                        structures.SetFrame(ctx.Local(centre.X, 0.0, centre.Y), dir.X, dir.Y);
                        structures.AddBox(new Vector3(-0.5f, ctx.Ground(centre.X, centre.Y) - 0.5f, -0.5f), new Vector3(0.5f, top - RampSlab + 0.02f, 0.5f));
                        structures.ClearFrame();
                    }
                    if (white != null)
                    {
                        StripeAlong(ctx, white, ca + pa * (rh - 0.45), cb + pb * (rh - 0.45), (pa + pb) * 0.5, 0.08, height);
                        StripeAlong(ctx, white, ca - pa * (rh - 0.45), cb - pb * (rh - 0.45), (pa + pb) * 0.5, 0.08, height);
                    }
                }
            }
        }

        /// <summary>
        /// The land an interchange cuts off between a ramp, the expressway and the cross street, as every city uses it: a car park. Painted stalls
        /// in rows along the expressway, and the exit's gantry sign up on the deck where the ramp leaves it.
        /// </summary>
        private static void BuildInterchangeLand(Context ctx, RampGeometry r, int rampIndex, MeshBuffer pavement, MeshBuffer structures, MeshBuffer white)
        {
            Vec2[] sliver = r.Plan.Sliver;
            var poly = new List<Vec2>(sliver);
            if (PlanMath.SignedArea(poly) < 0.0) poly.Reverse();
            var distances = new double[poly.Count];
            for (int i = 0; i < distances.Length; i++) distances[i] = 1.5;
            var lot = new List<Vec2>();
            if (PlanMath.InsetPolygon(poly, distances, lot, out _) && lot.Count >= 3)
            {
                Bounds(lot, out double mx, out double mz, out double Mx, out double Mz);
                if (ctx.Rect.Overlaps(mx, mz, Mx, Mz))
                {
                    Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight + 0.02;
                    pavement.Look = SurfaceLook.Pack(new Color(0.66f, 0.66f, 0.66f), SurfaceLook.Asphalt, 0.8f);
                    Fan(ctx, pavement, Clip(lot, ctx.Rect), height);

                    // Stalls: rows along the lot's longest side, an aisle between every two rows.
                    int longest = 0;
                    double longestLen = 0.0;
                    for (int i = 0; i < lot.Count; i++)
                    {
                        double l = (lot[(i + 1) % lot.Count] - lot[i]).Length;
                        if (l > longestLen) { longestLen = l; longest = i; }
                    }
                    Vec2 u = (lot[(longest + 1) % lot.Count] - lot[longest]).Normalized;
                    Vec2 v = u.Perp;
                    Vec2 o = lot[longest];
                    double umin = double.MaxValue, umax = double.MinValue, vmin = double.MaxValue, vmax = double.MinValue;
                    foreach (Vec2 p in lot)
                    {
                        double pu = Vec2.Dot(p - o, u), pv = Vec2.Dot(p - o, v);
                        umin = Math.Min(umin, pu); umax = Math.Max(umax, pu);
                        vmin = Math.Min(vmin, pv); vmax = Math.Max(vmax, pv);
                    }
                    double stall = CityMeshBuilder.StallWidth, depth = CityMeshBuilder.StallDepth, aisle = CityMeshBuilder.AisleWidth;
                    int row = 0;
                    for (double rv = vmin + 1.0; rv + depth <= vmax - 1.0; rv += (row % 2 == 1 ? depth + aisle : depth), row++)
                    {
                        int stallIndex = 0;
                        for (double su = umin + 1.0; su + stall <= umax - 1.0; su += stall, stallIndex++)
                        {
                            Vec2 p0 = o + u * su + v * rv, p1 = o + u * su + v * (rv + depth);
                            if (!PlanMath.PointInPolygon(p0, lot) || !PlanMath.PointInPolygon(p1, lot)) continue;
                            if (white != null)
                            {
                                StripeAlong(ctx, white, p0, p1, u, 0.06, height);
                            }
                        }
                    }
                }
            }

            // The exit's gantry sign over the deck where the off-ramp leaves it.
            Vec2 s = r.Plan.S;
            if (r.Plan.Leaving && ctx.Contains(s))
            {
                Vec2 across = r.DirA.Perp;   // across the deck
                EdgeGeometry highway = ctx.Plan.Geometry.Edges[r.Plan.HighwayEdge];
                double hw = highway != null ? highway.HalfWidth : ctx.Plan.Data.Config.HighwayHalfWidth;
                double toward = Vec2.Dot(r.Start - s, across) > 0.0 ? 1.0 : -1.0;   // the ramp's side
                Vector3 at = ctx.Local(s.X, 0.0, s.Y);
                CityProps.AddGantrySign(structures, at.x, at.z, ctx.LocalY(r.TopStart), (float)across.X, (float)across.Y, (float)hw, (float)(toward * hw * 0.5), r.Plan.ExitNode * 7 + rampIndex);
            }
        }

        /// <summary>Now and then a row of barricades across a street: the evacuation's last cordons.</summary>
        private static void AddStreetBarricades(Context ctx, EdgeGeometry g, MeshBuffer structures)
        {
            if (g.Edge.Bridge) return;
            if (g.Length - g.TrimA - g.TrimB < 22.0) return;
            Func<double, double, double> height = RoadHeight(ctx, g);
            Vec2 perp = g.Dir.Perp;
            double hw = g.HalfWidth;
            if (g.Length > 40.0 && LatticeHash(g.Edge.Id, 7, 79) < 0.035f)
            {
                double t = g.Length * 0.5;
                Vec2 c = g.A + g.Dir * t;
                int count = (int)((2.0 * hw - 1.0) / 3.2);
                for (int i = 0; i < count; i++)
                {
                    Vec2 pos = c + perp * (-hw + 1.0 + 1.6 + i * 3.2);
                    if (!ctx.Contains(pos)) continue;
                    Vector3 at = ctx.Local(pos.X, 0.0, pos.Y);
                    CityProps.AddBarrier(structures, at.x, at.z, ctx.LocalY(height(pos.X, pos.Y)), (float)perp.X, (float)perp.Y);
                }
            }
        }

        // ---------------------------------------------------------------- mall car parks

        /// <summary>Stalls are kept this far from the mall's buildings (a drive and a walk round them) and from the block's kerb (a drive along it).</summary>
        public const double MallLotBuildingClearance = 9.0;
        public const double MallLotKerbClearance = 4.0;

        /// <summary>Every this-many stalls along a row, a planted island instead; every other island has a lamp post.</summary>
        public const int MallLotIslandEvery = 12;

        /// <summary>
        /// A mall's car park, fitted to the land round the complex (owner, 2026-10-12, with a photo of one: "a large mall should have a large parking
        /// lot", "generate a parking lot fitted to the available space around the mall"). The way real ones are laid out: DOUBLE ROWS of stalls,
        /// nose to nose, with an aisle between each pair, the aisles running toward the mall's doors so people walk along them rather than across the
        /// rows; a PLANTED ISLAND at intervals along each double row, LAMP POSTS on the islands; a drive kept round the buildings and along the kerb.
        /// The grid is lined up with the mall's spine and anchored to the block, so every chunk paints its part of the same rows. Stalls are painted
        /// only where both ends lie on the block clear of every building, so the rows stop where the space does.
        /// </summary>
        private static void BuildMallGrounds(Context ctx, PlanBlock block, int blockIndex, MeshBuffer structures, MeshBuffer white)
        {
            List<Vec2> ring = block.Inner;
            if (ring == null || ring.Count < 3) return;
            Bounds(ring, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0, bz0, bx1, bz1)) return;
            BuildingSite[] sites = ctx.Plan.MallSites(blockIndex);
            if (sites.Length == 0) return;

            // The grid: along the spine (the first corridor that is not a wing), its +x along the frontage and +v out of its front.
            BuildingSite spine = sites[0];
            foreach (BuildingSite site in sites)
            {
                if (site.IsMall && !site.Wing)
                {
                    spine = site;
                    break;
                }
            }
            var u = new Vec2(spine.AxisX, spine.AxisZ);
            var v = new Vec2(-spine.AxisZ, spine.AxisX);
            var o = new Vec2(spine.CentreX, spine.CentreZ);
            double stall = CityMeshBuilder.StallWidth, depth = CityMeshBuilder.StallDepth, aisle = CityMeshBuilder.AisleWidth;
            double module = 2.0 * depth + aisle;   // a double row and its aisle, across the u direction

            // The chunk's square in grid coordinates, a module wide all round.
            double umin = double.MaxValue, umax = double.MinValue, vmin = double.MaxValue, vmax = double.MinValue;
            foreach (Vec2 corner in new[] { new Vec2(ctx.Rect.X0, ctx.Rect.Z0), new Vec2(ctx.Rect.X1, ctx.Rect.Z0), new Vec2(ctx.Rect.X1, ctx.Rect.Z1), new Vec2(ctx.Rect.X0, ctx.Rect.Z1) })
            {
                double cu = Vec2.Dot(corner - o, u), cv = Vec2.Dot(corner - o, v);
                umin = Math.Min(umin, cu);
                umax = Math.Max(umax, cu);
                vmin = Math.Min(vmin, cv);
                vmax = Math.Max(vmax, cv);
            }
            Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight;
            Vec2 At(double gu, double gv) => o + u * gu + v * gv;

            bool Clear(Vec2 p)
            {
                if (!PlanMath.PointInPolygon(p, ring)) return false;
                for (int i = 0; i < ring.Count; i++)
                {
                    if (PlanMath.DistanceToSegment(p, ring[i], ring[(i + 1) % ring.Count], out _) < MallLotKerbClearance) return false;
                }
                foreach (BuildingSite site in sites)
                {
                    site.WorldToLocal(p.X, p.Y, out double lx, out double lz);
                    if (Math.Abs(lx) < site.Params.Width * 0.5 + MallLotBuildingClearance && Math.Abs(lz) < site.Params.Depth * 0.5 + MallLotBuildingClearance) return false;
                }
                return true;
            }

            int m0 = (int)Math.Floor((umin - module) / module), m1 = (int)Math.Ceiling((umax + module) / module);
            int s0 = (int)Math.Floor((vmin - stall) / stall), s1 = (int)Math.Ceiling((vmax + stall) / stall);
            for (int m = m0; m <= m1; m++)
            {
                double ua = m * module;            // the double row: stalls from ua to ua + 2*depth, nose to nose at ua + depth; the aisle beyond
                double ub = ua + 2.0 * depth;
                for (int j = s0; j <= s1; j++)
                {
                    double va = j * stall;
                    bool island = ((j % MallLotIslandEvery) + MallLotIslandEvery) % MallLotIslandEvery == 0;
                    if (island)
                    {
                        // A planted island across the double row, a lamp post on every other one.
                        Vec2 c = At((ua + ub) * 0.5, va + stall * 0.5);
                        if (!ctx.Contains(c) || !Clear(At(ua + 0.5, va + stall * 0.5)) || !Clear(At(ub - 0.5, va + stall * 0.5))) continue;
                        Vector3 at = ctx.Local(c.X, 0.0, c.Y);
                        float y = ctx.LocalY(height(c.X, c.Y));
                        CityProps.AddPlanter(structures, at.x, at.z, y, (float)(depth - 0.4), (float)(stall * 0.5 - 0.3), 0.25f, (float)u.X, (float)u.Y);
                        if ((((j / MallLotIslandEvery) + m) & 1) == 0)
                        {
                            CityProps.AddLampPost(structures, at.x, at.z, y, 9.0f, (float)u.X, (float)u.Y);
                        }
                        continue;
                    }
                    // The line between this stall and the next, right across the double row (a stall either side of the nose line).
                    Vec2 pa = At(ua, va), pb = At(ub, va);
                    if (white == null || !Clear(pa) || !Clear(pb)) continue;
                    StripeAlong(ctx, white, pa, pb, v, 0.06, height);
                }
            }
            structures.Look = SurfaceLook.Default;
        }

        // ---------------------------------------------------------------- plazas

        /// <summary>
        /// A pedestrian plaza's furniture, Whyte's way: sittable edges (benches along every side, facing in), planters at the corners, bollards along
        /// the kerb, a feature in the middle (a dry fountain, or its needle alone) with benches round it, a shuttered kiosk on the longest side --
        /// and the rubble the years have left.
        /// </summary>
        private static void BuildPlazaFurniture(Context ctx, PlanBlock block, int blockIndex, MeshBuffer structures)
        {
            List<Vec2> ring = block.Inner;
            if (ring == null || ring.Count < 3) return;
            Bounds(ring, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0 - 1.0, bz0 - 1.0, bx1 + 1.0, bz1 + 1.0)) return;
            Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight;
            Vec2 centre = PlanMath.Centroid(ring);
            uint h = (uint)Mathf.FloorToInt(LatticeHash(blockIndex, 11, 113) * 1000.0f);
            int n = ring.Count;
            int longest = 0;
            double longestLen = 0.0;
            for (int e = 0; e < n; e++)
            {
                double l = (ring[(e + 1) % n] - ring[e]).Length;
                if (l > longestLen) { longestLen = l; longest = e; }
            }
            double extent = Math.Sqrt(block.Area);

            bool Inside(Vec2 p, double margin) => PlanMath.PointInPolygon(p, ring) && PlanMath.PointInPolygon(p + new Vec2(margin, margin), ring)
                && PlanMath.PointInPolygon(p - new Vec2(margin, margin), ring) && PlanMath.PointInPolygon(p + new Vec2(margin, -margin), ring)
                && PlanMath.PointInPolygon(p + new Vec2(-margin, margin), ring);

            // The eight compass directions, for the things set round the middle.
            Vec2[] compass =
            {
                new Vec2(1.0, 0.0), new Vec2(0.7071, 0.7071), new Vec2(0.0, 1.0), new Vec2(-0.7071, 0.7071),
                new Vec2(-1.0, 0.0), new Vec2(-0.7071, -0.7071), new Vec2(0.0, -1.0), new Vec2(0.7071, -0.7071),
            };

            // The middle.
            if (ctx.Contains(centre) && Inside(centre, 4.5))
            {
                Vector3 at = ctx.Local(centre.X, 0.0, centre.Y);
                float y = ctx.LocalY(height(centre.X, centre.Y));
                float radius = (float)Math.Min(3.4, 0.11 * extent);
                if (h % 3u != 0u)
                {
                    CityProps.AddFountain(structures, at.x, at.z, y, radius, h % 3u == 1u);
                }
                else
                {
                    CityProps.AddPlanter(structures, at.x, at.z, y, radius, radius, 0.8f);
                }
                for (int k = 0; k < 4; k++)
                {
                    Vec2 d = compass[k * 2];   // 90 degrees apart
                    Vec2 p = centre + d * (radius + 2.6);
                    if (!ctx.Contains(p) || !Inside(p, 1.5)) continue;
                    // A bench's seat faces the +z of its frame, which is its +x turned a quarter: a frame of +x = d.Perp faces -d, the middle.
                    Vec2 frame = d.Perp;
                    Vector3 b = ctx.Local(p.X, 0.0, p.Y);
                    CityProps.AddBench(structures, b.x, b.z, ctx.LocalY(height(p.X, p.Y)), (float)frame.X, (float)frame.Y);
                }
            }

            for (int e = 0; e < n; e++)
            {
                Vec2 i0 = ring[e], i1 = ring[(e + 1) % n];
                double len = (i1 - i0).Length;
                if (len < 6.0) continue;
                Vec2 dir = (i1 - i0) / len;
                Vec2 inward = dir.Perp;   // the kerb ring is counter-clockwise: the plaza is on the left of each edge
                for (double t = 1.6; t < len - 1.2; t += 3.0)
                {
                    Vec2 p = i0 + dir * t + inward * 0.45;
                    if (!ctx.Contains(p)) continue;
                    Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                    CityProps.AddBollard(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)));
                }
                for (double t = 5.0; t < len - 4.0; t += 7.5)
                {
                    // Along the edge, the seat facing in (the frame's +z is dir.Perp, the inward side).
                    Vec2 p = i0 + dir * t + inward * 2.2;
                    if (!ctx.Contains(p) || !Inside(p, 1.4)) continue;
                    Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                    CityProps.AddBench(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)), (float)dir.X, (float)dir.Y);
                }
                foreach (double t in new[] { 2.4, len - 2.4 })
                {
                    Vec2 p = i0 + dir * t + inward * 2.3;
                    if (!ctx.Contains(p) || !Inside(p, 1.5)) continue;
                    Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                    CityProps.AddPlanter(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)), 1.2f, 0.6f, 0.7f, (float)dir.X, (float)dir.Y);
                }
                if (e == longest && len > 20.0)
                {
                    Vec2 p = i0 + dir * (len * 0.5) + inward * 4.6;
                    if (ctx.Contains(p) && Inside(p, 2.0))
                    {
                        Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                        CityProps.AddKiosk(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)), (float)dir.X, (float)dir.Y, blockIndex);
                    }
                }
            }

            // What the years left.
            for (int k = 0; k < 2; k++)
            {
                Vec2 d = compass[(int)((h / (uint)(k + 1)) % 8u)];
                Vec2 p = centre + d * (extent * (0.18 + 0.1 * k));
                if (!ctx.Contains(p) || !Inside(p, 1.8)) continue;
                Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                CityProps.AddRubble(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)), 1.3f, blockIndex * 7 + k);
            }
            structures.Look = SurfaceLook.Default;
        }

        // ---------------------------------------------------------------- bridges

        /// <summary>The underside and sides of a bridge deck, its pavement, and a parapet along each side.</summary>
        private static void BuildBridgeParts(Context ctx, EdgeGeometry g, MeshBuffer pavement, MeshBuffer structures, MeshBuffer guardRail)
        {
            // A bridge is concrete, a little weathered.
            pavement.Look = SurfaceLook.Pack(new Color32(206, 204, 198, 255), (int)WallMaterial.Concrete, 0.5f);
            structures.Look = pavement.Look;
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
