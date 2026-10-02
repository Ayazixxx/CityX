using UnityEngine;

namespace CityX.World
{
    /// <summary>The eight wall materials in the facade texture array. The numbers ARE the slice numbers (View/FacadeTextures.cs).</summary>
    public enum WallMaterial
    {
        Concrete = 0,
        Brick = 1,
        Plaster = 2,
        Metal = 3,
        Stone = 4,
        Tile = 5,
        Spandrel = 6,
        Roof = 7,
    }

    /// <summary>
    /// What one building's outside is MADE OF: the material and tint of its walls, of its ground floor and podium, and of its roof, and how
    /// weathered each is. Decided once from the building's seed, its zone and its facade style, so every client dresses it the same.
    ///
    /// WHY A LOOK AND NOT A MATERIAL. A city of one concrete reads as a toy (that is what the Roblox complaint was). But the frame rate
    /// follows draw calls (docs/PERFORMANCE.md), so a brick block and a metal-clad shed cannot be two materials: they are one shader that
    /// reads this off the vertices (View/SurfaceLook.cs).
    ///
    /// THE PALETTE IS RESTRAINED ON PURPOSE. The aim is a dense, weathered megacity that is rich but not overwhelming: most walls are
    /// concrete, plaster and brick in muted tints, glass towers carry a cool dark tint, and the few strong colours (tile, painted metal)
    /// are the exception. Weathering follows the district: downtown is kept up, the old town and the sheds are not.
    /// </summary>
    public readonly struct BuildingLook
    {
        public readonly WallMaterial Wall;
        public readonly Color WallTint;
        public readonly float WallWear;

        /// <summary>The ground floor (and the podium under it): a different, sturdier material, darker and dirtier than the storeys above.</summary>
        public readonly WallMaterial Base;
        public readonly Color BaseTint;
        public readonly float BaseWear;

        /// <summary>What the trim, ribs, balconies, parapets and roof plant are made of.</summary>
        public readonly Color TrimTint;

        public readonly Color RoofTint;
        public readonly float RoofWear;

        /// <summary>Added to every wall UV, so two buildings of one size do not wear the same brick pattern.</summary>
        public readonly Vector2 UvOffset;

        /// <summary>Which of the eight glass tints (CityX/Glass) the windows are: neutral, blue, teal, bronze, grey, pale, deep blue, green-grey.</summary>
        public readonly int GlassTint;

        /// <summary>The window frames and mullions.</summary>
        public readonly Color FrameTint;

        /// <summary>The solid strips above and below the windows. Same as the walls unless the building has a grid: strips of one material, piers of another.</summary>
        public readonly WallMaterial BandMaterial;
        public readonly Color BandTint;

        /// <summary>How many windows (0 .. 1) have an air conditioner hung under them. Residential and old districts only.</summary>
        public readonly float AcDensity;

        public BuildingLook(WallMaterial wall, Color wallTint, float wallWear, WallMaterial baseMaterial, Color baseTint, float baseWear,
            Color trimTint, Color roofTint, float roofWear, Vector2 uvOffset, int glassTint, Color frameTint,
            WallMaterial bandMaterial, Color bandTint, float acDensity)
        {
            Wall = wall;
            WallTint = wallTint;
            WallWear = wallWear;
            Base = baseMaterial;
            BaseTint = baseTint;
            BaseWear = baseWear;
            TrimTint = trimTint;
            RoofTint = roofTint;
            RoofWear = roofWear;
            UvOffset = uvOffset;
            GlassTint = glassTint;
            FrameTint = frameTint;
            BandMaterial = bandMaterial;
            BandTint = bandTint;
            AcDensity = acDensity;
        }

        private const ulong LookStream = 0x4C4F4F4BUL;   // "LOOK"

        // ---- the weighted tables -------------------------------------------------------------------------------------------

        private readonly struct Weighted<T>
        {
            public readonly T Value;
            public readonly double Weight;

            public Weighted(T value, double weight)
            {
                Value = value;
                Weight = weight;
            }
        }

        private static Weighted<WallMaterial> W(WallMaterial material, double weight)
        {
            return new Weighted<WallMaterial>(material, weight);
        }

        private static Weighted<Color> C(double r, double g, double b, double weight)
        {
            return new Weighted<Color>(new Color((float)r, (float)g, (float)b, 1.0f), weight);
        }

        // What an ordinary (punched, scattered, blank, shed) wall is made of, by district.
        private static readonly Weighted<WallMaterial>[] CoreWalls =
            { W(WallMaterial.Concrete, 40), W(WallMaterial.Stone, 25), W(WallMaterial.Metal, 20), W(WallMaterial.Plaster, 10), W(WallMaterial.Tile, 5) };

        private static readonly Weighted<WallMaterial>[] UrbanWalls =
            { W(WallMaterial.Concrete, 35), W(WallMaterial.Plaster, 20), W(WallMaterial.Stone, 15), W(WallMaterial.Metal, 8), W(WallMaterial.Brick, 10), W(WallMaterial.Tile, 12) };

        private static readonly Weighted<WallMaterial>[] ResidentialWalls =
            { W(WallMaterial.Plaster, 30), W(WallMaterial.Concrete, 24), W(WallMaterial.Tile, 22), W(WallMaterial.Brick, 20), W(WallMaterial.Stone, 4) };

        private static readonly Weighted<WallMaterial>[] OldTownWalls =
            { W(WallMaterial.Brick, 45), W(WallMaterial.Plaster, 30), W(WallMaterial.Stone, 15), W(WallMaterial.Concrete, 10) };

        private static readonly Weighted<WallMaterial>[] OutskirtsWalls =
            { W(WallMaterial.Plaster, 38), W(WallMaterial.Brick, 25), W(WallMaterial.Concrete, 20), W(WallMaterial.Tile, 12), W(WallMaterial.Metal, 5) };

        private static readonly Weighted<WallMaterial>[] IndustrialWalls =
            { W(WallMaterial.Metal, 48), W(WallMaterial.Concrete, 28), W(WallMaterial.Brick, 20), W(WallMaterial.Plaster, 4) };

        // The solid bands of a glass building (the spandrel between its windows): glazed panel, metal, or concrete.
        private static readonly Weighted<WallMaterial>[] GlassWalls =
            { W(WallMaterial.Spandrel, 62), W(WallMaterial.Metal, 28), W(WallMaterial.Concrete, 10) };

        private static readonly Weighted<WallMaterial>[] RibbonWalls =
            { W(WallMaterial.Spandrel, 40), W(WallMaterial.Metal, 30), W(WallMaterial.Concrete, 30) };

        // A ground floor is stronger, plainer stuff than the storeys.
        private static readonly Weighted<WallMaterial>[] DowntownBases =
            { W(WallMaterial.Stone, 45), W(WallMaterial.Concrete, 30), W(WallMaterial.Spandrel, 15), W(WallMaterial.Tile, 10) };

        private static readonly Weighted<WallMaterial>[] OrdinaryBases =
            { W(WallMaterial.Concrete, 35), W(WallMaterial.Stone, 30), W(WallMaterial.Brick, 15), W(WallMaterial.Tile, 20) };

        private static readonly Weighted<WallMaterial>[] ShedBases =
            { W(WallMaterial.Concrete, 70), W(WallMaterial.Brick, 30) };

        // Tints, one table per material. They multiply the texture, so they can only darken or shift it: the textures are drawn light
        // enough to take them.
        private static readonly Weighted<Color>[] ConcreteTints =
            { C(1.00, 1.00, 1.00, 3), C(0.92, 0.95, 1.00, 2), C(1.00, 0.95, 0.88, 2), C(0.80, 0.80, 0.82, 1.5), C(0.90, 0.92, 0.88, 1) };

        private static readonly Weighted<Color>[] BrickTints =
            { C(1.00, 1.00, 1.00, 3), C(0.88, 0.80, 0.76, 2), C(0.72, 0.66, 0.66, 1.5), C(1.00, 0.92, 0.84, 1) };

        private static readonly Weighted<Color>[] PlasterTints =
            { C(1.00, 0.96, 0.86, 3), C(0.96, 0.90, 0.70, 1.5), C(0.82, 0.90, 0.82, 1.2), C(0.82, 0.88, 0.95, 1.5),
              C(1.00, 0.82, 0.74, 1.0), C(0.88, 0.88, 0.88, 2), C(0.92, 0.84, 0.72, 1.5) };

        private static readonly Weighted<Color>[] MetalTints =
            { C(0.90, 0.90, 0.92, 3), C(0.55, 0.57, 0.60, 2.5), C(0.62, 0.74, 0.66, 1), C(0.66, 0.74, 0.88, 1.2), C(0.86, 0.60, 0.48, 0.7), C(0.94, 0.92, 0.84, 1) };

        private static readonly Weighted<Color>[] StoneTints =
            { C(1.00, 0.96, 0.88, 3), C(0.86, 0.86, 0.88, 2.5), C(0.60, 0.60, 0.64, 1.5), C(0.90, 0.68, 0.62, 0.7) };

        private static readonly Weighted<Color>[] TileTints =
            { C(1.00, 1.00, 1.00, 3), C(0.74, 0.86, 0.97, 2.5), C(1.00, 0.96, 0.82, 2), C(0.82, 0.96, 0.88, 1.3), C(1.00, 0.88, 0.88, 0.8) };

        private static readonly Weighted<Color>[] SpandrelTints =
            { C(1.00, 1.00, 1.00, 2), C(0.55, 0.95, 0.90, 2), C(0.60, 0.80, 1.00, 2.5), C(0.95, 0.80, 0.55, 1), C(0.70, 0.75, 0.70, 1.5) };

        private static readonly Weighted<Color>[] RoofTints =
            { C(0.95, 0.95, 0.97, 3), C(0.80, 0.90, 0.82, 1.5), C(0.92, 0.82, 0.74, 1.5), C(0.70, 0.70, 0.72, 1.5) };

        // How kept-up a district is: wear, low to high, as a fraction.
        private static void WearRange(ZoneKind zone, out double low, out double high)
        {
            switch (zone)
            {
                case ZoneKind.Core: low = 0.05; high = 0.40; break;
                case ZoneKind.Urban: low = 0.15; high = 0.55; break;
                case ZoneKind.Residential: low = 0.25; high = 0.65; break;
                case ZoneKind.OldTown: low = 0.45; high = 0.90; break;
                case ZoneKind.Outskirts: low = 0.35; high = 0.80; break;
                default: low = 0.45; high = 0.95; break;   // Industrial
            }
        }

        public static BuildingLook For(BuildingSite site, FacadeStyle style)
        {
            return For(site.Seed, site.Zone, site.Params.Mass, style);
        }

        public static BuildingLook For(int seed, ZoneKind zone, BuildingMass mass, FacadeStyle style)
        {
            var rng = new DeterministicRandom(unchecked((ulong)(uint)seed), LookStream);
            bool shed = mass == BuildingMass.Warehouse || style == FacadeStyle.HighBand;

            Weighted<WallMaterial>[] wallTable;
            if (style == FacadeStyle.Curtain)
            {
                wallTable = GlassWalls;
            }
            else if (style == FacadeStyle.Ribbon)
            {
                wallTable = RibbonWalls;
            }
            else if (shed)
            {
                wallTable = IndustrialWalls;
            }
            else
            {
                switch (zone)
                {
                    case ZoneKind.Core: wallTable = CoreWalls; break;
                    case ZoneKind.Urban: wallTable = UrbanWalls; break;
                    case ZoneKind.Residential: wallTable = ResidentialWalls; break;
                    case ZoneKind.OldTown: wallTable = OldTownWalls; break;
                    case ZoneKind.Outskirts: wallTable = OutskirtsWalls; break;
                    default: wallTable = IndustrialWalls; break;
                }
            }

            WallMaterial wall = Pick(rng, wallTable);
            Color wallTint = Jitter(rng, Pick(rng, TintsFor(wall)));

            bool downtown = zone == ZoneKind.Core || zone == ZoneKind.Urban || style == FacadeStyle.Curtain;
            Weighted<WallMaterial>[] baseTable = shed ? ShedBases : (downtown ? DowntownBases : OrdinaryBases);
            WallMaterial baseMaterial = Pick(rng, baseTable);
            // A ground floor in the same material as the wall above is the common case when that material is already sturdy.
            if ((wall == WallMaterial.Brick || wall == WallMaterial.Stone) && rng.NextDouble() < 0.55)
            {
                baseMaterial = wall;
            }
            Color baseTint = Jitter(rng, Pick(rng, TintsFor(baseMaterial)));
            // Darker than the wall above it: it is nearer the dirt, and nearer the eye.
            baseTint = Scale(baseTint, 0.82f);

            WearRange(zone, out double low, out double high);
            float wallWear = (float)rng.Range(low, high);
            float baseWear = Mathf.Min(1.0f, wallWear + (float)rng.Range(0.10, 0.30));
            float roofWear = (float)rng.Range(System.Math.Max(0.3, low), 1.0);

            Color trim = Scale(Pick(rng, ConcreteTints), 0.85f);
            Color roofTint = Jitter(rng, Pick(rng, RoofTints));
            var uvOffset = new Vector2((float)rng.Range(0.0, 97.0), (float)rng.Range(0.0, 97.0));

            // Glass towers carry a cooler, more varied tint; every other building has the plain dark glass of a window in a wall.
            int glassTint = style == FacadeStyle.Curtain || style == FacadeStyle.Ribbon
                ? PickGlassTint(rng)
                : (rng.NextDouble() < 0.25 ? PickGlassTint(rng) : 0);
            Color frame = Pick(rng, FrameTints);

            // A building with a grid of piers and strips (not a curtain wall, not a shed) sometimes has the strips in another material:
            // dark metal between concrete piers, stone between brick.
            bool gridded = (style == FacadeStyle.Punched || style == FacadeStyle.Scattered) && !shed;
            WallMaterial bandMaterial = wall;
            Color bandTint = wallTint;
            if (gridded && rng.NextDouble() < 0.38)
            {
                bool first = rng.NextDouble() < 0.5;
                switch (wall)
                {
                    case WallMaterial.Concrete: bandMaterial = first ? WallMaterial.Metal : WallMaterial.Plaster; break;
                    case WallMaterial.Brick: bandMaterial = first ? WallMaterial.Stone : WallMaterial.Concrete; break;
                    case WallMaterial.Plaster: bandMaterial = first ? WallMaterial.Concrete : WallMaterial.Stone; break;
                    case WallMaterial.Tile: bandMaterial = WallMaterial.Concrete; break;
                    case WallMaterial.Stone: bandMaterial = WallMaterial.Concrete; break;
                    default: bandMaterial = wall; break;
                }
                bandTint = bandMaterial == wall ? Scale(wallTint, 0.78f) : Jitter(rng, Pick(rng, TintsFor(bandMaterial)));
                if (bandMaterial == WallMaterial.Metal)
                {
                    bandTint = Scale(bandTint, 0.8f);
                }
            }

            // Air conditioners hang under the windows of homes: most in the old town and the ordinary city, none downtown or on a shed.
            double acBase;
            switch (zone)
            {
                case ZoneKind.Residential: acBase = 0.20; break;
                case ZoneKind.OldTown: acBase = 0.26; break;
                case ZoneKind.Outskirts: acBase = 0.12; break;
                case ZoneKind.Urban: acBase = 0.06; break;
                default: acBase = 0.0; break;
            }
            float acDensity = gridded ? (float)(acBase * rng.NextDouble() * 1.5) : 0.0f;

            return new BuildingLook(wall, wallTint, wallWear, baseMaterial, baseTint, baseWear, trim, roofTint, roofWear, uvOffset,
                glassTint, frame, bandMaterial, bandTint, acDensity);
        }

        // Window frames: mostly dark anodised aluminium, some bronze, a few white.
        private static readonly Weighted<Color>[] FrameTints =
            { C(0.17, 0.18, 0.19, 5), C(0.10, 0.10, 0.11, 3), C(0.22, 0.17, 0.12, 1.5), C(0.78, 0.78, 0.76, 1.5), C(0.30, 0.31, 0.32, 1.5) };

        private static int PickGlassTint(DeterministicRandom rng)
        {
            // neutral, blue, teal, bronze, grey, pale, deep blue, green-grey
            double[] weights = { 3.0, 3.0, 2.0, 1.2, 1.5, 1.0, 1.5, 1.0 };
            double total = 0.0;
            foreach (double w in weights) total += w;
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0.0) return i;
            }
            return 0;
        }

        private static Weighted<Color>[] TintsFor(WallMaterial material)
        {
            switch (material)
            {
                case WallMaterial.Brick: return BrickTints;
                case WallMaterial.Plaster: return PlasterTints;
                case WallMaterial.Metal: return MetalTints;
                case WallMaterial.Stone: return StoneTints;
                case WallMaterial.Tile: return TileTints;
                case WallMaterial.Spandrel: return SpandrelTints;
                default: return ConcreteTints;
            }
        }

        private static T Pick<T>(DeterministicRandom rng, Weighted<T>[] table)
        {
            double total = 0.0;
            for (int i = 0; i < table.Length; i++)
            {
                total += table[i].Weight;
            }
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < table.Length; i++)
            {
                roll -= table[i].Weight;
                if (roll < 0.0)
                {
                    return table[i].Value;
                }
            }
            return table[table.Length - 1].Value;
        }

        /// <summary>A few percent of variation in how bright and how coloured, so two buildings of the same palette entry are still two buildings.</summary>
        private static Color Jitter(DeterministicRandom rng, Color c)
        {
            float v = 0.93f + 0.07f * (float)rng.NextDouble();
            float dr = 1.0f + 0.04f * ((float)rng.NextDouble() - 0.5f);
            float db = 1.0f + 0.04f * ((float)rng.NextDouble() - 0.5f);
            return new Color(Mathf.Clamp01(c.r * v * dr), Mathf.Clamp01(c.g * v), Mathf.Clamp01(c.b * v * db), 1.0f);
        }

        private static Color Scale(Color c, float s)
        {
            return new Color(c.r * s, c.g * s, c.b * s, 1.0f);
        }
    }
}
