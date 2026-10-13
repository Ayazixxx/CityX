using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>A piece of land completely surrounded by roads, as a counter-clockwise polygon along the road centrelines.</summary>
    public sealed class PlanBlock
    {
        public List<Vec2> Poly = new List<Vec2>();

        /// <summary>The class of the road along edge i (from Poly[i] to Poly[i+1]).</summary>
        public List<RoadClass> EdgeClass = new List<RoadClass>();

        public double Area;

        /// <summary>The block shrunk by each road's half width: where the kerb stands. Same vertex count as Poly (null if the inset failed).</summary>
        public List<Vec2> Inner;

        /// <summary>The block shrunk by half width plus pavement: the edge of the lots. Same vertex count as Poly.</summary>
        public List<Vec2> Outer;

        /// <summary>
        /// <see cref="Inner"/> cut into triangles (three indices each): the block is paved as ONE surface from the kerb to the walls of its buildings
        /// (CitySurfaceBuilder), and a concave block cannot be drawn as a fan. Null when there is no kerb line, or for a block that <see cref="Encloses"/>
        /// another (its middle is the island's).
        /// </summary>
        public int[] InnerTriangles;

        /// <summary>The block is paved kerb to kerb (it has an outline to pave and no island inside it).</summary>
        public bool Paved => InnerTriangles != null;

        /// <summary>
        /// Another block lies inside this one's outline: a loop of road standing alone in the middle of it, which the block's outline does not go
        /// round (a face of the road graph with a hole in it). Cutting lots from the outline would lay them over the island's own buildings, so such a
        /// block keeps its pavement and is left open ground. Seen with seed 87650022 (CityScale classic): one plot 432 m long over an island's houses.
        /// </summary>
        public bool Encloses;

        /// <summary>A pedestrian plaza: no lots, the whole block paved and furnished (Amenities.ChoosePlazas, CitySurfaceBuilder).</summary>
        public bool Plaza;

        /// <summary>A shopping mall takes the whole block as one lot (Amenities.ChooseMalls).</summary>
        public bool Mall;

        /// <summary>A mall's parking: always the open-air lot round it, now and then a garage beside it as well (MallParking).</summary>
        public MallParking MallParking;
    }

    /// <summary>
    /// One building plot: the lot a block was cut into, and the footprint set back inside it.
    /// Walls are the edges of <see cref="Footprint"/>; edge <see cref="FrontEdge"/> faces the street.
    /// </summary>
    public sealed class PlanLot
    {
        public int Id;
        public Vec2[] Lot;
        public Vec2[] Footprint;
        public int FrontEdge;
        public double Area;
        public int BlockIndex;

        public double Population;
        public double Height;

        /// <summary>The district scale this lot was cut at (see CityScale): how much bigger than the classic city, and how much taller.</summary>
        public double Scale = 1.0;
        public double HeightScale = 1.0;
        public RoadClass FrontClass;

        /// <summary>
        /// Downtown (LotSubdivider.StreetWallPopulation): the building fills its lot to the lot lines it shares with its neighbours, so the street
        /// is lined by one continuous wall of buildings with party walls between them, instead of a row of detached blocks with gaps.
        /// </summary>
        public bool StreetWall;

        /// <summary>What stands on the lot. A mall is decided here (its block is one lot); parking is decided as the buildings are resolved (Amenities.ParkingPlanner).</summary>
        public LotUse Use;
    }

    /// <summary>The whole generated plan for one region.</summary>
    public sealed class CityPlanData
    {
        public PlanConfig Config;
        public PlanFields Fields;
        public List<Vec2> Centres;
        public List<RoadNode> Nodes;
        public List<RoadEdge> Edges;
        public List<PlanBlock> Blocks = new List<PlanBlock>();
        public List<PlanLot> Lots = new List<PlanLot>();

        /// <summary>The expressways: their routes, decks, interchanges, piers and the land kept clear under them (ExpresswayPlanner).</summary>
        public ExpresswayNetwork Expressways = new ExpresswayNetwork();

        public List<int> PlazaBlocks = new List<int>();
        public List<int> MallBlocks = new List<int>();
    }

    public static class CityPlan
    {
        /// <summary>Generates a region's plan from the world seed. A pure function of (seed, config).</summary>
        public static CityPlanData Generate(int worldSeed, PlanConfig config = null)
        {
            PlanConfig cfg = config ?? new PlanConfig();
            var fields = new PlanFields(worldSeed, cfg);

            List<Vec2> centres = fields.FindCentres(cfg.Half, 100.0 * cfg.Scale, 650.0 * cfg.Scale, 0.52, 6);
            if (centres.Count == 0)
            {
                centres.Add(new Vec2(0.0, 0.0));
            }

            uint roadSeed = PlanNoise.Mix(unchecked((uint)worldSeed), 0x726F6164U);   // "road"
            var net = new RoadNetwork(fields, roadSeed);
            net.Grow(centres, cfg.Half);

            var data = new CityPlanData
            {
                Config = cfg,
                Fields = fields,
                Centres = centres,
                Nodes = net.Nodes,
                Edges = net.Edges,
            };

            // The expressways go over the finished network, before the blocks are cut into lots: decks and ramps are geometry over the land, not
            // roads of the graph, and no lot is cut under them.
            data.Expressways = ExpresswayPlanner.Plan(net, fields, cfg, PlanNoise.Mix(unchecked((uint)worldSeed), 0x78776179U));   // "xway"

            data.Blocks = BlockExtractor.Extract(net, cfg);
            MarkEnclosing(data.Blocks);

            uint lotSeed = PlanNoise.Mix(unchecked((uint)worldSeed), 0x6C6F7473U);    // "lots"
            LotSubdivider.Subdivide(data, lotSeed);
            return data;
        }

        /// <summary>Sets <see cref="PlanBlock.Encloses"/> on every block with another block inside its outline (a vertex of the smaller one inside it).</summary>
        public static void MarkEnclosing(List<PlanBlock> blocks)
        {
            int n = blocks.Count;
            var boxes = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
                foreach (Vec2 p in blocks[i].Poly)
                {
                    x0 = Math.Min(x0, p.X);
                    z0 = Math.Min(z0, p.Y);
                    x1 = Math.Max(x1, p.X);
                    z1 = Math.Max(z1, p.Y);
                }
                boxes[i] = new[] { x0, z0, x1, z1 };
            }
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n && !blocks[a].Encloses; b++)
                {
                    if (a == b || blocks[b].Area >= blocks[a].Area || blocks[b].Poly.Count == 0)
                    {
                        continue;
                    }
                    double[] A = boxes[a], B = boxes[b];
                    if (B[0] <= A[0] || B[1] <= A[1] || B[2] >= A[2] || B[3] >= A[3])
                    {
                        continue;   // not strictly inside the bigger one's box
                    }
                    // The island's middle (average of its corners) inside the outline. A neighbour sharing an edge never has its middle inside.
                    double cx = 0.0, cz = 0.0;
                    foreach (Vec2 p in blocks[b].Poly)
                    {
                        cx += p.X;
                        cz += p.Y;
                    }
                    cx /= blocks[b].Poly.Count;
                    cz /= blocks[b].Poly.Count;
                    if (Inside(blocks[a].Poly, cx, cz))
                    {
                        blocks[a].Encloses = true;
                    }
                }
            }
        }

        private static bool Inside(List<Vec2> poly, double x, double z)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].Y > z) != (poly[j].Y > z)
                    && x < (poly[j].X - poly[i].X) * (z - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public static double HalfWidth(PlanConfig cfg, RoadClass c)
        {
            switch (c)
            {
                case RoadClass.Boulevard: return cfg.BoulevardHalfWidth;
                case RoadClass.Arterial: return cfg.ArterialHalfWidth;
                default: return cfg.StreetHalfWidth;
            }
        }
    }

    /// <summary>
    /// Finds every block: the faces of the planar graph the roads make. Dead-end streets are pruned first (a cul-de-sac
    /// does not enclose land), then faces are traced half-edge by half-edge, interior on the left.
    /// </summary>
    public static class BlockExtractor
    {
        public static List<PlanBlock> Extract(RoadNetwork net, PlanConfig cfg)
        {
            int edgeCount = net.Edges.Count;
            var active = new bool[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                active[i] = net.Edges[i].Alive;
            }

            PruneSpurs(net, active);

            // A road with the same block on both sides (it joins two blocks, or hangs inside one) would be walked
            // twice in one face and make a corridor; take such roads out and trace again until none is left.
            for (int pass = 0; pass < 12; pass++)
            {
                List<int[]> faces = TraceFaces(net, active, out int[] halfFace);
                bool removed = false;
                for (int e = 0; e < edgeCount; e++)
                {
                    if (active[e] && halfFace[2 * e] == halfFace[2 * e + 1] && halfFace[2 * e] >= 0)
                    {
                        active[e] = false;
                        removed = true;
                    }
                }
                if (!removed)
                {
                    break;
                }
                PruneSpurs(net, active);
            }

            var blocks = new List<PlanBlock>();
            List<int[]> finalFaces = TraceFaces(net, active, out _);
            foreach (int[] face in finalFaces)
            {
                var block = new PlanBlock();
                foreach (int half in face)
                {
                    RoadEdge e = net.Edges[half >> 1];
                    RoadNode from = (half & 1) == 0 ? e.A : e.B;
                    block.Poly.Add(from.P);
                    block.EdgeClass.Add(e.Class);
                }
                double area = PlanMath.SignedArea(block.Poly);
                if (area < 400.0 * cfg.MinScale * cfg.MinScale)
                {
                    continue;   // the outer face (negative) and slivers
                }
                block.Area = area;
                blocks.Add(block);
            }
            return blocks;
        }

        private static void PruneSpurs(RoadNetwork net, bool[] active)
        {
            var degree = new int[net.Nodes.Count];
            bool changed = true;
            while (changed)
            {
                changed = false;
                Array.Clear(degree, 0, degree.Length);
                for (int e = 0; e < net.Edges.Count; e++)
                {
                    if (!active[e]) continue;
                    degree[net.Edges[e].A.Id]++;
                    degree[net.Edges[e].B.Id]++;
                }
                for (int e = 0; e < net.Edges.Count; e++)
                {
                    if (!active[e]) continue;
                    if (degree[net.Edges[e].A.Id] <= 1 || degree[net.Edges[e].B.Id] <= 1)
                    {
                        active[e] = false;
                        changed = true;
                    }
                }
            }
        }

        /// <summary>Traces every face. Returns each as a list of half-edge ids; halfFace maps a half-edge to its face (-1 if its edge is inactive).</summary>
        private static List<int[]> TraceFaces(RoadNetwork net, bool[] active, out int[] halfFace)
        {
            int edgeCount = net.Edges.Count;
            halfFace = new int[edgeCount * 2];
            for (int i = 0; i < halfFace.Length; i++) halfFace[i] = -1;

            // Outgoing half-edges of each node, sorted counter-clockwise.
            var outgoing = new List<int>[net.Nodes.Count];
            for (int e = 0; e < edgeCount; e++)
            {
                if (!active[e]) continue;
                RoadEdge edge = net.Edges[e];
                Add(outgoing, edge.A.Id, 2 * e);
                Add(outgoing, edge.B.Id, 2 * e + 1);
            }

            var position = new int[edgeCount * 2];
            for (int n = 0; n < outgoing.Length; n++)
            {
                List<int> list = outgoing[n];
                if (list == null) continue;
                RoadNode node = net.Nodes[n];
                list.Sort((h1, h2) =>
                {
                    double a1 = Angle(net, h1);
                    double a2 = Angle(net, h2);
                    int c = a1.CompareTo(a2);
                    return c != 0 ? c : h1.CompareTo(h2);
                });
                for (int i = 0; i < list.Count; i++)
                {
                    position[list[i]] = i;
                }
            }

            var faces = new List<int[]>();
            var visited = new bool[edgeCount * 2];
            for (int e = 0; e < edgeCount; e++)
            {
                if (!active[e]) continue;
                for (int side = 0; side < 2; side++)
                {
                    int start = 2 * e + side;
                    if (visited[start]) continue;
                    var face = new List<int>();
                    int h = start;
                    int guard = 0;
                    while (!visited[h] && guard++ < 4096)
                    {
                        visited[h] = true;
                        halfFace[h] = faces.Count;
                        face.Add(h);
                        RoadEdge edge = net.Edges[h >> 1];
                        RoadNode to = (h & 1) == 0 ? edge.B : edge.A;
                        int twin = h ^ 1;
                        List<int> around = outgoing[to.Id];
                        int idx = position[twin];
                        h = around[(idx + around.Count - 1) % around.Count];
                    }
                    faces.Add(face.ToArray());
                }
            }
            return faces;
        }

        private static void Add(List<int>[] lists, int node, int half)
        {
            if (lists[node] == null) lists[node] = new List<int>(4);
            lists[node].Add(half);
        }

        private static double Angle(RoadNetwork net, int half)
        {
            RoadEdge e = net.Edges[half >> 1];
            RoadNode from = (half & 1) == 0 ? e.A : e.B;
            RoadNode to = (half & 1) == 0 ? e.B : e.A;
            return PlanMath.PseudoAngle(to.P.X - from.P.X, to.P.Y - from.P.Y);
        }
    }

    /// <summary>
    /// Cuts each block into building lots, the way Parish and Muller do: recursively split across the long side
    /// of the plot until the pieces are lot-sized, and drop any piece that does not touch a street (the courtyard).
    /// Deep blocks are first split in two parallel to their frontage so lots sit back to back.
    /// </summary>
    public static class LotSubdivider
    {
        private sealed class Piece
        {
            public List<Vec2> V;
            public List<bool> Front;
        }

        private const double ClassicMinWidth = 12.0;
        private const double ClassicMaxDepth = 52.0;

        /// <summary>How far a building stands back from the pavement, and from the lot lines it shares with its neighbours (classic metres).</summary>
        public const double FrontSetback = 2.2;
        public const double SideSetback = 1.2;

        /// <summary>
        /// Above this population a block is downtown and its lots form a street wall (PlanLot.StreetWall): no gap to the neighbours, and only this
        /// much to the pavement (room for a shallow entrance step). Every dense city on earth looks like this; detached blocks with three-metre gaps
        /// between them were what made ours read as a suburb of towers.
        /// </summary>
        public const double StreetWallPopulation = 0.55;
        public const double StreetWallFrontSetback = 1.0;

        // The scale of the plan being subdivided (see CityScale); every length below is the classic one times this. Set at the start of Subdivide, which
        // runs to the end on one thread.
        [ThreadStatic] private static double _s;
        [ThreadStatic] private static double _h;
        private static double MinWidth => ClassicMinWidth * (_s <= 0.0 ? 1.0 : _s);
        private static double MaxDepth => ClassicMaxDepth * (_s <= 0.0 ? 1.0 : _s);

        public static void Subdivide(CityPlanData data, uint seed)
        {
            PlanConfig cfg = data.Config;
            PlanFields fields = data.Fields;
            int blockIndex = 0;
            _s = cfg.Scale;
            _h = cfg.HeightScale;

            // First every block's two outlines (the kerb and the lot line), so the set pieces can be chosen from the whole city before any lot is cut.
            foreach (PlanBlock block in data.Blocks)
            {
                int n = block.Poly.Count;
                var distances = new double[n];
                for (int i = 0; i < n; i++)
                {
                    distances[i] = CityPlan.HalfWidth(cfg, block.EdgeClass[i]) + cfg.SidewalkWidth;
                }

                var inset = new List<Vec2>();
                if (!PlanMath.InsetPolygon(block.Poly, distances, inset, out _))
                {
                    // Fold-over: lots are not cut from this block at all. (An earlier version retried with every
                    // road narrowed, which made lots whose edge sat on the wrong side of the kerb.)
                    continue;
                }

                // The kerb line (half width only) so the mesh can lay the pavement as the ring between the two.
                var kerbDistances = new double[n];
                for (int i = 0; i < n; i++)
                {
                    kerbDistances[i] = CityPlan.HalfWidth(cfg, block.EdgeClass[i]);
                }
                var kerb = new List<Vec2>();
                if (PlanMath.InsetPolygon(block.Poly, kerbDistances, kerb, out _))
                {
                    block.Inner = kerb;
                    block.Outer = inset;
                    if (!block.Encloses)
                    {
                        block.InnerTriangles = PlanMath.Triangulate(kerb);
                    }
                }
            }

            Amenities.ChooseMalls(data, PlanNoise.Mix(seed, 0x6D616C6CU));    // "mall"
            Amenities.ChoosePlazas(data, PlanNoise.Mix(seed, 0x706C617AU));   // "plaz"

            foreach (PlanBlock block in data.Blocks)
            {
                blockIndex++;
                if (block.Outer == null || block.Inner == null)
                {
                    continue;
                }
                List<Vec2> inset = block.Outer;
                if (block.Encloses || block.Plaza)
                {
                    continue;   // its pavement, but no lots: they would be laid over the island inside it, or over the plaza
                }

                Vec2 c = PlanMath.Centroid(inset);
                if (cfg.Varied)
                {
                    // This block's own size: its district, pushed a little either way so neighbours are not all alike.
                    double jitter = (PlanNoise.Mix(seed, (uint)(blockIndex * 3 + 5)) / 4294967296.0 - 0.5) * 0.4;
                    double t = PlanFields.BuildingSizeCurve(PlanMath.Clamp01(fields.DistrictAt(c.X, c.Y) + jitter));
                    _s = PlanFields.Lerp(cfg.MinScale, cfg.Scale, t);
                    _h = PlanFields.Lerp(cfg.MinHeightScale, cfg.HeightScale, t);
                }
                double pop = fields.Population(c);
                if (block.Mall)
                {
                    EmitMallLot(data, block, blockIndex - 1, inset, pop);
                    continue;
                }
                double baseArea = (380.0 + (1.0 - pop) * 1500.0) * _s * _s;
                double blockRand = PlanNoise.Mix(seed, (uint)blockIndex) / 4294967296.0;
                double targetArea = baseArea * (0.7 + 0.7 * blockRand);
                if (pop > 0.82)
                {
                    targetArea = Math.Max(targetArea, (1400.0 + 900.0 * blockRand) * _s * _s);   // downtown plots are big
                }

                var front = new List<bool>(inset.Count);
                for (int i = 0; i < inset.Count; i++) front.Add(true);
                var stack = new Stack<Piece>();
                stack.Push(new Piece { V = inset, Front = front });

                var done = new List<Piece>();
                int guard = 0;
                while (stack.Count > 0 && guard++ < 400)
                {
                    Piece p = stack.Pop();
                    if (!HasFront(p))
                    {
                        continue;   // a courtyard
                    }
                    double area = PlanMath.SignedArea(p.V);
                    if (area < 40.0 * _s * _s)
                    {
                        continue;
                    }
                    if (!TrySplit(p, area, targetArea, seed, guard + blockIndex * 977, out Piece a, out Piece b))
                    {
                        done.Add(p);
                        continue;
                    }
                    stack.Push(a);
                    stack.Push(b);
                }

                foreach (Piece lot in done)
                {
                    EmitLot(data, lot, seed, blockIndex - 1);
                }
            }
        }

        /// <summary>
        /// A mall's block as ONE lot: the whole lot line, with a shallow setback all round, facing the block's biggest road. The mall itself is
        /// the rectangle PlanBuildings fits in it; what is left of the lot round the rectangle is its car park.
        /// </summary>
        private static void EmitMallLot(CityPlanData data, PlanBlock block, int blockIndex, List<Vec2> inset, double pop)
        {
            int n = inset.Count;
            var distances = new double[n];
            for (int i = 0; i < n; i++)
            {
                distances[i] = FrontSetback * Math.Sqrt(_s);
            }
            var footprint = new List<Vec2>();
            if (!PlanMath.InsetPolygon(inset, distances, footprint, out _))
            {
                block.Mall = false;
                return;
            }
            data.Lots.Add(new PlanLot
            {
                Id = data.Lots.Count,
                BlockIndex = blockIndex,
                Lot = inset.ToArray(),
                Footprint = footprint.ToArray(),
                FrontEdge = Amenities.MallFront(block),
                Area = PlanMath.SignedArea(footprint),
                Population = pop,
                Height = Amenities.MallLevelHeight * 2.0,
                Scale = _s,
                HeightScale = _h,
                FrontClass = block.EdgeClass[Amenities.MallFront(block)],
                StreetWall = false,
                Use = LotUse.Mall,
            });
        }

        private static bool HasFront(Piece p)
        {
            for (int i = 0; i < p.Front.Count; i++)
            {
                if (p.Front[i]) return true;
            }
            return false;
        }

        private static bool TrySplit(Piece p, double area, double targetArea, uint seed, int salt, out Piece a, out Piece b)
        {
            a = null;
            b = null;
            int n = p.V.Count;

            // Frame: u along the longest street-facing edge, v across (the depth).
            int longest = -1;
            double longestLen = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (!p.Front[i]) continue;
                double len = (p.V[(i + 1) % n] - p.V[i]).Length;
                if (len > longestLen)
                {
                    longestLen = len;
                    longest = i;
                }
            }
            if (longest < 0) return false;

            Vec2 u = (p.V[(longest + 1) % n] - p.V[longest]).Normalized;
            Vec2 v = u.Perp;
            double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
            Vec2 origin = p.V[longest];
            for (int i = 0; i < n; i++)
            {
                Vec2 d = p.V[i] - origin;
                double pu = Vec2.Dot(d, u);
                double pv = Vec2.Dot(d, v);
                if (pu < minU) minU = pu;
                if (pu > maxU) maxU = pu;
                if (pv < minV) minV = pv;
                if (pv > maxV) maxV = pv;
            }
            double extentU = maxU - minU;
            double extentV = maxV - minV;
            double rand = PlanNoise.Mix(seed, (uint)salt) / 4294967296.0;
            double frac = 0.38 + 0.24 * rand;

            bool deep = extentV > MaxDepth * 1.7;
            bool wide = extentU > MinWidth * 2.0 && (area > targetArea || extentU > 60.0 * _s);

            if (deep)
            {
                // Parallel to the frontage, through the middle: lots back to back.
                Vec2 point = origin + v * (minV + (maxV - minV) * 0.5) ;
                return Cut(p, point, u, out a, out b);
            }
            if (wide)
            {
                Vec2 point = origin + u * (minU + extentU * frac);
                return Cut(p, point, v, out a, out b);
            }
            if (area > targetArea * 1.6 && extentV > MinWidth * 2.0)
            {
                Vec2 point = origin + v * (minV + extentV * frac);
                return Cut(p, point, u, out a, out b);
            }
            return false;
        }

        private static bool Cut(Piece p, Vec2 point, Vec2 dir, out Piece a, out Piece b)
        {
            a = null;
            b = null;
            int n = p.V.Count;
            var side = new double[n];
            for (int i = 0; i < n; i++)
            {
                side[i] = Vec2.Cross(dir, p.V[i] - point);
            }
            int ci = -1, cj = -1;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if ((side[i] < 0.0) != (side[j] < 0.0))
                {
                    if (ci < 0) ci = i;
                    else if (cj < 0) cj = i;
                    else return false;   // more than two crossings: a concave bite, leave this one whole
                }
            }
            if (cj < 0) return false;

            Vec2 pi = Cross(p.V[ci], p.V[(ci + 1) % n], side[ci], side[(ci + 1) % n]);
            Vec2 pj = Cross(p.V[cj], p.V[(cj + 1) % n], side[cj], side[(cj + 1) % n]);

            var va = new List<Vec2>();
            var fa = new List<bool>();
            va.Add(pi);
            fa.Add(p.Front[ci]);               // pi -> V[ci+1] keeps edge ci's frontage
            int k = (ci + 1) % n;
            while (true)
            {
                va.Add(p.V[k]);
                if (k == cj)
                {
                    fa.Add(p.Front[cj]);       // V[cj] -> pj
                    break;
                }
                fa.Add(p.Front[k]);
                k = (k + 1) % n;
            }
            va.Add(pj);
            fa.Add(false);                      // pj -> pi is the new cut

            var vb = new List<Vec2>();
            var fb = new List<bool>();
            vb.Add(pj);
            fb.Add(p.Front[cj]);
            k = (cj + 1) % n;
            while (true)
            {
                vb.Add(p.V[k]);
                if (k == ci)
                {
                    fb.Add(p.Front[ci]);
                    break;
                }
                fb.Add(p.Front[k]);
                k = (k + 1) % n;
            }
            vb.Add(pi);
            fb.Add(false);

            if (PlanMath.SignedArea(va) < 20.0 * _s * _s || PlanMath.SignedArea(vb) < 20.0 * _s * _s)
            {
                return false;
            }
            a = new Piece { V = va, Front = fa };
            b = new Piece { V = vb, Front = fb };
            return true;
        }

        private static Vec2 Cross(Vec2 p, Vec2 q, double sp, double sq)
        {
            double t = sp / (sp - sq);
            return p + (q - p) * t;
        }

        private static void EmitLot(CityPlanData data, Piece lot, uint seed, int blockIndex)
        {
            // An expressway beside the lot cuts it back to a margin from its decks and ramps (the lot keeps its own side); the land an interchange
            // encloses takes it whole. Nothing is built under a deck.
            if (!data.Expressways.CutBack(lot.V, lot.Front, out List<Vec2> cutV, out List<bool> cutFront))
            {
                return;
            }
            lot = new Piece { V = cutV, Front = cutFront };
            int n = lot.V.Count;
            double area = PlanMath.SignedArea(lot.V);
            if (area < 110.0 * _s * _s)
            {
                return;
            }
            PlanFields fields = data.Fields;

            // Footprint: set back from the street more than from its neighbours. Downtown there is no gap to the neighbours at all (a street wall)
            // and only a shallow one to the street.
            bool streetWall = fields.Population(PlanMath.Centroid(lot.V)) > StreetWallPopulation;
            var distances = new double[n];
            double? frontLen = null;
            int front = -1;
            for (int i = 0; i < n; i++)
            {
                distances[i] = streetWall
                    ? (lot.Front[i] ? StreetWallFrontSetback : 0.0) * Math.Sqrt(_s)
                    : (lot.Front[i] ? FrontSetback : SideSetback) * Math.Sqrt(_s);
                if (lot.Front[i])
                {
                    double len = (lot.V[(i + 1) % n] - lot.V[i]).Length;
                    if (!frontLen.HasValue || len > frontLen.Value)
                    {
                        frontLen = len;
                        front = i;
                    }
                }
            }
            var footprint = new List<Vec2>();
            if (!PlanMath.InsetPolygon(lot.V, distances, footprint, out _))
            {
                return;
            }
            double footArea = PlanMath.SignedArea(footprint);
            if (footArea < 70.0 * _s * _s)
            {
                return;
            }

            Vec2 c = PlanMath.Centroid(footprint);
            if (fields.IsWater(c))
            {
                return;
            }
            for (int i = 0; i < footprint.Count; i++)
            {
                if (fields.IsWater(footprint[i]))
                {
                    return;
                }
            }
            double pop = fields.Population(c);
            int id = data.Lots.Count;
            double r1 = PlanNoise.Mix(seed, unchecked((uint)(id * 2 + 1))) / 4294967296.0;
            double r2 = PlanNoise.Mix(seed, unchecked((uint)(id * 2 + 2))) / 4294967296.0;
            double height;
            if (pop > 0.78 && footArea > 700.0 * _s * _s && r1 < 0.6)
            {
                height = (90.0 + r2 * 170.0) * _h;
            }
            else if (pop > 0.58)
            {
                height = (24.0 + r1 * 56.0) * _h;
            }
            else if (pop > 0.34)
            {
                height = (10.0 + r1 * 28.0) * _h;
            }
            else
            {
                height = (5.0 + r1 * 12.0) * _h;
            }

            data.Lots.Add(new PlanLot
            {
                Id = id,
                BlockIndex = blockIndex,
                Lot = lot.V.ToArray(),
                Footprint = footprint.ToArray(),
                FrontEdge = front < 0 ? 0 : front,
                Area = footArea,
                Population = pop,
                Height = height,
                Scale = _s,
                HeightScale = _h,
                FrontClass = RoadClass.Street,
                StreetWall = streetWall,
            });
        }
    }
}
