using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>What an open block is for. Not every empty space is a furnished plaza (owner, 2026-10-15: "there should still be some more empty feeling areas").</summary>
    public enum OpenSpaceRole
    {
        /// <summary>The civic square: a focal piece, seats in rows, planters, shade, a kiosk, lights, a name.</summary>
        Plaza = 0,

        /// <summary>A dry lawn or two with loose chairs round them, a pergola or a tree bench, a kiosk by the street.</summary>
        PocketPark = 1,

        /// <summary>A long block walked along: tree planters and benches in two rows.</summary>
        Promenade = 2,

        /// <summary>Plain paving and one thing at most: a lone bench, a heap, a dead tree, three bollards.</summary>
        BareYard = 3,

        /// <summary>Earth, rubble and a hoarding along the street.</summary>
        VacantLot = 4,

        /// <summary>Planters in a ring or a grid, nothing to sit on.</summary>
        QuietCourt = 5,
    }

    /// <summary>The things an open block can hold (View/CityProps builds each).</summary>
    public enum OpenProp
    {
        Bench, SeatBlock, Planter, TreePlanter, DeadTree, ShadeSail, UmbrellaTable, PicnicTable, CubeSeat, BollardLight, Bollard, Fountain, Tower,
        Pergola, Chessboard, ChessPiece, Kiosk, BikeRack, NameWall, Rubble, Hoarding, Chair, Bin, RoundBench,
    }

    /// <summary>
    /// One thing standing on an open block: where (world x, z), which way its own +x points, its footprint as a CAPSULE (a segment of
    /// <see cref="HalfX"/> either side of <see cref="At"/> along <see cref="Dir"/>, <see cref="HalfZ"/> round it), a seed for its variation, whether
    /// it has fallen over, and whether a body walks round it (a chessboard is flat: nobody walks round that).
    /// </summary>
    public readonly struct OpenPiece
    {
        public readonly OpenProp Kind;
        public readonly Vec2 At;
        public readonly Vec2 Dir;
        public readonly double HalfX, HalfZ;
        public readonly int Seed;
        public readonly bool Fallen;
        public readonly bool Blocks;
        public readonly double Extra;   // a fountain's radius, a tower's height, a hoarding's gap

        public OpenPiece(OpenProp kind, Vec2 at, Vec2 dir, double halfX, double halfZ, int seed, bool fallen = false, bool blocks = true, double extra = 0.0)
        {
            Kind = kind;
            At = at;
            Dir = dir;
            HalfX = halfX;
            HalfZ = halfZ;
            Seed = seed;
            Fallen = fallen;
            Blocks = blocks;
            Extra = extra;
        }

        public Vec2 A => At - Dir * HalfX;
        public Vec2 B => At + Dir * HalfX;

        /// <summary>How far a point is from the piece's capsule (0 inside it).</summary>
        public double Distance(Vec2 p) => Math.Max(0.0, PlanMath.DistanceToSegment(p, A, B, out _) - HalfZ);
    }

    /// <summary>Everything decided for one open block, once, the same on every machine.</summary>
    public sealed class OpenSpaceLayout
    {
        public OpenSpaceRole Role;
        public readonly List<OpenPiece> Pieces = new List<OpenPiece>();

        /// <summary>Dry-lawn islands (world outlines, counter-clockwise), walked over, not round.</summary>
        public readonly List<Vec2[]> Lawns = new List<Vec2[]>();

        /// <summary>Whether the block's lights come on at night (half the blocks: the ones that still have power).</summary>
        public bool Powered;

        /// <summary>The kerb edge the block faces (its longest): where the kiosk, the name and the hoarding's gap go.</summary>
        public int Front;

        /// <summary>Bollards along the kerb (every role but the bare yard and the vacant lot).</summary>
        public bool KerbBollards;
    }

    /// <summary>
    /// THE OPEN SPACES (docs/OPEN_SPACE.md, owner 2026-10-15: "improve upon the open space pedestrian walk zones around the buildings. i want my city
    /// to feel like it was once alive", and "not every empty space should be a pedestrian plaza"): a plaza block (Amenities.ChoosePlazas) gets a ROLE
    /// from its district and its seed, and a layout of pieces for the role -- pure, from the plan and the world seed, so the chunks that share a
    /// block all draw the same thing. Every piece keeps a body's width clear of every other (<see cref="Clearance"/>) and off the SPINES, the
    /// straight walks from the middle of each kerb edge to the middle of the block (<see cref="SpineHalfWidth"/> either side), so the block can be
    /// crossed from any side to any other (<see cref="Walkable"/> checks it the way the tests do).
    /// </summary>
    public static class OpenSpace
    {
        /// <summary>The least paving between two pieces (a 0.9 m body and a little), and either side of a spine.</summary>
        public const double Clearance = 1.2;
        public const double SpineHalfWidth = 1.2;

        /// <summary>How far in from the kerb a piece keeps (the kerb bollards stand at 0.45).</summary>
        public const double KerbMargin = 1.4;

        /// <summary>A promenade needs a block this many times longer than wide; a shorter one is laid out as a plaza.</summary>
        public const double PromenadeAspect = 2.2;

        /// <summary>Role weights by district, in enum order (Plaza, PocketPark, Promenade, BareYard, VacantLot, QuietCourt): about a third or more left empty everywhere.</summary>
        private static readonly double[][] RoleWeights =
        {
            /* Core        */ new[] { 42.0, 10.0, 20.0, 16.0, 2.0, 10.0 },
            /* Urban       */ new[] { 34.0, 16.0, 10.0, 24.0, 8.0, 8.0 },
            /* OldTown     */ new[] { 24.0, 20.0, 4.0, 22.0, 6.0, 24.0 },
            /* Residential */ new[] { 24.0, 26.0, 4.0, 30.0, 10.0, 6.0 },
            /* Industrial  */ new[] { 10.0, 4.0, 0.0, 46.0, 34.0, 6.0 },
            /* Outskirts   */ new[] { 14.0, 16.0, 0.0, 40.0, 24.0, 6.0 },
        };

        /// <summary>The shade a civic plaza gets: sails on posts, or umbrella tables.</summary>
        public const double SailShare = 0.45;

        /// <summary>A square wider than this (metres, the square root of its area) is composed by quadrants, each a room of its own.</summary>
        public const double QuadrantExtent = 40.0;

        private static double Unit(int seed, int blockIndex, int salt)
        {
            uint a = PlanNoise.Mix(unchecked((uint)seed), 0x6F70656EU);   // "open"
            uint b = PlanNoise.Mix(unchecked((uint)blockIndex), unchecked((uint)salt));
            return PlanNoise.Mix(a, b) / 4294967296.0;
        }

        private static int Index(double unit, int count) => Math.Min(count - 1, (int)(unit * count));

        /// <summary>The district a block is in: the same rule as its buildings' (PlanWorld.ZoneFor).</summary>
        public static ZoneKind ZoneOf(PlanWorld plan, int blockIndex)
        {
            PlanBlock block = plan.Data.Blocks[blockIndex];
            Vec2 c = PlanMath.Centroid(block.Inner ?? block.Poly);
            uint characterSeed = PlanNoise.Mix(unchecked((uint)plan.Seed), 0x7A6F6E65U);   // "zone", as PlanBuildings.Resolve
            return PlanBuildings.ZoneFor(plan.Fields, plan.Fields.Population(c), c, characterSeed, Unit(plan.Seed, blockIndex, 0x7A));
        }

        public static OpenSpaceRole RoleOf(PlanWorld plan, int blockIndex)
        {
            double[] weights = RoleWeights[(int)ZoneOf(plan, blockIndex)];
            double total = 0.0;
            foreach (double w in weights) total += w;
            double target = Unit(plan.Seed, blockIndex, 0x01) * total, cursor = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                cursor += weights[i];
                if (target < cursor && weights[i] > 0.0) return (OpenSpaceRole)i;
            }
            return OpenSpaceRole.BareYard;
        }

        /// <summary>The whole layout of a plaza block (null for a block that is no plaza or has no kerb ring).</summary>
        public static OpenSpaceLayout LayoutFor(PlanWorld plan, int blockIndex)
        {
            PlanBlock block = plan.Data.Blocks[blockIndex];
            if (!block.Plaza || block.Inner == null || block.Inner.Count < 3) return null;
            var b = new Builder(plan, blockIndex, block.Inner);
            OpenSpaceRole role = RoleOf(plan, blockIndex);
            if (role == OpenSpaceRole.Promenade && b.SpanU < PromenadeAspect * b.SpanV) role = OpenSpaceRole.Plaza;
            b.Layout.Role = role;
            b.Layout.Powered = Unit(plan.Seed, blockIndex, 0x50) < 0.5;
            b.Layout.KerbBollards = role != OpenSpaceRole.BareYard && role != OpenSpaceRole.VacantLot;
            switch (role)
            {
                case OpenSpaceRole.Plaza: b.Plaza(); break;
                case OpenSpaceRole.PocketPark: b.PocketPark(); break;
                case OpenSpaceRole.Promenade: b.Promenade(); break;
                case OpenSpaceRole.BareYard: b.BareYard(); break;
                case OpenSpaceRole.VacantLot: b.VacantLot(); break;
                default: b.QuietCourt(); break;
            }
            return b.Layout;
        }

        /// <summary>
        /// Whether a body (radius <paramref name="radius"/>) can walk from the front kerb's middle to every other kerb's middle and to most of the
        /// block, round the pieces that block: a flood on a half-metre grid, the test's own check as much as the planner's.
        /// </summary>
        public static bool Walkable(OpenSpaceLayout layout, IList<Vec2> ring, double radius, out double shareReached)
        {
            const double cell = 0.5;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (Vec2 p in ring)
            {
                x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X);
                y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y);
            }
            int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / cell)), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / cell));
            var free = new bool[nx * ny];
            int freeCount = 0;
            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    var p = new Vec2(x0 + (ix + 0.5) * cell, y0 + (iy + 0.5) * cell);
                    bool ok = Inside(ring, p, radius);
                    for (int k = 0; ok && k < layout.Pieces.Count; k++)
                    {
                        if (layout.Pieces[k].Blocks && layout.Pieces[k].Distance(p) < radius) ok = false;
                    }
                    free[iy * nx + ix] = ok;
                    if (ok) freeCount++;
                }
            }
            shareReached = 0.0;
            if (freeCount == 0) return false;
            int n = ring.Count;
            int CellOf(Vec2 p) => Math.Min(ny - 1, Math.Max(0, (int)((p.Y - y0) / cell))) * nx + Math.Min(nx - 1, Math.Max(0, (int)((p.X - x0) / cell)));
            Vec2 Door(int e)
            {
                Vec2 a = ring[e], c = ring[(e + 1) % n];
                Vec2 inward = (c - a).Normalized.Perp;
                return (a + c) * 0.5 + inward * (radius + 0.6);
            }
            int start = CellOf(Door(layout.Front));
            if (!free[start]) return false;
            var seen = new bool[nx * ny];
            var queue = new Queue<int>();
            queue.Enqueue(start);
            seen[start] = true;
            int reached = 0;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                reached++;
                int cx = c % nx, cy = c / nx;
                foreach (int a in new[] { cx + 1 < nx ? c + 1 : -1, cx > 0 ? c - 1 : -1, cy + 1 < ny ? c + nx : -1, cy > 0 ? c - nx : -1 })
                {
                    if (a >= 0 && free[a] && !seen[a])
                    {
                        seen[a] = true;
                        queue.Enqueue(a);
                    }
                }
            }
            shareReached = reached / (double)freeCount;
            for (int e = 0; e < n; e++)
            {
                if ((ring[(e + 1) % n] - ring[e]).Length < 6.0) continue;   // a short edge has no walk of its own
                if (!seen[CellOf(Door(e))]) return false;
            }
            return shareReached >= 0.7;
        }

        /// <summary>Inside the ring and at least <paramref name="margin"/> from every edge of it.</summary>
        public static bool Inside(IList<Vec2> ring, Vec2 p, double margin)
        {
            if (!PlanMath.PointInPolygon(p, ring)) return false;
            if (margin <= 0.0) return true;
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                if (PlanMath.DistanceToSegment(p, ring[i], ring[(i + 1) % n], out _) < margin) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the layouts

        private sealed class Builder
        {
            public readonly OpenSpaceLayout Layout = new OpenSpaceLayout();
            private readonly int _seed, _block;
            private readonly IList<Vec2> _ring;
            private readonly Vec2 _c, _u, _v;   // the centre, along the front, inward from the front (toward the back)
            public readonly double SpanU, SpanV;
            private readonly double _su0, _su1, _sv0, _sv1, _extent;
            private readonly List<(Vec2 a, Vec2 b)> _spines = new List<(Vec2, Vec2)>();
            private double _focalRadius;

            public Builder(PlanWorld plan, int blockIndex, IList<Vec2> ring)
            {
                _seed = plan.Seed;
                _block = blockIndex;
                _ring = ring;
                _c = PlanMath.Centroid(ring);
                int n = ring.Count, front = 0;
                double longest = 0.0;
                for (int e = 0; e < n; e++)
                {
                    double l = (ring[(e + 1) % n] - ring[e]).Length;
                    if (l > longest) { longest = l; front = e; }
                }
                Layout.Front = front;
                _u = (ring[(front + 1) % n] - ring[front]).Normalized;
                _v = _u.Perp;   // the ring is counter-clockwise: the block is on the left of each edge
                _su0 = _sv0 = double.MaxValue;
                _su1 = _sv1 = double.MinValue;
                foreach (Vec2 p in ring)
                {
                    double s = Vec2.Dot(p - _c, _u), t = Vec2.Dot(p - _c, _v);
                    _su0 = Math.Min(_su0, s); _su1 = Math.Max(_su1, s);
                    _sv0 = Math.Min(_sv0, t); _sv1 = Math.Max(_sv1, t);
                }
                SpanU = _su1 - _su0;
                SpanV = _sv1 - _sv0;
                _extent = Math.Sqrt(Math.Abs(PlanMath.SignedArea(ring)));
                SetSpines(0.0);
            }

            private double U(int salt) => Unit(_seed, _block, salt);
            private Vec2 P(double s, double t) => _c + _u * s + _v * t;
            private Vec2 Edge(int e) => (_ring[(e + 1) % _ring.Count] - _ring[e]).Normalized;
            private Vec2 EdgeMid(int e) => (_ring[e] + _ring[(e + 1) % _ring.Count]) * 0.5;
            private double EdgeLength(int e) => (_ring[(e + 1) % _ring.Count] - _ring[e]).Length;

            /// <summary>The walks from each kerb's middle toward the centre, stopping short of a focal piece of the given radius.</summary>
            private void SetSpines(double focalRadius)
            {
                _focalRadius = focalRadius;
                _spines.Clear();
                int n = _ring.Count;
                for (int e = 0; e < n; e++)
                {
                    if (EdgeLength(e) < 6.0) continue;
                    Vec2 from = EdgeMid(e);
                    Vec2 toCentre = _c - from;
                    double length = toCentre.Length;
                    if (length < 1e-3) continue;
                    Vec2 to = length > focalRadius + 1.0 ? _c - toCentre / length * focalRadius : from;
                    _spines.Add((from, to));
                }
            }

            /// <summary>Adds a piece if it fits: inside the kerb by its margin, clear of every piece so far and of the spines.</summary>
            private bool TryPlace(OpenProp kind, Vec2 at, Vec2 dir, double halfX, double halfZ, int seed, bool fallen = false, bool blocks = true, double extra = 0.0,
                bool offSpines = true, double kerbMargin = KerbMargin)
            {
                var piece = new OpenPiece(kind, at, dir, halfX, halfZ, seed, fallen, blocks, extra);
                Vec2 a = piece.A, b = piece.B;
                if (!Inside(_ring, a, kerbMargin + halfZ) || !Inside(_ring, b, kerbMargin + halfZ) || !Inside(_ring, at, kerbMargin + halfZ)) return false;
                if (blocks)
                {
                    foreach (OpenPiece other in Layout.Pieces)
                    {
                        if (!other.Blocks) continue;
                        if (SegmentDistance(a, b, other.A, other.B) < halfZ + other.HalfZ + Clearance) return false;
                    }
                    if (offSpines)
                    {
                        foreach ((Vec2 sa, Vec2 sb) in _spines)
                        {
                            if (SegmentDistance(a, b, sa, sb) < halfZ + SpineHalfWidth) return false;
                        }
                    }
                }
                Layout.Pieces.Add(piece);
                return true;
            }

            private static double SegmentDistance(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
            {
                if (PlanMath.SegmentIntersect(a, b, c, d, out double t, out double u) && t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0) return 0.0;
                double best = PlanMath.DistanceToSegment(a, c, d, out _);
                best = Math.Min(best, PlanMath.DistanceToSegment(b, c, d, out _));
                best = Math.Min(best, PlanMath.DistanceToSegment(c, a, b, out _));
                return Math.Min(best, PlanMath.DistanceToSegment(d, a, b, out _));
            }

            private int Seed(int salt) => unchecked(_block * 131 + salt);

            // ---- the civic square ----
            public void Plaza()
            {
                // The focal piece in the middle, and the spines stop short of it.
                double f = U(0x10);
                if (f < 0.45)
                {
                    double radius = Math.Min(3.4, 0.11 * _extent);
                    SetSpines(radius + 0.8);
                    TryPlace(OpenProp.Fountain, _c, _u, radius, radius, Seed(1), fallen: U(0x11) < 0.5, extra: radius, offSpines: false);
                }
                else if (f < 0.6)
                {
                    SetSpines(2.4);
                    TryPlace(OpenProp.Tower, _c, _u, 1.6, 1.6, Seed(2), extra: 12.0 + 4.0 * U(0x12), offSpines: false);
                }
                else if (f < 0.8)
                {
                    SetSpines(3.6);
                    TryPlace(OpenProp.Pergola, _c, _u, 3.0, 2.0, Seed(3), offSpines: false);
                }
                else
                {
                    SetSpines(3.2);
                    TryPlace(OpenProp.Chessboard, _c, _u, 2.4, 2.4, Seed(4), blocks: false, offSpines: false);
                    int pieces = 2 + Index(U(0x13), 4);
                    for (int i = 0; i < pieces; i++)
                    {
                        Vec2 at = P((Index(U(0x20 + i), 8) - 3.5) * 0.6, (Index(U(0x30 + i), 8) - 3.5) * 0.6);
                        TryPlace(OpenProp.ChessPiece, at, _u, 0.18, 0.18, Seed(40 + i), fallen: U(0x40 + i) < 0.4, offSpines: false);
                    }
                }

                // A big square is composed by QUADRANTS between its walks (docs/OPEN_SPACE.md: the Dutch square, the sports precinct, the pocket
                // park): each gets a room of its own -- a dry lawn with chairs, a grove of dead trees, a seating court under sails, a terrace of
                // long planters, a cafe of umbrella tables -- and the edges, the walks' lights, the kiosk and the name are the same as a small one's.
                if (_extent > QuadrantExtent && Quadrants() >= 2)
                {
                    ShadeAnywhere(2, Shade());
                    KioskAndRack();
                    EdgeBenches(7.5);
                    EdgeTrees(9.0);
                    CornerPlanters(1.2, 0.6);
                    TryPlace(OpenProp.NameWall, P(_su0 + 4.0, _sv0 + 2.4), _u, 1.5, 0.15, Seed(5));
                    Lights(4.0, 2.2, true);
                    Bins(2 + (int)(_extent / 30.0));
                    Rubble(2, 0.18);
                    return;
                }

                // Seat blocks in rows flanking the walks either side of the middle (never on them), more rows the bigger the block, and a
                // second pair of rows flanking the front walk; a block too small for any gets a pair by its front corners.
                double rowS = _focalRadius + 2.6;
                int rows = Math.Max(2, (int)(_extent / 11.0));
                int seats = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int flank = -1; flank <= 1; flank += 2)
                    {
                        for (int k = 0; k < rows; k++)
                        {
                            if (TryPlace(OpenProp.SeatBlock, P(side * (rowS + k * 1.8), flank * 2.5), _u, 0.6, 0.3, Seed(50 + side * 40 + flank * 20 + k))) seats++;
                            if (TryPlace(OpenProp.SeatBlock, P(flank * 2.5, side * (rowS + k * 1.8)), _v, 0.6, 0.3, Seed(51 + side * 40 + flank * 20 + k))) seats++;
                        }
                    }
                }
                if (seats == 0)
                {
                    TryPlace(OpenProp.SeatBlock, P(_su0 + 3.6, _sv0 + 3.0), _u, 0.6, 0.3, Seed(58));
                    TryPlace(OpenProp.SeatBlock, P(_su1 - 3.6, _sv0 + 3.0), _u, 0.6, 0.3, Seed(59));
                }
                // Shade over the back half (before the planters take the corners of a small block): sails on posts or umbrella tables; a block
                // where none of the planned spots fit gets umbrella tables wherever they fit, from the back forward.
                int shade = 0;
                int want = 2 + (int)(_extent / 18.0);
                if (U(0x60) < SailShare)
                {
                    int sails = want + Index(U(0x61), 2);
                    for (int i = 0; i < sails; i++)
                    {
                        double s = (i - (sails - 1) * 0.5) * Math.Max(7.0, SpanU * 0.8 / Math.Max(1, sails));
                        if (TryPlace(OpenProp.ShadeSail, P(s, SpanV * 0.26), _u, 2.6, 2.6, Seed(70 + i), fallen: U(0x70 + i) < 0.2, blocks: false)) shade++;
                    }
                }
                else
                {
                    int tables = want + 1 + Index(U(0x61), 3);
                    for (int i = 0; i < tables; i++)
                    {
                        double s = (i - (tables - 1) * 0.5) * 4.2 + (U(0x80 + i) - 0.5) * 1.2;
                        double t = SpanV * (0.18 + 0.14 * U(0x90 + i));
                        if (TryPlace(OpenProp.UmbrellaTable, P(s, t), _u, 1.1, 1.1, Seed(90 + i), fallen: U(0xA0 + i) < 0.3)) shade++;
                    }
                }
                ShadeAnywhere(want, shade);

                KioskAndRack();
                // Whyte's sittable edges: benches along every kerb facing in, and dead trees in planters between them; then the corners.
                EdgeBenches(7.5);
                EdgeTrees(9.0);
                CornerPlanters(1.2, 0.6);
                TryPlace(OpenProp.NameWall, P(_su0 + 4.0, _sv0 + 2.4), _u, 1.5, 0.15, Seed(5));
                Lights(4.0, 2.2, true);
                Bins(2 + (int)(_extent / 30.0));
                Rubble(2, 0.18);
            }

            /// <summary>How many pieces of shade (sails, umbrella tables) the layout has so far.</summary>
            private int Shade()
            {
                int count = 0;
                foreach (OpenPiece p in Layout.Pieces)
                {
                    if (p.Kind == OpenProp.ShadeSail || p.Kind == OpenProp.UmbrellaTable) count++;
                }
                return count;
            }

            /// <summary>Umbrella tables wherever they fit, from the back forward, until the square has <paramref name="want"/> pieces of shade.</summary>
            private void ShadeAnywhere(int want, int shade)
            {
                for (double t = _sv1 - 3.5; shade < want && t > _sv0 + 3.5; t -= 3.2)
                {
                    for (double s = _su0 + 3.5; shade < want && s <= _su1 - 3.5; s += 3.2)
                    {
                        if (TryPlace(OpenProp.UmbrellaTable, P(s, t), _u, 1.1, 1.1, Seed(180 + shade), fallen: U(0xA8 + shade) < 0.3)) shade++;
                    }
                }
                // A thin square can be all walks and seats on that grid (seed 63428175's block 404, 43 m across, got no shade at all): then a
                // table or two on a finer grid, beside a walk if need be.
                for (double t = _sv1 - 2.5; shade < Math.Min(want, 2) && t > _sv0 + 2.5; t -= 1.6)
                {
                    for (double s = _su0 + 2.5; shade < Math.Min(want, 2) && s <= _su1 - 2.5; s += 1.6)
                    {
                        if (TryPlace(OpenProp.UmbrellaTable, P(s, t), _u, 1.1, 1.1, Seed(190 + shade), fallen: U(0xB8 + shade) < 0.3, offSpines: false)) shade++;
                    }
                }
            }

            /// <summary>The rooms a big square's quadrants are given.</summary>
            private enum Room { Lawn, Grove, Court, Terrace, Cafe }

            /// <summary>
            /// The four quadrants between the walks of a big square, each with its own room; the first is always a court or a cafe, so every
            /// square has shade, and no two neighbours are the same. Returns how many rooms were made (a thin block makes none, and is laid
            /// out as a small square instead).
            /// </summary>
            private int Quadrants()
            {
                int made = 0;
                var rooms = new Room[4];
                rooms[0] = U(0x400) < 0.5 ? Room.Court : Room.Cafe;
                for (int q = 1; q < 4; q++)
                {
                    Room pick = (Room)Index(U(0x401 + q), 5);
                    if (pick == rooms[q - 1]) pick = (Room)(((int)pick + 1) % 5);
                    rooms[q] = pick;
                }
                int q2 = 0;
                foreach ((int sx, int sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    // The quadrant's rectangle: from just off the walks to just inside the edge furniture.
                    double s0 = SpineHalfWidth + 1.4, s1 = (sx < 0 ? -_su0 : _su1) - KerbMargin - 4.2;
                    double t0 = SpineHalfWidth + 1.4, t1 = (sy < 0 ? -_sv0 : _sv1) - KerbMargin - 4.2;
                    double hs = (s1 - s0) * 0.5, ht = (t1 - t0) * 0.5;
                    if (hs < 4.0 || ht < 4.0) { q2++; continue; }
                    Vec2 centre = P(sx * (s0 + hs), sy * (t0 + ht));
                    int salt = 0x500 + q2 * 64;
                    made++;
                    switch (rooms[q2])
                    {
                        case Room.Lawn:
                            Lawn(centre, Math.Min(hs * 0.8, 14.0), Math.Min(ht * 0.8, 10.0), salt);
                            int chairs = 4 + (int)(hs * ht / 30.0);
                            for (int i = 0; i < chairs; i++)
                            {
                                Vec2 at = centre + _u * ((U(salt + 1 + i) - 0.5) * hs * 1.5) + _v * ((U(salt + 17 + i) - 0.5) * ht * 1.5);
                                double yaw = U(salt + 33 + i) * Math.PI * 2.0;
                                TryPlace(OpenProp.Chair, at, new Vec2(Math.Cos(yaw), Math.Sin(yaw)), 0.26, 0.26, Seed(400 + q2 * 50 + i), fallen: U(salt + 49 + i) < 0.35);
                            }
                            for (int i = 0; i < 2; i++)
                            {
                                TryPlace(OpenProp.CubeSeat, centre + _u * (sx * (hs - 1.0)) + _v * ((i - 0.5) * 1.6), _u, 0.23, 0.23, Seed(420 + q2 * 50 + i));
                            }
                            break;
                        case Room.Grove:
                            for (double s = -hs + 2.0; s <= hs - 2.0; s += 6.0)
                            {
                                for (double t = -ht + 2.0; t <= ht - 2.0; t += 6.0)
                                {
                                    TryPlace(OpenProp.TreePlanter, centre + _u * s + _v * t, _u, 0.6, 0.6, Seed(430 + q2 * 50 + (int)(s * 3 + t)));
                                }
                            }
                            break;
                        case Room.Court:
                            // The sails stand OVER the seats (three thin posts a body walks between: they block nothing in the layout).
                            TryPlace(OpenProp.ShadeSail, centre + _u * (sx * Math.Max(0.0, hs - 3.2)), _u, 2.6, 2.6, Seed(450 + q2), fallen: U(salt + 2) < 0.2, blocks: false);
                            if (hs > 8.0) TryPlace(OpenProp.ShadeSail, centre - _u * (sx * (hs - 3.2)), _u, 2.6, 2.6, Seed(451 + q2), fallen: U(salt + 3) < 0.2, blocks: false);
                            for (int row = -1; row <= 1; row++)
                            {
                                for (int k = -1; k <= 1; k++)
                                {
                                    TryPlace(OpenProp.SeatBlock, centre + _u * (k * 2.2) + _v * (row * 2.6), _u, 0.6, 0.3, Seed(440 + q2 * 50 + row * 3 + k));
                                }
                            }
                            break;
                        case Room.Terrace:
                            double len = Math.Min(hs - 0.5, 6.0);
                            for (int row = -1; row <= 1; row++)
                            {
                                TryPlace(OpenProp.Planter, centre + _v * (row * 3.2), _u, len, 0.6, Seed(460 + q2 * 50 + row));
                                TryPlace(OpenProp.SeatBlock, centre + _u * (len + 1.9) + _v * (row * 3.2), _v, 0.6, 0.3, Seed(470 + q2 * 50 + row));
                                TryPlace(OpenProp.SeatBlock, centre - _u * (len + 1.9) + _v * (row * 3.2), _v, 0.6, 0.3, Seed(480 + q2 * 50 + row));
                            }
                            break;
                        default:
                            for (double s = -hs + 2.2; s <= hs - 2.2; s += 4.2)
                            {
                                for (double t = -ht + 2.2; t <= ht - 2.2; t += 4.2)
                                {
                                    TryPlace(OpenProp.UmbrellaTable, centre + _u * s + _v * t, _u, 1.1, 1.1, Seed(490 + q2 * 50 + (int)(s * 3 + t)), fallen: U(salt + 5 + (int)(s + t)) < 0.3);
                                }
                            }
                            break;
                    }
                    q2++;
                }
                return made;
            }

            // ---- the pocket park ----
            public void PocketPark()
            {
                int lawns = SpanU > 2.0 * SpanV ? 2 : 1;
                for (int i = 0; i < lawns; i++)
                {
                    double s = lawns == 2 ? (i == 0 ? -0.24 : 0.24) * SpanU : 0.0;
                    Lawn(P(s, 0.04 * SpanV), (lawns == 2 ? 0.17 : 0.27) * SpanU, 0.23 * SpanV, 0xC0 + i);
                }
                // Loose chairs on and round the lawns (a third fallen), more the bigger the park; a pergola or a tree bench at the back; benches
                // and dead trees along the kerbs.
                int chairs = 6 + Index(U(0xD0), 5) + (int)(_extent / 7.0);
                for (int i = 0; i < chairs; i++)
                {
                    Vec2 at = P((U(0xD1 + i) - 0.5) * SpanU * 0.7, (U(0xE1 + i) - 0.5) * SpanV * 0.6);
                    double yaw = U(0xF1 + i) * Math.PI * 2.0;
                    TryPlace(OpenProp.Chair, at, new Vec2(Math.Cos(yaw), Math.Sin(yaw)), 0.26, 0.26, Seed(100 + i), fallen: U(0x101 + i) < 0.35);
                }
                if (!TryPlace(OpenProp.Pergola, P(0.0, SpanV * 0.32), _u, 3.0, 2.0, Seed(6)))
                {
                    TryPlace(OpenProp.RoundBench, P(0.0, SpanV * 0.3), _u, 1.3, 1.3, Seed(7));
                }
                if (_extent > 40.0)
                {
                    TryPlace(OpenProp.RoundBench, P(-SpanU * 0.3, SpanV * 0.1), _u, 1.3, 1.3, Seed(8));
                }
                EdgeBenches(9.0);
                EdgeTrees(8.0);
                KioskAndRack();
                Bins(1 + (int)(_extent / 40.0));
                Rubble(1, 0.3);
            }

            // ---- the promenade ----
            public void Promenade()
            {
                double t = Math.Min(SpanV * 0.28, SpanV * 0.5 - KerbMargin - 1.2);
                int k = 0;
                for (double s = _su0 + 4.5; s <= _su1 - 4.5; s += 4.5, k++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (k % 2 == 0)
                        {
                            TryPlace(OpenProp.TreePlanter, P(s, side * t), _u, 0.6, 0.6, Seed(120 + k * 2 + (side + 1)));
                        }
                        else
                        {
                            TryPlace(OpenProp.Bench, P(s, side * t), side < 0 ? _u : -_u, 1.1, 0.3, Seed(140 + k * 2 + (side + 1)));
                        }
                    }
                    if (k % 4 == 1)
                    {
                        TryPlace(OpenProp.Bin, P(s + 1.6, -t), _u, 0.42, 0.42, Seed(160 + k));
                    }
                }
                Lights(8.0, 2.2);
                Rubble(1, 0.25);
            }

            // ---- the bare yard: nothing, or one thing ----
            public void BareYard()
            {
                double h = U(0x200);
                if (h < 0.40) return;
                if (h < 0.60) TryPlace(OpenProp.Bench, P(0.1 * SpanU, -0.1 * SpanV), _u, 1.1, 0.3, Seed(8));
                else if (h < 0.80) Rubble(1, 0.22);
                else TryPlace(OpenProp.DeadTree, P(0.05 * SpanU, 0.1 * SpanV), _u, 0.3, 0.3, Seed(9));
            }

            // ---- the vacant lot ----
            public void VacantLot()
            {
                Rubble(2 + Index(U(0x210), 3), 0.2);
                // A hoarding along the street in two runs, with the gap between them where the walk in is (and the test's flood starts).
                int front = Layout.Front;
                double length = EdgeLength(front);
                const double gap = 3.2;
                double run = (length * 0.5 - 1.0 - gap * 0.5) * 0.5;
                if (run > 1.2)
                {
                    Vec2 along = Edge(front);
                    Vec2 mid = EdgeMid(front) + along.Perp * 0.9;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        TryPlace(OpenProp.Hoarding, mid + along * (side * (gap * 0.5 + run)), along, run, 0.1, Seed(10 + side), extra: 0.0, offSpines: false, kerbMargin: 0.3);
                    }
                }
            }

            // ---- the quiet court: planters along the kerbs and a sparse grid inside, nothing to sit on ----
            public void QuietCourt()
            {
                int n = _ring.Count, k = 0;
                for (int e = 0; e < n; e++)
                {
                    double len = EdgeLength(e);
                    if (len < 8.0) continue;
                    Vec2 dir = Edge(e), inward = dir.Perp;
                    for (double along = 3.5; along < len - 3.0; along += 7.0, k++)
                    {
                        TryPlace(OpenProp.Planter, _ring[e] + dir * along + inward * 3.0, dir, 0.9, 0.5, Seed(220 + k));
                    }
                }
                for (double s = _su0 + 9.0; s <= _su1 - 9.0; s += 11.0)
                {
                    for (double t = _sv0 + 9.0; t <= _sv1 - 9.0; t += 11.0, k++)
                    {
                        TryPlace(OpenProp.Planter, P(s, t), _u, 0.8, 0.8, Seed(300 + k));
                    }
                }
            }

            // ---- the pieces several roles share ----

            private void CornerPlanters(double halfX, double halfZ)
            {
                int n = _ring.Count;
                for (int e = 0; e < n; e++)
                {
                    double len = EdgeLength(e);
                    if (len < 6.0) continue;
                    Vec2 dir = Edge(e), inward = dir.Perp;
                    foreach (double along in new[] { 2.4, len - 2.4 })
                    {
                        TryPlace(OpenProp.Planter, _ring[e] + dir * along + inward * 2.3, dir, halfX, halfZ, Seed(230 + e * 2 + (along > 3.0 ? 1 : 0)));
                    }
                }
            }

            private void KioskAndRack()
            {
                int front = Layout.Front;
                if (EdgeLength(front) < 20.0) return;
                Vec2 dir = Edge(front);
                Vec2 at = EdgeMid(front) + dir.Perp * 4.6 + dir * 3.0;
                if (TryPlace(OpenProp.Kiosk, at, dir, 1.6, 1.2, Seed(11)))
                {
                    TryPlace(OpenProp.BikeRack, at + dir * 4.4, dir, 0.9, 0.4, Seed(12));
                }
            }

            /// <summary>Bollard lights down both sides of the front walk (or every walk), every <paramref name="every"/> metres, <paramref name="aside"/> off it.</summary>
            private void Lights(double every, double aside, bool everyWalk = false)
            {
                int n = _ring.Count, k = 0;
                for (int e = 0; e < n; e++)
                {
                    if (!everyWalk && e != Layout.Front) continue;
                    if (EdgeLength(e) < 6.0) continue;
                    Vec2 from = EdgeMid(e) + Edge(e).Perp * 2.0;
                    Vec2 to = _c;
                    double length = (to - from).Length;
                    if (length < 6.0) continue;
                    Vec2 dir = (to - from) / length;
                    for (double d = 2.0; d < length - _focalRadius - 1.5; d += every, k++)
                    {
                        foreach (int side in new[] { -1, 1 })
                        {
                            TryPlace(OpenProp.BollardLight, from + dir * d + dir.Perp * (side * aside), dir, 0.15, 0.15, Seed(240 + k * 2 + (side + 1) / 2), offSpines: false);
                        }
                    }
                }
            }

            /// <summary>Benches along every kerb, facing in, every <paramref name="every"/> metres, 2.2 m in from it.</summary>
            private void EdgeBenches(double every)
            {
                int n = _ring.Count, k = 0;
                for (int e = 0; e < n; e++)
                {
                    double len = EdgeLength(e);
                    if (len < 8.0) continue;
                    Vec2 dir = Edge(e), inward = dir.Perp;
                    for (double along = 5.0; along < len - 4.0; along += every, k++)
                    {
                        // The seat faces the frame's +z, which is +x turned a quarter: a frame along the kerb faces inward.
                        TryPlace(OpenProp.Bench, _ring[e] + dir * along + inward * 2.2, dir, 1.1, 0.3, Seed(280 + k));
                    }
                }
            }

            /// <summary>Dead trees in planters along every kerb, every <paramref name="every"/> metres, 3.2 m in, between the benches.</summary>
            private void EdgeTrees(double every)
            {
                int n = _ring.Count, k = 0;
                for (int e = 0; e < n; e++)
                {
                    double len = EdgeLength(e);
                    if (len < 10.0) continue;
                    Vec2 dir = Edge(e), inward = dir.Perp;
                    for (double along = every * 0.5 + 1.0; along < len - 4.0; along += every, k++)
                    {
                        TryPlace(OpenProp.TreePlanter, _ring[e] + dir * along + inward * 3.4, dir, 0.6, 0.6, Seed(250 + k));
                    }
                }
            }

            private void Bins(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    TryPlace(OpenProp.Bin, P((i == 0 ? -1 : 1) * 2.6, _sv0 + 3.2), _u, 0.42, 0.42, Seed(260 + i));
                }
            }

            private void Rubble(int count, double where)
            {
                for (int k = 0; k < count; k++)
                {
                    double angle = U(0x300 + k) * Math.PI * 2.0;
                    Vec2 at = _c + new Vec2(Math.Cos(angle), Math.Sin(angle)) * (_extent * (where + 0.1 * k));
                    TryPlace(OpenProp.Rubble, at, _u, 1.0, 1.0, Seed(270 + k), extra: 1.3);
                }
            }

            /// <summary>A dry lawn: an ellipse of 12 points pushed in and out a little, kept inside the kerb (shrunk until it is).</summary>
            private void Lawn(Vec2 centre, double a, double b, int salt)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    var outline = new Vec2[12];
                    bool fits = a >= 2.5 && b >= 2.0;
                    for (int i = 0; i < 12 && fits; i++)
                    {
                        double angle = i * Math.PI / 6.0;
                        double wobble = 0.88 + 0.24 * Unit(_seed, _block, salt * 16 + i);
                        outline[i] = centre + _u * (Math.Cos(angle) * a * wobble) + _v * (Math.Sin(angle) * b * wobble);
                        if (!Inside(_ring, outline[i], KerbMargin + 0.6)) fits = false;
                    }
                    if (fits)
                    {
                        Layout.Lawns.Add(outline);
                        return;
                    }
                    a *= 0.8;
                    b *= 0.8;
                }
            }
        }
    }
}
