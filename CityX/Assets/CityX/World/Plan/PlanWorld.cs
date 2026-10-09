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
        private static readonly Dictionary<(int, double, double), PlanWorld> Cache = new Dictionary<(int, double, double), PlanWorld>();

        public readonly int Seed;
        public readonly CityPlanData Data;
        public readonly PlanGeometry Geometry;
        public readonly PlanFields Fields;
        public readonly BuildingSite[] Buildings;

        private readonly Dictionary<long, List<int>> _buildingsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _edgesByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _junctionsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _blocksByCoarse = new Dictionary<long, List<int>>();
        private double[][] _blockBoxes;
        private static readonly List<int> NoneList = new List<int>(0);

        /// <summary>The height from which a building is a landmark: the streamer keeps the (distant version of the) chunk that builds one in view much further out.</summary>
        public const double LandmarkHeight = 150.0;

        /// <summary>The chunks that build a landmark, each once.</summary>
        public readonly List<(int X, int Y)> LandmarkChunks = new List<(int X, int Y)>();

        public static PlanWorld For(int seed)
        {
            lock (CacheLock)
            {
                // One plan per seed AND scale (CityScale.Active can be changed, which tests and tools do).
                CityScale scale = CityScale.Active;
                var key = (seed, scale.Footprint * 1000.0 + scale.MinFootprint, scale.Height * 1000.0 + scale.MinHeight);
                if (!Cache.TryGetValue(key, out PlanWorld world))
                {
                    world = new PlanWorld(seed, null);
                    Cache[key] = world;
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
            return PlanConfig.For(CityScale.Active);
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
        public List<int> BlocksIn(int cx, int cy)
        {
            // Looked up from a coarse grid and filtered by each block's bounding box, rather than registered chunk by chunk: at the larger city scales a
            // block covers hundreds of chunks and registering them all took seconds.
            if (!_blocksByCoarse.TryGetValue(CoarseKey(cx, cy), out List<int> candidates))
            {
                return NoneList;
            }
            List<int> hits = null;
            foreach (int i in candidates)
            {
                double[] box = _blockBoxes[i];
                if (ChunkOf(box[0]) <= cx && cx <= ChunkOf(box[2]) && ChunkOf(box[1]) <= cy && cy <= ChunkOf(box[3]))
                {
                    (hits ?? (hits = new List<int>(4))).Add(i);
                }
            }
            return hits ?? NoneList;
        }

        private const int CoarseChunks = 64;
        private static long CoarseKey(int cx, int cy) => Key((int)Math.Floor(cx / (double)CoarseChunks), (int)Math.Floor(cy / (double)CoarseChunks));

        /// <summary>The building a world point is inside (or within <paramref name="margin"/> of the walls), or -1.</summary>
        public int BuildingAt(double x, double z, double margin = 0.0)
        {
            return BuildingAtHeight(x, double.NaN, z, margin);
        }

        /// <summary>
        /// The building at a world POINT: like <see cref="BuildingAt"/> but with the height as well, because buildings are stacked (a
        /// tower stands on its podium's roof) and two of them can be over the same ground. Only a building whose walls reach the height
        /// is considered. <paramref name="y"/> NaN ignores height.
        /// </summary>
        public int BuildingAtHeight(double x, double y, double z, double margin = 0.0)
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
                        if (!double.IsNaN(y) && (y < b.PodiumBase - 1.0 || y > b.BaseHeight + b.Params.Height + 1.0))
                        {
                            continue;
                        }
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
                double scale = Fields.ScaleAt(n.P.X, n.P.Y);
                int reach = (int)Math.Ceiling(60.0 * scale / ChunkSize) + 1;
                double d = n.P.Length;
                if (d > 900.0 * scale) continue;
                bool bridge = false;
                foreach (RoadEdge e in n.Edges) bridge |= e.Bridge;
                if (bridge) continue;

                // Not beside the trench: no water within 90 m in eight directions.
                int wet = 0;
                for (int k = 0; k < 8; k++)
                {
                    double a = k * 0.7853981633974483;
                    double dx = Math.Cos(a), dz = Math.Sin(a);
                    if (Fields.IsWater(n.P.X + dx * 45.0 * scale, n.P.Y + dz * 45.0 * scale)) wet++;
                    if (Fields.IsWater(n.P.X + dx * 90.0 * scale, n.P.Y + dz * 90.0 * scale)) wet++;
                }
                if (wet > 0) continue;

                // Built up around it: plenty of buildings within about 60 m.
                int cx = ChunkOf(n.P.X), cy = ChunkOf(n.P.Y), near = 0;
                for (int oy = -reach; oy <= reach; oy++)
                {
                    for (int ox = -reach; ox <= reach; ox++)
                    {
                        List<int> owned = BuildingsOwnedBy(cx + ox, cy + oy);
                        for (int i = 0; i < owned.Count; i++)
                        {
                            BuildingSite b = Buildings[owned[i]];
                            double bx = b.CentreX - n.P.X, bz = b.CentreZ - n.P.Y;
                            if (bx * bx + bz * bz < 3600.0 * scale * scale) near++;
                        }
                    }
                }
                if (near < 10) continue;

                double score = d - (n.Edges.Count >= 4 ? 40.0 * scale : 0.0);
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
            var landmarkSeen = new HashSet<long>();
            for (int i = 0; i < Buildings.Length; i++)
            {
                BuildingSite b = Buildings[i];
                Add(_buildingsByChunk, b.OwnerChunkX, b.OwnerChunkY, i);
                if (b.Params.Height >= LandmarkHeight && landmarkSeen.Add(Key(b.OwnerChunkX, b.OwnerChunkY)))
                {
                    LandmarkChunks.Add((b.OwnerChunkX, b.OwnerChunkY));
                }
            }

            foreach (EdgeGeometry g in Geometry.Edges)
            {
                if (g == null) continue;
                AddSegment(_edgesByChunk, g.A, g.B, g.HalfWidth + 3.0, g.Edge.Id);
            }

            foreach (JunctionGeometry j in Geometry.Junctions)
            {
                if (j == null || j.Plate == null) continue;
                AddPoly(_junctionsByChunk, j.Plate, j.Node.Id);
            }

            _blockBoxes = new double[Data.Blocks.Count][];
            for (int i = 0; i < Data.Blocks.Count; i++)
            {
                PlanBlock b = Data.Blocks[i];
                if (b.Inner == null || b.Outer == null) continue;
                double bx0 = double.MaxValue, by0 = double.MaxValue, bx1 = double.MinValue, by1 = double.MinValue;
                foreach (Vec2 v in b.Poly)
                {
                    bx0 = Math.Min(bx0, v.X); bx1 = Math.Max(bx1, v.X);
                    by0 = Math.Min(by0, v.Y); by1 = Math.Max(by1, v.Y);
                }
                _blockBoxes[i] = new[] { bx0, by0, bx1, by1 };
                int k0x = (int)Math.Floor(ChunkOf(bx0) / (double)CoarseChunks), k1x = (int)Math.Floor(ChunkOf(bx1) / (double)CoarseChunks);
                int k0y = (int)Math.Floor(ChunkOf(by0) / (double)CoarseChunks), k1y = (int)Math.Floor(ChunkOf(by1) / (double)CoarseChunks);
                for (int ky = k0y; ky <= k1y; ky++)
                {
                    for (int kx = k0x; kx <= k1x; kx++)
                    {
                        Add(_blocksByCoarse, kx, ky, i);
                    }
                }
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

        /// <summary>
        /// Registers an id in every chunk within <paramref name="radius"/> of a segment. (A bounding box of a long diagonal road is a thousand
        /// chunks it never touches, which at the larger city scales was most of the index.)
        /// </summary>
        private static void AddSegment(Dictionary<long, List<int>> map, Vec2 a, Vec2 b, double radius, int id)
        {
            double length = (b - a).Length;
            int steps = Math.Max(1, (int)Math.Ceiling(length / (ChunkSize * 0.5)));
            var seen = new HashSet<long>();
            var cells = new List<long>();
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double px = a.X + (b.X - a.X) * t, py = a.Y + (b.Y - a.Y) * t;
                int cx0 = ChunkOf(px - radius), cx1 = ChunkOf(px + radius), cy0 = ChunkOf(py - radius), cy1 = ChunkOf(py + radius);
                for (int cy = cy0; cy <= cy1; cy++)
                {
                    for (int cx = cx0; cx <= cx1; cx++)
                    {
                        long key = Key(cx, cy);
                        if (seen.Add(key))
                        {
                            cells.Add(key);
                        }
                    }
                }
            }
            foreach (long key in cells)
            {
                if (!map.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(4);
                    map[key] = list;
                }
                list.Add(id);
            }
        }

        /// <summary>
        /// Registers an id in every chunk a polygon touches: for each row of chunks, from the x-range the polygon's edges cover in that row. (The
        /// bounding box of a long diagonal block is most of a thousand chunks it never touches.)
        /// </summary>
        private static void AddPoly(Dictionary<long, List<int>> map, IList<Vec2> poly, int id)
        {
            double y0 = double.MaxValue, y1 = double.MinValue;
            for (int i = 0; i < poly.Count; i++)
            {
                if (poly[i].Y < y0) y0 = poly[i].Y;
                if (poly[i].Y > y1) y1 = poly[i].Y;
            }
            int row0 = ChunkOf(y0), row1 = ChunkOf(y1);
            for (int row = row0; row <= row1; row++)
            {
                double bandLow = row * ChunkSize, bandHigh = bandLow + ChunkSize;
                double minX = double.MaxValue, maxX = double.MinValue;
                for (int i = 0, n = poly.Count; i < n; i++)
                {
                    Vec2 a = poly[i], b = poly[(i + 1) % n];
                    // The part of this edge inside the band.
                    double ta = 0.0, tb = 1.0;
                    double dy = b.Y - a.Y;
                    if (Math.Abs(dy) < 1e-12)
                    {
                        if (a.Y < bandLow || a.Y > bandHigh) continue;
                    }
                    else
                    {
                        double t0 = (bandLow - a.Y) / dy, t1 = (bandHigh - a.Y) / dy;
                        if (t0 > t1) { double s = t0; t0 = t1; t1 = s; }
                        ta = Math.Max(ta, t0);
                        tb = Math.Min(tb, t1);
                        if (ta > tb) continue;
                    }
                    double xa = a.X + (b.X - a.X) * ta, xb = a.X + (b.X - a.X) * tb;
                    if (xa < minX) minX = xa;
                    if (xb < minX) minX = xb;
                    if (xa > maxX) maxX = xa;
                    if (xb > maxX) maxX = xb;
                }
                if (minX > maxX) continue;
                int c0 = ChunkOf(minX), c1 = ChunkOf(maxX);
                for (int cx = c0; cx <= c1; cx++)
                {
                    Add(map, cx, row, id);
                }
            }
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
                if (!FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 centre, out Vec2 ex, out double width, out double depth, lot.Scale))
                {
                    continue;
                }

                double s = lot.Scale;
                double hs = lot.HeightScale;
                int id = list.Count;
                uint h = PlanNoise.Mix(seed, unchecked((uint)(lot.Id * 7 + 3)));
                var rng = new DeterministicRandom(unchecked((int)h));

                ZoneKind zone = ZoneFor(fields, lot.Population, centre, characterSeed, rng.NextDouble());

                // Height: the plan's, held to a sane slenderness so a narrow plot never makes a needle.
                double height = lot.Height;
                if (zone == ZoneKind.Industrial)
                {
                    height = (9.0 + rng.NextDouble() * 13.0) * hs;
                }
                height = Math.Min(height, MaxSlenderness * Math.Min(width, depth));
                height = Math.Max(height, 6.0 * hs);

                BuildingMass mass = MassFor(zone, height, width, depth, s, hs);
                string roof = RoofsByZone[(int)zone][rng.Below(8)];
                bool basement = rng.NextDouble() < 0.12 && height > 10.0;

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

                // The shape: one box, a stack of them (a tower on a podium, a crown that steps in), a bar with wings (L, T, U, a staggered pair) or a
                // sculpted tower (twisted, wonky, cantilevered). Drawn last, so the roof and basement rolled above are what they would have been anyway.
                List<MassVolume> volumes = Massing.Compose(zone, mass, width, depth, height, rng, out bool hasPodium, out BuildingForm form, s, hs);
                List<WallContact>[] contacts = ContactsBetween(volumes);

                // Every volume is its own BuildingSite. One at street level has the ground under it; one above stands on the roof of the one it
                // stands on and is reached from it. A tower on a podium is a different building from the podium and dresses differently; the stages of
                // one stepped or twisted tower, and the wings of a block, are one building and dress alike; the blocks of a wonky pile each their own.
                int towerSeed = unchecked((int)PlanNoise.Mix(h, 0x70746F77U));   // "ptow"
                var bases = new double[volumes.Count];
                var corners = new double[2][] { new double[4], new double[4] };
                for (int v = 0; v < volumes.Count; v++)
                {
                    MassVolume vol = volumes[v];
                    bool ground = vol.Support < 0;
                    bool isPodium = hasPodium && v == 0;
                    int above = -1;
                    for (int j = v + 1; j < volumes.Count && above < 0; j++)
                    {
                        if (volumes[j].Support == v)
                        {
                            above = j;
                        }
                    }
                    bool isTop = above < 0;

                    BuildingMass volumeMass = isPodium ? MassFor(zone, vol.Height, vol.Width, vol.Depth, s, hs) : mass;
                    // Below the top a roof is a terrace with a parapet round it; only a top carries the zone's roof plant.
                    string volumeRoof = isTop ? roof : "parapet";
                    int volumeSeed = unchecked((int)h);
                    if (hasPodium && v > 0 || form == BuildingForm.Cantilever && !ground)
                    {
                        volumeSeed = towerSeed;
                    }
                    else if (form == BuildingForm.Wonky && !ground)
                    {
                        volumeSeed = unchecked((int)PlanNoise.Mix(h, (uint)(0x776F6E00 + v)));   // "won."
                    }

                    var volumeParameters = new BuildingParams(true, (float)vol.Width, (float)vol.Height, (float)vol.Depth,
                        0.0f, 0.0f, volumeRoof, 0, "", v == 0 && basement, volumeMass);

                    double cx = centre.X + vol.CentreX * axisX - vol.CentreZ * axisZ;
                    double cz = centre.Y + vol.CentreX * axisZ + vol.CentreZ * axisX;
                    // The volume's own +X in the world: its turn in the lot frame, put through the lot's own turn.
                    double vAxisX = vol.TurnX * axisX - vol.TurnZ * axisZ;
                    double vAxisZ = vol.TurnX * axisZ + vol.TurnZ * axisX;

                    // Where the volume standing on this one's roof is, in this one's frame relative to its centre (its outline's box when it is turned).
                    float ox0 = 0.0f, ox1 = 0.0f, oz0 = 0.0f, oz1 = 0.0f;
                    if (!isTop)
                    {
                        volumes[above].Corners(corners[0], corners[1]);
                        double mnx = double.MaxValue, mxx = double.MinValue, mnz = double.MaxValue, mxz = double.MinValue;
                        for (int k = 0; k < 4; k++)
                        {
                            vol.ToOwn(corners[0][k], corners[1][k], out double ox, out double oz);
                            mnx = Math.Min(mnx, ox);
                            mxx = Math.Max(mxx, ox);
                            mnz = Math.Min(mnz, oz);
                            mxz = Math.Max(mxz, oz);
                        }
                        ox0 = (float)mnx;
                        ox1 = (float)mxx;
                        oz0 = (float)mnz;
                        oz1 = (float)mxz;
                    }

                    // Every block at street level shares one floor, so the wings of a building meet the bar at the same level; the next volume up
                    // stands a hair above the roof under it so its floor and that roof's top face never fight for one plane.
                    double vBase = ground ? baseHeight : bases[vol.Support] + volumes[vol.Support].Height + 0.05;
                    bases[v] = vBase;
                    double podiumBase = vBase, doorGround = vBase;
                    if (ground)
                    {
                        podiumBase = double.MaxValue;
                        for (int sz = -1; sz <= 1; sz++)
                        {
                            for (int sx = -1; sx <= 1; sx++)
                            {
                                double lx = sx * vol.Width * 0.5, lz = sz * vol.Depth * 0.5;
                                double gy = fields.Height(cx + lx * vAxisX - lz * vAxisZ, cz + lx * vAxisZ + lz * vAxisX);
                                podiumBase = Math.Min(podiumBase, gy);
                            }
                        }
                        podiumBase = Math.Min(podiumBase, lo);
                        // The ground two metres out from the front wall: a polygon's front side stands at its apothem, not its half-depth.
                        double out_ = (vol.Sides >= 3 ? vol.Width * 0.5 * BuildingSite.CosPiOver(vol.Sides) : vol.Depth * 0.5) + 2.0;
                        doorGround = fields.Height(cx - out_ * vAxisZ, cz + out_ * vAxisX);
                    }

                    list.Add(new BuildingSite(list.Count, volumeParameters, zone, (float)vBase, (float)podiumBase, (float)doorGround,
                        cx, cz, vAxisX, vAxisZ, PlanWorld.ChunkOf(cx), PlanWorld.ChunkOf(cz), volumeSeed,
                        ox0, ox1, oz0, oz1, !ground, vol.Shell, vol.Wing,
                        contacts[v] != null ? contacts[v].ToArray() : null,
                        !isTop && volumes[above].Shell, vol.Sides));
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// Where the blocks at street level of one building stand against each other: for each volume, the stretches of its walls another one covers,
        /// and how high. Street-level blocks are only ever quarter-turned, so every touching pair shares a face along one of the lot's axes.
        /// </summary>
        public static List<WallContact>[] ContactsBetween(List<MassVolume> volumes)
        {
            var result = new List<WallContact>[volumes.Count];
            const double eps = 0.05;
            for (int a = 0; a < volumes.Count; a++)
            {
                if (volumes[a].Support >= 0)
                {
                    continue;
                }
                Box(volumes[a], out double ax0, out double ax1, out double az0, out double az1);
                for (int b = 0; b < volumes.Count; b++)
                {
                    if (b == a || volumes[b].Support >= 0)
                    {
                        continue;
                    }
                    Box(volumes[b], out double bx0, out double bx1, out double bz0, out double bz1);
                    double zLo = Math.Max(az0, bz0), zHi = Math.Min(az1, bz1);
                    double xLo = Math.Max(ax0, bx0), xHi = Math.Min(ax1, bx1);
                    if (zHi - zLo > 0.3 && Math.Abs(ax1 - bx0) < eps)
                    {
                        AddContact(result, a, volumes[a], 1.0, 0.0, ax1, zLo, ax1, zHi, volumes[b].Height);
                    }
                    if (zHi - zLo > 0.3 && Math.Abs(ax0 - bx1) < eps)
                    {
                        AddContact(result, a, volumes[a], -1.0, 0.0, ax0, zLo, ax0, zHi, volumes[b].Height);
                    }
                    if (xHi - xLo > 0.3 && Math.Abs(az1 - bz0) < eps)
                    {
                        AddContact(result, a, volumes[a], 0.0, 1.0, xLo, az1, xHi, az1, volumes[b].Height);
                    }
                    if (xHi - xLo > 0.3 && Math.Abs(az0 - bz1) < eps)
                    {
                        AddContact(result, a, volumes[a], 0.0, -1.0, xLo, az0, xHi, az0, volumes[b].Height);
                    }
                }
            }
            return result;
        }

        /// <summary>A volume's footprint box in the lot frame.</summary>
        private static void Box(MassVolume v, out double x0, out double x1, out double z0, out double z1)
        {
            double ex = Math.Abs(v.TurnX) * v.Width * 0.5 + Math.Abs(v.TurnZ) * v.Depth * 0.5;
            double ez = Math.Abs(v.TurnZ) * v.Width * 0.5 + Math.Abs(v.TurnX) * v.Depth * 0.5;
            x0 = v.CentreX - ex;
            x1 = v.CentreX + ex;
            z0 = v.CentreZ - ez;
            z1 = v.CentreZ + ez;
        }

        /// <summary>One contact: the face of volume <paramref name="index"/> pointing along lot (dx, dz), between two lot-frame points, up to a height.</summary>
        private static void AddContact(List<WallContact>[] result, int index, MassVolume v, double dx, double dz,
            double px0, double pz0, double px1, double pz1, double top)
        {
            // Which of its own walls that is: the lot direction in its own frame.
            double ownX = dx * v.TurnX + dz * v.TurnZ;
            double ownZ = -dx * v.TurnZ + dz * v.TurnX;
            WindowGrid.Wall wall = ownX > 0.5 ? WindowGrid.Wall.East
                : ownX < -0.5 ? WindowGrid.Wall.West
                : ownZ > 0.5 ? WindowGrid.Wall.North : WindowGrid.Wall.South;
            bool northSouth = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South;
            v.ToOwn(px0, pz0, out double ax, out double az);
            v.ToOwn(px1, pz1, out double bx, out double bz);
            double half = northSouth ? v.Width * 0.5 : v.Depth * 0.5;
            double u0 = (northSouth ? Math.Min(ax, bx) : Math.Min(az, bz)) + half;
            double u1 = (northSouth ? Math.Max(ax, bx) : Math.Max(az, bz)) + half;
            (result[index] ?? (result[index] = new List<WallContact>(2))).Add(new WallContact(wall, (float)u0, (float)u1, (float)top));
        }

        private static BuildingMass MassFor(ZoneKind zone, double height, double width, double depth, double s = 1.0, double hs = 1.0)
        {
            height /= hs;   // the thresholds below are the classic city's, in the classic city's metres
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
        public static bool FitRectangle(Vec2[] poly, int front, out Vec2 centre, out Vec2 ex, out double width, out double depth, double scale = 1.0)
        {
            double minSide = MinSide * scale;
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
            for (double dep = deepest; dep >= minSide; dep -= 1.5 * scale)
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
                if (!ok || xmax - xmin < minSide) continue;
                double w = xmax - xmin;
                double dd = Math.Min(dep, 2.4 * w + 6.0 * scale);
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
            if (width < minSide || depth < minSide) return false;
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
