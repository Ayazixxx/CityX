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
