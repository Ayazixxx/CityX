using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>The way a footbridge's path crosses the gap (2026-10-16: "more variety in angles/curves/turns").</summary>
    public enum BridgeShape : byte
    {
        /// <summary>Straight out of one doorway into the one facing it.</summary>
        Straight,

        /// <summary>Straight, but across on the slant to a doorway further along the other wall.</summary>
        Angled,

        /// <summary>Out of each doorway onto a landing, the two landings joined on the slant (a landing on a pier where the ground allows).</summary>
        DogLeg,

        /// <summary>An S-curve, square out of one doorway and square into the other.</summary>
        Curve,
    }

    /// <summary>How a footbridge is guarded (2026-10-16: "make them have different types of guard rails").</summary>
    public enum BridgeRail : byte
    {
        /// <summary>A solid concrete parapet with a handrail along its top.</summary>
        Parapet,

        /// <summary>A catwalk's rails: posts, a top rail, a middle rail and a toe board.</summary>
        Rails,

        /// <summary>Pickets: posts, a top and a bottom rail, thin balusters close between them.</summary>
        Pickets,

        /// <summary>Posts and a top rail with a steel mesh panel between.</summary>
        Mesh,

        /// <summary>Industrial pipe rails: posts and three pipes.</summary>
        Pipe,
    }

    /// <summary>What a footbridge's deck is.</summary>
    public enum BridgeDeck : byte
    {
        /// <summary>A concrete slab.</summary>
        Concrete,

        /// <summary>A steel catwalk: a thin plate on two beams.</summary>
        Steel,
    }

    /// <summary>
    /// A footbridge's walking line, in the world: points at the walking surface along its middle, from A's doorway to B's. Each stretch between
    /// two points is deck (level, or sloping at most <see cref="Footbridges.MaxGrade"/>) or a flight of steps (<see cref="Risers"/> &gt; 0, each at
    /// most <see cref="Footbridges.MaxRiser"/> high and <see cref="Footbridges.Tread"/> deep). The ends lie on the walls' faces, the deck's ends cut
    /// along the walls (<see cref="TangentA"/>, <see cref="TangentB"/>); a dog-leg's turns are landings, and a pier holds a landing (or a long
    /// span's middle) up where the ground under it is not a road.
    /// </summary>
    public sealed class BridgePath
    {
        public readonly Vector3[] Points;

        /// <summary>Per stretch: 0 for deck, the number of steps for a flight.</summary>
        public readonly int[] Risers;

        /// <summary>Per point: a turn (a landing platform, square to neither stretch).</summary>
        public readonly bool[] Turns;

        /// <summary>Along A's and B's walls at the ends (world, unit): the deck's ends are cut to them.</summary>
        public readonly Vector2 TangentA, TangentB;

        /// <summary>Where piers stand: x, z and the underside of the deck they hold (the pier runs down to the ground from it).</summary>
        public readonly Vector3[] Piers;

        public BridgePath(Vector3[] points, int[] risers, bool[] turns, Vector2 tangentA, Vector2 tangentB, Vector3[] piers)
        {
            Points = points;
            Risers = risers;
            Turns = turns;
            TangentA = tangentA;
            TangentB = tangentB;
            Piers = piers;
        }

        public int Stretches => Points.Length - 1;

        /// <summary>The path's length in plan, metres.</summary>
        public float Length
        {
            get
            {
                float l = 0.0f;
                for (int i = 0; i + 1 < Points.Length; i++) l += Flat(Points[i + 1] - Points[i]).magnitude;
                return l;
            }
        }

        /// <summary>Whether any stretch is a flight.</summary>
        public bool HasStairs
        {
            get
            {
                for (int i = 0; i < Risers.Length; i++) if (Risers[i] > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The walking surface's height <paramref name="along"/> metres along the path in plan (a flight: the top of the step there; past either end:
        /// the end's). For a test or a body following the path.
        /// </summary>
        public float HeightAt(float along)
        {
            float s = 0.0f;
            for (int i = 0; i + 1 < Points.Length; i++)
            {
                Vector3 p = Points[i], q = Points[i + 1];
                float l = Flat(q - p).magnitude;
                if (along <= s + l || i + 2 == Points.Length)
                {
                    float t = l > 1e-5f ? Mathf.Clamp01((along - s) / l) : 0.0f;
                    if (Risers[i] <= 0) return Mathf.Lerp(p.y, q.y, t);
                    int n = Risers[i];
                    // Step k (0-based) covers [k, k+1) treads; its top is the start's height plus (k+1) risers (the first step a riser up).
                    int k = Mathf.Clamp(Mathf.FloorToInt(t * n), 0, n - 1);
                    return p.y + (q.y - p.y) * (k + 1) / n;
                }
                s += l;
            }
            return Points[Points.Length - 1].y;
        }

        /// <summary>The point <paramref name="along"/> metres along the path in plan (x, z), and the direction it runs there.</summary>
        public Vector2 PlanAt(float along, out Vector2 direction)
        {
            float s = 0.0f;
            for (int i = 0; i + 1 < Points.Length; i++)
            {
                Vector2 p = Flat(Points[i]), q = Flat(Points[i + 1]);
                float l = (q - p).magnitude;
                if (along <= s + l || i + 2 == Points.Length)
                {
                    direction = l > 1e-5f ? (q - p) / l : Vector2.right;
                    return Vector2.Lerp(p, q, l > 1e-5f ? Mathf.Clamp01((along - s) / l) : 0.0f);
                }
                s += l;
            }
            direction = Vector2.right;
            return Flat(Points[Points.Length - 1]);
        }

        internal static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
    }

    /// <summary>
    /// A stair from a footbridge down to the street, along the wall of the building it leaves (Chongqing's outside stairs; 2026-10-16: "have
    /// some of them connect to the sky bridges"): from the side of the deck where it leaves the wall, a straight run of flights hugging the wall
    /// down to the paving, a landing between flights and a short one on the paving. <see cref="Points"/> is its walking line from the top to the
    /// foot (each stretch deck or a flight, as a bridge's path); <see cref="U0"/>..<see cref="U1"/> is the stretch of the wall it covers, from the
    /// wall's start, and <see cref="Bottom"/>..<see cref="Top"/> its heights, so the facade keeps clear of it. The bridge's rail on that side is
    /// open for the first <see cref="Footbridges.StreetStairOpening"/> metres out from the wall.
    /// </summary>
    public sealed class StreetStair
    {
        public readonly int Site;
        public readonly WindowGrid.Wall Wall;

        /// <summary>The walking line from the top to the foot (each stretch deck or a flight, as a bridge's path): the way a body goes down it.</summary>
        public readonly Vector3[] Points;

        public readonly int[] Risers;

        /// <summary>The flights and landings it is built of.</summary>
        public readonly StairFlight[] Flights;

        public readonly StairLanding[] Landings;

        /// <summary>A zig-zag tower against the wall (flights doubling back on two lanes), not a straight run down it.</summary>
        public readonly bool Switchback;

        /// <summary>The wall's outward normal (world, unit): the stair's open side is this way.</summary>
        public readonly Vector2 Out;

        /// <summary>The way it runs along the wall from the deck (world, unit).</summary>
        public readonly Vector2 Along;

        public readonly float U0, U1;
        public readonly float Bottom, Top;
        public readonly float Width;

        public StreetStair(int site, WindowGrid.Wall wall, Vector3[] points, int[] risers, StairFlight[] flights, StairLanding[] landings, bool switchback,
            Vector2 outward, Vector2 along, float u0, float u1, float bottom, float top, float width)
        {
            Site = site;
            Wall = wall;
            Points = points;
            Risers = risers;
            Flights = flights;
            Landings = landings;
            Switchback = switchback;
            Out = outward;
            Along = along;
            U0 = u0;
            U1 = u1;
            Bottom = bottom;
            Top = top;
            Width = width;
        }
    }

    /// <summary>One flight of a street stair: its walking line's two ends (world; from <see cref="From"/> down or up to <see cref="To"/>) and its steps.</summary>
    public readonly struct StairFlight
    {
        public readonly Vector3 From, To;
        public readonly int Risers;

        /// <summary>A rail on its left (looking from <see cref="From"/> to <see cref="To"/>), on its right.</summary>
        public readonly bool RailLeft, RailRight;

        public StairFlight(Vector3 from, Vector3 to, int risers, bool railLeft, bool railRight)
        {
            From = from;
            To = to;
            Risers = risers;
            RailLeft = railLeft;
            RailRight = railRight;
        }
    }

    /// <summary>
    /// One landing of a street stair: a level platform at <see cref="Centre"/> (world, at the walking surface), <see cref="HalfAlong"/> either way
    /// along the wall (<see cref="Along"/>) and <see cref="HalfOut"/> either way across it; rails on the sides named (in its own frame: +along, -along,
    /// the side away from the wall).
    /// </summary>
    public readonly struct StairLanding
    {
        public readonly Vector3 Centre;
        public readonly Vector2 Along, Out;
        public readonly float HalfAlong, HalfOut;
        public readonly bool RailPlus, RailMinus, RailOut;

        public StairLanding(Vector3 centre, Vector2 along, Vector2 outward, float halfAlong, float halfOut, bool railPlus, bool railMinus, bool railOut)
        {
            Centre = centre;
            Along = along;
            Out = outward;
            HalfAlong = halfAlong;
            HalfOut = halfOut;
            RailPlus = railPlus;
            RailMinus = railMinus;
            RailOut = railOut;
        }
    }

    /// <summary>
    /// A footbridge between two buildings (Footbridges): from a doorway in a floor of one across the street to a doorway in a floor of the
    /// other. The lower-numbered building (<see cref="SiteA"/>) draws the deck; each building cuts its own doorway.
    /// </summary>
    public readonly struct Footbridge
    {
        public readonly int SiteA, SiteB;
        public readonly WindowGrid.Wall WallA, WallB;
        public readonly int FloorA, FloorB;
        public readonly int ColumnA, ColumnB;

        /// <summary>The deck's middle along each wall from the wall's start (the builder's column 0 edge), metres.</summary>
        public readonly float UA, UB;

        /// <summary>The deck's top at each end (world), at the middle of its width, level with the floor inside.</summary>
        public readonly Vector3 StartWorld, EndWorld;

        public readonly float Width;

        /// <summary>Which of A's bridges this is (its doors' unit numbers come from it).</summary>
        public readonly int Index;

        public readonly BridgeShape Shape;
        public readonly BridgeRail Rail;
        public readonly BridgeDeck Deck;

        /// <summary>The walking line from <see cref="StartWorld"/> to <see cref="EndWorld"/>.</summary>
        public readonly BridgePath Path;

        /// <summary>The stair down to the street from a landing near A, or null.</summary>
        public readonly StreetStair Stair;

        public Footbridge(int siteA, int siteB, WindowGrid.Wall wallA, WindowGrid.Wall wallB, int floorA, int floorB, int columnA, int columnB,
            float ua, float ub, Vector3 startWorld, Vector3 endWorld, float width, int index, BridgeShape shape, BridgeRail rail, BridgeDeck deck,
            BridgePath path, StreetStair stair)
        {
            SiteA = siteA;
            SiteB = siteB;
            WallA = wallA;
            WallB = wallB;
            FloorA = floorA;
            FloorB = floorB;
            ColumnA = columnA;
            ColumnB = columnB;
            UA = ua;
            UB = ub;
            StartWorld = startWorld;
            EndWorld = endWorld;
            Width = width;
            Index = index;
            Shape = shape;
            Rail = rail;
            Deck = deck;
            Path = path;
            Stair = stair;
        }

        /// <summary>From doorway to doorway in a straight line in plan (the path itself can be longer: <see cref="BridgePath.Length"/>).</summary>
        public float Span => Vector3.Distance(new Vector3(StartWorld.x, 0.0f, StartWorld.z), new Vector3(EndWorld.x, 0.0f, EndWorld.z));

        /// <summary>The wall, floor, column and position along the wall on <paramref name="site"/>'s side.</summary>
        public void EndOn(int site, out WindowGrid.Wall wall, out int floor, out int column, out float u)
        {
            bool a = site == SiteA;
            wall = a ? WallA : WallB;
            floor = a ? FloorA : FloorB;
            column = a ? ColumnA : ColumnB;
            u = a ? UA : UB;
        }
    }

    /// <summary>A stretch of a wall the facade keeps clear (a bridge's landing on it, a street stair along it): along the wall from the building's
    /// centre (as a door's U0) and in height above the building's floor.</summary>
    public readonly struct FacadeClearing
    {
        public readonly float U0, U1, Y0, Y1;

        public FacadeClearing(float u0, float u1, float y0, float y1)
        {
            U0 = u0;
            U1 = u1;
            Y0 = y0;
            Y1 = y1;
        }
    }

    /// <summary>
    /// FOOTBRIDGES BETWEEN BUILDINGS (the layered city, docs/LAYERED_CITY_PLAN.md L2, 2026-10-15; shapes, stairs, rails and the street stairs
    /// 2026-10-16): Chongqing's bridges from a floor of one block across the street into a floor of the block facing it, at several heights where
    /// the blocks are tall -- the street for those floors. At each end a DOOR in place of the window there (a balcony door's kind: drawn, opened and
    /// shared by BalconyDoors' machinery), so every bridge is walked onto from inside and off into the other building (owner: "make everything
    /// walkable and functionable as a pedestrian not just look good").
    ///
    /// 2026-10-16 (owner, with four reference images: "ive noticed the sky bridges are often times very low, make these have a lower chance of
    /// appearing closer to the surface and a higher chance of appear on taller buildings. I also want to make the sky bridges more intricate ...
    /// more variety in angles/curves/turns, make them have different types of guard rails ... have some of them connect to the sky bridges"):
    ///   * HIGHER: a pair's chance grows with the shorter block's floors (<see cref="TallFactor"/>: a three-floor pair a sixth of the old chance, a
    ///     ten-floor one the full share, taller more), and a bridge's floor is drawn weighted by its height over the street
    ///     (<see cref="HeightWeight"/>), so a bridge two floors up is a rarity between towers; tall pairs get a second bridge, very tall a third.
    ///   * SHAPES (<see cref="BridgeShape"/>): straight, on the slant (10-40 degrees off square), a dog-leg round two landings, or an S-curve.
    ///   * STAIRS: two floors up to <see cref="MaxStairRise"/> apart are joined by a flight (or flights with landings) in the bridge's straight
    ///     part, so the floors no longer have to line up.
    ///   * RAILS (<see cref="BridgeRail"/>) and DECKS (<see cref="BridgeDeck"/>) by district: concrete parapets in the old town, pickets in the
    ///     homes, catwalk rails and mesh downtown, pipe rails and steel catwalks in the industrial districts.
    ///   * PIERS under a dog-leg's landings and a long span's middle, where the ground under it is not a road.
    ///   * STREET STAIRS (<see cref="StreetStair"/>): a low dog-leg's landing beside a side or back wall gets, often, a stair down along that wall
    ///     to the paving, so the bridge is reached from the street.
    ///
    /// Pure, from the two buildings' seeds: the lower-numbered building of a facing pair decides (<see cref="FromSite"/>), the higher one
    /// finds it by looking at its neighbours, so both agree without talking; kept per building like the balcony doors.
    /// </summary>
    public static class Footbridges
    {
        /// <summary>The deck: how wide, how thick, its parapets' height and thickness, metres (the body is 2.6 m, its capsule 0.52 m across).</summary>
        public const float DeckWidth = 2.8f;

        public const float DeckThickness = 0.35f;

        public const float ParapetHeight = 1.25f;

        public const float ParapetThickness = 0.16f;

        /// <summary>How far across a street a bridge reaches, metres (door to door in a straight line), and the least gap worth bridging.</summary>
        public const float MaxSpan = 34.0f;

        public const float MinSpan = 5.0f;

        /// <summary>The most the two ends' floors may differ on a deck with no stairs (it slopes between them), and the steepest that deck gets.</summary>
        public const float MaxRise = 1.6f;

        public const float MaxGrade = 0.1f;

        /// <summary>The most two floors joined by a bridge's stairs may differ, metres (about two storeys).</summary>
        public const float MaxStairRise = 9.0f;

        /// <summary>A step outdoors: its going and the most it rises (BuildingStairs.Tread; EntranceStairs.Riser), metres.</summary>
        public const float Tread = 0.36f;

        public const float MaxRiser = 0.22f;

        /// <summary>The most steps in one flight before a landing (4 m: the building code's 12 ft between landings is 4.75 m at the game's 1.3x).</summary>
        public const int FlightMax = 18;

        /// <summary>A landing between two flights, and the deck kept level either side of a bridge's flights before a doorway or a turn, metres.</summary>
        public const float FlightLanding = 1.8f;

        public const float StairMargin = 1.2f;

        /// <summary>The deck's underside clears the ground (a street under it) by at least this.</summary>
        public const float GroundClearance = 5.2f;

        /// <summary>A building needs this many floors to carry one; the lowest floor a bridge leaves from.</summary>
        public const int MinFloors = 3;

        public const int LowestFloor = 1;

        /// <summary>A second bridge, higher up, between blocks of at least this many floors, with this chance; a third between very tall ones.</summary>
        public const int SecondBridgeFloors = 8;

        public const double SecondBridgeShare = 0.5;

        public const int ThirdBridgeFloors = 16;

        public const double ThirdBridgeShare = 0.35;

        /// <summary>How many floors apart two bridges of one pair stand at least.</summary>
        public const int FloorsBetweenBridges = 3;

        /// <summary>The slant of an angled bridge off square to its wall, degrees.</summary>
        public const float MinAngle = 10.0f;

        public const float MaxAngle = 40.0f;

        /// <summary>How far a dog-leg's landing stands out from its wall (its middle), metres.</summary>
        public const float LandingOut = 2.0f;

        /// <summary>A curve is sampled this often, metres.</summary>
        public const float CurveStep = 1.6f;

        /// <summary>A pier's side, the tallest one stood, and the span past which a straight or curved bridge gets one in its middle.</summary>
        public const float PierSize = 0.6f;

        public const float MaxPierHeight = 45.0f;

        public const float LongSpan = 24.0f;

        /// <summary>A street stair's width, the highest deck one descends from, and how often a bridge that can have one does.</summary>
        public const float StreetStairWidth = 1.8f;

        public const float MaxStreetStairRise = 16.0f;

        public const double StreetStairShare = 0.85;

        /// <summary>How far a street stair's middle runs out from the wall, and how far out the bridge's rail on its side is open for it.</summary>
        public const float StreetStairOffset = StreetStairWidth * 0.5f + 0.12f;

        public const float StreetStairOpening = StreetStairWidth + 0.3f;

        /// <summary>The short landing a street stair ends on, on the paving.</summary>
        public const float StreetStairFoot = 1.2f;

        /// <summary>The highest deck a street stair's zig-zag tower climbs to (about ten storeys), and the gap between its two lanes of flights.</summary>
        public const float MaxTowerRise = 45.0f;

        public const float TowerLaneGap = 0.1f;

        /// <summary>How far in from a wall's corners a doorway stays (columns), and how the doors are numbered among a building's balcony doors.</summary>
        public const int CornerColumns = 1;

        public const int DoorUnitBase = 1000;

        /// <summary>A switch for before/after renders and tests.</summary>
        public static bool On = true;

        private const ulong Salt = 0x42524447UL;   // "BRDG"

        /// <summary>The share of facing pairs that get a bridge, by the lower building's district (before <see cref="TallFactor"/>).</summary>
        public static double ShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.55;
                case ZoneKind.Urban: return 0.5;
                case ZoneKind.OldTown: return 0.35;
                case ZoneKind.Residential: return 0.4;
                case ZoneKind.Industrial: return 0.12;
                default: return 0.2;
            }
        }

        /// <summary>
        /// How the share scales with the shorter block's floors (owner: "a higher chance of appear on taller buildings"): rising as the floors over
        /// two to the power 1.5 -- three or four floors a twelfth of it, six a third, eight two thirds, ten the whole, twelve and more 1.4 times. (It
        /// rose in a straight line until 2026-10-16, and on seed 45678679 fewer than half the bridges near the spawn joined blocks of eight floors.)
        /// A square root, not Math.Pow, so every machine agrees.
        /// </summary>
        public static double TallFactor(int floors)
        {
            double f = System.Math.Max(0.0, (floors - 2) / 8.0);
            return System.Math.Min(1.4, System.Math.Max(0.08, f * System.Math.Sqrt(f)));
        }

        /// <summary>
        /// How much a floor's bridge is wanted, by its deck's clearance over the street, metres (owner: "a lower chance of appearing closer to the
        /// surface"): 8 m clear a fifth as much as 30 m, the most from 30 m up.
        /// </summary>
        public static double HeightWeight(float clearance)
        {
            return System.Math.Pow(Mathf.Clamp(clearance / 30.0f, 0.04f, 1.0f), 1.25);
        }

        // ---- the cache ----

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, Footbridge[]> Cache =
            new System.Collections.Concurrent.ConcurrentDictionary<long, Footbridge[]>();

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, Footbridge[]> Decided =
            new System.Collections.Concurrent.ConcurrentDictionary<long, Footbridge[]>();

        public const int CacheLimit = 4096;

        private static readonly Footbridge[] None = new Footbridge[0];

        /// <summary>A building, by its seed (which comes of the world's) and its id.</summary>
        private static long Key(BuildingSite site) => ((long)site.Seed << 32) ^ (uint)site.Id;

        /// <summary>Every bridge touching a building (as either end), in a fixed order: those it decides first (by wall), then its neighbours'.</summary>
        public static Footbridge[] For(CityWorld world, BuildingSite site)
        {
            if (!On || !Eligible(site))
            {
                return None;
            }
            long key = Key(site);
            if (Cache.TryGetValue(key, out Footbridge[] bridges))
            {
                return bridges;
            }
            var list = new List<Footbridge>(FromSite(world, site));
            AddIncoming(world, site, list);
            bridges = list.Count == 0 ? None : list.ToArray();
            if (Cache.Count >= CacheLimit)
            {
                Cache.Clear();
            }
            Cache[key] = bridges;
            return bridges;
        }

        /// <summary>The bridges a lower-numbered neighbour decided that land on this building: every building owned by the chunks round this one.</summary>
        private static void AddIncoming(CityWorld world, BuildingSite site, List<Footbridge> list)
        {
            int cx = Plan.PlanWorld.ChunkOf(site.CentreX), cy = Plan.PlanWorld.ChunkOf(site.CentreZ);
            int reach = Mathf.CeilToInt((MaxSpan + Mathf.Max(site.Params.Width, site.Params.Depth)) / CityLayout.ChunkSize) + 1;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    List<int> owned = world.BuildingsOwnedBy(cx + dx, cy + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        if (owned[i] >= site.Id) continue;
                        BuildingSite other = world.Building(owned[i]);
                        if (!Eligible(other)) continue;
                        foreach (Footbridge b in FromSite(world, other))
                        {
                            if (b.SiteB == site.Id) list.Add(b);
                        }
                    }
                }
            }
        }

        /// <summary>The bridges a building has, if they have been worked out (CityMeshBuilder.BuildSite does, before its walls); none otherwise.</summary>
        public static Footbridge[] Cached(BuildingSite site)
        {
            return Cache.TryGetValue(Key(site), out Footbridge[] bridges) ? bridges : None;
        }

        /// <summary>Forgets every bridge (a new world, or the switch turned).</summary>
        public static void Reset()
        {
            Cache.Clear();
            Decided.Clear();
        }

        /// <summary>An ordinary building of enough floors: not a sealed block, a roof-top block, a wing, a round tower or a set piece.</summary>
        public static bool Eligible(BuildingSite site)
        {
            return site.Exists && !site.Shell && !site.OnRoof && !site.Wing && site.Sides < 3 && !site.Params.IsSpecial
                && BuildingInterior.FloorCountFor(site.Params.Height) >= MinFloors;
        }

        /// <summary>Whether a window cell of a building is a bridge's doorway (it gets no window), from the bridges already worked out.</summary>
        public static bool IsDoorCell(BuildingSite site, WindowGrid.Wall wall, int column, int row)
        {
            Footbridge[] bridges = Cached(site);
            for (int i = 0; i < bridges.Length; i++)
            {
                bridges[i].EndOn(site.Id, out WindowGrid.Wall w, out int floor, out int c, out _);
                if (w == wall && c == column && floor * BuildingInterior.StoreyHeightRows == row)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The stretches of a wall the facade keeps clear for the bridges known for the building (<see cref="Cached"/>): each bridge's landing on
        /// it (the deck's width along the wall, wider on the slant, and the doorway's height, with a margin), and each street stair along it
        /// (from the ground to over its top). Along the wall from the building's centre, heights above its floor.
        /// </summary>
        public static void ClearingsOn(BuildingSite site, WindowGrid.Wall wall, List<FacadeClearing> into, float margin)
        {
            Footbridge[] bridges = Cached(site);
            float length = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
            float storey = BuildingInterior.StoreyHeight(site.Params.Height);
            for (int i = 0; i < bridges.Length; i++)
            {
                Footbridge b = bridges[i];
                b.EndOn(site.Id, out WindowGrid.Wall w, out int floor, out _, out float u);
                if (w == wall)
                {
                    // The deck's width along the wall: wider where it meets the wall on the slant.
                    Vector2 tangent = site.Id == b.SiteA ? b.Path.TangentA : b.Path.TangentB;
                    Vector2 direction = site.Id == b.SiteA
                        ? (BridgePath.Flat(b.Path.Points[1]) - BridgePath.Flat(b.Path.Points[0])).normalized
                        : (BridgePath.Flat(b.Path.Points[b.Path.Points.Length - 2]) - BridgePath.Flat(b.Path.Points[b.Path.Points.Length - 1])).normalized;
                    float across = Mathf.Max(0.3f, Mathf.Abs(direction.x * -tangent.y + direction.y * tangent.x));   // sine of the angle to the wall
                    float half = Mathf.Max(DeckWidth / across, BalconyDoors.MaxDoubleWidth) * 0.5f + margin;
                    float centre = u - length * 0.5f;
                    float y = floor * storey;
                    into.Add(new FacadeClearing(centre - half, centre + half, y - DeckThickness - margin,
                        y + Mathf.Min(BalconyDoors.MaxHeight, BuildingInterior.RowHeight(site.Params.Height) - BalconyDoors.HeadBar) + margin));
                }
                StreetStair s = b.Stair;
                if (s != null && s.Site == site.Id && s.Wall == wall)
                {
                    into.Add(new FacadeClearing(s.U0 - length * 0.5f - margin, s.U1 - length * 0.5f + margin, s.Bottom - site.BaseHeight - 1.0f,
                        s.Top - site.BaseHeight + ParapetHeight + margin));
                }
            }
        }

        // ---- deciding ----

        /// <summary>The bridges a building decides: per wall, to the building facing that wall across the street, one to three.</summary>
        private static Footbridge[] FromSite(CityWorld world, BuildingSite a)
        {
            long key = Key(a);
            if (Decided.TryGetValue(key, out Footbridge[] done))
            {
                return done;
            }
            var list = new List<Footbridge>(2);
            WindowPlan planA = WindowPlan.For(world, a);
            ApartmentFront frontA = ApartmentFront.For(a, planA);
            foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.North, WindowGrid.Wall.East, WindowGrid.Wall.South, WindowGrid.Wall.West })
            {
                if (planA.IsBlankWall(wall) || frontA.Covers(wall))
                {
                    continue;   // a party wall, or a wall of balconies or a walkway (their own parapets are in the way)
                }
                TryBridge(world, a, planA, wall, list);
            }
            done = list.Count == 0 ? None : list.ToArray();
            if (Decided.Count >= CacheLimit)
            {
                Decided.Clear();
            }
            Decided[key] = done;
            return done;
        }

        /// <summary>A wall's start in the building's frame (where the builder's column 0 begins), the direction it runs, its outward normal and its length.</summary>
        public static void WallFrame(BuildingSite site, WindowGrid.Wall wall, out Vector2 start, out Vector2 along, out Vector2 outward, out float length)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            switch (wall)
            {
                case WindowGrid.Wall.North: start = new Vector2(-hw, hd); along = Vector2.right; outward = Vector2.up; length = site.Params.Width; break;
                case WindowGrid.Wall.South: start = new Vector2(-hw, -hd); along = Vector2.right; outward = Vector2.down; length = site.Params.Width; break;
                case WindowGrid.Wall.East: start = new Vector2(hw, -hd); along = Vector2.up; outward = Vector2.right; length = site.Params.Depth; break;
                default: start = new Vector2(-hw, -hd); along = Vector2.up; outward = Vector2.left; length = site.Params.Depth; break;
            }
        }

        private static Vector2 ToWorld(BuildingSite site, Vector2 local)
        {
            site.LocalToWorld(local.x, local.y, out double wx, out double wz);
            return new Vector2((float)wx, (float)wz);
        }

        private static Vector2 ToLocal(BuildingSite site, Vector2 world)
        {
            site.WorldToLocal(world.x, world.y, out double lx, out double lz);
            return new Vector2((float)lx, (float)lz);
        }

        /// <summary>A direction in a building's frame, in the world.</summary>
        private static Vector2 DirectionToWorld(BuildingSite site, Vector2 local)
        {
            return (ToWorld(site, local) - ToWorld(site, Vector2.zero)).normalized;
        }

        /// <summary>One end of a bridge: the building, its wall, the doorway's column and place along the wall, and the wall's frame in the world.</summary>
        private struct End
        {
            public BuildingSite Site;
            public WindowGrid.Wall Wall;
            public int Column;
            public float U;
            public Vector2 Point, Out, Along;
        }

        private static void TryBridge(CityWorld world, BuildingSite a, in WindowPlan planA, WindowGrid.Wall wallA, List<Footbridge> into)
        {
            WallFrame(a, wallA, out Vector2 startA, out Vector2 alongA, out Vector2 outA, out float lengthA);
            Vector2 midA = startA + alongA * (lengthA * 0.5f);
            Vector2 midWorld = ToWorld(a, midA);
            Vector2 outWorld = DirectionToWorld(a, outA);

            // The building facing this wall: the first one a line out from the wall's middle meets. A low building or a set piece in the way means no bridge.
            BuildingSite b = BuildingSite.None;
            float gap = 0.0f;
            for (float d = 1.5f; d <= MaxSpan; d += 1.5f)
            {
                Vector2 p = midWorld + outWorld * d;
                if (!world.TryBuildingAt(p.x, p.y, out BuildingSite hit) || hit.Id == a.Id)
                {
                    continue;
                }
                if (!Eligible(hit))
                {
                    return;
                }
                b = hit;
                gap = d;
                break;
            }
            if (!b.Exists || gap < MinSpan || b.Id < a.Id)
            {
                return;   // only the lower-numbered building of a pair decides its bridge: the other finds it among its neighbours' (For)
            }
            // B's wall facing A: the nearest of its planes to the point found, facing back within about 30 degrees.
            Vector2 hitLocalB = ToLocal(b, midWorld + outWorld * gap);
            WindowGrid.Wall wallB = NearestWall(b, hitLocalB);
            WallFrame(b, wallB, out Vector2 startB, out Vector2 alongB, out Vector2 outB, out float lengthB);
            Vector2 outBWorld = DirectionToWorld(b, outB);
            if (Vector2.Dot(outBWorld, outWorld) > -0.85f)
            {
                return;
            }
            WindowPlan planB = WindowPlan.For(world, b);
            if (planB.IsBlankWall(wallB) || ApartmentFront.For(b, planB).Covers(wallB))
            {
                return;
            }

            // The pair's dice; a pair of short blocks is bridged far less often than a pair of towers.
            ulong pair = unchecked((ulong)(uint)a.Seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)b.Seed * 0xC2B2AE3D27D4EB4FUL);
            double D(int i) => WorldHash.Unit01(i, (int)wallA, Salt, pair);
            int floorsA = BuildingInterior.FloorCountFor(a.Params.Height), floorsB = BuildingInterior.FloorCountFor(b.Params.Height);
            if (D(0) >= ShareFor(a.Zone) * TallFactor(Mathf.Min(floorsA, floorsB)))
            {
                return;
            }

            // Where along A's wall: within the stretch B's wall faces, snapped to a window column clear of the corners.
            CellLayout columnsA = planA.ColumnsFor(lengthA), columnsB = planB.ColumnsFor(lengthB);
            float bLow = float.MaxValue, bHigh = float.MinValue;
            foreach (Vector2 cornerB in new[] { startB, startB + alongB * lengthB })
            {
                Vector2 inA = ToLocal(a, ToWorld(b, cornerB));
                float u = Vector2.Dot(inA - startA, alongA);
                bLow = Mathf.Min(bLow, u);
                bHigh = Mathf.Max(bHigh, u);
            }
            float overlap0 = Mathf.Max(0.0f, bLow), overlap1 = Mathf.Min(lengthA, bHigh);
            if (overlap1 - overlap0 < 4.0f)
            {
                return;
            }
            float uA = (overlap0 + overlap1) * 0.5f + (float)(D(1) - 0.5) * (overlap1 - overlap0) * 0.5f;
            int columnA = Mathf.Clamp(Mathf.FloorToInt(uA / columnsA.Size), CornerColumns, columnsA.Count - 1 - CornerColumns);
            uA = (columnA + 0.5f) * columnsA.Size;
            if (uA < overlap0 + DeckWidth * 0.5f || uA > overlap1 - DeckWidth * 0.5f)
            {
                return;
            }

            // Straight out from A's doorway to B's wall plane: where the square crossing lands on B.
            Vector2 startWorld2 = ToWorld(a, startA + alongA * uA);
            Vector2 sLocalB = ToLocal(b, startWorld2);
            Vector2 dirLocalB = ToLocal(b, startWorld2 + outWorld) - sLocalB;
            float toward = -Vector2.Dot(dirLocalB, outB);   // the line heads against B's outward normal
            if (toward <= 1e-4f)
            {
                return;
            }
            float t = Vector2.Dot(sLocalB - startB, outB) / toward;   // to B's wall plane
            if (t < MinSpan || t > MaxSpan)
            {
                return;
            }
            float uSquare = Vector2.Dot(sLocalB + dirLocalB * t - startB, alongB);

            var endA = new End { Site = a, Wall = wallA, Column = columnA, U = uA, Point = startWorld2, Out = outWorld, Along = DirectionToWorld(a, alongA) };

            // Up to three bridges for the pair, each higher than the one before and each of its own shape.
            int bridges = 1;
            if (Mathf.Min(floorsA, floorsB) >= SecondBridgeFloors && D(3) < SecondBridgeShare) bridges = 2;
            if (bridges == 2 && Mathf.Min(floorsA, floorsB) >= ThirdBridgeFloors && D(4) < ThirdBridgeShare) bridges = 3;
            int lowestNext = LowestFloor;
            bool stairTaken = false;
            for (int k = 0; k < bridges; k++)
            {
                double E(int i) => WorldHash.Unit01(i + 16 * (k + 1), (int)wallA, Salt, pair);
                if (!TryOneBridge(world, endA, b, wallB, planB, startB, alongB, outBWorld, lengthB, columnsB, uSquare, t, lowestNext, k, E, into,
                        ref stairTaken, out int floorA))
                {
                    if (k == 0) return;
                    continue;
                }
                lowestNext = floorA + FloorsBetweenBridges;
            }
        }

        /// <summary>
        /// One bridge of a pair: its shape (falling back to straight across when the shape will not fit), B's doorway, the path in plan, the floors
        /// (weighted up), the heights, piers, rail and deck, and (the pair's first that can have one) the street stair.
        /// </summary>
        private static bool TryOneBridge(CityWorld world, End endA, BuildingSite b, WindowGrid.Wall wallB, in WindowPlan planB, Vector2 startB, Vector2 alongB,
            Vector2 outBWorld, float lengthB, CellLayout columnsB, float uSquare, float gap, int lowestFloor, int index, System.Func<int, double> dice,
            List<Footbridge> into, ref bool stairTaken, out int chosenFloorA)
        {
            chosenFloorA = -1;
            BuildingSite a = endA.Site;
            BridgeShape wanted = PickShape(a.Zone, dice(0));
            foreach (BridgeShape shape in wanted == BridgeShape.Straight ? new[] { BridgeShape.Straight } : new[] { wanted, BridgeShape.Straight })
            {
                // B's doorway: square across for a straight bridge; along B's wall by an offset for the others.
                float uB = uSquare;
                if (shape != BridgeShape.Straight)
                {
                    float least = shape == BridgeShape.Angled ? gap * Mathf.Tan(MinAngle * Mathf.Deg2Rad) : 4.0f;
                    float most = shape == BridgeShape.Angled ? gap * Mathf.Tan(MaxAngle * Mathf.Deg2Rad) : Mathf.Min(14.0f, gap * 1.2f);
                    if (most < least) continue;
                    float offset = Mathf.Lerp(least, most, (float)dice(1));
                    uB = uSquare + (dice(2) < 0.5 ? -offset : offset);
                }
                int columnB = Mathf.Clamp(Mathf.FloorToInt(uB / columnsB.Size), CornerColumns, columnsB.Count - 1 - CornerColumns);
                uB = (columnB + 0.5f) * columnsB.Size;
                Vector2 endWorld2 = ToWorld(b, startB + alongB * uB);
                float span = Vector2.Distance(endA.Point, endWorld2);
                if (span < MinSpan || span > MaxSpan) continue;
                float sideways = Mathf.Abs(Vector2.Dot(endWorld2 - endA.Point, endA.Along));
                float angle = Mathf.Atan2(sideways, Mathf.Max(0.01f, Vector2.Dot(endWorld2 - endA.Point, endA.Out))) * Mathf.Rad2Deg;
                if (shape == BridgeShape.Straight && angle > 3.0f) continue;
                if (shape == BridgeShape.Angled && (angle < MinAngle - 2.0f || angle > MaxAngle + 2.0f)) continue;
                if ((shape == BridgeShape.DogLeg || shape == BridgeShape.Curve) && sideways < 3.0f) continue;
                var endB = new End { Site = b, Wall = wallB, Column = columnB, U = uB, Point = endWorld2, Out = outBWorld, Along = DirectionToWorld(b, alongB) };

                List<Vector2> plan = PlanPath(shape, endA, endB, out int stairStretch);
                if (plan == null || Obstructed(world, a, b, plan) || NearExpressway(world, plan)) continue;

                // The floors: each of A's from the lowest allowed with B's nearest that this path can join, weighted by the deck's height.
                float length = 0.0f;
                for (int i = 0; i + 1 < plan.Count; i++) length += Vector2.Distance(plan[i], plan[i + 1]);
                float stairRoom = stairStretch >= 0 ? Vector2.Distance(plan[stairStretch], plan[stairStretch + 1]) - 2.0f * StairMargin : 0.0f;
                float stairRise = StairCapacity(stairRoom);
                if (!Floors(world, endA, endB, plan, length, stairRise, lowestFloor, dice(3), out int fa, out int fb, out float ya, out float yb))
                {
                    continue;
                }
                BridgePath path = BuildPath(world, plan, stairStretch, ya, yb, endA.Along, endB.Along, shape);
                BridgeRail rail = PickRail(a.Zone, dice(4));
                BridgeDeck deck = rail == BridgeRail.Parapet || rail == BridgeRail.Pickets ? BridgeDeck.Concrete
                    : dice(5) < (a.Zone == ZoneKind.Industrial ? 0.75 : 0.45) ? BridgeDeck.Steel : BridgeDeck.Concrete;

                // The street stair: from the pair's lowest bridge, low enough, leaving A's wall square (not on the slant), down along A's wall
                // (the front only where the street storey is plain: no shop front and its canopies, no entrance stairs; and clear of the door).
                StreetStair stair = null;
                if (!stairTaken && index == 0 && shape != BridgeShape.Angled && dice(6) < StreetStairShare
                    && (endA.Wall != WindowGrid.Wall.North || PlainFront(world, a)))
                {
                    stair = TryStreetStair(world, endA, path, dice(7));
                    stairTaken = stair != null;
                }
                // A pier standing through a lower bridge of the pair, or the stair, is left out.
                path = WithoutPiersThrough(path, into, a.Id, b.Id, stair);
                into.Add(new Footbridge(a.Id, b.Id, endA.Wall, wallB, fa, fb, endA.Column, columnB, endA.U, uB,
                    new Vector3(endA.Point.x, ya, endA.Point.y), new Vector3(endWorld2.x, yb, endWorld2.y), DeckWidth, into.Count, shape, rail, deck, path, stair));
                chosenFloorA = fa;
                return true;
            }
            return false;
        }

        /// <summary>A pair's bridge shape by district: straight across most often in the old town and the homes, every kind downtown.</summary>
        private static BridgeShape PickShape(ZoneKind zone, double r)
        {
            // Straight, angled, dog-leg, curve.
            float[] weights;
            switch (zone)
            {
                case ZoneKind.Core: weights = new[] { 0.30f, 0.22f, 0.28f, 0.20f }; break;
                case ZoneKind.Urban: weights = new[] { 0.35f, 0.20f, 0.30f, 0.15f }; break;
                case ZoneKind.Industrial: weights = new[] { 0.40f, 0.20f, 0.35f, 0.05f }; break;
                case ZoneKind.OldTown: weights = new[] { 0.45f, 0.15f, 0.30f, 0.10f }; break;
                default: weights = new[] { 0.45f, 0.20f, 0.25f, 0.10f }; break;
            }
            float acc = 0.0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r < acc) return (BridgeShape)i;
            }
            return BridgeShape.Straight;
        }

        /// <summary>A bridge's rails by district.</summary>
        private static BridgeRail PickRail(ZoneKind zone, double r)
        {
            // Parapet, rails, pickets, mesh, pipe.
            float[] weights;
            switch (zone)
            {
                case ZoneKind.Core: weights = new[] { 0.20f, 0.30f, 0.20f, 0.25f, 0.05f }; break;
                case ZoneKind.Urban: weights = new[] { 0.30f, 0.30f, 0.15f, 0.20f, 0.05f }; break;
                case ZoneKind.Residential: weights = new[] { 0.30f, 0.25f, 0.40f, 0.05f, 0.00f }; break;
                case ZoneKind.OldTown: weights = new[] { 0.45f, 0.05f, 0.40f, 0.00f, 0.10f }; break;
                case ZoneKind.Industrial: weights = new[] { 0.05f, 0.20f, 0.00f, 0.35f, 0.40f }; break;
                default: weights = new[] { 0.30f, 0.30f, 0.20f, 0.10f, 0.10f }; break;
            }
            float acc = 0.0f;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += weights[i];
                if (r < acc) return (BridgeRail)i;
            }
            return BridgeRail.Parapet;
        }

        /// <summary>
        /// The path in plan from A's doorway to B's (world x, z): straight or angled, two points; a dog-leg, out square to a landing by each wall
        /// and across between them; a curve, a cubic square to each wall sampled every <see cref="CurveStep"/>. The stretch a flight may go in
        /// (the straight across, a dog-leg's middle; a curve has none), or -1. Null when the shape does not fit.
        /// </summary>
        private static List<Vector2> PlanPath(BridgeShape shape, End a, End b, out int stairStretch)
        {
            stairStretch = -1;
            var plan = new List<Vector2>();
            switch (shape)
            {
                case BridgeShape.Straight:
                case BridgeShape.Angled:
                    plan.Add(a.Point);
                    plan.Add(b.Point);
                    stairStretch = 0;
                    return plan;
                case BridgeShape.DogLeg:
                {
                    Vector2 la = a.Point + a.Out * LandingOut, lb = b.Point + b.Out * LandingOut;
                    Vector2 middle = lb - la;
                    // The middle stretch has to cross on the slant (else it is a straight bridge with a kink) and leave room either side.
                    if (middle.magnitude < 4.0f || Vector2.Dot(middle.normalized, a.Out) < 0.2f) return null;
                    float slant = Mathf.Acos(Mathf.Clamp(Vector2.Dot(middle.normalized, a.Out), -1.0f, 1.0f)) * Mathf.Rad2Deg;
                    if (slant < 20.0f || slant > 70.0f) return null;
                    plan.Add(a.Point);
                    plan.Add(la);
                    plan.Add(lb);
                    plan.Add(b.Point);
                    stairStretch = 1;
                    return plan;
                }
                default:
                {
                    float gap = Mathf.Abs(Vector2.Dot(b.Point - a.Point, a.Out));
                    if (gap < 8.0f) return null;
                    Vector2 p0 = a.Point, p1 = a.Point + a.Out * (gap * 0.45f), p2 = b.Point + b.Out * (gap * 0.45f), p3 = b.Point;
                    float rough = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
                    int n = Mathf.Max(4, Mathf.CeilToInt(rough / CurveStep));
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n, u = 1.0f - t;
                        plan.Add(u * u * u * p0 + 3.0f * u * u * t * p1 + 3.0f * u * t * t * p2 + t * t * t * p3);
                    }
                    return plan;
                }
            }
        }

        /// <summary>Whether another building stands on the path (its middle or either edge), or the path runs into A or B away from its ends.</summary>
        private static bool Obstructed(CityWorld world, BuildingSite a, BuildingSite b, List<Vector2> plan)
        {
            float length = 0.0f;
            for (int i = 0; i + 1 < plan.Count; i++) length += Vector2.Distance(plan[i], plan[i + 1]);
            float s = 0.0f;
            for (int i = 0; i + 1 < plan.Count; i++)
            {
                Vector2 p = plan[i], q = plan[i + 1];
                float l = Vector2.Distance(p, q);
                if (l < 1e-4f) continue;
                Vector2 dir = (q - p) / l, side = new Vector2(-dir.y, dir.x);
                for (float d = 0.0f; d <= l; d += 1.0f)
                {
                    float along = s + d;
                    foreach (float w in new[] { 0.0f, DeckWidth * 0.5f + 0.2f, -(DeckWidth * 0.5f + 0.2f) })
                    {
                        Vector2 x = p + dir * d + side * w;
                        if (!world.TryBuildingAt(x.x, x.y, out BuildingSite other)) continue;
                        if (other.Id != a.Id && other.Id != b.Id) return true;
                        // Into A or B itself away from its doorway: a curve cutting a corner.
                        if (other.Id == a.Id && along > 0.6f || other.Id == b.Id && along < length - 0.6f) return true;
                    }
                }
                s += l;
            }
            return false;
        }

        /// <summary>Whether an expressway's deck is anywhere near the path.</summary>
        private static bool NearExpressway(CityWorld world, List<Vector2> plan)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            foreach (Vector2 p in plan)
            {
                x0 = Mathf.Min(x0, p.x);
                z0 = Mathf.Min(z0, p.y);
                x1 = Mathf.Max(x1, p.x);
                z1 = Mathf.Max(z1, p.y);
            }
            var decks = new List<(int path, int seg)>();
            world.Plan.Data.Expressways.SegmentsNear(x0 - 6.0f, z0 - 6.0f, x1 + 6.0f, z1 + 6.0f, decks);
            return decks.Count > 0;
        }

        /// <summary>How many steps a flight (or flights with landings between) of this rise has, at most <see cref="MaxRiser"/> each.</summary>
        public static int StepsFor(float rise) => Mathf.Max(1, Mathf.CeilToInt(rise / MaxRiser - 1e-4f));

        /// <summary>How long in plan the flights for so many steps run, with a landing every <see cref="FlightMax"/> steps.</summary>
        public static float RunFor(int steps) => steps * Tread + (Mathf.CeilToInt(steps / (float)FlightMax) - 1) * FlightLanding;

        /// <summary>The most a bridge's flights can climb in so much room, metres.</summary>
        private static float StairCapacity(float room)
        {
            if (room <= Tread) return 0.0f;
            int steps = 0;
            while (RunFor(steps + 1) <= room && (steps + 1) * MaxRiser <= MaxStairRise) steps++;
            return steps * MaxRiser;
        }

        /// <summary>
        /// The floors the bridge joins: each of A's from <paramref name="lowestFloor"/> with B's nearest whose rise this path can take (on the deck,
        /// or by its flights), clear of the ground and free at both walls; one drawn weighted by its clearance over the street.
        /// </summary>
        private static bool Floors(CityWorld world, End a, End b, List<Vector2> plan, float length, float stairRise, int lowestFloor, double r,
            out int floorA, out int floorB, out float ya, out float yb)
        {
            floorA = floorB = -1;
            ya = yb = 0.0f;
            int floorsA = BuildingInterior.FloorCountFor(a.Site.Params.Height), floorsB = BuildingInterior.FloorCountFor(b.Site.Params.Height);
            float storeyA = BuildingInterior.StoreyHeight(a.Site.Params.Height), storeyB = BuildingInterior.StoreyHeight(b.Site.Params.Height);
            CellLayout columnsA = WindowPlan.For(world, a.Site).ColumnsFor(WallLength(a.Site, a.Wall));
            CellLayout columnsB = WindowPlan.For(world, b.Site).ColumnsFor(WallLength(b.Site, b.Wall));
            float groundTop = HighestGround(world, plan);
            var options = new List<(int fa, int fb, float ya, float yb, double weight)>();
            double total = 0.0;
            for (int fa = Mathf.Max(LowestFloor, lowestFloor); fa < floorsA; fa++)
            {
                float y0 = a.Site.BaseHeight + fa * storeyA;
                int best = -1;
                float bestDy = float.MaxValue;
                for (int fb = LowestFloor; fb < floorsB; fb++)
                {
                    float dy = Mathf.Abs(b.Site.BaseHeight + fb * storeyB - y0);
                    bool fits = dy <= MaxRise && dy <= MaxGrade * length || dy > MaxRise && dy <= stairRise;
                    if (fits && dy < bestDy)
                    {
                        bestDy = dy;
                        best = fb;
                    }
                }
                if (best < 0) continue;
                float y1 = b.Site.BaseHeight + best * storeyB;
                float clearance = Mathf.Min(y0, y1) - DeckThickness - groundTop;
                if (clearance < GroundClearance) continue;
                if (!WallFree(a.Site, a.Wall, a.U, fa * storeyA) || !WallFree(b.Site, b.Wall, b.U, best * storeyB)) continue;
                if (!FloorFreeInside(a.Site, a.Wall, a.Column, columnsA, fa) || !FloorFreeInside(b.Site, b.Wall, b.Column, columnsB, best)) continue;
                double w = HeightWeight(clearance);
                options.Add((fa, best, y0, y1, w));
                total += w;
            }
            if (options.Count == 0) return false;
            double pick = r * total;
            foreach (var o in options)
            {
                pick -= o.weight;
                if (pick > 0.0) continue;
                floorA = o.fa;
                floorB = o.fb;
                ya = o.ya;
                yb = o.yb;
                return true;
            }
            var last = options[options.Count - 1];
            floorA = last.fa;
            floorB = last.fb;
            ya = last.ya;
            yb = last.yb;
            return true;
        }

        private static float WallLength(BuildingSite site, WindowGrid.Wall wall) =>
            wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;

        /// <summary>The highest street under the path (the ground and the road lifted on it, with a margin), sampled every metre.</summary>
        private static float HighestGround(CityWorld world, List<Vector2> plan)
        {
            float top = float.MinValue;
            for (int i = 0; i + 1 < plan.Count; i++)
            {
                Vector2 p = plan[i], q = plan[i + 1];
                float l = Vector2.Distance(p, q);
                int n = Mathf.Max(1, Mathf.CeilToInt(l));
                for (int k = 0; k <= n; k++)
                {
                    Vector2 x = Vector2.Lerp(p, q, k / (float)n);
                    top = Mathf.Max(top, (float)world.Plan.GroundHeight(x.x, x.y) + (float)Plan.PlanGeometry.RoadLift + 0.3f);
                }
            }
            return top;
        }

        /// <summary>
        /// The path with heights: a deck from floor to floor sloping evenly; with flights (the floors more than <see cref="MaxRise"/> apart), the deck
        /// level at each floor's height and the flights in the middle of the stair stretch, a landing every <see cref="FlightMax"/> steps. Piers under a
        /// dog-leg's landings, and in a long straight or curved span's middle, where the ground under them is not a road.
        /// </summary>
        private static BridgePath BuildPath(CityWorld world, List<Vector2> plan, int stairStretch, float ya, float yb, Vector2 tangentA, Vector2 tangentB,
            BridgeShape shape)
        {
            var points = new List<Vector3>();
            var risers = new List<int>();
            var turns = new List<bool>();
            float rise = yb - ya;
            bool stairs = Mathf.Abs(rise) > MaxRise && stairStretch >= 0;
            if (!stairs)
            {
                float length = 0.0f;
                for (int i = 0; i + 1 < plan.Count; i++) length += Vector2.Distance(plan[i], plan[i + 1]);
                float s = 0.0f;
                for (int i = 0; i < plan.Count; i++)
                {
                    if (i > 0) s += Vector2.Distance(plan[i - 1], plan[i]);
                    points.Add(new Vector3(plan[i].x, ya + rise * (length > 1e-4f ? s / length : 0.0f), plan[i].y));
                    turns.Add(shape == BridgeShape.DogLeg && i > 0 && i + 1 < plan.Count);
                    if (i + 1 < plan.Count) risers.Add(0);
                }
            }
            else
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    float y = i <= stairStretch ? ya : yb;
                    points.Add(new Vector3(plan[i].x, y, plan[i].y));
                    turns.Add(shape == BridgeShape.DogLeg && i > 0 && i + 1 < plan.Count);
                    if (i + 1 >= plan.Count) break;
                    if (i != stairStretch)
                    {
                        risers.Add(0);
                        continue;
                    }
                    // The stair stretch: level, the flights (a landing between each), level.
                    Vector2 p = plan[i], q = plan[i + 1];
                    float l = Vector2.Distance(p, q);
                    Vector2 dir = (q - p) / l;
                    int steps = StepsFor(Mathf.Abs(rise));
                    float run = RunFor(steps);
                    float margin = (l - run) * 0.5f;
                    float step = rise / steps;
                    float y0 = ya;
                    float at = margin;
                    points.Add(new Vector3(p.x + dir.x * at, y0, p.y + dir.y * at));
                    turns.Add(false);
                    risers.Add(0);
                    int left = steps;
                    while (left > 0)
                    {
                        int n = Mathf.Min(FlightMax, left);
                        at += n * Tread;
                        y0 += step * n;
                        points.Add(new Vector3(p.x + dir.x * at, y0, p.y + dir.y * at));
                        turns.Add(false);
                        risers.Add(n);
                        left -= n;
                        if (left > 0)
                        {
                            at += FlightLanding;
                            points.Add(new Vector3(p.x + dir.x * at, y0, p.y + dir.y * at));
                            turns.Add(false);
                            risers.Add(0);
                        }
                    }
                    risers.Add(0);   // on, level, to the stretch's far end (the next plan point)
                }
            }

            // Piers.
            var piers = new List<Vector3>();
            for (int i = 1; i + 1 < points.Count; i++)
            {
                if (turns[i]) TryPier(world, points[i], piers);
            }
            float total = 0.0f;
            for (int i = 0; i + 1 < points.Count; i++) total += BridgePath.Flat(points[i + 1] - points[i]).magnitude;
            if (shape != BridgeShape.DogLeg && total > LongSpan)
            {
                var path0 = new BridgePath(points.ToArray(), risers.ToArray(), turns.ToArray(), tangentA, tangentB, new Vector3[0]);
                Vector2 middle = path0.PlanAt(total * 0.5f, out _);
                TryPier(world, new Vector3(middle.x, path0.HeightAt(total * 0.5f), middle.y), piers);
            }
            return new BridgePath(points.ToArray(), risers.ToArray(), turns.ToArray(), tangentA, tangentB, piers.ToArray());
        }

        /// <summary>
        /// The path without the piers that would stand through a lower bridge of the same pair (decided before it: the pair's bridges go up) or
        /// through its street stair: a pier within the other's deck (its half width and a margin) under the other's height.
        /// </summary>
        private static BridgePath WithoutPiersThrough(BridgePath path, List<Footbridge> decided, int siteA, int siteB, StreetStair own)
        {
            if (path.Piers.Length == 0) return path;
            var kept = new List<Vector3>(path.Piers.Length);
            foreach (Vector3 pier in path.Piers)
            {
                bool through = false;
                var at = new Vector2(pier.x, pier.z);
                if (own != null) through |= Near(own.Points, at, own.Width * 0.5f + PierSize + 1.0f, pier.y);   // its own stair (a tower is two lanes wide)
                foreach (Footbridge other in decided)
                {
                    if (other.SiteA != siteA || other.SiteB != siteB) continue;
                    through |= Near(other.Path.Points, at, other.Width * 0.5f + PierSize, pier.y);
                    if (other.Stair != null) through |= Near(other.Stair.Points, at, other.Stair.Width * 0.5f + PierSize, pier.y);
                }
                if (!through) kept.Add(pier);
            }
            return kept.Count == path.Piers.Length ? path
                : new BridgePath(path.Points, path.Risers, path.Turns, path.TangentA, path.TangentB, kept.ToArray());
        }

        /// <summary>Whether a point in plan is within <paramref name="reach"/> of a walking line somewhere lower than <paramref name="under"/>.</summary>
        private static bool Near(Vector3[] line, Vector2 at, float reach, float under)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vector2 p = BridgePath.Flat(line[i]), q = BridgePath.Flat(line[i + 1]);
                Vector2 d = q - p;
                float t = d.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(at - p, d) / d.sqrMagnitude) : 0.0f;
                if (Vector2.Distance(at, p + d * t) <= reach && Mathf.Min(line[i].y, line[i + 1].y) < under) return true;
            }
            return false;
        }

        /// <summary>A pier under a point of the deck, unless the ground there is a road's carriageway or the deck is too high to stand one.</summary>
        private static void TryPier(CityWorld world, Vector3 deckTop, List<Vector3> piers)
        {
            float ground = (float)world.Plan.GroundHeight(deckTop.x, deckTop.z);
            if (deckTop.y - ground > MaxPierHeight || OnACarriageway(world, deckTop.x, deckTop.z, PierSize)) return;
            piers.Add(new Vector3(deckTop.x, deckTop.y - DeckThickness, deckTop.z));
        }

        /// <summary>Whether a point (or anything within <paramref name="margin"/> of it) is on a road's carriageway or a junction.</summary>
        public static bool OnACarriageway(CityWorld world, double x, double z, double margin)
        {
            Plan.PlanWorld plan = world.Plan;
            var p = new Plan.Vec2(x, z);
            int cx = Plan.PlanWorld.ChunkOf(x), cy = Plan.PlanWorld.ChunkOf(z);
            foreach (int e in plan.EdgesIn(cx, cy))
            {
                Plan.EdgeGeometry g = plan.Geometry.Edges[e];
                if (g != null && Plan.PlanMath.DistanceToSegment(p, g.A, g.B, out _) <= g.HalfWidth + margin) return true;
            }
            foreach (int n in plan.JunctionsIn(cx, cy))
            {
                Plan.JunctionGeometry j = plan.Geometry.Junctions[n];
                if (j?.Plate != null && Plan.PlanMath.PointInPolygon(p, j.Plate)) return true;
            }
            return false;
        }

        /// <summary>
        /// A stair from the deck's side where it leaves A's wall down to the paving, along that wall: the way along the wall with more room, a
        /// straight run of flights (a landing between each) hugging the wall, ending on the paving with a short landing. None where the deck is
        /// higher than <see cref="MaxStreetStairRise"/> over the paving at the foot, the run will not fit along the wall clear of its corners, the
        /// ground under it is a road or another building, or a bridge from another building lands on that stretch of the wall.
        /// </summary>
        private static StreetStair TryStreetStair(CityWorld world, End a, BridgePath path, double r)
        {
            // Toward the end of the wall with more room first (the dice when it is even), then the other way.
            float length = WallLength(a.Site, a.Wall);
            float roomUp = length - a.U, roomDown = a.U;
            int first = roomUp > roomDown + 1.0f ? 1 : roomDown > roomUp + 1.0f ? -1 : (r < 0.5 ? 1 : -1);
            return TryStreetStair(world, a, path, first) ?? TryStreetStair(world, a, path, -first);
        }

        /// <summary>A building whose street storey is plain along its front: no shop front (its glass and canopies), no stairs up to its door.</summary>
        private static bool PlainFront(CityWorld world, BuildingSite site)
        {
            return !WindowPlan.For(world, site).Storefront && !BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs;
        }

        /// <summary>The street stair one way along the wall (+1 toward its far end, -1 toward its start), or null.</summary>
        private static StreetStair TryStreetStair(CityWorld world, End a, BridgePath path, int way)
        {
            float top = path.HeightAt(StreetStairOffset);
            float length = WallLength(a.Site, a.Wall);
            float startU = a.U + way * DeckWidth * 0.5f;   // the deck's side, where the stair leaves it
            // A straight run down the wall where it fits and the deck is low enough; else a tower zig-zagging on two lanes against the wall.
            StreetStair stair = StraightStair(world, a, top, startU, way, length) ?? SwitchbackStair(world, a, top, startU, way, length);
            if (stair == null) return null;
            // The wall's foot free of other buildings' bridges landing on that stretch.
            foreach (Footbridge incoming in IncomingOn(world, a.Site, a.Wall))
            {
                if (incoming.UB > stair.U0 - DeckWidth && incoming.UB < stair.U1 + DeckWidth && incoming.EndWorld.y < top + 3.0f) return null;
            }
            return stair;
        }

        /// <summary>A point by a wall: <paramref name="d"/> metres along it the stair's way from where the stair starts, <paramref name="o"/> out from its face.</summary>
        private static Vector2 ByWall(in End a, float startU, int way, float d, float o)
        {
            return a.Point + a.Along * (startU + way * d - a.U) + a.Out * o;
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>The paving's height at a point.</summary>
        private static float PavingAt(CityWorld world, Vector2 p) => (float)world.Plan.GroundHeight(p.x, p.y) + (float)Plan.PlanBuildings.PavingRise;

        /// <summary>
        /// Whether a stair's footprint by the wall, <paramref name="d0"/>..<paramref name="d1"/> along it and <paramref name="o0"/>..<paramref name="o1"/>
        /// out, stays on the wall's length clear of its corners, clear of the front's entrance, off the roads and out of other buildings.
        /// </summary>
        private static bool FootprintClear(CityWorld world, in End a, float startU, int way, float length, float d0, float d1, float o0, float o1, out float u0, out float u1)
        {
            float ua = startU + way * d0, ub = startU + way * d1;
            u0 = Mathf.Min(ua, ub) - 0.2f;
            u1 = Mathf.Max(ua, ub) + 0.2f;
            const float cornerMargin = 1.0f;
            if (u0 < cornerMargin || u1 > length - cornerMargin) return false;
            if (a.Wall == WindowGrid.Wall.North)
            {
                // Along the front: clear of the entrance (its door, its canopy, its step) by a good stride either side.
                float door = length * 0.5f, clear = BuildingInterior.EntranceWidth(a.Site) * 0.5f + 2.5f;
                if (u0 < door + clear && u1 > door - clear) return false;
            }
            for (float d = d0; d <= d1 + 1e-3f; d += 0.8f)
            {
                for (float o = o0; o <= o1 + 1e-3f; o += Mathf.Max(0.3f, (o1 - o0) / 3.0f))
                {
                    Vector2 x = ByWall(a, startU, way, d, o);
                    if (OnACarriageway(world, x.x, x.y, 0.2)) return false;
                    if (world.TryBuildingAt(x.x, x.y, out BuildingSite other) && other.Id != a.Site.Id) return false;
                }
            }
            return true;
        }

        /// <summary>Whether every step of a flight from <paramref name="from"/> (its top) stands over the paving under it (a bump between top and foot).</summary>
        private static bool StepsClearTheGround(CityWorld world, Vector3 from, Vector3 to, int risers)
        {
            for (int k = 0; k < risers; k++)
            {
                Vector3 x = Vector3.Lerp(from, to, (k + 0.5f) / risers);
                float stepTop = from.y + (to.y - from.y) * (k + 1) / risers;
                if (stepTop < PavingAt(world, new Vector2(x.x, x.z)) - 0.05f) return false;
            }
            return true;
        }

        /// <summary>Whether, looking along <paramref name="direction"/> (world x, z), the side away from the wall is the left.</summary>
        private static bool OutIsLeft(Vector2 direction, Vector2 outward) => Vector2.Dot(new Vector2(-direction.y, direction.x), outward) > 0.0f;

        /// <summary>
        /// The stair as one straight run down the wall: flights hugging it, a landing between each, a short landing on the paving at the foot; a
        /// rail along its open side. None when the deck is higher than <see cref="MaxStreetStairRise"/> over the paving at the foot, or it will not fit.
        /// </summary>
        private static StreetStair StraightStair(CityWorld world, End a, float top, float startU, int way, float length)
        {
            float o = StreetStairOffset;
            float foot = 0.0f, run = 0.0f;
            int steps = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                foot = PavingAt(world, ByWall(a, startU, way, run + StreetStairFoot, o));
                float rise = top - foot;
                if (rise < 1.0f || rise > MaxStreetStairRise) return null;
                steps = StepsFor(rise);
                run = RunFor(steps);
            }
            float total = run + StreetStairFoot;
            if (!FootprintClear(world, a, startU, way, length, 0.0f, total, 0.15f, o * 2.0f - 0.1f, out float u0, out float u1)) return null;

            Vector2 along = a.Along * way;
            bool outLeft = OutIsLeft(along, a.Out);
            var flights = new List<StairFlight>();
            var landings = new List<StairLanding>();
            var points = new List<Vector3>();
            var risers = new List<int>();
            float riser = (top - foot) / steps;
            float y = top, d = 0.0f;
            int left = steps;
            points.Add(At(ByWall(a, startU, way, 0.0f, o), y));
            while (left > 0)
            {
                int n = Mathf.Min(FlightMax, left);
                Vector3 from = At(ByWall(a, startU, way, d, o), y);
                d += n * Tread;
                y -= riser * n;
                Vector3 to = At(ByWall(a, startU, way, d, o), y);
                if (!StepsClearTheGround(world, from, to, n)) return null;
                flights.Add(new StairFlight(from, to, n, outLeft, !outLeft));
                points.Add(to);
                risers.Add(n);
                left -= n;
                if (left > 0)
                {
                    landings.Add(new StairLanding(At(ByWall(a, startU, way, d + FlightLanding * 0.5f, o), y), along, a.Out, FlightLanding * 0.5f,
                        StreetStairWidth * 0.5f, false, false, true));
                    d += FlightLanding;
                    points.Add(At(ByWall(a, startU, way, d, o), y));
                    risers.Add(0);
                }
            }
            landings.Add(new StairLanding(At(ByWall(a, startU, way, d + StreetStairFoot * 0.5f, o), y), along, a.Out, StreetStairFoot * 0.5f,
                StreetStairWidth * 0.5f, false, false, false));
            d += StreetStairFoot;
            points.Add(At(ByWall(a, startU, way, d, o), y));
            risers.Add(0);
            return new StreetStair(a.Site.Id, a.Wall, points.ToArray(), risers.ToArray(), flights.ToArray(), landings.ToArray(), false, a.Out, along,
                u0, u1, foot, top, StreetStairWidth);
        }

        /// <summary>
        /// The stair as a tower against the wall: flights doubling back on two lanes (the inner one against the wall running the stair's way, the
        /// outer one running back), a landing across both lanes at each end, a short landing on the paving at the foot; rails between the lanes, along
        /// the outer side and round the landings' open ends (Chongqing's and the reference images' zig-zag stairs). Up to <see cref="MaxTowerRise"/>.
        /// </summary>
        private static StreetStair SwitchbackStair(CityWorld world, End a, float top, float startU, int way, float length)
        {
            float o1 = StreetStairOffset, o2 = o1 + StreetStairWidth + TowerLaneGap;
            float half = StreetStairWidth * 0.5f;
            float full = FlightMax * Tread;   // every flight but the last runs this long
            float foot = PavingAt(world, ByWall(a, startU, way, full * 0.5f, (o1 + o2) * 0.5f));
            int steps = 0, flightsCount = 0;
            float lastD = 0.0f, lastO = o1;
            for (int pass = 0; pass < 4; pass++)
            {
                float rise = top - foot;
                if (rise <= MaxStreetStairRise * 0.25f || rise > MaxTowerRise) return null;
                steps = StepsFor(rise);
                flightsCount = Mathf.CeilToInt(steps / (float)FlightMax);
                int lastN = steps - (flightsCount - 1) * FlightMax;
                bool lastInner = (flightsCount - 1) % 2 == 0;
                lastD = lastInner ? lastN * Tread + StreetStairFoot : full - lastN * Tread - StreetStairFoot;
                lastO = lastInner ? o1 : o2;
                foot = PavingAt(world, ByWall(a, startU, way, lastD, lastO));
            }
            // The last pass moved the foot onto new paving after the steps were counted: counted again for the rise to it, or the steps are
            // a little over MaxRiser where it is lower (a 0.226 m riser on seed 19077151).
            if (top - foot > MaxTowerRise) return null;
            steps = Mathf.Max(steps, StepsFor(top - foot));
            flightsCount = Mathf.CeilToInt(steps / (float)FlightMax);
            if (flightsCount < 2) return null;   // one flight is a straight run
            float d0 = -FlightLanding, d1 = full + FlightLanding;
            if (!FootprintClear(world, a, startU, way, length, d0, d1, 0.15f, o2 + half - 0.1f, out float u0, out float u1)) return null;

            Vector2 along = a.Along * way;
            var flights = new List<StairFlight>();
            var landings = new List<StairLanding>();
            var points = new List<Vector3>();
            var risers = new List<int>();
            float riser = (top - foot) / steps;
            float y = top;
            int left = steps;
            points.Add(At(ByWall(a, startU, way, 0.0f, o1), y));
            for (int k = 0; k < flightsCount; k++)
            {
                int n = Mathf.Min(FlightMax, left);
                bool inner = k % 2 == 0;
                float o = inner ? o1 : o2;
                float dFrom = inner ? 0.0f : full;
                float dTo = inner ? n * Tread : full - n * Tread;
                Vector3 from = At(ByWall(a, startU, way, dFrom, o), y);
                y -= riser * n;
                Vector3 to = At(ByWall(a, startU, way, dTo, o), y);
                if (!StepsClearTheGround(world, from, to, n)) return null;
                // Inner flights: the rail on their outer side (between the lanes); outer flights: on their outer side (away from the wall).
                Vector2 dir = inner ? along : -along;
                bool outLeft = OutIsLeft(dir, a.Out);
                flights.Add(new StairFlight(from, to, n, outLeft, !outLeft));
                points.Add(to);
                risers.Add(n);
                left -= n;
                if (k + 1 < flightsCount)
                {
                    // The landing across both lanes at this end, a rail round its open end and its outer side; the walking line crosses it.
                    float dEnd = inner ? full + FlightLanding * 0.5f : -FlightLanding * 0.5f;
                    float dOver = inner ? full + FlightLanding * 0.5f : -FlightLanding * 0.5f;
                    landings.Add(new StairLanding(At(ByWall(a, startU, way, dEnd, (o1 + o2) * 0.5f), y), along, a.Out, FlightLanding * 0.5f,
                        (o2 - o1) * 0.5f + half, inner, !inner, true));
                    points.Add(At(ByWall(a, startU, way, dOver, o), y));
                    risers.Add(0);
                    points.Add(At(ByWall(a, startU, way, dOver, inner ? o2 : o1), y));
                    risers.Add(0);
                    points.Add(At(ByWall(a, startU, way, inner ? full : 0.0f, inner ? o2 : o1), y));
                    risers.Add(0);
                }
            }
            // The foot: on along the last flight's lane onto the paving.
            Vector3 lastTo = points[points.Count - 1];
            bool lastWasInner = (flightsCount - 1) % 2 == 0;
            float dLast = lastWasInner ? (steps - (flightsCount - 1) * FlightMax) * Tread : full - (steps - (flightsCount - 1) * FlightMax) * Tread;
            float dFoot = dLast + (lastWasInner ? StreetStairFoot : -StreetStairFoot);
            float oLast = lastWasInner ? o1 : o2;
            landings.Add(new StairLanding(At(ByWall(a, startU, way, (dLast + dFoot) * 0.5f, oLast), lastTo.y), along, a.Out, StreetStairFoot * 0.5f, half,
                false, false, false));
            points.Add(At(ByWall(a, startU, way, dFoot, oLast), lastTo.y));
            risers.Add(0);
            return new StreetStair(a.Site.Id, a.Wall, points.ToArray(), risers.ToArray(), flights.ToArray(), landings.ToArray(), true, a.Out, along,
                u0, u1, foot, top, StreetStairWidth);
        }


        /// <summary>The bridges lower-numbered neighbours decided that land on a wall of this building (worked out without this building's own).</summary>
        private static List<Footbridge> IncomingOn(CityWorld world, BuildingSite site, WindowGrid.Wall wall)
        {
            var list = new List<Footbridge>();
            AddIncoming(world, site, list);
            list.RemoveAll(x => x.WallB != wall);
            return list;
        }

        /// <summary>The wall of a building nearest a point in its frame.</summary>
        private static WindowGrid.Wall NearestWall(BuildingSite site, Vector2 local)
        {
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            float n = Mathf.Abs(local.y - hd), s = Mathf.Abs(local.y + hd), e = Mathf.Abs(local.x - hw), w = Mathf.Abs(local.x + hw);
            float best = n;
            WindowGrid.Wall wall = WindowGrid.Wall.North;
            if (s < best) { best = s; wall = WindowGrid.Wall.South; }
            if (e < best) { best = e; wall = WindowGrid.Wall.East; }
            if (w < best) { wall = WindowGrid.Wall.West; }
            return wall;
        }

        /// <summary>No other block of the building stands against the wall where the doorway is, at that height.</summary>
        private static bool WallFree(BuildingSite site, WindowGrid.Wall wall, float u, float y)
        {
            return site.ContactTop(wall, u - DeckWidth * 0.5f - 0.5f, u + DeckWidth * 0.5f + 0.5f) <= y - 0.5f;
        }

        /// <summary>The floor just inside the doorway is clear of the stairs, the opening they come up through and the corner room's walls.</summary>
        private static bool FloorFreeInside(BuildingSite site, WindowGrid.Wall wall, int column, CellLayout columns, int floor)
        {
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            if (floor >= stairs.FloorCount)
            {
                return false;
            }
            float halfW = site.Params.Width * 0.5f, halfD = site.Params.Depth * 0.5f;
            StairPlan leaving = stairs.Floors[floor];
            StairPlan arriving = floor == 0 ? stairs.Basement : stairs.Floors[floor - 1];
            List<Rect> solids = BuildingStairs.FloorSolids(leaving, arriving, floor, halfW, halfD);
            float t = BuildingInterior.WallThickness, clear = BalconyDoors.ClearInside;
            float c0 = column * columns.Size, c1 = c0 + columns.Size;
            Rect zone;
            switch (wall)
            {
                case WindowGrid.Wall.North: zone = Rect.MinMaxRect(-halfW + c0, halfD - t - clear, -halfW + c1, halfD - t); break;
                case WindowGrid.Wall.South: zone = Rect.MinMaxRect(-halfW + c0, -halfD + t, -halfW + c1, -halfD + t + clear); break;
                case WindowGrid.Wall.East: zone = Rect.MinMaxRect(halfW - t - clear, -halfD + c0, halfW - t, -halfD + c1); break;
                default: zone = Rect.MinMaxRect(-halfW + t, -halfD + c0, -halfW + t + clear, -halfD + c1); break;
            }
            for (int i = 0; i < solids.Count; i++)
            {
                if (solids[i].Overlaps(zone))
                {
                    return false;
                }
            }
            return true;
        }

        // ---- the doors ----

        /// <summary>
        /// A building's bridge doorways as balcony doors (BalconyDoor, on the bridge's wall, unit <see cref="DoorUnitBase"/> plus the bridge's
        /// place in <paramref name="bridges"/>): a pair of glazed doors in a doorway one column wide, opening into the floor the bridge meets.
        /// </summary>
        public static void DoorsOf(BuildingSite site, in WindowPlan plan, Footbridge[] bridges, List<BalconyDoor> into)
        {
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            for (int i = 0; i < bridges.Length; i++)
            {
                Footbridge b = bridges[i];
                b.EndOn(site.Id, out WindowGrid.Wall wall, out int floor, out int column, out _);
                float length = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? site.Params.Width : site.Params.Depth;
                CellLayout columns = plan.ColumnsFor(length);
                int row = floor * BuildingInterior.StoreyHeightRows;
                float y0 = row * rows.Size;
                float height = Mathf.Min(BalconyDoors.MaxHeight, rows.Size - BalconyDoors.HeadBar);
                float width = Mathf.Min(BalconyDoors.MaxDoubleWidth, columns.Size - 2.0f * BalconyDoors.Jamb - 0.1f);
                if (width < BalconyDoors.MinSingleWidth)
                {
                    continue;
                }
                // A door's U0 runs along its wall from the building's CENTRE (as the flats' balcony doors do); the bridge's u runs from the wall's start.
                float u0 = (column + 0.5f) * columns.Size - length * 0.5f - width * 0.5f;
                into.Add(new BalconyDoor(DoorUnitBase + i, row, column, 1, u0, u0 + width, y0, y0 + height,
                    width >= BalconyDoors.MinDoubleWidth ? BalconyDoorKind.Double : BalconyDoorKind.Single, true, wall));
            }
        }

        /// <summary>The bridge doors of a building whose bridges are known, for the wall builder and the door interactor.</summary>
        public static void DoorsOf(CityWorld world, BuildingSite site, in WindowPlan plan, List<BalconyDoor> into)
        {
            Footbridge[] bridges = For(world, site);
            if (bridges.Length > 0)
            {
                DoorsOf(site, plan, bridges, into);
            }
        }
    }
}
