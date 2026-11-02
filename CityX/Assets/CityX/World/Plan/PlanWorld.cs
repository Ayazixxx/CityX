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

        /// <summary>Where Old Guy's house stands: the land nearest the spawn that holds it. The generated buildings on it are left out of <see cref="Buildings"/>.</summary>
        public readonly TutorialLot TutorialHouse;

        /// <summary>Where the sky hub stands: its island over the city, its stair's landing in a block near the spawn (SkyHubPlan).</summary>
        public readonly SkyHubPlan SkyHub;

        private readonly bool _hasSpawn;
        private readonly double _spawnX, _spawnZ;

        private readonly Dictionary<long, List<int>> _buildingsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _edgesByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _junctionsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _viaductsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _piersByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _signsByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _barriersByChunk = new Dictionary<long, List<int>>();
        private readonly Dictionary<long, List<int>> _kerbCutsByChunk = new Dictionary<long, List<int>>();

        /// <summary>Every expressway deck segment, as (path, segment): what <see cref="ViaductSegmentsIn"/> indexes into.</summary>
        public readonly List<(int Path, int Seg)> ViaductSegments = new List<(int Path, int Seg)>();
        private readonly Dictionary<int, BuildingSite[]> _mallSites = new Dictionary<int, BuildingSite[]>();
        private static readonly BuildingSite[] NoSites = new BuildingSite[0];
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

            Buildings = PlanBuildings.Resolve(Data, Fields, unchecked((uint)seed), out int[] lotOf);
            IndexBuildings();
            // The spawn is chosen among the city as generated, and Old Guy's house then takes the land nearest it: the buildings it clears do not move
            // the spawn away from it.
            _hasSpawn = FindSpawn(out _spawnX, out _spawnZ);
            if (_hasSpawn)
            {
                TutorialHouse = TutorialLot.Find(Data, Fields, _spawnX, _spawnZ);
            }
            // The sky hub's landing and stair go where they leave room for the spawn and the house.
            if (_hasSpawn)
            {
                SkyHub = SkyHubPlan.Find(Data, Fields, _spawnX, _spawnZ, TutorialHouse);
            }
            if (TutorialHouse.Found || SkyHub.Found)
            {
                Buildings = WithoutLotsUnder(Buildings, lotOf, TutorialHouse, SkyHub);
                IndexBuildings();
            }
            IndexAll();
        }

        /// <summary>
        /// The buildings with every lot that has a building on the tutorial house's land, or in the way of the sky hub's landing and stair, taken out
        /// whole (an L's wing goes with its bar), numbered afresh so a building's id stays its place in the array.
        /// </summary>
        private static BuildingSite[] WithoutLotsUnder(BuildingSite[] sites, int[] lotOf, in TutorialLot house, in SkyHubPlan hub)
        {
            var cleared = new HashSet<int>();
            for (int i = 0; i < sites.Length; i++)
            {
                if (house.Overlaps(sites[i]) || hub.Overlaps(sites[i])) cleared.Add(lotOf[i]);
            }
            if (cleared.Count == 0)
            {
                return sites;
            }
            var kept = new List<BuildingSite>(sites.Length);
            for (int i = 0; i < sites.Length; i++)
            {
                if (!cleared.Contains(lotOf[i])) kept.Add(sites[i].WithId(kept.Count));
            }
            return kept.ToArray();
        }

        /// <summary>Whether a world point is on the tutorial house's lot (or within <paramref name="margin"/> of it): nothing generated goes there.</summary>
        public bool OnTutorialLot(double x, double z, double margin = 0.0) => TutorialHouse.Contains(x, z, margin);

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

        /// <summary>The expressway deck segments (indexes into <see cref="ViaductSegments"/>) whose footprint touches this chunk: each builds its part.</summary>
        public List<int> ViaductSegmentsIn(int cx, int cy) =>
            _viaductsByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>The expressway piers (indexes into Data.Expressways.Piers) standing in this chunk: it builds each whole.</summary>
        public List<int> PiersIn(int cx, int cy) =>
            _piersByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>The exit signs over the expressways (indexes into Data.Expressways.Signs) standing in this chunk.</summary>
        public List<int> SignsIn(int cx, int cy) =>
            _signsByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>The rows of barriers closing an expressway at the region's edge (indexes into Data.Expressways.Barriers) in this chunk.</summary>
        public List<int> BarriersIn(int cx, int cy) =>
            _barriersByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        /// <summary>Where ramps meet streets (indexes into Data.Expressways.KerbCuts): the pavement in this chunk is cut away under them.</summary>
        public List<int> KerbCutsIn(int cx, int cy) =>
            _kerbCutsByChunk.TryGetValue(Key(cx, cy), out List<int> l) ? l : NoneList;

        private Dictionary<(long, long), int> _nodeIdAtPosition;
        private Dictionary<(int, int), RoadEdge> _edgeBetweenNodes;
        private readonly object _edgeLookupLock = new object();

        /// <summary>
        /// The live road edge joining two road nodes given by their positions (a block's corners ARE road nodes: the block edge from Poly[i] to
        /// Poly[i + 1] runs along it), or null. Built once, on first use, on whichever thread asks (the pedestrian streets, PedestrianStreets.Along).
        /// </summary>
        public RoadEdge EdgeBetween(Vec2 a, Vec2 b)
        {
            lock (_edgeLookupLock)
            {
                if (_nodeIdAtPosition == null)
                {
                    _nodeIdAtPosition = new Dictionary<(long, long), int>();
                    foreach (RoadNode node in Data.Nodes)
                    {
                        _nodeIdAtPosition[PositionKey(node.P)] = node.Id;
                    }
                    _edgeBetweenNodes = new Dictionary<(int, int), RoadEdge>();
                    foreach (RoadEdge e in Data.Edges)
                    {
                        if (e == null || !e.Alive) continue;
                        _edgeBetweenNodes[(Math.Min(e.A.Id, e.B.Id), Math.Max(e.A.Id, e.B.Id))] = e;
                    }
                }
                if (!_nodeIdAtPosition.TryGetValue(PositionKey(a), out int na) || !_nodeIdAtPosition.TryGetValue(PositionKey(b), out int nb))
                {
                    return null;
                }
                return _edgeBetweenNodes.TryGetValue((Math.Min(na, nb), Math.Max(na, nb)), out RoadEdge edge) ? edge : null;
            }
        }

        private static (long, long) PositionKey(Vec2 p) => ((long)Math.Round(p.X * 1000.0), (long)Math.Round(p.Y * 1000.0));

        private Dictionary<(int, int), List<int>> _blocksBesideEdge;
        private HashSet<int> _blocksWithLots;

        /// <summary>The blocks a road edge runs along (one or two: the faces either side of it), by block index; empty for an edge at the plan's edge.</summary>
        public List<int> BlocksBeside(RoadEdge edge)
        {
            lock (_edgeLookupLock)
            {
                if (_blocksBesideEdge == null)
                {
                    EdgeBetween(edge.A.P, edge.B.P);   // the node lookup
                    _blocksBesideEdge = new Dictionary<(int, int), List<int>>();
                    for (int b = 0; b < Data.Blocks.Count; b++)
                    {
                        List<Vec2> poly = Data.Blocks[b].Poly;
                        int n = poly.Count;
                        for (int i = 0; i < n; i++)
                        {
                            if (!_nodeIdAtPosition.TryGetValue(PositionKey(poly[i]), out int p) || !_nodeIdAtPosition.TryGetValue(PositionKey(poly[(i + 1) % n]), out int q)) continue;
                            var key = (Math.Min(p, q), Math.Max(p, q));
                            if (!_blocksBesideEdge.TryGetValue(key, out List<int> list)) _blocksBesideEdge[key] = list = new List<int>(2);
                            if (!list.Contains(b)) list.Add(b);
                        }
                    }
                    _blocksWithLots = new HashSet<int>();
                    foreach (PlanLot lot in Data.Lots) _blocksWithLots.Add(lot.BlockIndex);
                }
                return _blocksBesideEdge.TryGetValue((Math.Min(edge.A.Id, edge.B.Id), Math.Max(edge.A.Id, edge.B.Id)), out List<int> beside) ? beside : NoneList;
            }
        }

        /// <summary>Whether any lot (so any building) was cut from a block.</summary>
        public bool BlockHasLots(int blockIndex)
        {
            lock (_edgeLookupLock)
            {
                if (_blocksWithLots == null)
                {
                    _blocksWithLots = new HashSet<int>();
                    foreach (PlanLot lot in Data.Lots) _blocksWithLots.Add(lot.BlockIndex);
                }
                return _blocksWithLots.Contains(blockIndex);
            }
        }

        /// <summary>The buildings of the mall complex on a mall block (its corridors, stores, garage and pads); empty for any other block.</summary>
        public BuildingSite[] MallSites(int blockIndex) =>
            _mallSites.TryGetValue(blockIndex, out BuildingSite[] sites) ? sites : NoSites;

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
                        if (b.IsOpenGround)
                        {
                            continue;   // an open car park is a lot, not a building
                        }
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
            x = _spawnX;
            z = _spawnZ;
            return _hasSpawn;
        }

        private bool FindSpawn(out double x, out double z)
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
                foreach (RoadEdge e in n.Edges)
                {
                    bridge |= e.Bridge;
                }
                if (bridge) continue;
                // Not under an expressway or beside its ramps: the player starts on an open street, not in the shadow of a pier.
                if (Data.Expressways.Covers(n.P, 30.0 * scale)) continue;

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

        private void IndexBuildings()
        {
            _buildingsByChunk.Clear();
            LandmarkChunks.Clear();
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
        }

        private void IndexAll()
        {
            // Every building of a mall complex (its corridors and stores, a garage, the pads), by the block it stands on: its car park is laid
            // round them (CitySurfaceBuilder.BuildMallGrounds).
            foreach (int blockIndex in Data.MallBlocks)
            {
                PlanBlock block = Data.Blocks[blockIndex];
                if (block.Outer == null) continue;
                var sites = new List<BuildingSite>();
                foreach (BuildingSite b in Buildings)
                {
                    if (!b.OnRoof && PlanMath.PointInPolygon(new Vec2(b.CentreX, b.CentreZ), block.Outer)) sites.Add(b);
                }
                _mallSites[blockIndex] = sites.ToArray();
            }

            foreach (EdgeGeometry g in Geometry.Edges)
            {
                if (g == null) continue;
                AddSegment(_edgesByChunk, g.A, g.B, g.HalfWidth + 3.0, g.Edge.Id);
            }

            foreach (JunctionGeometry j in Geometry.Junctions)
            {
                if (j == null) continue;
                if (j.Plate != null)
                {
                    AddPoly(_junctionsByChunk, j.Plate, j.Node.Id);
                }
            }

            // The expressways: each deck segment in every chunk its footprint (and its parapets and side faces) touches; a pier, a sign or a row
            // of barriers in the one chunk it stands in; a kerb cut in every chunk whose pavement it cuts.
            ExpresswayNetwork xw = Data.Expressways;
            foreach (ViaductPath path in xw.Paths)
            {
                for (int s = 0; s < path.Segments; s++)
                {
                    AddPoly(_viaductsByChunk, path.Footprint(s, 1.2), ViaductSegments.Count);
                    ViaductSegments.Add((path.Id, s));
                }
            }
            for (int i = 0; i < xw.Piers.Count; i++) Add(_piersByChunk, ChunkOf(xw.Piers[i].P.X), ChunkOf(xw.Piers[i].P.Y), i);
            for (int i = 0; i < xw.Signs.Count; i++) Add(_signsByChunk, ChunkOf(xw.Signs[i].P.X), ChunkOf(xw.Signs[i].P.Y), i);
            for (int i = 0; i < xw.Barriers.Count; i++) Add(_barriersByChunk, ChunkOf(xw.Barriers[i].P.X), ChunkOf(xw.Barriers[i].P.Y), i);
            for (int i = 0; i < xw.KerbCuts.Count; i++) AddPoly(_kerbCutsByChunk, xw.KerbCuts[i].Poly, i);

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

        /// <summary>How far inside its footprint a building's rectangle stays on every side (metres).</summary>
        public const double FitMargin = 0.3;

        /// <summary>The same for a street-wall lot (PlanLot.StreetWall): two neighbours end up this far apart twice over -- a joint, not a gap.</summary>
        public const double StreetWallFitMargin = 0.08;

        /// <summary>Two buildings whose facing walls are at most this far apart share a party wall (AddPartyWalls).</summary>
        public const double PartyWallReach = 0.6;

        /// <summary>
        /// How far the paving of a block -- pavement and everything inside it up to the walls -- stands above the bare ground: the road's lift plus the
        /// kerb (CitySurfaceBuilder.RoadLift + KerbHeight, which a test holds equal to this).
        /// </summary>
        public const double PavingRise = 0.23;

        /// <summary>How far above the paving a ground floor stands at least: the ground can bulge a few centimetres between the points it is sampled at.</summary>
        public const double FloorClearance = 0.05;

        /// <summary>The ground under a lot is sampled at most this far apart to find the highest of it (the floor).</summary>
        public const double GroundSampleMetres = 5.0;

        /// <summary>The paving in front of a door is sampled this far out from its wall, metres: at the wall's foot, and two metres out.</summary>
        private static readonly double[] DoorGroundOutMetres = { 0.3, 2.0 };

        /// <summary>... and this far along the wall from the door's middle, metres: its middle and both its edges (BuildingInterior.DoorWidth is 1.6).</summary>
        private static readonly double[] DoorGroundAlongMetres = { -0.8, 0.0, 0.8 };

        private static readonly string[][] RoofsByZone =
        {
            /* Core        */ new[] { "antenna", "antenna", "parapet", "", "antenna", "parapet", "", "water_tower" },
            /* Urban       */ new[] { "", "parapet", "antenna", "shed", "antenna", "parapet", "", "water_tower" },
            /* Residential */ new[] { "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower" },
            /* OldTown     */ new[] { "", "", "chimney", "chimney", "shed", "", "parapet", "water_tower" },
            /* Industrial  */ new[] { "shed", "shed", "chimney", "chimney", "", "water_tower", "shed", "water_tower" },
            /* Outskirts   */ new[] { "", "", "", "shed", "", "chimney", "shed", "water_tower" },
        };

        /// <summary>
        /// Whether the street a lot fronts (the edge of its block's outline nearest the middle of its front) is too steep to drive, and so steps
        /// (StepStreets.Steep): no garage or car park is put there.
        /// </summary>
        private static bool FrontsSteepStreet(CityPlanData data, PlanFields fields, PlanLot lot)
        {
            if (lot.Footprint == null || lot.BlockIndex < 0 || lot.BlockIndex >= data.Blocks.Count) return false;
            PlanBlock block = data.Blocks[lot.BlockIndex];
            int n = lot.Footprint.Length, m = block.Poly.Count;
            Vec2 front = (lot.Footprint[lot.FrontEdge] + lot.Footprint[(lot.FrontEdge + 1) % n]) * 0.5;
            int nearest = -1;
            double best = double.MaxValue;
            for (int e = 0; e < m; e++)
            {
                double d = PlanMath.DistanceToSegment(front, block.Poly[e], block.Poly[(e + 1) % m], out _);
                if (d < best)
                {
                    best = d;
                    nearest = e;
                }
            }
            return nearest >= 0 && StepStreets.Steep(fields, block.Poly[nearest], block.Poly[(nearest + 1) % m], block.EdgeClass[nearest]);
        }

        public static BuildingSite[] Resolve(CityPlanData data, PlanFields fields, uint seed) => Resolve(data, fields, seed, out _);

        /// <summary>The buildings, and for each the plan lot it stands on (<paramref name="lotOf"/>: the blocks of one building share their lot).</summary>
        public static BuildingSite[] Resolve(CityPlanData data, PlanFields fields, uint seed, out int[] lotOf)
        {
            var list = new List<BuildingSite>(data.Lots.Count);
            var blockOf = new List<int>(data.Lots.Count);
            var lots = new List<int>(data.Lots.Count);
            uint characterSeed = PlanNoise.Mix(seed, 0x7A6F6E65U);   // "zone"
            var parking = new Amenities.ParkingPlanner();

            foreach (PlanLot lot in data.Lots)
            {
                if (!FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 centre, out Vec2 ex, out double width, out double depth, lot.Scale,
                    lot.StreetWall ? StreetWallFitMargin : FitMargin))
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

                // The set pieces: a mall (its block was cut as one lot for it), a car park (handed out here, rarely and spaced out). Their own
                // dice, so the dice above keep meaning what they always did for every other building.
                // Never onto a street too steep to drive, which is steps (StepStreets): its drive would meet a flight.
                LotUse use = lot.Use == LotUse.Building && FrontsSteepStreet(data, fields, lot)
                    ? LotUse.Building
                    : parking.Decide(zone, lot, centre, width, depth, PlanNoise.Mix(h, 0x7061726BU) / 4294967296.0);   // "park"
                string special = "";
                if (use != LotUse.Building)
                {
                    uint levelsHash = PlanNoise.Mix(h, 0x6C76656CU);   // "lvel"
                    switch (use)
                    {
                        case LotUse.Mall:
                            // The whole block's rectangle: the mall complex stands at its back (Amenities.MallLayout), the rest is its car park.
                            special = SpecialTypes.Mall;
                            height = (levelsHash % 5U < 2U ? 2 : 3) * Amenities.MallLevelHeight;
                            mass = BuildingMass.Warehouse;
                            break;
                        case LotUse.ParkingGarage:
                            special = SpecialTypes.ParkingGarage;
                            height = (Amenities.MinGarageLevels + (int)(levelsHash % (uint)(Amenities.MaxGarageLevels - Amenities.MinGarageLevels + 1)))
                                * Amenities.GarageFloorHeight;
                            mass = BuildingMass.MidRise;
                            break;
                        default:
                            special = SpecialTypes.ParkingLot;
                            height = Amenities.ParkingLotHeight;
                            mass = BuildingMass.LowRise;
                            break;
                    }
                    roof = "";
                    basement = false;
                }

                // The ground under the footprint, sampled on a grid at most GroundSampleMetres apart (corners included): the floor stands above
                // the highest of it, and a sparse sample missed a bulge the paving then came up through.
                // (Local +Z is the front, so the door is at (0, +depth/2).)
                double axisX = ex.X, axisZ = ex.Y;
                double hi = double.MinValue, lo = double.MaxValue;
                int stepsX = Math.Max(2, (int)Math.Ceiling(width / GroundSampleMetres)), stepsZ = Math.Max(2, (int)Math.Ceiling(depth / GroundSampleMetres));
                for (int iz = 0; iz <= stepsZ; iz++)
                {
                    for (int ix = 0; ix <= stepsX; ix++)
                    {
                        double lx = (ix / (double)stepsX - 0.5) * width;
                        double lz = (iz / (double)stepsZ - 0.5) * depth;
                        double wx = centre.X + lx * axisX - lz * axisZ;
                        double wz = centre.Y + lx * axisZ + lz * axisX;
                        double gy = fields.Height(wx, wz);
                        if (gy > hi) hi = gy;
                        if (gy < lo) lo = gy;
                    }
                }
                // A flat floor level with (or a step above) the paving round it, on whole decimetres. The block is paved kerb to wall at the
                // pavement's height (PavingRise above the ground); a floor at the bare ground's height had the paving lapping over its threshold.
                double baseHeight = Math.Ceiling((hi + PavingRise + FloorClearance) * 10.0 - 1e-6) / 10.0;

                // The shape: one box, a stack of them (a tower on a podium, a crown that steps in), a bar with wings (L, T, U, a staggered pair) or a
                // sculpted tower (twisted, wonky, cantilevered). Drawn last, so the roof and basement rolled above are what they would have been anyway.
                List<MassVolume> volumes;
                bool hasPodium;
                BuildingForm form;
                // A mall is a complex of boxes, each its own kind (the concourse, its anchors, a garage, the pads by the road), each with the floor
                // the ground under it calls for -- not the highest ground of the whole block, which is mostly car park.
                Amenities.MallPart[] mallParts = null;
                double[] mallBase = null, mallLow = null;
                if (use == LotUse.Mall)
                {
                    List<Amenities.MallPiece> pieces = Amenities.MallLayout.Compose(width, depth, s, height, PlanNoise.Mix(h, 0x6D6C6179U),   // "mlay"
                        data.Blocks[lot.BlockIndex].MallParking);
                    volumes = new List<MassVolume>(pieces.Count);
                    mallParts = new Amenities.MallPart[pieces.Count];
                    mallBase = new double[pieces.Count];
                    mallLow = new double[pieces.Count];
                    double groupHigh = double.MinValue;
                    var pieceHigh = new double[pieces.Count];
                    for (int p = 0; p < pieces.Count; p++)
                    {
                        volumes.Add(pieces[p].Volume);
                        mallParts[p] = pieces[p].Part;
                        GroundUnder(fields, centre, axisX, axisZ, pieces[p].Volume, out pieceHigh[p], out mallLow[p]);
                        if (pieces[p].Part == Amenities.MallPart.Concourse || pieces[p].Part == Amenities.MallPart.Anchor)
                        {
                            groupHigh = Math.Max(groupHigh, pieceHigh[p]);
                        }
                    }
                    for (int p = 0; p < pieces.Count; p++)
                    {
                        // The concourse and its anchors share one floor, so the anchors open level into it; a garage and a pad stand on their own.
                        bool group = mallParts[p] == Amenities.MallPart.Concourse || mallParts[p] == Amenities.MallPart.Anchor;
                        double high = group ? groupHigh : pieceHigh[p];
                        mallBase[p] = Math.Ceiling((high + PavingRise + FloorClearance) * 10.0 - 1e-6) / 10.0;
                    }
                    hasPodium = false;
                    form = BuildingForm.Box;
                }
                else if (use != LotUse.Building)
                {
                    // A set piece is one plain box its own builder fills in.
                    volumes = new List<MassVolume> { new MassVolume(-width * 0.5, width * 0.5, -depth * 0.5, depth * 0.5, height) };
                    hasPodium = false;
                    form = BuildingForm.Box;
                }
                else
                {
                    volumes = Massing.Compose(zone, mass, width, depth, height, rng, out hasPodium, out form, s, hs);
                }
                if (!InsideLot(volumes, width, depth))
                {
                    // A shape whose minimum sizes outgrew a narrow lot: downtown the neighbour stands right at the lot line (PlanLot.StreetWall), so
                    // a block poking past it would stand inside the next building. A plain box always fits.
                    volumes = new List<MassVolume> { new MassVolume(-width * 0.5, width * 0.5, -depth * 0.5, depth * 0.5, height) };
                    hasPodium = false;
                    form = BuildingForm.Box;
                }
                if (AnyBlockInWater(fields, volumes, centre, axisX, axisZ))
                {
                    // A narrow river channel can run through the middle of a lot between the corners and centre the lot was checked at
                    // (CityPlan.LotSubdivider); a building whose block would stand in it is not built.
                    continue;
                }
                // The shared floor also clears the ground at the foot of every block at street level -- its corners and the middles of its sides --
                // not only the lot's own samples: a wing's corner fell between them and the paving came up 8 cm through its floor (BlockPavingTests,
                // 2026-10-15). A mall's pieces have floors of their own (above).
                if (mallBase == null)
                {
                    foreach (MassVolume vol in volumes)
                    {
                        if (vol.Support < 0)
                        {
                            double foot = HighestAtFoot(fields, centre, axisX, axisZ, vol);
                            baseHeight = Math.Max(baseHeight, Math.Ceiling((foot + PavingRise + FloorClearance) * 10.0 - 1e-6) / 10.0);
                        }
                    }
                }
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

                    string volumeSpecial = special;
                    if (mallParts != null)
                    {
                        switch (mallParts[v])
                        {
                            case Amenities.MallPart.Anchor:
                                volumeSpecial = SpecialTypes.MallAnchor;
                                break;
                            case Amenities.MallPart.Garage:
                                volumeSpecial = SpecialTypes.ParkingGarage;
                                volumeMass = BuildingMass.MidRise;
                                volumeSeed = unchecked((int)PlanNoise.Mix(h, 0x67617261U));   // "gara"
                                break;
                            case Amenities.MallPart.Pad:
                                // A pad is an ordinary little building (a restaurant, a bank): its own look, a shop front, a parapet roof.
                                volumeSpecial = "";
                                volumeMass = BuildingMass.LowRise;
                                volumeRoof = "parapet";
                                volumeSeed = unchecked((int)PlanNoise.Mix(h, (uint)(0x70616400 + v)));   // "pad."
                                break;
                        }
                    }

                    var volumeParameters = new BuildingParams(true, (float)vol.Width, (float)vol.Height, (float)vol.Depth,
                        0.0f, 0.0f, volumeRoof, 0, volumeSpecial, v == 0 && basement, volumeMass);

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
                    double vBase = ground ? (mallBase != null ? mallBase[v] : baseHeight) : bases[vol.Support] + volumes[vol.Support].Height + 0.05;
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
                        podiumBase = Math.Min(podiumBase, mallLow != null ? mallLow[v] : lo);
                        // The paving a body walks over to the door: the LOWEST of it at the wall's foot and two metres out, under the door's middle and
                        // both its edges (a polygon's front side stands at its apothem, not its half-depth). One point two metres out read a door as
                        // flush over a dip at the wall's foot that left a 0.77 m step and no stairs (RealCityDoorwayPlayTests, 2026-10-15).
                        double front = vol.Sides >= 3 ? vol.Width * 0.5 * BuildingSite.CosPiOver(vol.Sides) : vol.Depth * 0.5;
                        double lowest = double.MaxValue;
                        foreach (double outward in DoorGroundOutMetres)
                        {
                            foreach (double along in DoorGroundAlongMetres)
                            {
                                double d = front + outward;
                                lowest = Math.Min(lowest, fields.Height(cx + along * vAxisX - d * vAxisZ, cz + along * vAxisZ + d * vAxisX));
                            }
                        }
                        doorGround = lowest + PavingRise;
                    }

                    list.Add(new BuildingSite(list.Count, volumeParameters, zone, (float)vBase, (float)podiumBase, (float)doorGround,
                        cx, cz, vAxisX, vAxisZ, PlanWorld.ChunkOf(cx), PlanWorld.ChunkOf(cz), volumeSeed,
                        ox0, ox1, oz0, oz1, !ground, vol.Shell, vol.Wing,
                        contacts[v] != null ? contacts[v].ToArray() : null,
                        !isTop && volumes[above].Shell, vol.Sides, (float)baseHeight));
                    blockOf.Add(lot.BlockIndex);
                    lots.Add(lot.Id);
                }
            }
            BuildingSite[] sites = list.ToArray();
            AddPartyWalls(sites, blockOf, lots);
            lotOf = lots.ToArray();
            return sites;
        }

        /// <summary>
        /// Party walls: where the street-level blocks of two DIFFERENT buildings in one city block stand against each other (a downtown street wall,
        /// LotSubdivider.StreetWallPopulation), each gets a contact on the wall that faces the other, so neither puts windows, ornament or corner piers
        /// into the joint -- the blank-wall rule the blocks of one building already follow (ContactsBetween). Only neighbours turned the same way or a
        /// quarter turn apart are matched (the lots along one frontage); anything at an angle keeps its gap and its windows.
        /// </summary>
        public static void AddPartyWalls(BuildingSite[] sites, IList<int> blockOf, IList<int> lotOf)
        {
            var byBlock = new Dictionary<int, List<int>>();
            var order = new List<int>();
            for (int i = 0; i < sites.Length; i++)
            {
                BuildingSite s = sites[i];
                if (s.OnRoof || s.Sides >= 3 || s.IsOpenGround)
                {
                    continue;
                }
                if (!byBlock.TryGetValue(blockOf[i], out List<int> members))
                {
                    byBlock[blockOf[i]] = members = new List<int>();
                    order.Add(blockOf[i]);
                }
                members.Add(i);
            }

            var added = new List<WallContact>[sites.Length];
            foreach (int block in order)
            {
                List<int> members = byBlock[block];
                foreach (int a in members)
                {
                    foreach (int b in members)
                    {
                        if (a != b && lotOf[a] != lotOf[b])
                        {
                            PartyWall(sites[a], sites[b], ref added[a]);
                        }
                    }
                }
            }

            for (int i = 0; i < sites.Length; i++)
            {
                if (added[i] == null)
                {
                    continue;
                }
                if (sites[i].Contacts != null)
                {
                    added[i].AddRange(sites[i].Contacts);
                }
                sites[i] = sites[i].WithContacts(added[i].ToArray());
            }
        }

        /// <summary>The contacts building <paramref name="b"/> makes on the walls of building <paramref name="a"/>, if they stand against each other.</summary>
        private static void PartyWall(in BuildingSite a, in BuildingSite b, ref List<WallContact> into)
        {
            double cos = a.AxisX * b.AxisX + a.AxisZ * b.AxisZ;
            // Square to each other within a third of a degree: a tilt of a degree opens up a real gap along a 40 m wall (seed 61839025, #1249).
            if (Math.Abs(cos) < 0.99998 && Math.Abs(cos) > 0.005)
            {
                return;
            }
            double hw = a.Params.Width * 0.5, hd = a.Params.Depth * 0.5;
            double reach = Math.Max(hw, hd) + Math.Max(b.Params.Width, b.Params.Depth) + PartyWallReach;
            double dx = b.CentreX - a.CentreX, dz = b.CentreZ - a.CentreZ;
            if (dx * dx + dz * dz > reach * reach)
            {
                return;
            }

            // b's footprint as a box in a's frame (relative to a's centre).
            double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
            double bhw = b.Params.Width * 0.5, bhd = b.Params.Depth * 0.5;
            for (int k = 0; k < 4; k++)
            {
                b.LocalToWorld((k & 1) == 0 ? -bhw : bhw, (k & 2) == 0 ? -bhd : bhd, out double wx, out double wz);
                a.WorldToLocal(wx, wz, out double lx, out double lz);
                x0 = Math.Min(x0, lx);
                x1 = Math.Max(x1, lx);
                z0 = Math.Min(z0, lz);
                z1 = Math.Max(z1, lz);
            }

            float top = b.Top - a.BaseHeight;
            if (top <= 0.0f)
            {
                return;
            }
            const double overlap = 0.3, behind = 0.05;
            // East and west walls run along z (measured from a's -z end), north and south along x (from its -x end).
            double zLo = Math.Max(-hd, z0), zHi = Math.Min(hd, z1);
            double xLo = Math.Max(-hw, x0), xHi = Math.Min(hw, x1);
            if (zHi - zLo > overlap && InReach(x0 - hw))
            {
                Add(ref into, new WallContact(WindowGrid.Wall.East, (float)(zLo + hd), (float)(zHi + hd), top));
            }
            if (zHi - zLo > overlap && InReach(-hw - x1))
            {
                Add(ref into, new WallContact(WindowGrid.Wall.West, (float)(zLo + hd), (float)(zHi + hd), top));
            }
            if (xHi - xLo > overlap && InReach(z0 - hd))
            {
                Add(ref into, new WallContact(WindowGrid.Wall.North, (float)(xLo + hw), (float)(xHi + hw), top));
            }
            if (xHi - xLo > overlap && InReach(-hd - z1))
            {
                Add(ref into, new WallContact(WindowGrid.Wall.South, (float)(xLo + hw), (float)(xHi + hw), top));
            }

            bool InReach(double gap) => gap >= -behind && gap <= PartyWallReach;
        }

        private static void Add(ref List<WallContact> into, WallContact contact)
        {
            (into ?? (into = new List<WallContact>(2))).Add(contact);
        }

        /// <summary>The highest and lowest ground under one volume of a lot (on a grid at most GroundSampleMetres apart, corners included).</summary>
        private static void GroundUnder(PlanFields fields, Vec2 centre, double axisX, double axisZ, MassVolume vol, out double high, out double low)
        {
            high = double.MinValue;
            low = double.MaxValue;
            // The volume's extent in the lot frame (a quarter-turned one lies across it).
            Box(vol, out double bx0, out double bx1, out double bz0, out double bz1);
            double sizeX = bx1 - bx0, sizeZ = bz1 - bz0;
            int stepsX = Math.Max(2, (int)Math.Ceiling(sizeX / GroundSampleMetres)), stepsZ = Math.Max(2, (int)Math.Ceiling(sizeZ / GroundSampleMetres));
            for (int iz = 0; iz <= stepsZ; iz++)
            {
                for (int ix = 0; ix <= stepsX; ix++)
                {
                    double lx = vol.CentreX + (ix / (double)stepsX - 0.5) * sizeX;
                    double lz = vol.CentreZ + (iz / (double)stepsZ - 0.5) * sizeZ;
                    double gy = fields.Height(centre.X + lx * axisX - lz * axisZ, centre.Y + lx * axisZ + lz * axisX);
                    if (gy > high) high = gy;
                    if (gy < low) low = gy;
                }
            }
        }

        /// <summary>Whether every block of a building, turned ones by their corners, stays inside its lot rectangle (a few centimetres of rounding allowed).</summary>
        public static bool InsideLot(List<MassVolume> volumes, double width, double depth)
        {
            const double slack = 0.02;
            double hx = width * 0.5 + slack, hz = depth * 0.5 + slack;
            var xs = new double[4];
            var zs = new double[4];
            foreach (MassVolume v in volumes)
            {
                v.Corners(xs, zs);
                for (int k = 0; k < 4; k++)
                {
                    if (Math.Abs(xs[k]) > hx || Math.Abs(zs[k]) > hz)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Whether the centre or a corner of any street-level block of a building is over water.</summary>
        private static bool AnyBlockInWater(PlanFields fields, List<MassVolume> volumes, Vec2 centre, double axisX, double axisZ)
        {
            var xs = new double[4];
            var zs = new double[4];
            foreach (MassVolume v in volumes)
            {
                if (v.Support >= 0)
                {
                    continue;
                }
                v.Corners(xs, zs);
                for (int k = -1; k < 4; k++)
                {
                    double lx = k < 0 ? v.CentreX : xs[k];
                    double lz = k < 0 ? v.CentreZ : zs[k];
                    if (fields.IsWater(centre.X + lx * axisX - lz * axisZ, centre.Y + lx * axisZ + lz * axisX))
                    {
                        return true;
                    }
                }
            }
            return false;
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

        /// <summary>The highest ground at a block's foot: its four corners and the middles of its four sides.</summary>
        private static double HighestAtFoot(PlanFields fields, Vec2 centre, double axisX, double axisZ, MassVolume vol)
        {
            Box(vol, out double x0, out double x1, out double z0, out double z1);
            double high = double.MinValue;
            for (int iz = 0; iz <= 2; iz++)
            {
                for (int ix = 0; ix <= 2; ix++)
                {
                    if (ix == 1 && iz == 1)
                    {
                        continue;
                    }
                    double lx = x0 + (x1 - x0) * ix * 0.5, lz = z0 + (z1 - z0) * iz * 0.5;
                    high = Math.Max(high, fields.Height(centre.X + lx * axisX - lz * axisZ, centre.Y + lx * axisZ + lz * axisX));
                }
            }
            return high;
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
        public static bool FitRectangle(Vec2[] poly, int front, out Vec2 centre, out Vec2 ex, out double width, out double depth, double scale = 1.0,
            double margin = FitMargin)
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
                    double z = -0.15 - dep * (s / 8.0);   // the whole depth the rectangle will cover, from its front (-0.15) to its back
                    ok = Chord(lx, lz, z, xs, out double a, out double b);
                    if (ok)
                    {
                        if (a > xmin) xmin = a;
                        if (b < xmax) xmax = b;
                    }
                }
                // ... and at every corner of the lot over that depth, and just either side of it: the lot's sides are straight between its
                // corners, so the narrowest it gets over the depth is at one of them or at an end. The eight even steps alone let a corner
                // between two of them poke into the rectangle (seed 70485213, lot 8103: a street wall's back corner 5 cm into the next lot).
                for (int i = 0; i < n && ok; i++)
                {
                    for (int side = -1; side <= 1 && ok; side++)
                    {
                        double z = lz[i] + side * CornerProbe;
                        if (z >= -0.15 || z <= -0.15 - dep) continue;
                        ok = Chord(lx, lz, z, xs, out double a, out double b);
                        if (ok)
                        {
                            if (a > xmin) xmin = a;
                            if (b < xmax) xmax = b;
                        }
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

            width = bestX1 - bestX0 - 2.0 * margin;
            depth = bestDepth - 2.0 * margin;
            if (width < minSide || depth < minSide) return false;
            double cx = (bestX0 + bestX1) * 0.5;
            double cz = -0.15 - bestDepth * 0.5 + 0.0;
            centre = o + ex * cx + ez * cz;
            return true;
        }

        /// <summary>How far either side of a lot's corner FitRectangle also measures the lot (a corner that is a peak has no width at its own depth).</summary>
        private const double CornerProbe = 1e-3;

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
