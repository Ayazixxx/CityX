using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// THE APARTMENT BLOCK'S FACES (2026-10-15, the owner, comparing the city with a procedural town generator they admired, "World Gacha": "I love
    /// their variety in building types and details ... make all the building types and details possible in their world possible in ours too").
    /// Its own notes name an apartment block (集合住宅) by its balconies: a balcony across the whole back wall on every floor above the street, a
    /// parapet along its edge and a partition between one flat's and the next (隔て板); and on the street face an open access walkway (外廊下) on
    /// every floor. Our buildings had a few separate balconies here and there, which reads as an office with a fire escape, not a home.
    ///
    /// Pure: chosen from the building's own seed on a salt of its own (the dice every other choice rolled keep meaning what they did), without
    /// allocating, so every machine builds the same faces and the chunk builder can ask as often as it likes. The mesh is
    /// CityMeshBuilder.AddApartmentFronts' (View/CityMeshBuilder.Apartment.cs).
    /// </summary>
    public readonly struct ApartmentFront
    {
        /// <summary>A balcony on every floor above the street along the back wall (the one facing away from the street door).</summary>
        public readonly bool Balconies;

        /// <summary>An open walkway on every floor above the street along the street wall (the door's).</summary>
        public readonly bool Walkway;

        /// <summary>How many window columns one flat's balcony spans: a partition every this many.</summary>
        public readonly int UnitColumns;

        /// <summary>How far the balconies stand out from the wall, metres.</summary>
        public readonly float Depth;

        /// <summary>True: a steel railing (rails and posts, open between); false: a solid parapet wall (the commoner, heavier look).</summary>
        public readonly bool Railing;

        /// <summary>What the parapets and side walls are finished in: the building's own wall, bare concrete, or white.</summary>
        public readonly ParapetFinish Finish;

        public ApartmentFront(bool balconies, bool walkway, int unitColumns, float depth, bool railing, ParapetFinish finish)
        {
            Balconies = balconies;
            Walkway = walkway;
            UnitColumns = unitColumns;
            Depth = depth;
            Railing = railing;
            Finish = finish;
        }

        public bool Any => Balconies || Walkway;

        public static readonly ApartmentFront None = new ApartmentFront(false, false, 2, 0.0f, false, ParapetFinish.Wall);

        public enum ParapetFinish
        {
            Wall = 0,
            Concrete = 1,
            White = 2,
        }

        // --- the numbers ---

        /// <summary>A block needs this many window rows above the street to be an apartment block (a shop with one floor over it is not).</summary>
        public const int MinRowsAbove = 2;

        /// <summary>
        /// The lowest window row a block may have and still get them: the first floor's slab hangs this less its thickness over the street, and a
        /// 2.6 m body walks out of the door and along the back under it (rows are nominally 3.2 m; a short building's can be squeezed to 2.7).
        /// </summary>
        public const float MinRowHeight = 2.95f;

        /// <summary>How deep a balcony and a walkway are, metres (a real one is 1.2 to 1.6; the city is 1.3 times life size).</summary>
        public const float MinDepth = 1.5f;

        public const float MaxDepth = 2.1f;

        public const float WalkwayDepth = 1.8f;

        /// <summary>The slab's thickness; its top is the row line, level with the floor inside.</summary>
        public const float SlabThickness = 0.2f;

        /// <summary>The parapet's height above the slab and its thickness, metres (waist height on a 2.6 m body).</summary>
        public const float ParapetHeight = 1.35f;

        public const float ParapetThickness = 0.14f;

        /// <summary>A partition between two flats' balconies: this thick, and this tall above the slab (a fire panel stops short of the ceiling).</summary>
        public const float PartitionThickness = 0.1f;

        public const float PartitionHeight = 2.4f;

        /// <summary>The solid side wall (袖壁) closing each end of a run of balconies, floor to the slab above.</summary>
        public const float EndWallThickness = 0.2f;

        /// <summary>How far in from each end of the wall the band stops (clear of the corner piers, FacadeOrnament.CornerPierHalf).</summary>
        public const float EndInset = 0.35f;

        /// <summary>A railing's top rail and posts, metres.</summary>
        public const float RailSize = 0.07f;

        public const float PostSize = 0.05f;

        /// <summary>
        /// The laundry pole's height above the balcony floor (over a 2.6 m head, under the slab above) and its distance out from the wall, metres.
        /// Near the wall, so what hangs on it leaves the strip behind the parapet clear: a climber pulling up over the parapet lands there.
        /// </summary>
        public const float PoleHeight = 2.7f;

        public const float PoleFromWall = 0.55f;

        /// <summary>
        /// Whether the balconies get their laundry poles, washing and airing futons. OFF (owner, 2026-10-15: "removed folded laundry from
        /// balconies we can add stuff like that later right now it just gets in the way"); the code stays for later.
        /// </summary>
        public const bool LaundryOn = false;

        /// <summary>The share of flats with a laundry pole up, and with a futon or a rug airing over the parapet (solid parapets only).</summary>
        public const double PoleShare = 0.45;

        public const double FutonShare = 0.12;

        private const ulong Salt = 0x41505254UL;   // "APRT"

        private const ulong UnitSalt = 0x41505455UL;   // "APTU"

        /// <summary>The share of each zone's eligible blocks that are apartment blocks (Core, Urban, Residential, OldTown, Industrial, Outskirts).</summary>
        private static readonly double[] ShareByZone = { 0.25, 0.45, 0.7, 0.4, 0.0, 0.5 };

        /// <summary>
        /// The faces a building gets. Only an ordinary building that people live in: not a special one (a mall, a car park, a shop with its own
        /// shape), not a sealed or roof-top block or a wing, not a round tower (its sides are short), not glass-skinned, a shed or a blank box,
        /// and with at least <see cref="MinRowsAbove"/> rows of windows over the street. No balconies on a back wall that is a party wall; no
        /// walkway over a shopfront (its sign board and awnings are on that wall).
        /// </summary>
        public static ApartmentFront For(BuildingSite site, in WindowPlan plan)
        {
            if (!site.Exists || site.Params.IsSpecial || site.Shell || site.OnRoof || site.Wing || site.Sides >= 3)
            {
                return None;
            }
            // A house under a pitched roof (RoofShape) is a house, not an apartment block (and its eaves would meet the band's eave).
            if (RoofShape.For(site).Pitched)
            {
                return None;
            }
            FacadeStyle style = plan.Style;
            if (style == FacadeStyle.Curtain || style == FacadeStyle.HighBand || style == FacadeStyle.Blank)
            {
                return None;
            }
            BuildingMass mass = site.Params.Mass;
            if (mass == BuildingMass.Warehouse || mass == BuildingMass.Supertall)
            {
                return None;
            }
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            if (rows.Count - 1 < MinRowsAbove || rows.Size < MinRowHeight)
            {
                return None;
            }
            int zone = Mathf.Clamp((int)site.Zone, 0, ShareByZone.Length - 1);
            ulong seed = unchecked((ulong)(uint)site.Seed);
            if (WorldHash.Unit01(0, 0, Salt, seed) >= ShareByZone[zone])
            {
                return None;
            }
            bool balconies = !plan.IsBlankWall(WindowGrid.Wall.South);
            // Most blocks have their walkways on the street side (Japanese practice); a shop's street wall keeps its shopfront.
            bool walkway = !plan.Storefront && WorldHash.Unit01(1, 0, Salt, seed) < 0.55;
            if (!balconies && !walkway)
            {
                return None;
            }
            int unit = WorldHash.Unit01(2, 0, Salt, seed) < 0.6 ? 2 : 3;
            float depth = Mathf.Lerp(MinDepth, MaxDepth, (float)WorldHash.Unit01(3, 0, Salt, seed));
            bool railing = WorldHash.Unit01(4, 0, Salt, seed) < 0.3;
            double f = WorldHash.Unit01(5, 0, Salt, seed);
            ParapetFinish finish = f < 0.45 ? ParapetFinish.Wall : (f < 0.75 ? ParapetFinish.White : ParapetFinish.Concrete);
            return new ApartmentFront(balconies, walkway, unit, depth, railing, finish);
        }

        /// <summary>Which wall the balconies are on (the back) and the walkway's (the street door's).</summary>
        public const WindowGrid.Wall BalconyWall = WindowGrid.Wall.South;

        public const WindowGrid.Wall WalkwayWall = WindowGrid.Wall.North;

        /// <summary>True when this front puts something along the wall facing <paramref name="normal"/> (in the building's own frame).</summary>
        public bool Covers(Vector3 normal)
        {
            if (normal.z < -0.5f)
            {
                return Balconies;
            }
            if (normal.z > 0.5f)
            {
                return Walkway;
            }
            return false;
        }

        /// <summary>True when this front puts something along <paramref name="wall"/>.</summary>
        public bool Covers(WindowGrid.Wall wall)
        {
            return wall == BalconyWall ? Balconies : wall == WalkwayWall && Walkway;
        }

        /// <summary>
        /// Where the partitions between flats stand along a wall whose window columns are <paramref name="columns"/>, starting at
        /// <paramref name="u0"/>: on every <see cref="UnitColumns"/>th column line (in the solid strip between two panes, never across glass),
        /// strictly inside the band. Index 0 and the last are the band's own ends, <see cref="EndInset"/> in from the wall's.
        /// </summary>
        public int UnitEdges(float u0, CellLayout columns, System.Span<float> edges)
        {
            float span = columns.Count * columns.Size;
            int n = 0;
            edges[n++] = u0 + EndInset;
            int step = Mathf.Max(1, UnitColumns);
            for (int c = step; c < columns.Count; c += step)
            {
                float u = u0 + c * columns.Size;
                // A last flat of one column or less is folded into the one before it: a 1.6 m flat has no room for its door (BalconyDoors).
                if (u > u0 + span - EndInset - columns.Size * 1.5f)
                {
                    break;
                }
                edges[n++] = u;
            }
            edges[n++] = u0 + span - EndInset;
            return n;
        }

        /// <summary>The most edges <see cref="UnitEdges"/> can write for a wall of <paramref name="columns"/> columns.</summary>
        public static int MaxEdges(CellLayout columns)
        {
            return columns.Count + 2;
        }

        /// <summary>
        /// A die for one flat's balcony on one row (<paramref name="what"/> below <see cref="DiePerFlat"/>): <see cref="PoleDie"/> the laundry
        /// pole, <see cref="FutonDie"/> the futon, then its colour and place, <see cref="WashingDie"/> washing on the pole, then how many pieces,
        /// then each piece's size and colour.
        /// </summary>
        public static double UnitDie(int siteSeed, int unit, int row, int what)
        {
            return WorldHash.Unit01(unit * DiePerFlat + what, row, UnitSalt, unchecked((ulong)(uint)siteSeed));
        }

        public const int DiePerFlat = 16;

        public const int PoleDie = 0;

        public const int FutonDie = 1;

        public const int FutonColourDie = 2;

        public const int FutonPlaceDie = 3;

        public const int WashingDie = 4;

        public const int WashingCountDie = 5;

        /// <summary>The first of each piece of washing's dice (two each: its size, its colour).</summary>
        public const int PieceDie = 6;

        /// <summary>The share of laundry poles with washing hung on them, and how many pieces at most.</summary>
        public const double WashingShare = 0.5;

        public const int MaxPieces = 4;
    }
}
