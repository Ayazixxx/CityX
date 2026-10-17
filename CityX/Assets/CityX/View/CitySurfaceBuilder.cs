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
    public static partial class CitySurfaceBuilder
    {
        /// <summary>A road floats this far above the terrain under it, so the two never z-fight.</summary>
        public const float RoadLift = 0.07f;

        /// <summary>How far the pavement stands above the road: the kerb height.</summary>
        public const float KerbHeight = 0.16f;

        /// <summary>
        /// Lane paint floats this far above the road (not lower: it would vanish into the road where two ribbons overlap). 2 cm until 2026-10-09; the
        /// hillier spawn of seed 61839025 buried a tenth of its paint at 2 cm (RoadPaintTests), so 2.5; seed 83920571's buried 9 % at 2.5 against 7 %
        /// at 3, so 3 (still nothing a 2.6 m body's eye sees as floating).
        /// </summary>
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
            MeshBuffer laneDividers, MeshBuffer guardRail, bool far, MeshBuffer lit = null)
        {
            PlanWorld plan = world.Plan;
            double size = PlanWorld.ChunkSize;
            var rect = new Rect2(chunkX * size, chunkY * size, (chunkX + 1) * size, (chunkY + 1) * size);
            var ctx = new Context(plan, origin, rect);

            float step = far ? FarTerrainStep : TerrainStep;
            // The ground is cut away over every basement (BasementCuts): it ran through them a foot under the lobby floor; and under every garage
            // (GarageEntrances: its lowest deck is at the street, dug into the hill).
            var drives = new List<Driveway>();
            List<Vec2[]> basements = BasementCuts(ctx, chunkX, chunkY, drives);
            // ... and under every road and junction: they lay their own surface, graded straight across a piece, and on a curving hillside the
            // ground (sampled every 4 m) bulged up through it by as much as 0.7 m, and fought it where the two lay within a few centimetres (owner,
            // 2026-10-16: "on areas where the terrain slopes there are often times different colored textures poking through"). RoadCuts, and the
            // skirt down each road's edge to the ground beside it (RoadSkirt).
            List<Vec2[]> terrainCuts = RoadCuts(ctx, chunkX, chunkY);
            terrainCuts.AddRange(basements);
            // The stairways through the steep blocks (CitySurfaceBuilder.StairLanes.cs) are cut out of the ground and the paving, their treads
            // sinking into the slope where it steepens. (The sidewalks beside steep roads were laid as steps for a day; the owner had them
            // taken out, 2026-10-16: "the along side the road stairs look like shit and serve no purpose". They are sloping paving again.)
            var stairCuts = new List<Vec2[]>();
            List<StairLane> stairLanes = StairLanesHere(ctx, chunkX, chunkY, stairCuts);
            terrainCuts.AddRange(stairCuts);
            BuildTerrain(ctx, pavement, step, plan.BlocksIn(chunkX, chunkY), terrainCuts);
            List<Vec2[]> cuts = KerbCuts(ctx, chunkX, chunkY);
            List<Vec2[]> pavingCuts = cuts;
            if (basements.Count > 0 || stairCuts.Count > 0)
            {
                pavingCuts = new List<Vec2[]>(cuts);
                pavingCuts.AddRange(basements);
                pavingCuts.AddRange(stairCuts);
            }

            foreach (int blockIndex in plan.BlocksIn(chunkX, chunkY))
            {
                PlanBlock block = plan.Data.Blocks[blockIndex];
                BuildPavement(ctx, block, blockIndex, pavement, far, pavingCuts);
                BuildBlockPaving(ctx, block, blockIndex, pavement, step, pavingCuts);
                if (block.Plaza && !far)
                {
                    BuildOpenSpace(ctx, block, blockIndex, structures, lit);   // CitySurfaceBuilder.OpenSpace.cs: the block's role and its pieces
                }
                if (block.Mall && !far)
                {
                    BuildMallGrounds(ctx, block, blockIndex, structures, laneDividers);   // white paint (the buffer named for the lane dividers draws white, "markings" yellow)
                }
            }
            BuildDriveways(ctx, drives, pavement);   // CitySurfaceBuilder.Garages.cs: the drives into the garages, over their cuts
            BuildStairLanes(ctx, stairLanes, pavement, far);

            foreach (int edgeId in plan.EdgesIn(chunkX, chunkY))
            {
                EdgeGeometry g = plan.Geometry.Edges[edgeId];
                if (g == null) continue;
                if (StepStreets.Is(plan, g.Edge))
                {
                    // Too steep to drive: steps from plate to plate, bollards across its ends (CitySurfaceBuilder.StepStreets.cs). No paint.
                    BuildStepStreet(ctx, g, pavement, structures, far);
                    continue;
                }
                BuildRoad(ctx, g, ground, step);
                if (g.Edge.Bridge)
                {
                    BuildBridgeParts(ctx, g, pavement, structures, guardRail);
                }
                if (!far)
                {
                    BuildPaint(ctx, g, markings, laneDividers);
                    AddStreetBarricades(ctx, g, structures);
                    if (PedestrianStreets.Is(plan, g.Edge))
                    {
                        BuildPedestrianStreet(ctx, g, structures, lit);   // CitySurfaceBuilder.Pedestrian.cs: bollards across its ends, furniture down its middle
                    }
                }
            }

            foreach (int nodeId in plan.JunctionsIn(chunkX, chunkY))
            {
                JunctionGeometry j = plan.Geometry.Junctions[nodeId];
                if (j == null) continue;
                if (j.Plate != null)
                {
                    BuildPlate(ctx, j, ground, step);
                }
            }

            // The expressways over it all: decks, ramps and connectors, piers, signs (yellow paint is the "markings" buffer, white the dividers).
            BuildExpressways(ctx, chunkX, chunkY, ground, pavement, structures, markings, laneDividers, guardRail, far);

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
            float wear = roadClass == RoadClass.Boulevard ? 0.22f + 0.25f * h : (roadClass == RoadClass.Arterial ? 0.30f + 0.30f * h : 0.40f + 0.45f * h);
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

        private static void BuildTerrain(Context ctx, MeshBuffer buffer, float step, List<int> blockIndices, List<Vec2[]> basements = null)
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
                    if (basements != null && basements.Count > 0 && Touches(basements, x0, z0, x1, z1))
                    {
                        // Over a basement: the cell less the footprints over it, each piece at the cell's own surface (bilinear in its corners,
                        // so straight along its edges like the whole cells beside it, and no seam opens between them).
                        double h00 = heights[ix, iz], h01 = heights[ix, iz + 1], h11 = heights[ix + 1, iz + 1], h10 = heights[ix + 1, iz];
                        double cx0 = x0, cz0 = z0, cs = s;
                        Func<double, double, double> surface = (x, z) =>
                        {
                            double u = (x - cx0) / cs, v = (z - cz0) / cs;
                            return h00 * (1.0 - u) * (1.0 - v) + h10 * u * (1.0 - v) + h01 * (1.0 - u) * v + h11 * u * v;
                        };
                        var cell = new List<Vec2> { new Vec2(x0, z0), new Vec2(x1, z0), new Vec2(x1, z1), new Vec2(x0, z1) };
                        foreach (List<Vec2> part in Subtract(cell, basements))
                        {
                            Fan(ctx, buffer, part, surface);
                        }
                        continue;
                    }
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

        private static Func<double, double, double> RoadHeight(Context ctx, EdgeGeometry g) => RoadHeight(ctx.Plan, g);

        private static Func<double, double, double> RoadHeight(PlanWorld plan, EdgeGeometry g)
        {
            if (!g.Edge.Bridge)
            {
                return (x, z) => plan.GroundHeight(x, z) + RoadLift;
            }
            double hA = plan.GroundHeight(g.A.X, g.A.Y) + RoadLift;
            double hB = plan.GroundHeight(g.B.X, g.B.Y) + RoadLift;
            return (x, z) =>
            {
                double t = ((x - g.A.X) * g.Dir.X + (z - g.A.Y) * g.Dir.Y) / g.Length;
                t = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
                return hA + (hB - hA) * t;
            };
        }

        /// <summary>
        /// The ground under every road and junction plate that reaches into this chunk, as convex pieces to cut out of the terrain: each ribbon
        /// piece as BuildRoad lays it, each plate as BuildPlate fans it. Not under a bridge (its deck stands over the dip it spans).
        /// </summary>
        private static List<Vec2[]> RoadCuts(Context ctx, int chunkX, int chunkY)
        {
            var cuts = new List<Vec2[]>();
            foreach (int edgeId in ctx.Plan.EdgesIn(chunkX, chunkY))
            {
                EdgeGeometry g = ctx.Plan.Geometry.Edges[edgeId];
                if (g == null || g.Edge.Bridge) continue;
                double t0 = g.TrimA, t1 = g.Length - g.TrimB;
                if (t1 - t0 < 0.05) continue;
                Vec2 perp = g.Dir.Perp * g.HalfWidth;
                int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
                for (int i = 0; i < pieces; i++)
                {
                    Vec2 ca = g.A + g.Dir * (t0 + (t1 - t0) * i / pieces);
                    Vec2 cb = g.A + g.Dir * (t0 + (t1 - t0) * (i + 1) / pieces);
                    var quad = new[] { ca + perp, cb + perp, cb - perp, ca - perp };
                    Bounds(quad, out double mx, out double mz, out double Mx, out double Mz);
                    if (ctx.Rect.Overlaps(mx, mz, Mx, Mz)) cuts.Add(quad);
                }
            }
            foreach (int nodeId in ctx.Plan.JunctionsIn(chunkX, chunkY))
            {
                JunctionGeometry j = ctx.Plan.Geometry.Junctions[nodeId];
                if (j?.Plate == null) continue;
                for (int i = 0; i < j.Plate.Length; i++)
                {
                    var tri = new[] { j.Node.P, j.Plate[i], j.Plate[(i + 1) % j.Plate.Length] };
                    if (Math.Abs(PlanMath.SignedArea(tri)) < 1e-4) continue;
                    Bounds(tri, out double mx, out double mz, out double Mx, out double Mz);
                    if (ctx.Rect.Overlaps(mx, mz, Mx, Mz)) cuts.Add(tri);
                }
            }
            return cuts;
        }

        /// <summary>
        /// The terrain mesh's own height at a point (BuildTerrain: the ground sampled on the step's grid, each cell split corner (x0, z0) to
        /// (x1, z1)), not the smooth ground under it.
        /// </summary>
        private static double TerrainMeshHeight(Context ctx, double x, double z, float step)
        {
            double s = PlanWorld.ChunkSize / Math.Max(1, Math.Round(PlanWorld.ChunkSize / step));
            double x0 = Math.Floor(x / s) * s, z0 = Math.Floor(z / s) * s;
            double u = (x - x0) / s, v = (z - z0) / s;
            double ha = ctx.Plan.GroundHeight(x0, z0), hb = ctx.Plan.GroundHeight(x0, z0 + s);
            double hc = ctx.Plan.GroundHeight(x0 + s, z0 + s), hd = ctx.Plan.GroundHeight(x0 + s, z0);
            return v >= u ? ha + (hc - hb) * u + (hb - ha) * v : ha + (hd - ha) * u + (hc - hd) * v;
        }

        /// <summary>How far a road's skirt reaches past the ground beside it, so no sliver of the cut under the road shows.</summary>
        private const float SkirtPastGround = 0.15f;

        /// <summary>... and stands this far back from the road's edge, behind a kerb's face (which is in the edge's plane) and under the pavement.</summary>
        private const double SkirtSetBack = 0.02;

        /// <summary>
        /// A road's skirt along one edge (the part in this chunk): a face down (or up) from the road's edge to the ground's mesh beside it and on
        /// past it, both ways round, the terrain under the road being cut away. Under a pavement it is hidden; at the edge of town, or wherever the
        /// road's straight piece and the ground part, it is the edge of the road's bed.
        /// </summary>
        private static void RoadSkirt(Context ctx, MeshBuffer buffer, Vec2 a, Vec2 b, Vec2 towardRoad, Func<double, double, double> height, float step)
        {
            if (!ClipSegment(a, b, ctx.Rect, out Vec2 ca, out Vec2 cb) || (cb - ca).Length < 0.02) return;
            int pieces = Math.Max(1, (int)Math.Ceiling((cb - ca).Length / TerrainStep));
            var inward = new Vector3((float)towardRoad.X, 0.0f, (float)towardRoad.Y);
            if (inward.sqrMagnitude < 1e-8f) return;
            inward.Normalize();
            // Set back a hair from the road's edge: where a kerb runs along it, the kerb's face is in that very plane, and the two fought.
            Vec2 back = towardRoad.Normalized * -SkirtSetBack;
            for (int i = 0; i < pieces; i++)
            {
                Vec2 p = ca + (cb - ca) * ((double)i / pieces) + back, q = ca + (cb - ca) * ((double)(i + 1) / pieces) + back;
                double rp = height(p.X, p.Y), rq = height(q.X, q.Y);
                double gp = TerrainMeshHeight(ctx, p.X, p.Y, step), gq = TerrainMeshHeight(ctx, q.X, q.Y, step);
                double topP = Math.Max(rp, gp), topQ = Math.Max(rq, gq);
                double lowP = Math.Min(rp, gp) - SkirtPastGround, lowQ = Math.Min(rq, gq) - SkirtPastGround;
                Vector3 p0 = ctx.Local(p.X, lowP, p.Y), q0 = ctx.Local(q.X, lowQ, q.Y);
                Vector3 p1 = ctx.Local(p.X, topP, p.Y), q1 = ctx.Local(q.X, topQ, q.Y);
                float len = (float)(q - p).Length;
                // Seen from the road where the bank stands over it (a cutting), from outside where the road stands over the ground (its bed).
                Vector3 facing = gp + gq > rp + rq ? inward : -inward;
                buffer.AddQuad(p0, q0, q1, p1, facing, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, 1), new Vector2(0, 1));
            }
        }

        private static void BuildRoad(Context ctx, EdgeGeometry g, MeshBuffer buffer, float step)
        {
            double t0 = g.TrimA;
            double t1 = g.Length - g.TrimB;
            if (t1 - t0 < 0.05) return;
            Vec2 perp = g.Dir.Perp * g.HalfWidth;
            Func<double, double, double> height = RoadHeight(ctx, g);

            // A pedestrian street (PedestrianStreets) is laid in setts at the PAVEMENT's height, flush with the pavements either side (BuildPavement
            // draws no kerb face along it); the junction plates at its ends stay asphalt, a kerb's step below.
            bool walk = PedestrianStreets.Is(ctx.Plan, g.Edge);
            if (walk)
            {
                Func<double, double, double> road = height;
                height = (x, z) => road(x, z) + KerbHeight;
            }
            buffer.Look = walk ? PedestrianStreetLook(g.Edge.Id) : AsphaltLook(g.Edge.Class, g.Edge.Id);
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
                // Its two edges' skirts, down to the ground the terrain is cut back to (a bridge's deck has its own sides).
                if (!g.Edge.Bridge)
                {
                    RoadSkirt(ctx, buffer, ca + perp, cb + perp, -perp, height, step);
                    RoadSkirt(ctx, buffer, ca - perp, cb - perp, perp, height, step);
                }
            }
        }

        private static void BuildPlate(Context ctx, JunctionGeometry j, MeshBuffer buffer, float step)
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
                // Its outline's skirt (along the roads' ends it lies under the ribbons, hidden).
                RoadSkirt(ctx, buffer, plate[i], plate[(i + 1) % plate.Length], centre - (plate[i] + plate[(i + 1) % plate.Length]) * 0.5, height, step);
            }
        }

        // ---------------------------------------------------------------- pavement and kerb

        /// <summary>
        /// The one look of a block's paving: the pavement and everything inside it up to the walls, so they read as one surface. An open block is
        /// laid by its role (setts for the square, earth for the vacant lot: OpenSpacePavingLook).
        /// </summary>
        private static Color32 BlockPavingLook(PlanWorld plan, PlanBlock block, int blockIndex)
        {
            if (block.Plaza)
            {
                return OpenSpacePavingLook(plan, blockIndex);
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
        private static void BuildBlockPaving(Context ctx, PlanBlock block, int blockIndex, MeshBuffer buffer, float step, List<Vec2[]> cuts)
        {
            if (!block.Paved) return;
            Bounds(block.Inner, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0, bz0, bx1, bz1)) return;
            Rect2 r = ctx.Rect;
            int n = Mathf.Max(1, Mathf.RoundToInt((float)(r.X1 - r.X0) / step));
            double s = (r.X1 - r.X0) / n;
            buffer.Look = BlockPavingLook(ctx.Plan, block, blockIndex);
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
                            // A sliver where a triangle only grazes a cell is left out on both sides of a seam alike; where a ramp comes down
                            // onto the street the paving is cut away under it.
                            foreach (List<Vec2> part in Subtract(piece, cuts))
                            {
                                Fan(ctx, buffer, part, height);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>A clipped piece of paving smaller than this (square metres) is a sliver of float noise, not ground: not drawn.</summary>
        private const double SliverArea = 1e-4;

        // ---------------------------------------------------------------- basements: the ground cut away over them

        /// <summary>How many chunks from its centre's chunk a building's footprint can reach (the largest blocks are some 70 m across).</summary>
        private const int BasementReachChunks = 4;

        /// <summary>... and a garage's (one runs 360 m along its street).</summary>
        private const int GarageReachChunks = 8;

        /// <summary>
        /// Whether a building has a basement under it the ground must keep out of: an ordinary box building on the ground with one in its plan
        /// (CityMeshBuilder.BuildBasement). Garages, car parks, malls, round towers and sealed blocks are built without one; a block standing on
        /// another's roof has its "basement" inside that one.
        /// </summary>
        public static bool HasBasementUnder(BuildingSite site) =>
            site.Exists && site.Params.HasBasement && site.Sides < 3 && !site.Shell && !site.OnRoof && !site.IsOpenGround && !site.IsParkingGarage
            && !site.IsMall && !site.IsMallAnchor;

        /// <summary>A building's footprint on the ground, counter-clockwise.</summary>
        public static Vec2[] Footprint(BuildingSite site)
        {
            double hw = site.Params.Width * 0.5, hd = site.Params.Depth * 0.5;
            var poly = new Vec2[4];
            int i = 0;
            foreach ((double lx, double lz) in new[] { (-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd) })
            {
                site.LocalToWorld(lx, lz, out double x, out double z);
                poly[i++] = new Vec2(x, z);
            }
            if (PlanMath.SignedArea(poly) < 0.0)
            {
                System.Array.Reverse(poly);
            }
            return poly;
        }

        /// <summary>
        /// The footprints of the buildings with a basement that reach into this chunk. The terrain and the block's paving are cut away over them:
        /// they ran right through every basement, a foot or so under the lobby floor, so its stairs led down into the street's paving (owner,
        /// 2026-10-16: "the basement is only 1 foot high"). Nothing is seen through the cut: the lobby floor closes it from above and the walls
        /// all round, and the basement's own floor and walls are a storey down.
        /// </summary>
        private static List<Vec2[]> BasementCuts(Context ctx, int chunkX, int chunkY, List<Driveway> drives = null)
        {
            var cuts = new List<Vec2[]>();
            for (int dy = -GarageReachChunks; dy <= GarageReachChunks; dy++)
            {
                for (int dx = -GarageReachChunks; dx <= GarageReachChunks; dx++)
                {
                    List<int> owned = ctx.Plan.BuildingsOwnedBy(chunkX + dx, chunkY + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        BuildingSite site = ctx.Plan.Buildings[owned[i]];
                        if (site.IsParkingGarage && site.Exists)
                        {
                            // A garage's lowest deck is at its street (GarageEntrances), below the top of its lot's ground on a hillside, and a drive
                            // runs to each opening from the kerb: the ground and the paving are cut away under the whole garage and each drive, or
                            // the hill comes up through them (CitySurfaceBuilder.Garages.cs). Its walls and podium close the cut.
                            if (drives != null) AddGarageCuts(ctx, site, cuts, drives);
                            continue;
                        }
                        if (site.IsOpenGround && site.Exists)
                        {
                            // An open car park lays its own asphalt on its own grid: the ground and paving under it stood through it on a hill and
                            // its paint flickered in and out (owner, 2026-10-16). Cut away; its wall and its entrance's edge close the cut.
                            Vec2[] lot = Footprint(site);
                            Bounds(lot, out double lx0, out double lz0, out double lx1, out double lz1);
                            if (ctx.Rect.Overlaps(lx0, lz0, lx1, lz1)) cuts.Add(lot);
                            continue;
                        }
                        if (!HasBasementUnder(site) || System.Math.Abs(dx) > BasementReachChunks || System.Math.Abs(dy) > BasementReachChunks)
                        {
                            continue;
                        }
                        Vec2[] poly = Footprint(site);
                        Bounds(poly, out double x0, out double z0, out double x1, out double z1);
                        if (ctx.Rect.Overlaps(x0, z0, x1, z1))
                        {
                            cuts.Add(poly);
                        }
                    }
                }
            }
            return cuts;
        }

        /// <summary>Whether any of the polygons' bounds overlap a cell.</summary>
        private static bool Touches(List<Vec2[]> polys, double x0, double z0, double x1, double z1)
        {
            for (int i = 0; i < polys.Count; i++)
            {
                Bounds(polys[i], out double px, out double pz, out double Px, out double Pz);
                if (Px > x0 && px < x1 && Pz > z0 && pz < z1)
                {
                    return true;
                }
            }
            return false;
        }

        private static void BuildPavement(Context ctx, PlanBlock block, int blockIndex, MeshBuffer buffer, bool far, List<Vec2[]> cuts)
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
                // Along a step street the steps close their own sides (CitySurfaceBuilder.StepStreets.cs): no kerb between its junction plates.
                RoadEdge road = ctx.Plan.EdgeBetween(block.Poly[e], block.Poly[f]);
                EdgeGeometry steps = road != null && StepStreets.Is(ctx.Plan, road) ? ctx.Plan.Geometry.Edges[road.Id] : null;
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
                        foreach (List<Vec2> part in Subtract(Clip(quad, ctx.Rect), cuts))
                        {
                            Fan(ctx, buffer, part, (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight);
                        }
                    }
                    buffer.Look = SurfaceLook.Pack(new Color32(214, 212, 206, 255), (int)WallMaterial.Concrete, 0.45f);

                    // The kerb: a vertical face along the inner edge, from the road up to the pavement, facing the road -- not where a ramp crosses it,
                    // and not along a pedestrian street, whose setts lie flush with the pavement.
                    bool flush = PedestrianStreets.Along(ctx.Plan, block.Poly[e], block.Poly[f]);
                    if (!flush && ClipSegment(ia, ib, ctx.Rect, out Vec2 ca, out Vec2 cb) && (cb - ca).Length > 0.02)
                    foreach ((Vec2 ka, Vec2 kb) in OffTheSteps(OutsideCuts(ca, cb, cuts), steps))
                    {
                        if ((kb - ka).Length < 0.02) continue;
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
                    AddSidewalkProps(ctx, block, e, buffer, cuts);
                }
            }
        }

        /// <summary>
        /// The parts of kerb segments beside a road that lie off a step street's steps (between its trims, where the junction plates end): all
        /// of them when <paramref name="steps"/> is null.
        /// </summary>
        private static IEnumerable<(Vec2 a, Vec2 b)> OffTheSteps(List<(Vec2 a, Vec2 b)> segments, EdgeGeometry steps)
        {
            foreach ((Vec2 a, Vec2 b) in segments)
            {
                if (steps == null)
                {
                    yield return (a, b);
                    continue;
                }
                double ta = Vec2.Dot(a - steps.A, steps.Dir), tb = Vec2.Dot(b - steps.A, steps.Dir);
                double t0 = steps.TrimA, t1 = steps.Length - steps.TrimB;
                if (Math.Abs(tb - ta) < 1e-9)
                {
                    if (ta < t0 || ta > t1) yield return (a, b);
                    continue;
                }
                // The segment's own fractions where the steps begin and end, in order along it.
                double u0 = (t0 - ta) / (tb - ta), u1 = (t1 - ta) / (tb - ta);
                double lo = Math.Max(0.0, Math.Min(u0, u1)), hi = Math.Min(1.0, Math.Max(u0, u1));
                if (lo >= hi)
                {
                    yield return (a, b);   // wholly off the steps
                    continue;
                }
                if (lo > 0.0) yield return (a, a + (b - a) * lo);
                if (hi < 1.0) yield return (a + (b - a) * hi, b);
            }
        }

        // ---------------------------------------------------------------- crossings and street furniture

        /// <summary>
        /// Zebra crossings at the mouth of most side streets: bars along the road, spaced across it, just beyond the junction. Not on a
        /// highway, and not at every one (a city with a crossing at every corner looks like a diagram).
        /// </summary>
        private static void Crossings(Context ctx, EdgeGeometry g, MeshBuffer white, Func<double, double, double> height)
        {
            if (white == null)
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
        private static void AddSidewalkProps(Context ctx, PlanBlock block, int e, MeshBuffer buffer, List<Vec2[]> cuts)
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
                if (at.X < ctx.Rect.X0 || at.X >= ctx.Rect.X1 || at.Y < ctx.Rect.Z0 || at.Y >= ctx.Rect.Z1 || InCut(at, cuts) || BeforeStairway(ctx, block, at))
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
            if (PedestrianStreets.Is(ctx.Plan, g.Edge)) return;   // no lanes, no crossings: nothing drives here
            Func<double, double, double> height = RoadHeight(ctx, g);
            Crossings(ctx, g, white, height);
            PaintLanes(ctx, g, yellow, white, height, g.TrimA + 1.5, g.Length - g.TrimB - 1.5, g.Edge.Class, false);
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
                case RoadClass.Boulevard:
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

        /// <summary>Whether a road has a row of barricades across its middle (AddStreetBarricades): one long road in thirty or so.</summary>
        public static bool HasCordon(EdgeGeometry g) =>
            !g.Edge.Bridge && g.Length - g.TrimA - g.TrimB >= 22.0 && g.Length > 40.0 && LatticeHash(g.Edge.Id, 7, 79) < 0.035f;

        /// <summary>
        /// Now and then a row of barricades across a street: the evacuation's last cordons. On a pedestrian street (World/PedestrianStreets) the
        /// whole ribbon is the footway, so the cordon stands across its middle only and leaves the walk along each side clear (a 2.6 m body walks
        /// the street end to end: PedestrianStreetWalkPlayTests; seed 46283197's street had its cordon's end barrier across the walk).
        /// </summary>
        private static void AddStreetBarricades(Context ctx, EdgeGeometry g, MeshBuffer structures)
        {
            if (g.Edge.Bridge) return;
            if (g.Length - g.TrimA - g.TrimB < 22.0) return;
            Func<double, double, double> height = RoadHeight(ctx, g);
            Vec2 perp = g.Dir.Perp;
            double hw = g.HalfWidth;
            if (HasCordon(g))
            {
                double t = g.Length * 0.5;
                Vec2 c = g.A + g.Dir * t;
                bool walk = PedestrianStreets.Is(ctx.Plan, g.Edge);
                double span = walk ? hw - PedestrianStreets.WalkWidth : hw - 0.5;   // how far from the middle a barrier may reach
                int count = (int)(2.0 * span / 3.2);
                double first = walk ? -(count - 1) * 1.6 : -hw + 1.0 + 1.6;   // centred on a pedestrian street; from the kerb side on a road
                for (int i = 0; i < count; i++)
                {
                    Vec2 pos = c + perp * (first + i * 3.2);
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
