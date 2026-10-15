using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>What a flat's door onto its balcony is: a sliding glass door (the commonest), a pair of glazed doors, or one door.</summary>
    public enum BalconyDoorKind
    {
        /// <summary>Two glass panels in an aluminium frame, one fixed on the outer track and one sliding behind it on the inner one.</summary>
        Sliding = 0,

        /// <summary>A pair of glazed doors (French doors), hinged at the jambs, opening into the flat.</summary>
        Double = 1,

        /// <summary>One door like the street doors (BuildingDoor): a solid wooden leaf with a lever handle on each face, opening into the flat.</summary>
        Single = 2,
    }

    /// <summary>One flat's door onto its balcony, in the building's own frame (metres from its centre along +X; metres above its base).</summary>
    public readonly struct BalconyDoor
    {
        /// <summary>Which flat (the balcony band's unit, ApartmentFront.UnitEdges) and which window row.</summary>
        public readonly int Unit;
        public readonly int Row;

        /// <summary>The first window column the doorway takes, and how many (one or two): those cells get no window.</summary>
        public readonly int Column;
        public readonly int Columns;

        /// <summary>The doorway: along the back wall, and from the balcony floor up to its head.</summary>
        public readonly float U0, U1, Y0, Y1;

        /// <summary>The building's kind, or a narrower one where the flat has no room for it (a sliding door needs the widest doorway).</summary>
        public readonly BalconyDoorKind Kind;

        /// <summary>
        /// False where it cannot be opened: no floor behind it (a storey is two window rows, BuildingInterior.StoreyHeightRows, and a balcony stands on
        /// every row, so every other balcony is half way up a room; above the floors that are fitted out, a sealed void), or the floor just inside
        /// is not free (the stairs, the opening they come up through, a partition). Drawn shut, locked.
        /// </summary>
        public readonly bool Opens;

        public BalconyDoor(int unit, int row, int column, int columns, float u0, float u1, float y0, float y1, BalconyDoorKind kind, bool opens)
        {
            Unit = unit;
            Row = row;
            Column = column;
            Columns = columns;
            U0 = u0;
            U1 = u1;
            Y0 = y0;
            Y1 = y1;
            Kind = kind;
            Opens = opens;
        }

        public float Width => U1 - U0;

        public float Height => Y1 - Y0;
    }

    /// <summary>
    /// THE DOORS ONTO THE BALCONIES (2026-10-15, the owner: "make it so all balconies have a door leading between the inside of the building and
    /// the balcony, make it so the type of door varies by building ... most will be sliding glass doors which is the most common balcony door in
    /// real life ... double doors ... along with the single doors we built already").
    ///
    /// Every flat's balcony on every row has one, in the middle of the flat's stretch of the back wall, in place of the window there (one window
    /// column, two where the columns are narrow): a doorway from the balcony floor up to the slab above. The kind is the building's -- six in ten
    /// sliding, a quarter pairs of glazed doors, the rest one door like the street doors -- or a narrower one in a flat with no room for it. Pure:
    /// from the building's seed, its balcony band and its stairs, so every machine puts the same doors in the same places. The wall builder cuts
    /// the doorways and draws the doors shut or open (CityMeshBuilder.BalconyDoors), BalconyDoorState says which are open, and
    /// BalconyDoorInteractor opens them.
    ///
    /// SIZED FOR A 2.6 M BODY. The doorway's head is right under the slab above (a balcony is 2.75 m clear at the lowest rows that get one,
    /// ApartmentFront.MinRowHeight, and the body is 2.66 m with its capsule's skin), and its width leaves the half a sliding door opens, or the
    /// space between open leaves, wider than the body.
    ///
    /// HALF OF THEM ARE LOCKED: a storey is two window rows here (one row, 3.2 m, is too low for the stairs of a 2.6 m body), and the balconies
    /// are on every row. A door on an even row opens onto a floor; one on an odd row would open half way up a room, and stays shut.
    /// </summary>
    public static class BalconyDoors
    {
        /// <summary>The shares of buildings with each kind of balcony door: sliding, pairs, the rest single.</summary>
        public const double SlidingShare = 0.6;

        public const double DoubleShare = 0.25;

        /// <summary>A column at least this wide takes a doorway on its own; narrower, the doorway takes two.</summary>
        public const float SingleColumnMetres = 2.6f;

        /// <summary>The tallest a balcony doorway is, metres (on a tall row a strip of wall is left under the slab).</summary>
        public const float MaxHeight = 2.9f;

        /// <summary>How far in from the flat's own edges (a partition, a side wall) the doorway keeps, metres.</summary>
        public const float FromFlatEdge = 0.3f;

        /// <summary>A sliding door's doorway, metres: the narrowest that leaves the open half a body wide, and the widest (two 1.5 m panels).</summary>
        public const float MinSlidingWidth = 2.0f;

        public const float MaxSlidingWidth = 3.0f;

        /// <summary>A pair of doors' doorway, metres (a real pair is 1.2 to 1.6 m; the city is 1.3 times life size).</summary>
        public const float MinDoubleWidth = 1.5f;

        public const float MaxDoubleWidth = 2.2f;

        /// <summary>One door's doorway, metres: the street door's leaf is 1.6 m, a balcony's narrower; and the narrowest a body still goes through.</summary>
        public const float SingleWidth = 1.3f;

        public const float MinSingleWidth = 1.1f;

        // ---- the frame and the leaves (the builder draws these; the sizes here are what a body has to fit through) ----

        /// <summary>The frame's jambs (each side), its head bar and the threshold, metres (aluminium sections; the threshold barely proud of the floor).</summary>
        public const float Jamb = 0.07f;

        public const float HeadBar = 0.05f;

        public const float Threshold = 0.01f;

        /// <summary>A glazed panel or leaf's thickness, and a solid door's (BuildingDoor.Thickness), metres.</summary>
        public const float PanelThickness = 0.045f;

        public const float SolidLeafThickness = 0.08f;

        /// <summary>How far a sliding door's two panels overlap where they meet, and how far short of the jamb the open one stops (its handle), metres.</summary>
        public const float SlidingOverlap = 0.06f;

        public const float SlidingStop = 0.04f;

        /// <summary>The depths, inward from the wall's outer face, of a sliding door's outer and inner tracks and of a hinged leaf's plane, metres.</summary>
        public const float OuterTrack = 0.10f;

        public const float InnerTrack = 0.17f;

        public const float LeafPlane = 0.13f;

        /// <summary>
        /// How far into the flat the floor in front of a door that opens has to be free, metres: the leaves open inward (a balcony is narrow, and has
        /// its laundry pole and its airing on it), and the way in must not be a stair's side, the opening a stair comes up through, or a partition.
        /// </summary>
        public const float ClearInside = 1.4f;

        private const ulong Salt = 0x42444F52UL;   // "BDOR"

        /// <summary>The kind of door a building's balconies have (a flat with no room for it gets a narrower one: <see cref="BalconyDoor.Kind"/>).</summary>
        public static BalconyDoorKind KindFor(BuildingSite site)
        {
            double d = WorldHash.Unit01(0, 0, Salt, unchecked((ulong)(uint)site.Seed));
            return d < SlidingShare ? BalconyDoorKind.Sliding : (d < SlidingShare + DoubleShare ? BalconyDoorKind.Double : BalconyDoorKind.Single);
        }

        /// <summary>
        /// Which side a building's sliding doors keep their fixed panel on, per flat: alternating flat to flat, so neighbours' open halves are not
        /// all at the same end. True: the fixed panel is on the doorway's -U side. A single door's hinge is on the same side.
        /// </summary>
        public static bool FixedOnLow(BuildingSite site, int unit)
        {
            return ((unit + (int)(WorldHash.Unit01(1, 0, Salt, unchecked((ulong)(uint)site.Seed)) * 2.0)) & 1) == 0;
        }

        /// <summary>How tall the way through a doorway is: from the threshold to the underside of the head bar.</summary>
        public static float ClearHeight(in BalconyDoor door)
        {
            return door.Height - HeadBar - Threshold;
        }

        /// <summary>How wide the way through an open door is: the half a sliding door leaves, or between the jambs less the open leaves.</summary>
        public static float PassageWidth(in BalconyDoor door)
        {
            float clear = door.Width - 2.0f * Jamb;
            switch (door.Kind)
            {
                case BalconyDoorKind.Sliding:
                    return clear * 0.5f - SlidingOverlap * 0.5f - SlidingStop;
                case BalconyDoorKind.Double:
                    return clear - 2.0f * PanelThickness;
                default:
                    return clear - SolidLeafThickness;
            }
        }

        /// <summary>
        /// Where the way through an open door is, along it from its low jamb (0 .. Width): beside the panel a sliding door's open half leaves,
        /// between a pair's open leaves, beside a single door's (on the side away from its hinge).
        /// </summary>
        public static void Passage(BuildingSite site, in BalconyDoor door, out float from, out float to)
        {
            float w = door.Width;
            bool low = FixedOnLow(site, door.Unit);
            switch (door.Kind)
            {
                case BalconyDoorKind.Sliding:
                {
                    float panel = (w - 2.0f * Jamb) * 0.5f + SlidingOverlap * 0.5f;
                    from = low ? Jamb + SlidingStop + panel : Jamb;
                    to = low ? w - Jamb : w - Jamb - SlidingStop - panel;
                    break;
                }
                case BalconyDoorKind.Double:
                    from = Jamb + PanelThickness;
                    to = w - Jamb - PanelThickness;
                    break;
                default:
                    from = low ? Jamb + SolidLeafThickness : Jamb;
                    to = low ? w - Jamb : w - Jamb - SolidLeafThickness;
                    break;
            }
        }

        /// <summary>
        /// Every balcony door of the building on a site (none unless it has balconies: ApartmentFront), into <paramref name="into"/> (cleared
        /// first), row by row. Worked out once per building and kept (<see cref="CacheLimit"/>): its builder, its glass, the door interactor and
        /// the network all ask, on any thread.
        /// </summary>
        public static void For(BuildingSite site, in WindowPlan plan, List<BalconyDoor> into)
        {
            into.Clear();
            var key = new SiteKey(site);
            if (!Cache.TryGetValue(key, out BalconyDoor[] doors))
            {
                var laid = new List<BalconyDoor>();
                Lay(site, plan, laid);
                doors = laid.ToArray();
                if (Cache.Count >= CacheLimit)
                {
                    Cache.Clear();
                }
                Cache[key] = doors;
            }
            into.AddRange(doors);
        }

        /// <summary>How many buildings' doors are kept before the store is emptied and starts again (a city's worth streamed in, a few MB).</summary>
        public const int CacheLimit = 4096;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<SiteKey, BalconyDoor[]> Cache =
            new System.Collections.Concurrent.ConcurrentDictionary<SiteKey, BalconyDoor[]>();

        /// <summary>A building, by everything that tells one from another (its id alone repeats from world to world).</summary>
        private readonly struct SiteKey : IEquatable<SiteKey>
        {
            private readonly int _id, _seed;
            private readonly float _width, _depth, _height;

            public SiteKey(BuildingSite site)
            {
                _id = site.Id;
                _seed = site.Seed;
                _width = site.Params.Width;
                _depth = site.Params.Depth;
                _height = site.Params.Height;
            }

            public bool Equals(SiteKey o) => _id == o._id && _seed == o._seed && _width == o._width && _depth == o._depth && _height == o._height;

            public override bool Equals(object obj) => obj is SiteKey k && Equals(k);

            public override int GetHashCode() => unchecked(((_id * 397) ^ _seed) * 397 ^ _width.GetHashCode() ^ (_height.GetHashCode() << 1));
        }

        private static void Lay(BuildingSite site, in WindowPlan plan, List<BalconyDoor> into)
        {
            ApartmentFront front = ApartmentFront.For(site, plan);
            if (!front.Balconies)
            {
                return;
            }
            float halfW = site.Params.Width * 0.5f, halfD = site.Params.Depth * 0.5f;
            float u0 = -halfW;   // the back wall's column 0 starts at the building's -X side
            CellLayout columns = plan.ColumnsFor(site.Params.Width);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            Span<float> edges = stackalloc float[ApartmentFront.MaxEdges(columns)];
            int n = front.UnitEdges(u0, columns, edges);
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            BalconyDoorKind kind = KindFor(site);
            float height = Mathf.Min(MaxHeight, rows.Size - ApartmentFront.SlabThickness);

            // Each flat's doorway, once: the same on every row.
            Span<float> doorU0 = stackalloc float[Mathf.Max(1, n)];
            Span<float> doorU1 = stackalloc float[Mathf.Max(1, n)];
            Span<int> doorColumn = stackalloc int[Mathf.Max(1, n)];
            Span<int> doorColumns = stackalloc int[Mathf.Max(1, n)];
            Span<int> doorKind = stackalloc int[Mathf.Max(1, n)];
            for (int k = 0; k + 1 < n; k++)
            {
                doorKind[k] = -1;
                // The flat's columns: those whose middles lie between its edges.
                int first = Mathf.CeilToInt((edges[k] - u0) / columns.Size - 0.5f - 1e-3f);
                int last = Mathf.FloorToInt((edges[k + 1] - u0) / columns.Size - 0.5f + 1e-3f);
                first = Mathf.Clamp(first, 0, columns.Count - 1);
                last = Mathf.Clamp(last, first, columns.Count - 1);
                int flatColumns = last - first + 1;
                int count = columns.Size >= SingleColumnMetres ? 1 : Mathf.Min(2, flatColumns);
                int column = first + (flatColumns - count) / 2;
                float a = Mathf.Max(u0 + column * columns.Size, edges[k] + FromFlatEdge);
                float b = Mathf.Min(u0 + (column + count) * columns.Size, edges[k + 1] - FromFlatEdge);
                if (!Fit(kind, b - a, out BalconyDoorKind fitted, out float width))
                {
                    continue;   // too narrow a flat for any door a body goes through
                }
                float middle = (a + b) * 0.5f;
                doorU0[k] = middle - width * 0.5f;
                doorU1[k] = middle + width * 0.5f;
                doorColumn[k] = column;
                doorColumns[k] = count;
                doorKind[k] = (int)fitted;
            }

            // The floor a body walks on: inside the outer walls (BuildingStairs.WalkPath's own room).
            float hx = halfW - BuildingInterior.WallThickness - 0.05f, hz = halfD - BuildingInterior.WallThickness - 0.05f;
            for (int r = 1; r < rows.Count; r++)
            {
                float y0 = r * rows.Size;
                int floor = r / BuildingInterior.StoreyHeightRows;
                bool floorRow = r % BuildingInterior.StoreyHeightRows == 0 && floor < stairs.FloorCount;
                List<Rect> solids = null;
                bool flooded = false;
                for (int k = 0; k + 1 < n; k++)
                {
                    if (doorKind[k] < 0)
                    {
                        continue;
                    }
                    // Only where the balcony is: not a flat that another block of the building stands against at this height.
                    float mid = (edges[k] + edges[k + 1]) * 0.5f, half = (edges[k + 1] - edges[k]) * 0.5f;
                    if (site.ContactTopCentred(WindowGrid.Wall.South, mid - half, mid + half) > y0 - 0.5f)
                    {
                        continue;
                    }
                    bool opens = false;
                    if (floorRow)
                    {
                        StairPlan leaving = stairs.Floors[floor];
                        StairPlan arriving = floor == 0 ? stairs.Basement : stairs.Floors[floor - 1];
                        if (solids == null)
                        {
                            solids = BuildingStairs.FloorSolids(leaving, arriving, floor, halfW, halfD);
                            flooded = FloodFromStairs(solids, hx, hz, leaving, arriving);
                        }
                        Rect inside = Rect.MinMaxRect(doorU0[k], -halfD + BuildingInterior.WallThickness, doorU1[k],
                            -halfD + BuildingInterior.WallThickness + ClearInside);
                        // The floor just inside is free, and from it a body walks to the stairs (not into a pocket the stairs and walls shut off).
                        opens = !Blocked(solids, inside) && (!flooded || Flooded(hx, hz, new Vector2((doorU0[k] + doorU1[k]) * 0.5f, -hz + 0.8f)));
                    }
                    into.Add(new BalconyDoor(k, r, doorColumn[k], doorColumns[k], doorU0[k], doorU1[k], y0, y0 + height,
                        (BalconyDoorKind)doorKind[k], opens));
                }
            }
        }

        /// <summary>
        /// The door a flat gets, given the building's kind and the room in the flat's stretch of wall (<paramref name="room"/> metres): the
        /// building's kind if it fits, else a narrower kind, else none.
        /// </summary>
        public static bool Fit(BalconyDoorKind kind, float room, out BalconyDoorKind fitted, out float width)
        {
            if (kind == BalconyDoorKind.Sliding && room >= MinSlidingWidth)
            {
                fitted = BalconyDoorKind.Sliding;
                width = Mathf.Min(room, MaxSlidingWidth);
                return true;
            }
            if (kind != BalconyDoorKind.Single && room >= MinDoubleWidth)
            {
                fitted = BalconyDoorKind.Double;
                width = Mathf.Min(room, MaxDoubleWidth);
                return true;
            }
            if (room >= MinSingleWidth)
            {
                fitted = BalconyDoorKind.Single;
                width = Mathf.Min(room, SingleWidth);
                return true;
            }
            fitted = kind;
            width = 0.0f;
            return false;
        }

        // ---- can a body walk from a door to the stairs? BuildingStairs.WalkPath's grid and body, flooded once per floor from the stairs ----

        private const float WalkCell = 0.25f;

        private const float WalkRadius = 0.45f;

        /// <summary>The most cells a floor's flood takes (a 125 m square floor); a bigger floor is not flooded and every door on it counts as reaching.</summary>
        private const int MaxWalkCells = 250000;

        // One grid per thread, reused: 0 a cell a body cannot stand in, 1 free, 2 free and reached from the stairs.
        [ThreadStatic] private static byte[] s_cells;
        [ThreadStatic] private static int[] s_queue;

        /// <summary>
        /// Floods a floor (half extents <paramref name="hx"/> by <paramref name="hz"/>) from where its stairs meet it -- the foot of the flight that
        /// leaves it, the head of the one that arrives -- round its solids (BuildingStairs.FloorSolids). Afterwards <see cref="Flooded"/> says which
        /// points a body walks to from the stairs. False when there is nothing to flood from or the floor is too big to grid.
        /// </summary>
        private static bool FloodFromStairs(List<Rect> solids, float hx, float hz, StairPlan leaving, StairPlan arriving)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(2.0f * hx / WalkCell)), nz = Mathf.Max(1, Mathf.CeilToInt(2.0f * hz / WalkCell));
            if ((leaving == null && arriving == null) || (long)nx * nz > MaxWalkCells)
            {
                return false;
            }
            int count = nx * nz;
            if (s_cells == null || s_cells.Length < count)
            {
                s_cells = new byte[count];
                s_queue = new int[count];
            }
            byte[] cells = s_cells;
            int[] queue = s_queue;
            for (int iz = 0; iz < nz; iz++)
            {
                float z = -hz + (iz + 0.5f) * WalkCell;
                bool zOk = z > -hz + WalkRadius && z < hz - WalkRadius;
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = -hx + (ix + 0.5f) * WalkCell;
                    cells[iz * nx + ix] = zOk && x > -hx + WalkRadius && x < hx - WalkRadius ? (byte)1 : (byte)0;
                }
            }
            for (int s = 0; s < solids.Count; s++)
            {
                // The cells whose centre is strictly inside the solid grown by the body's radius.
                Rect r = solids[s];
                float sx0 = r.xMin - WalkRadius, sx1 = r.xMax + WalkRadius, sz0 = r.yMin - WalkRadius, sz1 = r.yMax + WalkRadius;
                int cx0 = Mathf.Max(0, Mathf.FloorToInt((sx0 + hx) / WalkCell - 0.5f)), cx1 = Mathf.Min(nx - 1, Mathf.CeilToInt((sx1 + hx) / WalkCell - 0.5f));
                int cz0 = Mathf.Max(0, Mathf.FloorToInt((sz0 + hz) / WalkCell - 0.5f)), cz1 = Mathf.Min(nz - 1, Mathf.CeilToInt((sz1 + hz) / WalkCell - 0.5f));
                for (int iz = cz0; iz <= cz1; iz++)
                {
                    float z = -hz + (iz + 0.5f) * WalkCell;
                    if (!(z > sz0 && z < sz1))
                    {
                        continue;
                    }
                    for (int ix = cx0; ix <= cx1; ix++)
                    {
                        float x = -hx + (ix + 0.5f) * WalkCell;
                        if (x > sx0 && x < sx1)
                        {
                            cells[iz * nx + ix] = 0;
                        }
                    }
                }
            }
            int head = 0, tail = 0;
            if (leaving != null)
            {
                Seed(leaving.EntryPoint);
            }
            if (arriving != null)
            {
                Seed(arriving.ExitPoint);
            }
            if (tail == 0)
            {
                return false;   // the stairs' ends are not where a body stands on this grid: no judgement
            }
            while (head < tail)
            {
                int c = queue[head++];
                int cx = c % nx, cz = c / nx;
                if (cx + 1 < nx) Visit(c + 1);
                if (cx > 0) Visit(c - 1);
                if (cz + 1 < nz) Visit(c + nx);
                if (cz > 0) Visit(c - nx);
            }
            return true;

            void Seed(Vector2 p)
            {
                int i = CellAt(p, hx, hz, nx, nz);
                if (cells[i] == 1)
                {
                    cells[i] = 2;
                    queue[tail++] = i;
                }
            }

            void Visit(int a)
            {
                if (cells[a] == 1)
                {
                    cells[a] = 2;
                    queue[tail++] = a;
                }
            }
        }

        /// <summary>After <see cref="FloodFromStairs"/> on this thread: does a body walk from the stairs to <paramref name="p"/>?</summary>
        private static bool Flooded(float hx, float hz, Vector2 p)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(2.0f * hx / WalkCell)), nz = Mathf.Max(1, Mathf.CeilToInt(2.0f * hz / WalkCell));
            return s_cells[CellAt(p, hx, hz, nx, nz)] == 2;
        }

        private static int CellAt(Vector2 p, float hx, float hz, int nx, int nz)
        {
            return Mathf.Clamp(Mathf.FloorToInt((p.y + hz) / WalkCell), 0, nz - 1) * nx + Mathf.Clamp(Mathf.FloorToInt((p.x + hx) / WalkCell), 0, nx - 1);
        }

        private static bool Blocked(List<Rect> solids, Rect area)
        {
            for (int i = 0; i < solids.Count; i++)
            {
                Rect s = solids[i];
                if (s.xMin < area.xMax && s.xMax > area.xMin && s.yMin < area.yMax && s.yMax > area.yMin)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Where a door stands in the world: the bottom of its doorway's low (-U) jamb on the wall's outer face, the direction along the wall
        /// toward its other jamb, and the wall's outward normal (out over the balcony).
        /// </summary>
        public static void InWorld(BuildingSite site, in BalconyDoor door, out Vector3 corner, out Vector3 along, out Vector3 outward)
        {
            double back = -site.Params.Depth * 0.5;   // the back wall (ApartmentFront.BalconyWall) is the building's -Z face
            site.LocalToWorld(door.U0, back, out double x0, out double z0);
            site.LocalToWorld(door.U0 + 1.0, back, out double x1, out double z1);
            site.LocalToWorld(door.U0, back - 1.0, out double xo, out double zo);
            corner = new Vector3((float)x0, site.BaseHeight + door.Y0, (float)z0);
            along = new Vector3((float)(x1 - x0), 0.0f, (float)(z1 - z0)).normalized;
            outward = new Vector3((float)(xo - x0), 0.0f, (float)(zo - z0)).normalized;
        }

        /// <summary>
        /// The balcony door a window cell of the back wall gives way to, if any: a cell a doorway passes through gets no window (its pane would
        /// be inside the door), the same whole-cell exclusion as the street door's (GlassDamage.IsDoorCell).
        /// </summary>
        public static bool IsDoorCell(List<BalconyDoor> doors, int column, int row)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                BalconyDoor d = doors[i];
                if (d.Row == row && column >= d.Column && column < d.Column + d.Columns)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The door of a flat on a row, if there is one (index into <paramref name="doors"/>, or -1).</summary>
        public static int Find(List<BalconyDoor> doors, int unit, int row)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Unit == unit && doors[i].Row == row)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Which balcony doors stand open: shared by everyone in a session (Character.BalconyDoorsNetwork keeps every machine's copy the same), as
    /// the light switches are (RoomLights). Read by the chunk builders on their own threads, so it is locked. Changed carries the building whose
    /// door it was: its chunk is built again with the door drawn the new way.
    /// </summary>
    public sealed class BalconyDoorState
    {
        /// <summary>The game's (one per running game, like RoomLights). A test may make its own.</summary>
        public static BalconyDoorState Shared { get; } = new BalconyDoorState();

        /// <summary>When a session is running, what a player does at a door goes through this so every machine hears of it; null in singleplayer.</summary>
        public IBalconyDoorRelay Relay;

        private readonly object _lock = new object();
        private readonly HashSet<long> _open = new HashSet<long>();

        /// <summary>Raised with the building's id when one of its doors opens or shuts (on the thread that set it: the main thread).</summary>
        public event Action<int> Changed;

        /// <summary>A door's key: its building, its flat and its row.</summary>
        public static long Key(int building, int unit, int row)
        {
            return ((long)building << 24) | ((long)(unit & 0xFFF) << 12) | (long)(row & 0xFFF);
        }

        /// <summary>The building, flat and row a key names.</summary>
        public static void Unkey(long key, out int building, out int unit, out int row)
        {
            building = (int)(key >> 24);
            unit = (int)((key >> 12) & 0xFFF);
            row = (int)(key & 0xFFF);
        }

        public bool IsOpen(int building, int unit, int row)
        {
            lock (_lock)
            {
                return _open.Contains(Key(building, unit, row));
            }
        }

        /// <summary>What a player does at a door: opens or shuts it, here and (through the session, when there is one) on every machine.</summary>
        public void Use(int building, int unit, int row, bool open)
        {
            Set(building, unit, row, open);
            Relay?.Send(building, unit, row, open);
        }

        /// <summary>Opens or shuts a door (a player here, or heard from another machine). Raises <see cref="Changed"/> when it moved.</summary>
        public void Set(int building, int unit, int row, bool open)
        {
            bool changed;
            lock (_lock)
            {
                long key = Key(building, unit, row);
                changed = open ? _open.Add(key) : _open.Remove(key);
            }
            if (changed)
            {
                Changed?.Invoke(building);
            }
        }

        /// <summary>Every door that stands open (what a player joining a session is told), as keys.</summary>
        public List<long> AllOpen()
        {
            lock (_lock)
            {
                return new List<long>(_open);
            }
        }

        /// <summary>How many doors stand open.</summary>
        public int OpenCount
        {
            get
            {
                lock (_lock)
                {
                    return _open.Count;
                }
            }
        }

        /// <summary>Shuts every door (a new world; a test).</summary>
        public void Clear()
        {
            List<long> was = AllOpen();
            lock (_lock)
            {
                _open.Clear();
            }
            foreach (long key in was)
            {
                Unkey(key, out int building, out _, out _);
                Changed?.Invoke(building);
            }
        }
    }

    /// <summary>Carries a balcony door opened or shut to the other machines in a session (Character.BalconyDoorsNetwork).</summary>
    public interface IBalconyDoorRelay
    {
        void Send(int building, int unit, int row, bool open);
    }
}
