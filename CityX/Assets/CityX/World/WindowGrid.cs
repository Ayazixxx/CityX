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
    /// facade is deliberately sparse -- punched openings with long blank runs
    /// of concrete, which is the reference the texture work was built against
    /// -- so the sparse pattern has to survive the move from texture to
    /// geometry. The two shares below are the texture's own, carried across
    /// unchanged.
    /// </summary>
    public static class WindowGrid
    {
        /// <summary>
        /// Fraction of cells that are glazed. CityFacadeTextures' own
        /// WindowedCellShare: sparse, per the reference, rather than the
        /// curtain wall every-cell glazing would give.
        /// </summary>
        public const double GlazedShare = 0.42;

        /// <summary>
        /// Fraction of GLAZED cells with a light on. Well under half -- a city
        /// where every window is lit looks like an office block at 3pm.
        /// </summary>
        public const double LitShare = 0.34;

        private const ulong GlazedSalt = 0x57494E44UL;   // "WIND"
        private const ulong LitSalt = 0x4C495448UL;      // "LITH"

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
        /// Whether a cell carries a window.
        ///
        /// Hashed rather than drawn from a sequence because chunks are built in
        /// whatever order the player walks -- see WorldHash's own note on why
        /// that rules out a cursor.
        /// </summary>
        public static bool IsGlazed(int buildingSeed, Wall wall, int column, int row)
        {
            return WorldHash.Unit01(column, row, GlazedSalt + (ulong)(int)wall,
                unchecked((ulong)(long)buildingSeed)) < GlazedShare;
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
            return WorldHash.Unit01(column, row, LitSalt + (ulong)(int)wall,
                unchecked((ulong)(long)buildingSeed)) < LitShare;
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
