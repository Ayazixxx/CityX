using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
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

        public Footbridge(int siteA, int siteB, WindowGrid.Wall wallA, WindowGrid.Wall wallB, int floorA, int floorB, int columnA, int columnB,
            float ua, float ub, Vector3 startWorld, Vector3 endWorld, float width, int index)
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
        }

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

    /// <summary>
    /// FOOTBRIDGES BETWEEN BUILDINGS (the layered city, docs/LAYERED_CITY_PLAN.md L2, 2026-10-15): Chongqing's bridges from a floor of one
    /// block across the street into a floor of the block facing it, at several heights where the blocks are tall -- the street for those
    /// floors, with the ground a courtyard under it. A concrete deck a body's width and a half with solid parapets, level with the floor at
    /// each end (sloping a little where the two floors differ), and at each end a DOOR in place of the window there (a balcony door's kind:
    /// drawn, opened and shared by BalconyDoors' machinery), so every bridge is walked onto from inside and off into the other building
    /// (owner: "make everything walkable and functionable as a pedestrian not just look good").
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

        /// <summary>How far across a street a bridge reaches, metres, and the least gap worth bridging.</summary>
        public const float MaxSpan = 34.0f;

        public const float MinSpan = 5.0f;

        /// <summary>The most the two ends' floors may differ (the deck slopes between them), and the steepest the deck gets.</summary>
        public const float MaxRise = 1.6f;

        public const float MaxGrade = 0.1f;

        /// <summary>The deck's underside clears the ground (a street under it) by at least this.</summary>
        public const float GroundClearance = 5.2f;

        /// <summary>A building needs this many floors to carry one; the lowest floor a bridge leaves from.</summary>
        public const int MinFloors = 3;

        public const int LowestFloor = 1;

        /// <summary>A second bridge, higher up, between blocks of at least this many floors, with this chance.</summary>
        public const int SecondBridgeFloors = 8;

        public const double SecondBridgeShare = 0.4;

        /// <summary>How far in from a wall's corners a doorway stays (columns), and how the doors are numbered among a building's balcony doors.</summary>
        public const int CornerColumns = 1;

        public const int DoorUnitBase = 1000;

        /// <summary>A switch for before/after renders and tests.</summary>
        public static bool On = true;

        private const ulong Salt = 0x42524447UL;   // "BRDG"

        /// <summary>The share of facing pairs that get a bridge, by the lower building's district.</summary>
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
            // The bridges a lower-numbered neighbour decided that land on this building: every building owned by the chunks round this one.
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
            bridges = list.Count == 0 ? None : list.ToArray();
            if (Cache.Count >= CacheLimit)
            {
                Cache.Clear();
            }
            Cache[key] = bridges;
            return bridges;
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

        // ---- deciding ----

        /// <summary>The bridges a building decides: at most one per wall, to the building facing that wall across the street, plus one higher up.</summary>
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

        private static void TryBridge(CityWorld world, BuildingSite a, in WindowPlan planA, WindowGrid.Wall wallA, List<Footbridge> into)
        {
            WallFrame(a, wallA, out Vector2 startA, out Vector2 alongA, out Vector2 outA, out float lengthA);
            Vector2 midA = startA + alongA * (lengthA * 0.5f);
            Vector2 midWorld = ToWorld(a, midA);
            Vector2 outWorld = (ToWorld(a, midA + outA) - midWorld).normalized;

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
            Vector2 outBWorld = (ToWorld(b, startB + outB) - ToWorld(b, startB)).normalized;
            if (Vector2.Dot(outBWorld, outWorld) > -0.85f)
            {
                return;
            }
            WindowPlan planB = WindowPlan.For(world, b);
            if (planB.IsBlankWall(wallB) || ApartmentFront.For(b, planB).Covers(wallB))
            {
                return;
            }

            // The pair's dice.
            ulong pair = unchecked((ulong)(uint)a.Seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)b.Seed * 0xC2B2AE3D27D4EB4FUL);
            double D(int i) => WorldHash.Unit01(i, (int)wallA, Salt, pair);
            if (D(0) >= ShareFor(a.Zone))
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

            // Straight out from A's doorway to B's wall; the landing on B snapped to B's column there.
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
            Vector2 landLocalB = sLocalB + dirLocalB * t;
            float uB = Vector2.Dot(landLocalB - startB, alongB);
            int columnB = Mathf.Clamp(Mathf.FloorToInt(uB / columnsB.Size), CornerColumns, columnsB.Count - 1 - CornerColumns);
            uB = (columnB + 0.5f) * columnsB.Size;
            Vector2 endWorld2 = ToWorld(b, startB + alongB * uB);
            float span = Vector2.Distance(startWorld2, endWorld2);
            if (span < MinSpan || span > MaxSpan)
            {
                return;
            }
            // Nothing else stands on the line (a third building's corner between them).
            for (float s = 2.0f; s < span - 2.0f; s += 2.0f)
            {
                Vector2 p = Vector2.Lerp(startWorld2, endWorld2, s / span);
                if (world.TryBuildingAt(p.x, p.y, out BuildingSite other) && other.Id != a.Id && other.Id != b.Id)
                {
                    return;
                }
            }
            // Not under or through an expressway's deck.
            var decks = new List<(int path, int seg)>();
            world.Plan.Data.Expressways.SegmentsNear(Mathf.Min(startWorld2.x, endWorld2.x) - 6.0f, Mathf.Min(startWorld2.y, endWorld2.y) - 6.0f,
                Mathf.Max(startWorld2.x, endWorld2.x) + 6.0f, Mathf.Max(startWorld2.y, endWorld2.y) + 6.0f, decks);
            if (decks.Count > 0)
            {
                return;
            }

            // The floors: pairs of A's and B's floors whose tops are within MaxRise of each other, the deck no steeper than MaxGrade, its
            // underside clear of the ground along the way, the walls free there and the floor inside free of the stairs.
            var pairs = new List<(int fa, int fb, float ya, float yb)>();
            int floorsA = BuildingInterior.FloorCountFor(a.Params.Height), floorsB = BuildingInterior.FloorCountFor(b.Params.Height);
            float storeyA = BuildingInterior.StoreyHeight(a.Params.Height), storeyB = BuildingInterior.StoreyHeight(b.Params.Height);
            for (int fa = LowestFloor; fa < floorsA; fa++)
            {
                float ya = a.BaseHeight + fa * storeyA;
                int bestFb = -1;
                float bestDy = float.MaxValue;
                for (int fb = LowestFloor; fb < floorsB; fb++)
                {
                    float dy = Mathf.Abs(b.BaseHeight + fb * storeyB - ya);
                    if (dy < bestDy)
                    {
                        bestDy = dy;
                        bestFb = fb;
                    }
                }
                if (bestFb < 0 || bestDy > MaxRise || bestDy / span > MaxGrade)
                {
                    continue;
                }
                float yb = b.BaseHeight + bestFb * storeyB;
                if (!ClearsTheGround(world, startWorld2, endWorld2, Mathf.Min(ya, yb)))
                {
                    continue;
                }
                if (!WallFree(a, wallA, uA, fa * storeyA) || !WallFree(b, wallB, uB, bestFb * storeyB))
                {
                    continue;
                }
                if (!FloorFreeInside(a, wallA, columnA, columnsA, fa) || !FloorFreeInside(b, wallB, columnB, columnsB, bestFb))
                {
                    continue;
                }
                pairs.Add((fa, bestFb, ya, yb));
            }
            if (pairs.Count == 0)
            {
                return;
            }
            int first = Mathf.Min(pairs.Count - 1, (int)(D(2) * pairs.Count));
            Add(pairs[first]);
            // A second bridge well above the first between tall blocks.
            if (Mathf.Min(floorsA, floorsB) >= SecondBridgeFloors && D(3) < SecondBridgeShare)
            {
                int best = -1;
                for (int i = 0; i < pairs.Count; i++)
                {
                    if (pairs[i].fa >= pairs[first].fa + 3 && (best < 0 || pairs[i].fa > pairs[best].fa)) best = i;
                }
                if (best >= 0) Add(pairs[best]);
            }

            void Add((int fa, int fb, float ya, float yb) p)
            {
                into.Add(new Footbridge(a.Id, b.Id, wallA, wallB, p.fa, p.fb, columnA, columnB, uA, uB,
                    new Vector3(startWorld2.x, p.ya, startWorld2.y), new Vector3(endWorld2.x, p.yb, endWorld2.y), DeckWidth, into.Count));
            }
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

        /// <summary>The deck's underside clears the ground (and the road lifted on it) along the span.</summary>
        private static bool ClearsTheGround(CityWorld world, Vector2 from, Vector2 to, float deckTop)
        {
            for (int i = 1; i < 4; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / 4.0f);
                float ground = (float)world.Plan.GroundHeight(p.x, p.y) + (float)Plan.PlanGeometry.RoadLift + 0.3f;
                if (deckTop - DeckThickness - ground < GroundClearance)
                {
                    return false;
                }
            }
            return true;
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
