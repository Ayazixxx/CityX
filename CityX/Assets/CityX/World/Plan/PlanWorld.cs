using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// The city as the game asks about it: the plan, its road geometry, every building resolved into a rectangle that faces its
    /// street, and indexes by streaming chunk so a chunk can ask "what is in me" without searching the whole city.
    ///
    /// Generated ONCE per seed (about a second for a 5 km region, on whichever thread asks first) and shared; everything in it is
    /// read-only afterwards, so any number of chunk builders can read it from worker threads at the same time.
    /// </summary>
    public sealed class PlanWorld
    {
        /// <summary>Side of a streaming chunk, metres. The same number as CityLayout.ChunkSize.</summary>
        public const double ChunkSize = 24.0;

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<int, PlanWorld> Cache = new Dictionary<int, PlanWorld>();

        public readonly int Seed;
        public readonly CityPlanData Data;
        public readonly PlanGeometry Geometry;
        public readonly PlanFields Fields;
        public readonly BuildingSite[] Buildings;

        private readonly Dictionary<long, List<int>> _buildingsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _edgesByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _junctionsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _blocksByChunk = new Dictionary<long, List<int>>();
        private static readonly List<int> NoneList = new List<int>(0);

        public static PlanWorld For(int seed)
        {
            lock (CacheLock)
            {
                if (!Cache.TryGetValue(seed, out PlanWorld world))
                {
                    world = new PlanWorld(seed, null);
                    Cache[seed] = world;
                }
                return world;
            }
        }

        /// <summary>A plan with its own configuration (tests and tools). Not cached.</summary>
        public static PlanWorld Create(int seed, PlanConfig config)
        {
            return new PlanWorld(seed, config);
        }

        private PlanWorld(int seed, PlanConfig config)
        {
            Seed = seed;
            PlanConfig cfg = config ?? DefaultConfig();
            Data = CityPlan.Generate(seed, cfg);
            Fields = Data.Fields;
            Geometry = PlanGeometry.Build(Data);

            Buildings = PlanBuildings.Resolve(Data, Fields, unchecked((uint)seed));
            IndexAll();
        }

        /// <summary>The configuration the game's own city uses.</summary>
        public static PlanConfig DefaultConfig()
        {
            return new PlanConfig
            {
                Half = 2500.0,
                HillAmplitude = 30.0,
            };
        }

        // ------------------------------------------------------------------ ground

        /// <summary>The ground height at a world position, metres above sea level (a dry trench where a river runs).</summary>
        public double GroundHeight(double x, double z) => Fields.Height(x, z);

        // ------------------------------------------------------------------ queries

        public static int ChunkOf(double v) => (int)Math.Floor(v / ChunkSize);

        private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        /// <summary>The buildings whose centres are in this chunk (they are built by it).</summary>
        public List<int> BuildingsOwnedBy(int cx, int cy) =>
            _buildingsByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>Roads whose ribbon touches this chunk.</summary>
        public List<int> EdgesIn(int cx, int cy) =>
            _edgesByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>Junctions whose plate touches this chunk.</summary>
        public List<int> JunctionsIn(int cx, int cy) =>
            _junctionsByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>Blocks whose pavement ring touches this chunk.</summary>
        public List<int> BlocksIn(int cx, int cy) =>
            _blocksByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>The building a world point is inside (or within <paramref name="margin"/> of the walls), or -1.</summary>
        public int BuildingAt(double x, double z, double margin = 0.0)
        {
            int cx = ChunkOf(x);
            int cy = ChunkOf(z);
            int best = -1;
            double bestDist = double.MaxValue;
            // A building is owned by the chunk its centre is in, but it can reach well into its neighbours: search around.
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    List<int> list = BuildingsOwnedBy(cx + dx, cy + dy);
                    for (int i = 0; i < list.Count; i++)
                    {
                        BuildingSite b = Buildings[list[i]];
                        b.WorldToLocal(x, z, out double lx, out double lz);
                        double hw = b.Params.Width * 0.5 + margin;
                        double hd = b.Params.Depth * 0.5 + margin;
                        if (Math.Abs(lx) <= hw && Math.Abs(lz) <= hd)
                        {
                            double d = lx * lx + lz * lz;
                            if (d < bestDist)
                            {
                                bestDist = d;
                                best = b.Id;
                            }
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>A road junction near the origin with three or more roads, or the nearest node: where the player starts.</summary>
        public bool TryFindSpawn(out double x, out double z)
        {
            RoadNode best = null;
            double bestScore = double.MaxValue;
            foreach (RoadNode n in Data.Nodes)
            {
                if (n.Edges.Count < 3) continue;
                double d = n.P.Length;
                if (d > 900.0) continue;
                bool bridge = false;
                foreach (RoadEdge e in n.Edges) bridge |= e.Bridge;
                if (bridge) continue;

                // Not beside the trench: no water within 90 m in eight directions.
                int wet = 0;
                for (int k = 0; k < 8; k++)
                {
                    double a = k * 0.7853981633974483;
                    double dx = Math.Cos(a), dz = Math.Sin(a);
                    if (Fields.IsWater(n.P.X + dx * 45.0, n.P.Y + dz * 45.0)) wet++;
                    if (Fields.IsWater(n.P.X + dx * 90.0, n.P.Y + dz * 90.0)) wet++;
                }
                if (wet > 0) continue;

                // Built up around it: plenty of buildings within about 60 m.
                int cx = ChunkOf(n.P.X), cy = ChunkOf(n.P.Y), near = 0;
                for (int oy = -3; oy <= 3; oy++)
                {
                    for (int ox = -3; ox <= 3; ox++)
                    {
                        List<int> owned = BuildingsOwnedBy(cx + ox, cy + oy);
                        for (int i = 0; i < owned.Count; i++)
                        {
                            BuildingSite b = Buildings[owned[i]];
                            double bx = b.CentreX - n.P.X, bz = b.CentreZ - n.P.Y;
                            if (bx * bx + bz * bz < 3600.0) near++;
                        }
                    }
                }
                if (near < 10) continue;

                double score = d - (n.Edges.Count >= 4 ? 40.0 : 0.0);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
            if (best == null)
            {
                // Nothing met every test: the nearest real junction at all.
                foreach (RoadNode n in Data.Nodes)
                {
                    if (n.Edges.Count < 2) continue;
                    double d = n.P.Length;
                    if (d < bestScore || best == null)
                    {
                        bestScore = d;
                        best = n;
                    }
                }
            }
            if (best == null)
            {
                x = 0.0;
                z = 0.0;
                return false;
            }
            x = best.P.X;
            z = best.P.Y;
            return true;
        }

        // ------------------------------------------------------------------ indexing

        private void IndexAll()
        {
            for (int i = 0; i < Buildings.Length; i++)
            {
                BuildingSite b = Buildings[i];
                Add(_buildingsByChunk, b.OwnerChunkX, b.OwnerChunkY, i);
            }

            foreach (EdgeGeometry g in Geometry.Edges)
            {
                if (g == null) continue;
                double m = g.HalfWidth + 2.0;
                AddBox(_edgesByChunk, Math.Min(g.A.X, g.B.X) - m, Math.Min(g.A.Y, g.B.Y) - m,
                    Math.Max(g.A.X, g.B.X) + m, Math.Max(g.A.Y, g.B.Y) + m, g.Edge.Id);
            }

            foreach (JunctionGeometry j in Geometry.Junctions)
            {
                if (j == null || j.Plate == null) continue;
                AddPoly(_junctionsByChunk, j.Plate, j.Node.Id);
            }

            for (int i = 0; i < Data.Blocks.Count; i++)
            {
                PlanBlock b = Data.Blocks[i];
                if (b.Inner == null || b.Outer == null) continue;
                AddPoly(_blocksByChunk, b.Poly, i);
            }
        }

        private static void Add(Dictionary<long, List<int>> map, int cx, int cy, int id)
        {
            long key = Key(cx, cy);
            if (!map.TryGetValue(key, out List<int> list))
            {
                list = new List<int>(4);
                map[key] = list;
            }
            list.Add(id);
        }

        private static void AddBox(Dictionary<long, List<int>> map, double x0, double y0, double x1, double y1, int id)
        {
            int cx0 = ChunkOf(x0), cx1 = ChunkOf(x1), cy0 = ChunkOf(y0), cy1 = ChunkOf(y1);
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    Add(map, cx, cy, id);
                }
            }
        }

        private static void AddPoly(Dictionary<long, List<int>> map, IList<Vec2> poly, int id)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            for (int i = 0; i < poly.Count; i++)
            {
                if (poly[i].X < x0) x0 = poly[i].X;
                if (poly[i].X > x1) x1 = poly[i].X;
                if (poly[i].Y < y0) y0 = poly[i].Y;
                if (poly[i].Y > y1) y1 = poly[i].Y;
            }
            AddBox(map, x0, y0, x1, y1, id);
        }
    }

    /// <summary>
    /// Turns the plan's lots into buildings: a rectangle fitted inside each lot, turned so its front faces the street, with a height,
    /// roof, zone and everything else the building generator needs.
    /// </summary>
    public static class PlanBuildings
    {
        public const double MinSide = 8.0;
        public const double MaxSlenderness = 7.5;

        private static readonly string[][] RoofsByZone =
        {
            /* Core        */ new[] { "antenna", "antenna", "parapet", "", "antenna", "parapet", "", "water_tower" },
            /* Urban       */ new[] { "", "parapet", "antenna", "shed", "antenna", "parapet", "", "water_tower" },
            /* Residential */ new[] { "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower" },
            /* OldTown     */ new[] { "", "", "chimney", "chimney", "shed", "", "parapet", "water_tower" },
            /* Industrial  */ new[] { "shed", "shed", "chimney", "chimney", "", "water_tower", "shed", "water_tower" },
            /* Outskirts   */ new[] { "", "", "", "shed", "", "chimney", "shed", "water_tower" },
        };

        public static BuildingSite[] Resolve(CityPlanData data, PlanFields fields, uint seed)
        {
            var list = new List<BuildingSite>(data.Lots.Count);
            uint characterSeed = PlanNoise.Mix(seed, 0x7A6F6E65U);   // "zone"

            foreach (PlanLot lot in data.Lots)
            {
                if (!FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 centre, out Vec2 ex, out double width, out double depth))
                {
                    continue;
                }

                int id = list.Count;
                uint h = PlanNoise.Mix(seed, unchecked((uint)(lot.Id * 7 + 3)));
                var rng = new DeterministicRandom(unchecked((int)h));

                ZoneKind zone = ZoneFor(fields, lot.Population, centre, characterSeed, rng.NextDouble());

                // Height: the plan's, held to a sane slenderness so a narrow plot never makes a needle.
                double height = lot.Height;
                if (zone == ZoneKind.Industrial)
                {
                    height = 9.0 + rng.NextDouble() * 13.0;
                }
                height = Math.Min(height, MaxSlenderness * Math.Min(width, depth));
                height = Math.Max(height, 6.0);

                BuildingMass mass = MassFor(zone, height, width, depth);
                string roof = RoofsByZone[(int)zone][rng.Below(8)];
                bool basement = rng.NextDouble() < 0.12 && height > 10.0;

                var parameters = new BuildingParams(true, (float)width, (float)height, (float)depth,
                    0.0f, 0.0f, roof, 0, "", basement, mass);

                // The ground under the footprint, sampled at the corners, edge middles and centre.
                // (Local +Z is the front, so the door is at (0, +depth/2).)
                double axisX = ex.X, axisZ = ex.Y;
                double hi = double.MinValue, lo = double.MaxValue;
                for (int sz = -1; sz <= 1; sz++)
                {
                    for (int sx = -1; sx <= 1; sx++)
                    {
                        double lx = sx * width * 0.5;
                        double lz = sz * depth * 0.5;
                        double wx = centre.X + lx * axisX - lz * axisZ;
                        double wz = centre.Y + lx * axisZ + lz * axisX;
                        double gy = fields.Height(wx, wz);
                        if (gy > hi) hi = gy;
                        if (gy < lo) lo = gy;
                    }
                }
                // A flat floor a little above the ground, on whole decimetres.
                double baseHeight = Math.Round(hi * 10.0) / 10.0;

                double doorX = centre.X + (depth * 0.5 + 2.0) * (-axisZ);
                double doorZ = centre.Y + (depth * 0.5 + 2.0) * axisX;
                double doorGround = fields.Height(doorX, doorZ);

                int ownerX = PlanWorld.ChunkOf(centre.X);
                int ownerY = PlanWorld.ChunkOf(centre.Y);

                list.Add(new BuildingSite(id, parameters, zone, (float)baseHeight, (float)lo, (float)doorGround,
                    centre.X, centre.Y, axisX, axisZ, ownerX, ownerY, unchecked((int)h)));
            }
            return list.ToArray();
        }

        private static BuildingMass MassFor(ZoneKind zone, double height, double width, double depth)
        {
            if (zone == ZoneKind.Industrial) return BuildingMass.Warehouse;
            if (height >= 240.0) return BuildingMass.Supertall;
            if (height >= 110.0) return BuildingMass.Tower;
            double longSide = Math.Max(width, depth);
            double shortSide = Math.Min(width, depth);
            if (height >= 50.0 && longSide >= 2.6 * shortSide) return BuildingMass.Slab;
            if (height >= 28.0) return BuildingMass.MidRise;
            return BuildingMass.LowRise;
        }

        public static ZoneKind ZoneFor(PlanFields fields, double population, Vec2 p, uint characterSeed, double hash)
        {
            double character = PlanNoise.Fbm(p.X / 650.0, p.Y / 650.0, 2, characterSeed);
            if (population > 0.80) return ZoneKind.Core;
            if (population > 0.55) return ZoneKind.Urban;
            if (population < 0.50 && population > 0.10 && character > 0.30) return ZoneKind.Industrial;
            if (population > 0.30)
            {
                return character < -0.35 ? ZoneKind.OldTown : ZoneKind.Residential;
            }
            return ZoneKind.Outskirts;
        }

        /// <summary>
        /// The biggest rectangle that fits inside a lot's footprint with one side along its street frontage, as a centre, the
        /// world direction of the rectangle's local +X, and its width (along the frontage) and depth (into the lot).
        /// Local +Z is the outward normal of the frontage, so a door on the +Z wall faces the street.
        /// </summary>
        public static bool FitRectangle(Vec2[] poly, int front, out Vec2 centre, out Vec2 ex, out double width, out double depth)
        {
            centre = default;
            ex = default;
            width = 0.0;
            depth = 0.0;
            int n = poly.Length;
            if (n < 3) return false;

            Vec2 p0 = poly[front];
            Vec2 p1 = poly[(front + 1) % n];
            Vec2 d = (p1 - p0).Normalized;
            Vec2 ez = new Vec2(d.Y, -d.X);   // out of the front (the interior is on the left of the edge)
            ex = new Vec2(-d.X, -d.Y);
            Vec2 o = (p0 + p1) * 0.5;

            // The polygon in the rectangle's frame: x along the frontage, z out of the front (inside is z < 0).
            var lx = new double[n];
            var lz = new double[n];
            double deepest = 0.0;
            for (int i = 0; i < n; i++)
            {
                Vec2 q = poly[i] - o;
                lx[i] = Vec2.Dot(q, ex);
                lz[i] = Vec2.Dot(q, ez);
                if (-lz[i] > deepest) deepest = -lz[i];
            }

            var xs = new List<double>(8);
            double bestArea = 0.0;
            double bestX0 = 0.0, bestX1 = 0.0, bestDepth = 0.0;
            for (double dep = deepest; dep >= MinSide; dep -= 1.5)
            {
                double xmin = double.NegativeInfinity, xmax = double.PositiveInfinity;
                bool ok = true;
                for (int s = 0; s <= 8 && ok; s++)
                {
                    double z = -0.15 - (dep - 0.15) * (s / 8.0);
                    ok = Chord(lx, lz, z, xs, out double a, out double b);
                    if (ok)
                    {
                        if (a > xmin) xmin = a;
                        if (b < xmax) xmax = b;
                    }
                }
                if (!ok || xmax - xmin < MinSide) continue;
                double w = xmax - xmin;
                double dd = Math.Min(dep, 2.4 * w + 6.0);
                double area = w * dd;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestX0 = xmin;
                    bestX1 = xmax;
                    bestDepth = dd;
                }
            }
            if (bestArea <= 0.0) return false;

            width = bestX1 - bestX0 - 0.6;
            depth = bestDepth - 0.6;
            if (width < MinSide || depth < MinSide) return false;
            double cx = (bestX0 + bestX1) * 0.5;
            double cz = -0.15 - bestDepth * 0.5 + 0.0;
            centre = o + ex * cx + ez * cz;
            return true;
        }

        /// <summary>The span of the polygon on the horizontal line at z that contains x = 0 (or lies closest to it).</summary>
        private static bool Chord(double[] lx, double[] lz, double z, List<double> xs, out double left, out double right)
        {
            xs.Clear();
            int n = lx.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double zi = lz[i], zj = lz[j];
                if ((zi > z) != (zj > z))
                {
                    xs.Add(lx[i] + (z - zi) * (lx[j] - lx[i]) / (zj - zi));
                }
            }
            xs.Sort();
            left = 0.0;
            right = 0.0;
            if (xs.Count < 2) return false;
            double best = double.MaxValue;
            bool found = false;
            for (int i = 0; i + 1 < xs.Count; i += 2)
            {
                double a = xs[i], b = xs[i + 1];
                double dist = (a <= 0.0 && b >= 0.0) ? 0.0 : Math.Min(Math.Abs(a), Math.Abs(b));
                if (dist < best)
                {
                    best = dist;
                    left = a;
                    right = b;
                    found = true;
                }
            }
            return found;
        }
    }
}
