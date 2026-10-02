using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Which cells of a wall are glazed, and which of those have a light on.
    ///
    /// THE SECOND GRID, DELETED. Until now the city drew windows one way only:
    /// painted into the facade texture, on a tile anchored to world metres. The
    /// real cell grid -- <see cref="CellLayout"/>, which stretches cells to
    /// divide each wall exactly -- never had to agree with it, because nothing
    /// else drew anything window-shaped. A 19.8m wall has twelve 1.65m columns
    /// and the texture has 1.6m ones, and that was harmless right up until real
    /// openings were cut.
    ///
    /// CellLayout's own comment records what happens when two grids both draw
    /// windows on one wall: in the Godot build's own words, black windows and
    /// transparent windows combined and walls overlapping each other. The fix
    /// there was to delete the second grid so only one system can ever draw
    /// something window-shaped. This is that same fix, applied the other way
    /// round -- geometry wins, and the texture stops painting windows.
    ///
    /// WHY THIS FILE EXISTS AT ALL, given city_chunk.gd has no counterpart.
    /// Godot glazes EVERY cell, so it needs no such decision. This city's
    /// facades are deliberately varied -- see <see cref="WindowPlan"/> and
    /// docs/FACADES.md: each building wears one whole style (glass curtain
    /// wall, ribbon bands, punched windows, irregular openings, a shed's
    /// high strip, or almost nothing), picked here from its zone and massing.
    /// </summary>
    public static class WindowGrid
    {
        /// <summary>
        /// Fraction of GLAZED cells with a light on. Well under half -- a city
        /// where every window is lit looks like an office block at 3pm.
        /// </summary>
        public const double LitShare = 0.34;

        internal const ulong GlazedSalt = 0x57494E44UL;      // "WIND"
        internal const ulong LitSalt = 0x4C495448UL;         // "LITH"
        internal const ulong FloorLitSalt = 0x464C4F52UL;    // "FLOR"
        private const ulong StyleSalt = 0x53545956UL;        // "STYV"
        private const ulong DetailSalt = 0x44455441UL;       // "DETA"
        private const ulong PartySalt = 0x50415254UL;        // "PART"
        private const ulong ShopSalt = 0x53484F50UL;         // "SHOP"

        /// <summary>
        /// A wall's index, used to keep the four walls of one building from
        /// wearing the same pattern. Order matches CityMeshBuilder's own walls.
        /// </summary>
        public enum Wall
        {
            West = 0,
            East = 1,
            South = 2,
            North = 3,
        }

        /// <summary>
        /// Which way a wall faces the street -- matches CityMeshBuilder's own
        /// AddWindowedWall call sites exactly (Vector3.left/right/back/forward
        /// per wall), so anything spawned off a broken pane flies the same
        /// way the glass itself was ever facing.
        /// </summary>
        public static Vector3 OutwardNormal(Wall wall)
        {
            switch (wall)
            {
                case Wall.West: return Vector3.left;
                case Wall.East: return Vector3.right;
                case Wall.South: return Vector3.back;
                default: return Vector3.forward;
            }
        }

        // ---- which facade a building wears ----

        /// <summary>
        /// Relative chance of each <see cref="FacadeStyle"/> (in enum order: curtain, ribbon, punched,
        /// scattered, high band, blank) for a building of a given massing, BEFORE its zone weighs in.
        ///
        /// From how real cities are put together: towers are glass (curtain wall is what a high-rise office
        /// IS -- Chicago's multifamily towers run 75% glazed and up), the mid-rise mix is the widest, low-rise
        /// is mostly separate punched windows, the brutalist slab leans blank and irregular, and a shed has
        /// one clerestory strip or nothing.
        /// </summary>
        private static readonly double[][] StyleWeightsByMass =
        {
            /* LowRise   */ new[] { 8.0, 8.0, 46.0, 28.0, 0.0, 10.0 },
            /* MidRise   */ new[] { 18.0, 18.0, 40.0, 24.0, 0.0, 0.0 },
            /* Slab      */ new[] { 8.0, 24.0, 30.0, 30.0, 0.0, 8.0 },
            /* Tower     */ new[] { 45.0, 25.0, 30.0, 0.0, 0.0, 0.0 },
            /* Supertall */ new[] { 75.0, 25.0, 0.0, 0.0, 0.0, 0.0 },
            /* Warehouse */ new[] { 0.0, 0.0, 0.0, 12.0, 48.0, 40.0 },
        };

        /// <summary>
        /// How a zone pushes those weights: downtown is glass, the old quarter is small punched windows in
        /// masonry, an industrial estate is blank, the everyday city is mostly regular flats.
        /// </summary>
        private static double[] StyleBiasFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return new[] { 1.3, 1.0, 0.9, 0.8, 1.0, 0.4 };
                case ZoneKind.Urban: return new[] { 1.25, 1.1, 0.9, 0.9, 1.0, 0.6 };
                case ZoneKind.OldTown: return new[] { 0.12, 0.4, 2.0, 1.1, 1.0, 0.7 };
                case ZoneKind.Industrial: return new[] { 0.35, 0.5, 0.4, 1.0, 1.5, 2.0 };
                case ZoneKind.Outskirts: return new[] { 0.3, 0.6, 1.2, 1.4, 1.0, 1.0 };
                default: return new[] { 0.4, 0.9, 1.5, 1.0, 1.0, 0.8 };   // Residential
            }
        }

        /// <summary>Chance that a side wall is a party wall (blank, because another building stood against it).</summary>
        private static double PartyWallChanceFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.30;
                case ZoneKind.Urban: return 0.30;
                case ZoneKind.OldTown: return 0.40;
                case ZoneKind.Residential: return 0.22;
                case ZoneKind.Outskirts: return 0.05;
                default: return 0.0;
            }
        }

        /// <summary>Chance that the door wall's ground floor is a shopfront.</summary>
        private static double StorefrontChanceFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.55;
                case ZoneKind.Urban: return 0.50;
                case ZoneKind.OldTown: return 0.40;
                case ZoneKind.Residential: return 0.20;
                default: return 0.0;
            }
        }

        /// <summary>
        /// The facade of the building described by a seed, a zone and its dice-rolled parameters. Pure: the
        /// same inputs give the same plan on any thread, in any order.
        /// </summary>
        public static WindowPlan PlanFor(int seed, ZoneKind zone, BuildingParams building)
        {
            ulong s = unchecked((ulong)(long)seed);
            FacadeStyle style;
            bool special = building.IsSpecial;
            bool forceStorefront = false;

            if (special)
            {
                // The rare landmark types have a look of their own.
                switch (building.SpecialType)
                {
                    case "supermarket": style = FacadeStyle.Blank; forceStorefront = true; break;
                    case "hospital": style = FacadeStyle.Ribbon; break;
                    case "bank": style = FacadeStyle.Punched; forceStorefront = true; break;
                    default: style = FacadeStyle.Punched; break;
                }
            }
            else
            {
                style = PickStyle(WorldHash.Unit01(0, 0, StyleSalt, s), zone, building.Mass);
            }

            // Detail draws: independent streams so retuning one never moves another.
            double detail = WorldHash.Unit01(1, 0, DetailSalt, s);
            double detail2 = WorldHash.Unit01(2, 0, DetailSalt, s);

            int pitch = 1;
            int phase = 0;
            int bandParity = detail < 0.5 ? 0 : 1;
            double scatter = 0.0;
            int bandRow = Mathf.Min(2, Mathf.Max(0, RowsFor(building.Height).Count - 1));
            float sill = PaneInsetVerticalClear;
            float head = PaneInsetVerticalClear;

            switch (style)
            {
                case FacadeStyle.Curtain:
                    break;
                case FacadeStyle.Ribbon:
                    sill = 0.12f;
                    head = 0.10f;
                    break;
                case FacadeStyle.Punched:
                    pitch = detail < 0.2 ? 1 : 2;
                    phase = pitch == 1 ? 0 : (detail2 < 0.5 ? 0 : 1);
                    sill = 0.28f;
                    head = 0.14f;
                    break;
                case FacadeStyle.Scattered:
                    scatter = 0.20 + 0.20 * detail;   // 0.20 - 0.40, every building its own density
                    break;
                case FacadeStyle.HighBand:
                    phase = detail < 0.5 ? 0 : 1;
                    sill = 0.35f;
                    head = 0.20f;
                    break;
                case FacadeStyle.Blank:
                    scatter = 0.03;
                    sill = 0.25f;
                    head = 0.15f;
                    break;
            }

            // A party wall only makes sense on a building that stands in a row of others, and never on one that
            // is a whole glass face or a shed.
            int blank = 0;
            bool canShareWalls = style == FacadeStyle.Punched || style == FacadeStyle.Scattered
                || style == FacadeStyle.Ribbon;
            if (canShareWalls && !special && building.Mass != BuildingMass.Warehouse
                && building.Mass != BuildingMass.Supertall)
            {
                double chance = PartyWallChanceFor(zone);
                if (WorldHash.Unit01(0, 0, PartySalt + (ulong)(int)Wall.West, s) < chance)
                {
                    blank |= 1 << (int)Wall.West;
                }
                if (WorldHash.Unit01(0, 0, PartySalt + (ulong)(int)Wall.East, s) < chance)
                {
                    blank |= 1 << (int)Wall.East;
                }
                if (WorldHash.Unit01(0, 0, PartySalt + (ulong)(int)Wall.South, s) < chance * 0.35)
                {
                    blank |= 1 << (int)Wall.South;
                }
            }

            bool storefront = forceStorefront;
            if (!storefront && !special && style != FacadeStyle.Blank && style != FacadeStyle.HighBand
                && building.Mass != BuildingMass.Warehouse)
            {
                storefront = WorldHash.Unit01(0, 0, ShopSalt, s) < StorefrontChanceFor(zone);
            }

            return new WindowPlan(seed, style, blank, storefront, pitch, phase, bandParity, scatter,
                bandRow, sill, head);
        }

        /// <summary>The style a roll in [0,1) picks, given where the building stands and how big it is.</summary>
        public static FacadeStyle PickStyle(double roll, ZoneKind zone, BuildingMass mass)
        {
            double[] baseWeights = StyleWeightsByMass[(int)mass];
            double[] bias = StyleBiasFor(zone);
            double total = 0.0;
            for (int i = 0; i < baseWeights.Length; i++)
            {
                total += baseWeights[i] * bias[i];
            }

            double target = roll * total;
            double cursor = 0.0;
            for (int i = 0; i < baseWeights.Length; i++)
            {
                cursor += baseWeights[i] * bias[i];
                if (target < cursor && baseWeights[i] > 0.0)
                {
                    return (FacadeStyle)i;
                }
            }
            return FacadeStyle.Punched;   // floating-point safety net, not a normal path
        }

        /// <summary>
        /// Whether a glazed cell has its light on.
        ///
        /// A DIFFERENT SALT FROM <see cref="IsGlazed"/>, so the lit ones are
        /// scattered through the glazed ones rather than being the same cells
        /// or a neat subset of them. Sharing a salt would light every window in
        /// exactly the pattern the windows themselves follow.
        /// </summary>
        public static bool IsLit(int buildingSeed, Wall wall, int column, int row)
        {
            return IsLit(buildingSeed, wall, column, row, LitShare);
        }

        /// <summary>
        /// As above, with the fraction of lit windows chosen by the caller --
        /// see <see cref="LitShareFor"/>. The SAME hash decides which cells
        /// are lit; a higher share simply lights more of them (every cell lit
        /// at a low share stays lit at a higher one), so a building does not
        /// reshuffle its whole pattern when its zone's share is retuned.
        /// </summary>
        public static bool IsLit(int buildingSeed, Wall wall, int column, int row, double litShare)
        {
            return WorldHash.Unit01(column, row, LitSalt + (ulong)(int)wall,
                unchecked((ulong)(long)buildingSeed)) < litShare;
        }

        /// <summary>
        /// How many windows are lit at night in a zone. Downtown glows,
        /// an industrial estate is nearly dark, the outskirts are sleepy -- so
        /// the city has a night-time silhouette of where things happen.
        /// </summary>
        public static double LitShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.46;
                case ZoneKind.Urban: return 0.40;
                case ZoneKind.OldTown: return 0.26;
                case ZoneKind.Industrial: return 0.10;
                case ZoneKind.Outskirts: return 0.22;
                default: return LitShare;
            }
        }

        /// <summary>
        /// The opening inside one cell, as fractions of the cell.
        ///
        /// The same <see cref="CellLayout.WindowMarginRatio"/> the texture
        /// already insets its painted panes by, so the openings land where the
        /// old painted windows did -- just on the real grid instead of a
        /// world-anchored one.
        /// </summary>
        public static float PaneInset => CellLayout.WindowMarginRatio;

        /// <summary>
        /// The vertical margin on whichever edge of a window row has NO
        /// floor slab against it -- small, mostly cosmetic (a sill/lintel),
        /// since nothing structural sits there to clear.
        /// </summary>
        public const float PaneInsetVerticalClear = 0.05f;

        /// <summary>
        /// The vertical margin on the edge that DOES have a floor slab
        /// against it. AddSlab hangs a slab DOWNWARD from its own boundary
        /// (bottomY = topY - SlabThickness -- see its own comment), so the
        /// slab between two floors always occupies the TOP of the row
        /// below it and never the bottom of the row above -- there is no
        /// symmetric "clear the slab on both sides" case to design for.
        /// 0.2m of BuildingInterior.SlabThickness plus a real 0.06m buffer,
        /// not a hairline -- the previous attempt at this fix used a margin
        /// only 0.024m past the slab and still measurably clipped it.
        /// </summary>
        public const float PaneInsetVerticalSlab = 0.08125f;

        /// <summary>
        /// Whether THIS window row's own top edge has a floor slab hanging
        /// into it.
        ///
        /// CHECKED AGAINST THE REAL BuildingInterior.FloorTop VALUES, not a
        /// row-is-odd parity guess. A first attempt at this used parity
        /// (BuildingInterior spans a fixed two rows per storey, so an
        /// ordinary slab always lands on an even row boundary counted from
        /// the door row) and it is correct for every floor except the last:
        /// FloorTop's own comment says the TOP floor "swallows everything
        /// the cap left over" rather than sitting at a clean multiple of
        /// the storey height, so its own ceiling -- which is still a real
        /// slab; the roof is built through the exact same AddSlab call the
        /// comment right there says so -- can land on a row boundary
        /// parity never predicts. Confirmed as the actual cause of a test
        /// failure parity alone could not explain: same building, same
        /// column and row, unmoved by three different margin values.
        /// </summary>
        public static bool TopEdgeHasSlab(int row, float rowSize, float buildingHeight)
        {
            float storeyHeight = BuildingInterior.StoreyHeight(buildingHeight);
            int floorCount = BuildingInterior.FloorCountFor(buildingHeight);
            float rowTop = (row + 1) * rowSize;
            if (Mathf.Abs(rowTop - buildingHeight) < 0.01f)
            {
                return true;   // the roof slab, even over a sealed upper void
            }
            for (int floor = 0; floor < floorCount; floor++)
            {
                float floorTop = BuildingInterior.FloorTop(floor, floorCount, buildingHeight, storeyHeight);
                if (Mathf.Abs(rowTop - floorTop) < 0.01f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Window rows across a building's height. Rows are counted from the
        /// LOBBY FLOOR, not from the podium base -- a podium is buried on the
        /// uphill side and exposed downhill, and putting windows in it would
        /// glaze a retaining wall.
        /// </summary>
        public static CellLayout RowsFor(float height)
        {
            return CellLayout.For(height, CellLayout.NominalFloorMetres);
        }

        /// <summary>Window columns across a wall of the given span.</summary>
        public static CellLayout ColumnsFor(float span)
        {
            return CellLayout.For(span, CellLayout.NominalColumnMetres);
        }
    }
}
