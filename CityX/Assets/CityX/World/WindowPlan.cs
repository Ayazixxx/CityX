using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The families of facade a real city is made of. A building is ONE of these, chosen once, so a whole
    /// street reads as different buildings rather than the same building with the dice re-rolled.
    /// </summary>
    public enum FacadeStyle
    {
        /// <summary>Glass from floor to ceiling on every cell of every wall: the office tower. Only a thin mullion grid remains of the wall.</summary>
        Curtain = 0,

        /// <summary>Alternating horizontal bands of glass and solid spandrel: the mid-century office and the modern residential block.</summary>
        Ribbon = 1,

        /// <summary>A regular grid of separate, smaller windows punched in a solid wall: apartments, old masonry, institutions.</summary>
        Punched = 2,

        /// <summary>Irregular openings, a few here and a few there: the brutalist slab and the building that grew by additions.</summary>
        Scattered = 3,

        /// <summary>One high strip of windows under the eaves and a blank wall below it: sheds, warehouses and factories.</summary>
        HighBand = 4,

        /// <summary>Almost nothing: a window or two on a windowless face. Cold stores, substations, the back of a supermarket.</summary>
        Blank = 5,
    }

    /// <summary>
    /// Which cells of a building's walls carry a window, and how tall that window is -- decided ONCE for the
    /// building from its seed, its zone, its massing and what it is, and then read by everything that has to
    /// agree about where the glass is (the mesh builder, the shooting code that finds a pane under a hit, the
    /// title-screen skyline).
    ///
    /// WHY A PLAN INSTEAD OF A HASH PER CELL. Every cell used to be glazed independently at the same 42%, so
    /// every building in the city wore the same speckled pattern and none could be a glass tower or a blank
    /// warehouse. Research on real facades (Wonka/Müller split grammars, glazing ratios by building type)
    /// says the opposite: a facade is chosen as a WHOLE -- the tile that repeats across it -- and variety comes
    /// from which whole facade a building gets, from the ground floor differing from the storeys above it, and
    /// from side walls that are blank because another building stood against them.
    ///
    /// A struct of a few numbers, so a chunk can build one per building on a worker thread with no allocation.
    /// </summary>
    public readonly struct WindowPlan
    {
        public readonly int Seed;
        public readonly FacadeStyle Style;

        /// <summary>Bit per <see cref="WindowGrid.Wall"/>: a wall with no windows at all (a party wall).</summary>
        private readonly int _blankWalls;

        /// <summary>The door wall's ground floor is shopfront glass, whatever the storeys above look like.</summary>
        public readonly bool Storefront;

        /// <summary>Punched: every Nth column carries a window, from <see cref="Phase"/>.</summary>
        public readonly int Pitch;
        public readonly int Phase;

        /// <summary>Ribbon: which parity of row is a glass band.</summary>
        public readonly int BandParity;

        /// <summary>Scattered / Blank: the fraction of cells that get a window.</summary>
        public readonly double ScatterShare;

        /// <summary>The window row of a HighBand shed.</summary>
        public readonly int BandRow;

        /// <summary>How far up a row the glass starts and how far down from the top it ends, as fractions of the row.</summary>
        public readonly float Sill;
        public readonly float Head;

        /// <summary>
        /// How wide a column is, nominally. A curtain wall or a ribbon window is one wide unit between
        /// mullions, not a run of narrow flats' windows, so it uses TWICE the ordinary column width: the same
        /// look from the street, half the panes -- and a tall glass tower has thousands of them, which is the
        /// difference between a chunk that streams in cheaply and one that hitches.
        /// </summary>
        public readonly float ColumnMetres;

        /// <summary>
        /// The cells another block of the building, or a neighbour's party wall, stands against (see BuildingSite.Contacts): any number of stretches,
        /// each a wall, a run of columns [from, to) and the rows below a top row. The arrays are made once, when the plan takes its contacts.
        /// </summary>
        private readonly ContactCells _contacts;

        private readonly struct ContactCells
        {
            public readonly int Count;
            private readonly int[] _wall, _from, _to, _rows;

            public ContactCells(int count, int[] wall, int[] from, int[] to, int[] rows)
            {
                Count = count;
                _wall = wall;
                _from = from;
                _to = to;
                _rows = rows;
            }

            public bool Covers(int wall, int column, int row)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_wall[i] == wall && column >= _from[i] && column < _to[i] && row < _rows[i])
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private WindowPlan(in WindowPlan from, ContactCells contacts)
        {
            Seed = from.Seed;
            Style = from.Style;
            _blankWalls = from._blankWalls;
            Storefront = from.Storefront;
            Pitch = from.Pitch;
            Phase = from.Phase;
            BandParity = from.BandParity;
            ScatterShare = from.ScatterShare;
            BandRow = from.BandRow;
            Sill = from.Sill;
            Head = from.Head;
            ColumnMetres = from.ColumnMetres;
            _contacts = contacts;
        }

        /// <summary>Whether a cell is inside another block of the building (no window, whatever the style says).</summary>
        public bool IsAgainstAnotherBlock(WindowGrid.Wall wall, int column, int row) => _contacts.Covers((int)wall, column, row);

        /// <summary>The same plan with the cells the site's other blocks stand against blanked out.</summary>
        public WindowPlan WithContacts(BuildingSite site)
        {
            WallContact[] contacts = site.Contacts;
            if (contacts == null || contacts.Length == 0)
            {
                return this;
            }
            var wall = new int[contacts.Length];
            var from = new int[contacts.Length];
            var to = new int[contacts.Length];
            var rows = new int[contacts.Length];
            CellLayout rowLayout = WindowGrid.RowsFor(site.Params.Height);
            int count = 0;
            for (int i = 0; i < contacts.Length; i++)
            {
                WallContact c = contacts[i];
                bool northSouth = c.Wall == WindowGrid.Wall.North || c.Wall == WindowGrid.Wall.South;
                CellLayout columns = ColumnsFor(northSouth ? site.Params.Width : site.Params.Depth);
                // A column is against the other block if any of it is: a window half in a wall is no window.
                int a = Mathf.Max(0, Mathf.FloorToInt(c.U0 / columns.Size + 0.02f));
                int b = Mathf.Min(columns.Count, Mathf.CeilToInt(c.U1 / columns.Size - 0.02f));
                int r = Mathf.Min(rowLayout.Count, Mathf.CeilToInt(c.Top / rowLayout.Size - 0.05f));
                if (b <= a || r <= 0)
                {
                    continue;
                }
                wall[count] = (int)c.Wall;
                from[count] = a;
                to[count] = b;
                rows[count] = r;
                count++;
            }
            return count == 0 ? this : new WindowPlan(this, new ContactCells(count, wall, from, to, rows));
        }

        public WindowPlan(int seed, FacadeStyle style, int blankWalls, bool storefront, int pitch, int phase,
            int bandParity, double scatterShare, int bandRow, float sill, float head, float columnScale = 1.0f)
        {
            Seed = seed;
            Style = style;
            _blankWalls = blankWalls;
            _contacts = default;
            Storefront = storefront;
            Pitch = pitch;
            Phase = phase;
            BandParity = bandParity;
            ScatterShare = scatterShare;
            BandRow = bandRow;
            Sill = sill;
            Head = head;
            // A bigger building has bigger windows (see WindowGrid.ColumnScaleFor): a hundred panes across a facade is a cost, not a look.
            float scaleUp = columnScale;
            ColumnMetres = style == FacadeStyle.Curtain || style == FacadeStyle.Ribbon
                ? CellLayout.NominalColumnMetres * 2.0f * scaleUp
                : CellLayout.NominalColumnMetres * scaleUp;
        }

        /// <summary>The same plan with no shopfront glass on the door wall's ground floor.</summary>
        public WindowPlan WithoutStorefront()
        {
            float nominal = Style == FacadeStyle.Curtain || Style == FacadeStyle.Ribbon
                ? CellLayout.NominalColumnMetres * 2.0f
                : CellLayout.NominalColumnMetres;
            return new WindowPlan(Seed, Style, _blankWalls, false, Pitch, Phase, BandParity, ScatterShare, BandRow, Sill, Head, ColumnMetres / nominal);
        }

        /// <summary>The columns of a wall of the given span, at this facade's own unit width.</summary>
        public CellLayout ColumnsFor(float span) => CellLayout.For(span, ColumnMetres);

        public bool IsBlankWall(WindowGrid.Wall wall) => (_blankWalls & (1 << (int)wall)) != 0;

        /// <summary>
        /// Whether a cell carries a window. Pure and hashed, never drawn from a sequence: chunks are built in
        /// whatever order the player walks.
        /// </summary>
        public bool IsGlazed(WindowGrid.Wall wall, int column, int row)
        {
            if (IsBlankWall(wall) || _contacts.Covers((int)wall, column, row))
            {
                return false;
            }
            if (Storefront && row == 0 && wall == WindowGrid.Wall.North)
            {
                return true;
            }

            switch (Style)
            {
                case FacadeStyle.Curtain:
                    return true;
                case FacadeStyle.Ribbon:
                    return (row & 1) == BandParity;
                case FacadeStyle.Punched:
                    return Pitch <= 1 || ((column + Phase) % Pitch) == 0;
                case FacadeStyle.HighBand:
                    return row == BandRow && ((column + Phase) & 1) == 0;
                default:
                    return WorldHash.Unit01(column, row, WindowGrid.GlazedSalt + (ulong)(int)wall,
                        unchecked((ulong)(long)Seed)) < ScatterShare;
            }
        }

        /// <summary>
        /// Whether a glazed cell has its light on. A glass office is lit FLOOR BY FLOOR -- a whole storey on or
        /// off, with the odd blind or desk lamp breaking it up -- which is what makes a lit tower read as a
        /// tower at night; a random speckle over a fully glazed face reads as a screen of noise. Every other
        /// style keeps the scattered per-window light of a block of separate flats.
        /// </summary>
        public bool IsLit(WindowGrid.Wall wall, int column, int row, double litShare)
        {
            if (litShare <= 0.0)
            {
                return false;   // nothing is lit at all (the city since 2026-10-09: only switched-on rooms light their windows)
            }
            if (Style != FacadeStyle.Curtain && Style != FacadeStyle.Ribbon)
            {
                return WindowGrid.IsLit(Seed, wall, column, row, litShare);
            }

            // A storey is two rows, so the whole storey shares one decision. The wall is NOT part of the
            // floor's hash: an office floor is lit all the way round, not on one face only.
            bool floorLit = WorldHash.Unit01(row >> 1, 0, WindowGrid.FloorLitSalt,
                unchecked((ulong)(long)Seed)) < litShare;
            double cell = WorldHash.Unit01(column, row, WindowGrid.LitSalt + (ulong)(int)wall,
                unchecked((ulong)(long)Seed));
            return floorLit ? cell < 0.92 : cell < 0.04;
        }

        /// <summary>
        /// How much of a window row is solid at its bottom (the sill) and its top (the head), as fractions of
        /// the row. A floor slab hangs down into the top of the row below it -- see
        /// <see cref="WindowGrid.TopEdgeHasSlab"/> -- so the top is never less than the slab margin.
        /// </summary>
        public void Insets(bool topHasSlab, out float bottom, out float top)
        {
            bottom = Sill;
            float clearTop = topHasSlab ? WindowGrid.PaneInsetVerticalSlab : WindowGrid.PaneInsetVerticalClear;
            top = Mathf.Max(Head, clearTop);
        }

        /// <summary>
        /// The plan for the building on a site. The zone is looked up here, so callers that already have the
        /// site and the world do not each need to know how a zone is found.
        /// </summary>
        public static WindowPlan For(CityWorld world, BuildingSite site)
        {
            return For(site);
        }

        /// <summary>The same, from the site alone: everything it needs (its seed, zone, parameters and contacts) is on the site.</summary>
        public static WindowPlan For(BuildingSite site)
        {
            WindowPlan plan = WindowGrid.PlanFor(site.Seed, site.Zone, site.Params);
            // A volume on a roof is an attachment, not a shop: whatever its roll, its ground row is ordinary windows. So is a wing.
            if ((site.OnRoof || site.Wing) && plan.Storefront)
            {
                plan = plan.WithoutStorefront();
            }
            return plan.WithContacts(site);
        }
    }
}
