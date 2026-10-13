using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    public enum RoadClass
    {
        Street = 0,
        Arterial = 1,

        /// <summary>
        /// An expressway: a wide service road on the ground, with the EXPRESSWAY DECK on piers above it (PlanGeometry.DeckTop, CitySurfaceBuilder). Streets
        /// cross it on the ground, under the deck; the deck is reached by the ramps of its interchanges (Interchanges).
        /// </summary>
        Highway = 2,
    }

    public sealed class RoadNode
    {
        public int Id;
        public Vec2 P;
        public readonly List<RoadEdge> Edges = new List<RoadEdge>(4);

        public RoadNode Other(RoadEdge e) => e.A == this ? e.B : e.A;
    }

    public sealed class RoadEdge
    {
        public int Id;
        public RoadNode A;
        public RoadNode B;
        public RoadClass Class;
        public bool Bridge;
        public bool Alive = true;

        public double Length => (B.P - A.P).Length;

        public RoadNode Other(RoadNode n) => A == n ? B : A;
    }

    /// <summary>
    /// The road network, grown the way Parish and Muller's "Procedural Modelling of Cities" describes (and the way
    /// Introversion's Subversion generator, which names that paper as its starting point, produced its cities):
    ///
    ///  1. Urban centres are the high points of a population map.
    ///  2. HIGHWAYS grow out of each centre. Each new segment aims toward where the population is higher and away
    ///     from steep ground and water, and snaps toward a rigid grid near the centre -- so they bend gently
    ///     through the country and run square through downtown.
    ///  3. STREETS branch off at right angles, more densely where the population is dense, and carry on in the
    ///     direction of the road they came from, which is what makes each district its own grid at its own angle.
    ///  4. Local constraints keep it a network and not a tangle: a road that would cross another stops at the
    ///     crossing and makes a junction, one that ends near a junction or a road joins it, one that would lie in
    ///     the water is shortened, rejected, or turned into a bridge, and a road leaving a junction at a very
    ///     shallow angle to another is rejected as a sliver.
    ///
    /// Growth is driven by a priority queue (smallest time first, ties broken by creation order), so it is
    /// breadth-first through time: highways, then the streets they spawn, then those streets' side streets. All
    /// random choices are integer hashes of something stable (see PlanConfig).
    /// </summary>
    public sealed class RoadNetwork
    {
        private double CellSize => 64.0 * _cfg.Scale;

        /// <summary>The plan's scale where a point is: how much bigger than the classic city its roads and blocks are there (see CityScale).</summary>
        private double Local(Vec2 p) => _fields.ScaleAt(p.X, p.Y);

        private readonly PlanFields _fields;
        private readonly PlanConfig _cfg;
        private readonly uint _seed;

        public readonly List<RoadNode> Nodes = new List<RoadNode>();
        public readonly List<RoadEdge> Edges = new List<RoadEdge>();

        private readonly Dictionary<long, List<RoadEdge>> _edgeCells = new Dictionary<long, List<RoadEdge>>();
        private readonly Dictionary<long, List<RoadNode>> _nodeCells = new Dictionary<long, List<RoadNode>>();

        private struct Item
        {
            public double T;
            public int Seq;
            public RoadNode From;
            public Vec2 Dir;
            public RoadClass Class;

            /// <summary>An orbital's segment: a highway that keeps to a circle round a centre instead of chasing the population (see Grow).</summary>
            public bool Ring;
            public Vec2 RingCentre;
            public double RingRadius;
            public int RingTurn;
        }

        private readonly List<Item> _heap = new List<Item>();
        private int _seq;

        public RoadNetwork(PlanFields fields, uint seed)
        {
            _fields = fields;
            _cfg = fields.Config;
            _seed = seed;
        }

        // ------------------------------------------------------------------ public

        /// <summary>
        /// Grows the whole network from the given centres: RADIAL expressways out of every centre, and an ORBITAL round the strongest one.
        ///
        /// Real expressway networks are rings and radials (Beijing's plan: radial routes and ring roads; Moscow; London's M25; Paris's
        /// Périphérique; Tokyo's C1 loop round the centre): the radials come in through the city, the ring carries traffic round it. The
        /// radials here already run through downtown (they snap to a grid in the core) and out into the country until the population runs
        /// out, where each one ends (PlanGeometry brings the deck down to the ground there, as a freeway ends at its last surface junction).
        /// </summary>
        public void Grow(IList<Vec2> centres, double regionHalf)
        {
            foreach (Vec2 centre in centres)
            {
                RoadNode node = NewNode(centre);
                // A grid angle for this centre, in 5-degree steps within +-45.
                int gridSteps = (int)(Rand(node.Id, 11U) * 18.0) - 9;
                Vec2 axis = PlanMath.RotateSteps(new Vec2(1.0, 0.0), gridSteps);
                Push(0.0, node, axis, RoadClass.Highway);
                Push(0.0, node, -axis, RoadClass.Highway);
                Push(0.05, node, axis.Perp, RoadClass.Highway);
                Push(0.05, node, -axis.Perp, RoadClass.Highway);
            }

            if (_cfg.OrbitalRadius > 0.0)
            {
                // Round the origin: the downtown the player spawns in (PlanFields.Population always makes one there), whichever centre is strongest.
                SeedOrbital(new Vec2(0.0, 0.0), _cfg.OrbitalRadius);
            }

            while (_heap.Count > 0 && Nodes.Count < _cfg.MaxNodes)
            {
                Item item = PopMin();
                TryPlace(item);
            }
        }

        /// <summary>
        /// The orbital: from a dry point on its circle, one arm grown each way round. Each arm keeps to the circle (RingDirection) until it
        /// meets the other (its end snaps onto a node and it stops) or runs out of land, so a river or the region's edge leaves a part ring --
        /// which is what most real orbitals are too (Rome's A90 is a partial ring).
        /// </summary>
        private void SeedOrbital(Vec2 centre, double radius)
        {
            Vec2 start = default;
            bool found = false;
            for (int k = 0; k < 24 && !found; k++)
            {
                Vec2 p = centre + PlanMath.RotateSteps(new Vec2(radius, 0.0), k * 3);
                if (InRegion(p) && !_fields.IsWater(p) && _fields.Population(p) > 0.05)
                {
                    start = p;
                    found = true;
                }
            }
            if (!found)
            {
                return;
            }
            RoadNode node = NewNode(start);
            Vec2 radial = (start - centre).Normalized;
            PushRing(0.02, node, radial.Perp, centre, radius, 1);
            PushRing(0.02, node, -radial.Perp, centre, radius, -1);
        }

        private void PushRing(double t, RoadNode from, Vec2 dir, Vec2 centre, double radius, int turn)
        {
            Push(t, from, dir, RoadClass.Highway);
            Item item = _heap[_heap.Count - 1];
            // The item just pushed may have sifted up; find it by its sequence number.
            for (int i = 0; i < _heap.Count; i++)
            {
                if (_heap[i].Seq == _seq - 1)
                {
                    item = _heap[i];
                    item.Ring = true;
                    item.RingCentre = centre;
                    item.RingRadius = radius;
                    item.RingTurn = turn;
                    _heap[i] = item;
                    break;
                }
            }
        }

        /// <summary>Where an orbital's next segment heads: along the circle, steered back onto it if the last one drifted.</summary>
        private static Vec2 RingDirection(in Item item, Vec2 a)
        {
            Vec2 radial = a - item.RingCentre;
            double r = radial.Length;
            if (r < 1e-6)
            {
                return item.Dir;
            }
            Vec2 outward = radial / r;
            Vec2 tangent = outward.Perp * item.RingTurn;
            double drift = (item.RingRadius - r) / item.RingRadius;   // positive: inside the circle, head out
            return (tangent + outward * (drift * 1.5)).Normalized;
        }

        // ------------------------------------------------------------------ growth

        private double Rand(int a, uint salt)
        {
            return PlanNoise.Mix(PlanNoise.Mix(_seed, unchecked((uint)a)), salt) / 4294967296.0;
        }

        private void Push(double t, RoadNode from, Vec2 dir, RoadClass cls)
        {
            Item item = new Item { T = t, Seq = _seq++, From = from, Dir = dir, Class = cls };
            _heap.Add(item);
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Less(_heap[i], _heap[parent]))
                {
                    Item tmp = _heap[i];
                    _heap[i] = _heap[parent];
                    _heap[parent] = tmp;
                    i = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private static bool Less(Item a, Item b) => a.T < b.T || (a.T == b.T && a.Seq < b.Seq);

        private Item PopMin()
        {
            Item top = _heap[0];
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            int n = _heap.Count;
            while (true)
            {
                int l = i * 2 + 1;
                int r = l + 1;
                int smallest = i;
                if (l < n && Less(_heap[l], _heap[smallest])) smallest = l;
                if (r < n && Less(_heap[r], _heap[smallest])) smallest = r;
                if (smallest == i) break;
                Item tmp = _heap[i];
                _heap[i] = _heap[smallest];
                _heap[smallest] = tmp;
                i = smallest;
            }
            return top;
        }

        private bool InRegion(Vec2 p) => Math.Abs(p.X) <= _cfg.Half && Math.Abs(p.Y) <= _cfg.Half;

        private double SegmentLength(RoadClass cls, double pop, RoadNode from, Vec2 dir)
        {
            double jitter = 0.88 + 0.24 * Rand(from.Id, 21U + (uint)cls);
            switch (cls)
            {
                case RoadClass.Highway: return (190.0 + 80.0 * Rand(from.Id, 5U)) * Local(from.P);
                case RoadClass.Arterial: return (150.0 + 60.0 * Rand(from.Id, 6U)) * Local(from.P);
                default: return (74.0 + (1.0 - pop) * 120.0) * jitter * Local(from.P);
            }
        }

        private bool SegmentTouchesWater(Vec2 a, Vec2 b)
        {
            for (int i = 1; i <= 4; i++)
            {
                double t = i * 0.25;
                if (_fields.IsWater(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Picks where the next highway segment should head: toward people, off the steep ground, square near the core.</summary>
        private Vec2 ChooseHighwayDir(Vec2 a, Vec2 dir, int nodeId)
        {
            double popHere = _fields.Population(a);
            Vec2 best = dir;
            double bestScore = double.NegativeInfinity;
            for (int step = -6; step <= 6; step += 2)
            {
                Vec2 d = PlanMath.RotateSteps(dir, step);
                double score = 0.0;
                for (int k = 1; k <= 3; k++)
                {
                    Vec2 p = a + d * (150.0 * Local(a) * k);
                    if (Math.Abs(p.X) > _cfg.Half || Math.Abs(p.Y) > _cfg.Half)
                    {
                        score -= 0.35;
                        continue;
                    }
                    double w = 1.0 / k;
                    score += _fields.Population(p) * w;
                    score -= _fields.Slope(p.X, p.Y) * 1.1 * w;
                    if (_fields.IsWater(p)) score -= 0.12 * w;
                }
                // Prefer to carry on straight.
                score += step == 0 ? 0.10 : 0.0;
                // A little hash noise so two highways in the same place do not make the same choice.
                score += Rand(nodeId, 31U + (uint)(step + 8)) * 0.07;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }

            if (popHere > 0.62)
            {
                // Downtown: snap to the nearest of the dir's own axes, so the core is a square grid.
                Vec2 perp = dir.Perp;
                double a1 = Vec2.Dot(best, dir);
                double a2 = Vec2.Dot(best, perp);
                Vec2 axis = Math.Abs(a1) >= Math.Abs(a2) ? dir * (a1 >= 0 ? 1.0 : -1.0) : perp * (a2 >= 0 ? 1.0 : -1.0);
                double snap = (popHere - 0.62) / 0.38;   // 0..1
                best = (best * (1.0 - snap * 0.8) + axis * (snap * 0.8)).Normalized;
            }
            return best;
        }

        private bool TryBridge(Vec2 a, Vec2 dir, double popHere, out double bridgeLength)
        {
            bridgeLength = 0.0;
            bool wasWater = false;
            double ls = Local(a);
            for (double d = 12.0 * ls; d <= _cfg.MaxBridge * ls; d += 12.0 * ls)
            {
                Vec2 p = a + dir * d;
                if (!InRegion(p))
                {
                    return false;
                }
                if (_fields.IsWater(p))
                {
                    wasWater = true;
                    continue;
                }
                if (!wasWater)
                {
                    continue;   // still on the near bank
                }
                // Landed on the far side: demand a little real land past it.
                Vec2 beyond = a + dir * (d + 24.0 * ls);
                if (!InRegion(beyond) || _fields.IsWater(beyond))
                {
                    continue;
                }
                double popFar = _fields.Population(beyond);
                if (Math.Max(popHere, popFar) < 0.22)
                {
                    return false;
                }
                bridgeLength = d + 12.0 * ls;
                return true;
            }
            return false;
        }

        private void TryPlace(Item item)
        {
            RoadNode from = item.From;
            Vec2 a = from.P;
            double popHere = _fields.Population(a);
            RoadClass cls = item.Class;

            Vec2 dir = item.Dir.Normalized;
            if (item.Ring)
            {
                dir = RingDirection(item, a);
            }
            else if (cls == RoadClass.Highway)
            {
                dir = ChooseHighwayDir(a, dir, from.Id);
            }

            double len = SegmentLength(cls, popHere, from, dir);
            bool bridge = false;
            Vec2 b = a + dir * len;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!InRegion(b))
                {
                    len *= 0.6;
                    b = a + dir * len;
                    continue;
                }
                if (SegmentTouchesWater(a, b))
                {
                    if (cls != RoadClass.Street && TryBridge(a, dir, popHere, out double span))
                    {
                        len = span;
                        b = a + dir * len;
                        bridge = true;
                        break;
                    }
                    len *= 0.6;
                    b = a + dir * len;
                    continue;
                }
                break;
            }
            if (len < _cfg.MinSegment * Local(a) || !InRegion(b) || (!bridge && SegmentTouchesWater(a, b)))
            {
                return;
            }

            // ---- local constraints ----
            RoadNode endNode = null;
            RoadEdge splitEdge = null;
            Vec2 splitPoint = default;

            double bestT = 2.0;
            RoadEdge crossed = null;
            FindCrossing(a, b, from, out bestT, out crossed);

            if (crossed != null)
            {
                if (bridge || crossed.Bridge)
                {
                    return;
                }
                Vec2 x = a + (b - a) * bestT;
                double dist = bestT * len;
                if ((x - crossed.A.P).Length < _cfg.SnapNode * 0.6 * Local(x))
                {
                    endNode = crossed.A;
                }
                else if ((x - crossed.B.P).Length < _cfg.SnapNode * 0.6 * Local(x))
                {
                    endNode = crossed.B;
                }
                else
                {
                    splitEdge = crossed;
                    splitPoint = x;
                }
                if (dist < _cfg.MinSegment * Local(a) && endNode == null)
                {
                    return;
                }
                if (endNode != null)
                {
                    b = endNode.P;
                }
            }
            else
            {
                // Snap onto a junction near the end.
                RoadNode near = NearestNode(b, _cfg.SnapNode * Local(b), from);
                if (near != null && !bridge)
                {
                    endNode = near;
                    b = near.P;
                }
                else if (!bridge)
                {
                    RoadEdge nearEdge = NearestEdge(b, _cfg.SnapEdge * Local(b), from, out Vec2 proj, out double t);
                    if (nearEdge != null && t > 0.1 && t < 0.9 &&
                        (proj - nearEdge.A.P).Length > _cfg.SnapNode * 0.6 * Local(b) && (proj - nearEdge.B.P).Length > _cfg.SnapNode * 0.6 * Local(b))
                    {
                        splitEdge = nearEdge;
                        splitPoint = proj;
                        b = proj;
                    }
                }

                if (endNode != null || splitEdge != null)
                {
                    // Re-check the redirected road does not cross anything on its way.
                    FindCrossing(a, b, from, out double t2, out RoadEdge crossed2);
                    if (crossed2 != null && t2 < 0.985 && crossed2 != splitEdge)
                    {
                        return;
                    }
                    if ((b - a).Length < _cfg.MinSegment * Local(a))
                    {
                        return;
                    }
                }
            }

            // Never lay a road onto a node it is already joined to, or to itself.
            if (endNode == from)
            {
                return;
            }
            if (endNode != null)
            {
                foreach (RoadEdge e in from.Edges)
                {
                    if (e.Other(from) == endNode)
                    {
                        return;
                    }
                }
            }

            // Sliver check at the start node.
            Vec2 d = (b - a).Normalized;
            foreach (RoadEdge e in from.Edges)
            {
                Vec2 away = (e.Other(from).P - a).Normalized;
                if (Vec2.Dot(d, away) > _cfg.MaxSliverCos)
                {
                    return;
                }
            }
            // ...and at the far end.
            if (endNode != null)
            {
                foreach (RoadEdge e in endNode.Edges)
                {
                    Vec2 away = (e.Other(endNode).P - endNode.P).Normalized;
                    if (Vec2.Dot(-d, away) > _cfg.MaxSliverCos)
                    {
                        return;
                    }
                }
            }
            else if (splitEdge != null)
            {
                Vec2 edgeDir = (splitEdge.B.P - splitEdge.A.P).Normalized;
                if (Math.Abs(Vec2.Dot(d, edgeDir)) > _cfg.MaxSliverCos)
                {
                    return;
                }
            }

            bool fresh = false;
            if (splitEdge != null)
            {
                endNode = SplitEdge(splitEdge, splitPoint);
            }
            else if (endNode == null)
            {
                endNode = NewNode(b);
                fresh = true;
            }

            AddEdge(from, endNode, cls, bridge);

            if (fresh)
            {
                SpawnChildren(item, endNode, d, bridge);
            }
        }

        private void SpawnChildren(Item item, RoadNode node, Vec2 d, bool bridge)
        {
            double pop = _fields.Population(node.P);
            double t = item.T;
            Vec2 left = d.Perp;
            Vec2 right = -d.Perp;

            switch (item.Class)
            {
                case RoadClass.Highway:
                {
                    if (item.Ring)
                    {
                        PushRing(t + 0.4, node, d, item.RingCentre, item.RingRadius, item.RingTurn);   // round it goes, whatever the population
                    }
                    else if (pop > 0.07 || bridge)
                    {
                        Push(t + 0.4, node, d, RoadClass.Highway);
                    }
                    double streetP = 0.2 + 0.7 * pop;
                    if (Rand(node.Id, 41U) < streetP) Push(t + 3.0, node, left, pop > 0.6 ? RoadClass.Arterial : RoadClass.Street);
                    if (Rand(node.Id, 42U) < streetP) Push(t + 3.0, node, right, pop > 0.6 ? RoadClass.Arterial : RoadClass.Street);
                    break;
                }
                case RoadClass.Arterial:
                {
                    if (pop > 0.1)
                    {
                        Push(t + 0.8, node, d, RoadClass.Arterial);
                    }
                    double streetP = 0.35 + 0.6 * pop;
                    if (Rand(node.Id, 43U) < streetP) Push(t + 2.0, node, left, RoadClass.Street);
                    if (Rand(node.Id, 44U) < streetP) Push(t + 2.0, node, right, RoadClass.Street);
                    break;
                }
                default:
                {
                    if (pop < 0.11)
                    {
                        break;
                    }
                    // Carry on, bending a little where the city thins out.
                    int bend = 0;
                    double wobble = Rand(node.Id, 45U);
                    if (pop < 0.5)
                    {
                        bend = wobble < 0.25 ? -1 : (wobble > 0.75 ? 1 : 0);
                    }
                    if (Rand(node.Id, 46U) < 0.80 + 0.2 * pop)
                    {
                        Push(t + 1.0, node, PlanMath.RotateSteps(d, bend), RoadClass.Street);
                    }
                    double branchP = 0.14 + 0.66 * pop;
                    if (Rand(node.Id, 47U) < branchP) Push(t + 1.5, node, left, RoadClass.Street);
                    if (Rand(node.Id, 48U) < branchP) Push(t + 1.5, node, right, RoadClass.Street);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ graph editing

        /// <summary>
        /// Whether the segment a-b crosses any live road strictly between both segments' ends (an end touching a road is not a crossing),
        /// other than the two roads given. For things laid over the finished network (an interchange's ramps) that must not cut across a street.
        /// </summary>
        public bool CrossesAnyRoad(Vec2 a, Vec2 b, RoadEdge ignore1 = null, RoadEdge ignore2 = null)
        {
            int x0 = CellOf(Math.Min(a.X, b.X));
            int x1 = CellOf(Math.Max(a.X, b.X));
            int y0 = CellOf(Math.Min(a.Y, b.Y));
            int y1 = CellOf(Math.Max(a.Y, b.Y));
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!_edgeCells.TryGetValue(CellKey(cx, cy), out List<RoadEdge> list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        RoadEdge e = list[i];
                        if (!e.Alive || e == ignore1 || e == ignore2)
                        {
                            continue;
                        }
                        if (PlanMath.SegmentIntersect(a, b, e.A.P, e.B.P, out double t, out double u)
                            && t > 1e-4 && t < 1.0 - 1e-4 && u > 1e-4 && u < 1.0 - 1e-4)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>Whether any node lies within <paramref name="radius"/> of a point (other than the ones given): a junction a ramp must not land on.</summary>
        public bool AnyNodeNear(Vec2 p, double radius, RoadNode ignore1 = null, RoadNode ignore2 = null)
        {
            RoadNode n = NearestNode(p, radius, ignore1);
            return n != null && n != ignore2;
        }

        private RoadNode NewNode(Vec2 p)
        {
            var node = new RoadNode { Id = Nodes.Count, P = p };
            Nodes.Add(node);
            long key = CellKey(p);
            if (!_nodeCells.TryGetValue(key, out List<RoadNode> list))
            {
                list = new List<RoadNode>(4);
                _nodeCells[key] = list;
            }
            list.Add(node);
            return node;
        }

        private RoadEdge AddEdge(RoadNode a, RoadNode b, RoadClass cls, bool bridge)
        {
            var edge = new RoadEdge { Id = Edges.Count, A = a, B = b, Class = cls, Bridge = bridge };
            Edges.Add(edge);
            a.Edges.Add(edge);
            b.Edges.Add(edge);
            IndexEdge(edge);
            return edge;
        }

        private RoadNode SplitEdge(RoadEdge e, Vec2 p)
        {
            RoadNode w = NewNode(p);
            e.Alive = false;
            e.A.Edges.Remove(e);
            e.B.Edges.Remove(e);
            AddEdge(e.A, w, e.Class, e.Bridge);
            AddEdge(w, e.B, e.Class, e.Bridge);
            return w;
        }

        private static long CellKey(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

        private long CellKey(Vec2 p) => CellKey(CellOf(p.X), CellOf(p.Y));

        private int CellOf(double v)
        {
            int i = (int)(v / CellSize);
            return v < 0 && i * CellSize != v ? i - 1 : i;
        }

        private void IndexEdge(RoadEdge e)
        {
            int x0 = CellOf(Math.Min(e.A.P.X, e.B.P.X));
            int x1 = CellOf(Math.Max(e.A.P.X, e.B.P.X));
            int y0 = CellOf(Math.Min(e.A.P.Y, e.B.P.Y));
            int y1 = CellOf(Math.Max(e.A.P.Y, e.B.P.Y));
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    long key = CellKey(cx, cy);
                    if (!_edgeCells.TryGetValue(key, out List<RoadEdge> list))
                    {
                        list = new List<RoadEdge>(4);
                        _edgeCells[key] = list;
                    }
                    list.Add(e);
                }
            }
        }

        private void FindCrossing(Vec2 a, Vec2 b, RoadNode from, out double bestT, out RoadEdge best)
        {
            bestT = 2.0;
            best = null;
            int x0 = CellOf(Math.Min(a.X, b.X));
            int x1 = CellOf(Math.Max(a.X, b.X));
            int y0 = CellOf(Math.Min(a.Y, b.Y));
            int y1 = CellOf(Math.Max(a.Y, b.Y));
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!_edgeCells.TryGetValue(CellKey(cx, cy), out List<RoadEdge> list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        RoadEdge e = list[i];
                        if (!e.Alive || e.A == from || e.B == from)
                        {
                            continue;
                        }
                        if (PlanMath.SegmentIntersect(a, b, e.A.P, e.B.P, out double t, out double u) && t > 1e-6)
                        {
                            if (t < bestT || (t == bestT && e.Id < best.Id))
                            {
                                bestT = t;
                                best = e;
                            }
                        }
                    }
                }
            }
        }

        private RoadNode NearestNode(Vec2 p, double radius, RoadNode exclude)
        {
            int span = (int)(radius / CellSize) + 1;
            int cx0 = CellOf(p.X);
            int cy0 = CellOf(p.Y);
            RoadNode best = null;
            double bestD = radius;
            for (int cy = cy0 - span; cy <= cy0 + span; cy++)
            {
                for (int cx = cx0 - span; cx <= cx0 + span; cx++)
                {
                    if (!_nodeCells.TryGetValue(CellKey(cx, cy), out List<RoadNode> list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        RoadNode n = list[i];
                        if (n == exclude)
                        {
                            continue;
                        }
                        double d = (n.P - p).Length;
                        if (d < bestD || (d == bestD && best != null && n.Id < best.Id))
                        {
                            bestD = d;
                            best = n;
                        }
                    }
                }
            }
            return best;
        }

        private RoadEdge NearestEdge(Vec2 p, double radius, RoadNode exclude, out Vec2 projection, out double t)
        {
            projection = p;
            t = 0.0;
            int span = (int)(radius / CellSize) + 1;
            int cx0 = CellOf(p.X);
            int cy0 = CellOf(p.Y);
            RoadEdge best = null;
            double bestD = radius;
            for (int cy = cy0 - span; cy <= cy0 + span; cy++)
            {
                for (int cx = cx0 - span; cx <= cx0 + span; cx++)
                {
                    if (!_edgeCells.TryGetValue(CellKey(cx, cy), out List<RoadEdge> list))
                    {
                        continue;
                    }
                    for (int i = 0; i < list.Count; i++)
                    {
                        RoadEdge e = list[i];
                        if (!e.Alive || e.A == exclude || e.B == exclude)
                        {
                            continue;
                        }
                        double d = PlanMath.DistanceToSegment(p, e.A.P, e.B.P, out double tt);
                        if (d < bestD || (d == bestD && best != null && e.Id < best.Id))
                        {
                            bestD = d;
                            best = e;
                            t = tt;
                            projection = e.A.P + (e.B.P - e.A.P) * tt;
                        }
                    }
                }
            }
            return best;
        }
    }
}
