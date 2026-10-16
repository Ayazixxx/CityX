using UnityEngine;

namespace CityX.World
{
    /// <summary>The shape of a building's roof: flat behind a parapet (every roof before 2026-10-15), one of the pitched ones, or a flat slab with eaves.</summary>
    public enum RoofKind
    {
        Flat = 0,

        /// <summary>Two slopes meeting at a ridge along the long side, a triangular gable wall at each end (切妻).</summary>
        Gable = 1,

        /// <summary>Four slopes: two up the long sides to a shorter ridge, a triangle up each end (寄棟).</summary>
        Hip = 2,

        /// <summary>One slope across the whole roof, from a low wall to a high one (片流れ): the modern house, the shed, the lean-to.</summary>
        Skillion = 3,

        /// <summary>
        /// A gable whose ridge sits near the front wall: a short steep slope to the front, a long one down over the back, the front wall a storey
        /// taller than the back (New England's saltbox: two storeys in front, one behind).
        /// </summary>
        Saltbox = 4,

        /// <summary>
        /// A flat roof with no parapet: a slab whose eaves stand out past every wall over a soffit, a fascia round the edge (the mid-century house,
        /// the 1960s school, motel or strip shop, a modern pavilion). A level deck to stand on, like the parapet roof.
        /// </summary>
        FlatEaves = 5,
    }

    /// <summary>What a pitched roof is covered with: tiles (the Tile wall material, at a roof tile's size) or standing-seam metal sheet.</summary>
    public enum RoofCovering
    {
        Tile = 0,
        Metal = 1,
    }

    /// <summary>
    /// A ROOF'S SHAPE (2026-10-15, docs/WORLD_GACHA.md phase 2). Every roof in the city was flat behind a parapet, which is a downtown's
    /// roofscape; a town's is pitched, and the owner's model town has gables (切妻), hips (寄棟), the single slope of a modern house (片流れ)
    /// and the low metal gable of a warehouse. Most low buildings out of the centre get one, a few downtown (an old house between towers).
    ///
    /// Later the same day, three more from the owner's icons (phase 2b): the SHED roof (a skillion on more houses and on the sheds of the
    /// industrial zone), the SALTBOX (a gable with a long back slope) and the FLAT ROOF WITH EAVES (a slab with no parapet, its eaves out over a
    /// soffit). Their shares follow what was found of the real world: OpenStreetMap's 9.75 million roof-tagged buildings are 56 % gabled, 21 %
    /// flat, 10 % hipped, 3 % skillion and 0.8 % saltbox (0.35 % plain plus the double and quadruple ones); the gable is the commonest house roof
    /// of North America and the hip the second; a saltbox is a 17th-18th century New England form kept today on garages, sheds and the odd
    /// house; a skillion is the modern house, the addition, the garage, the small industrial shed; the flat roof with eaves is the mid-century
    /// house (the ranch, the Eichler), the 1950s-80s school, motel and strip shop, the modern pavilion. So: of the pitched house roofs a few
    /// per cent are saltboxes (most in the old town, where the form belongs), skillions one in twelve in the homes and one in five downtown
    /// (the few houses between towers are the modern ones); of the FLAT house roofs about a quarter to a third are slabs with eaves instead of
    /// parapets, a tenth in the old town, and a few mid-rise blocks downtown get the overhang of a 1960s office.
    ///
    /// NOT WHERE ITS EAVES WOULD MEET ANOTHER BLOCK: not a special one, not a block of a shaped building, not one something stands on. What
    /// stands against it (BuildingSite.Contacts: a terrace's neighbours, the leg of an L) may stand against two opposite walls only, and those
    /// become the gable's ends, flush, with no verge: the roof of a row house. Against walls both ways round, it keeps a flat roof.
    ///
    /// Pure: from the building's seed on a salt of its own, without allocating. The mesh is CityMeshBuilder.AddPitchedRoof's (near and far)
    /// and AddFlatEavesRoof's. A pitched roof is nobody's to stand on, so BuildingStairs leaves out the stair up to it (the top floor's ceiling
    /// is closed), FacadeOrnament gives it no cornice (there is no parapet for one to cap) and ApartmentFront leaves the building alone. A flat
    /// roof with eaves keeps its stair (it is a deck) but has no parapet either: no cornice, no billboard, the corner piers stop at its soffit.
    /// </summary>
    public readonly struct RoofShape
    {
        public readonly RoofKind Kind;

        /// <summary>True: the ridge (or a skillion's fall) runs along the building's X, its width; false: along Z, its depth.</summary>
        public readonly bool AlongX;

        /// <summary>A skillion's low side, or a saltbox's long back slope: true the -Z (or -X) wall, false the +Z (or +X) one.</summary>
        public readonly bool LowAtMin;

        /// <summary>The slope, degrees from level (both of a saltbox's slopes: the back one is the same pitch, only longer).</summary>
        public readonly float Pitch;

        /// <summary>How far the eaves stand out past the walls, and a gable's verge past its end walls, metres.</summary>
        public readonly float Eave;

        public readonly float Verge;

        public readonly RoofCovering Covering;

        /// <summary>The covering's colour (a flat roof with eaves: its fascia's).</summary>
        public readonly Color Tint;

        /// <summary>A saltbox's front wall is carried up this far over the walls' top, metres: its second storey in front. 0 on every other roof.</summary>
        public readonly float Attic;

        public RoofShape(RoofKind kind, bool alongX, bool lowAtMin, float pitch, float eave, float verge, RoofCovering covering, Color tint,
            float attic = 0.0f)
        {
            Kind = kind;
            AlongX = alongX;
            LowAtMin = lowAtMin;
            Pitch = pitch;
            Eave = eave;
            Verge = verge;
            Covering = covering;
            Tint = tint;
            Attic = attic;
        }

        /// <summary>Sloped: nobody stands on it (no stair up to it, no plant, no billboard, no cornice).</summary>
        public bool Pitched => Kind == RoofKind.Gable || Kind == RoofKind.Hip || Kind == RoofKind.Skillion || Kind == RoofKind.Saltbox;

        /// <summary>Flat behind a parapet: the only roof with a cornice to cap it, corner piers carried up it, and a billboard over its street wall.</summary>
        public bool HasParapet => Kind == RoofKind.Flat;

        /// <summary>A level deck to stand on, behind a parapet or out to the eaves: the stair comes up through it.</summary>
        public bool IsFlat => Kind == RoofKind.Flat || Kind == RoofKind.FlatEaves;

        public static readonly RoofShape Flat = new RoofShape(RoofKind.Flat, true, true, 0.0f, 0.0f, 0.0f, RoofCovering.Metal, Color.grey);

        // --- the numbers ---

        /// <summary>A house's roof is at most this tall from eave to ridge, metres (a steep roof on a wide house is a tower).</summary>
        public const float MaxRise = 5.5f;

        /// <summary>The roof's own thickness (rafters, boarding and covering), metres.</summary>
        public const float Thickness = 0.22f;

        /// <summary>The ridge cap's width and height over the slopes, metres.</summary>
        public const float RidgeWidth = 0.34f;

        public const float RidgeHeight = 0.2f;

        /// <summary>The steepest pitch any roof gets: a 2.6 m body still walks up it (the controller's slope limit is 45 degrees).</summary>
        public const float MaxPitch = 32.0f;

        /// <summary>A house is a low building no wider than this across its ridge, nor longer than <see cref="MaxHouseLong"/> along it, metres.</summary>
        public const float MaxHouseShort = 26.0f;

        public const float MaxHouseLong = 48.0f;

        /// <summary>A saltbox's front wall stands at most this far over the others, metres (the storey it has in front and not behind: a 2.6 m body's storey).</summary>
        public const float MaxAttic = 3.6f;

        /// <summary>...and this share of the building's height at least on a low house (two storeys in front, one behind: the attic is near half the height).</summary>
        public const float AtticShare = 0.45f;

        /// <summary>A saltbox's ridge is at least this share of the roof's span from its front wall.</summary>
        public const float MinSaltboxRidgeShare = 0.2f;

        /// <summary>A flat roof's eaves stand out this far past the walls, metres: the most is ClimbJut.MaxLean, so a climb leans round them as round a cornice.</summary>
        public const float MinFlatEave = 0.7f;

        public const float MaxFlatEave = 0.9f;

        /// <summary>A flat roof's slab: how far it shows under the deck at the edge, and how far its edge stands over the deck (a gravel stop), metres.</summary>
        public const float FlatEavesThickness = 0.3f;

        public const float FlatEavesLip = 0.1f;

        private const ulong Salt = 0x50544348UL;   // "PTCH"

        /// <summary>The share of each zone's houses (low buildings) that get a pitched roof (Core, Urban, Residential, OldTown, Industrial, Outskirts).</summary>
        private static readonly double[] HouseShareByZone = { 0.16, 0.30, 0.6, 0.75, 0.0, 0.7 };

        /// <summary>Of the houses with a FLAT roof, the share whose roof is a slab with eaves and no parapet (the mid-century house), by zone.</summary>
        private static readonly double[] FlatEavesHouseShareByZone = { 0.25, 0.25, 0.30, 0.10, 0.15, 0.30 };

        /// <summary>...and of the mid-rise blocks (a 1960s office or walk-up with an overhang), by zone.</summary>
        private static readonly double[] FlatEavesMidRiseShareByZone = { 0.08, 0.08, 0.06, 0.03, 0.0, 0.06 };

        /// <summary>Of the flat-roofed warehouses, the share with a low overhang and a gutter instead of a parapet.</summary>
        public const double FlatEavesWarehouseShare = 0.25;

        /// <summary>The share of warehouses that get a pitched roof (a low metal gable, a monopitch shed, or a shed with a lean-to).</summary>
        public const double WarehouseShare = 0.45;

        /// <summary>A pitched house roof's kind, cumulative shares of gable, hip, skillion, saltbox: in the old town, downtown (Core, Urban), and the homes.</summary>
        private static readonly double[] OldTownKinds = { 0.57, 0.92, 0.96, 1.0 };

        private static readonly double[] DowntownKinds = { 0.49, 0.79, 0.99, 1.0 };

        private static readonly double[] HomesKinds = { 0.58, 0.90, 0.98, 1.0 };

        /// <summary>A pitched warehouse roof's kind, cumulative: the low gable, a monopitch shed, a shed with a lean-to (saltbox).</summary>
        private static readonly double[] WarehouseKinds = { 0.60, 0.90, 1.0 };

        // Tiles: silver-grey and blue-black glazed (kawara), terracotta, brown.
        private static readonly Color[] TileTints =
        {
            new Color(0.42f, 0.44f, 0.47f), new Color(0.18f, 0.20f, 0.25f), new Color(0.60f, 0.30f, 0.19f),
            new Color(0.50f, 0.25f, 0.17f), new Color(0.38f, 0.27f, 0.21f), new Color(0.30f, 0.32f, 0.35f),
        };

        // Metal: dark grey, navy, green, red-brown, silver, teal.
        private static readonly Color[] MetalTints =
        {
            new Color(0.22f, 0.23f, 0.25f), new Color(0.18f, 0.24f, 0.38f), new Color(0.22f, 0.36f, 0.30f),
            new Color(0.46f, 0.18f, 0.14f), new Color(0.60f, 0.62f, 0.64f), new Color(0.24f, 0.42f, 0.43f),
        };

        // A warehouse's: silver, pale grey, blue, green.
        private static readonly Color[] ShedTints =
        {
            new Color(0.62f, 0.64f, 0.66f), new Color(0.74f, 0.75f, 0.74f), new Color(0.26f, 0.36f, 0.52f), new Color(0.30f, 0.45f, 0.36f),
        };

        // A flat roof's fascia: white, cream, dark brown, charcoal, pale grey.
        private static readonly Color[] FasciaTints =
        {
            new Color(0.90f, 0.89f, 0.86f), new Color(0.82f, 0.80f, 0.74f), new Color(0.30f, 0.24f, 0.20f),
            new Color(0.22f, 0.23f, 0.25f), new Color(0.55f, 0.57f, 0.60f),
        };

        /// <summary>
        /// False makes every roof flat, as before pitched roofs: the "before" of the comparison renders (RoofShapeCapture). The chunk builders read
        /// it on their own threads, so it is changed only between rebuilds, and never in a game (every machine must build the same city).
        /// </summary>
        public static bool PitchedRoofsOn = true;

        /// <summary>The roof a building gets. <see cref="Flat"/> for most; see the class comment for which can have another.</summary>
        public static RoofShape For(BuildingSite site)
        {
            if (!PitchedRoofsOn || !site.Exists || site.Params.IsSpecial || site.Shell || site.OnRoof || site.Wing || site.Sides >= 3 || site.HasRoofObstacle
                || site.RoofSealed)
            {
                return Flat;
            }
            // What stands against it (a terrace's neighbours, the leg of an L): allowed on two opposite walls only, which are then a gable's ends,
            // flush with no verge -- the roof of a row house. Against walls both ways round, it keeps a flat roof.
            bool againstEnds = false, againstSides = false;   // something on the west or east wall / on the south or north wall
            if (site.Contacts != null)
            {
                for (int i = 0; i < site.Contacts.Length; i++)
                {
                    WindowGrid.Wall wall = site.Contacts[i].Wall;
                    if (wall == WindowGrid.Wall.West || wall == WindowGrid.Wall.East)
                    {
                        againstEnds = true;
                    }
                    else
                    {
                        againstSides = true;
                    }
                }
            }
            if (againstEnds && againstSides)
            {
                return Flat;
            }
            bool terraced = againstEnds || againstSides;
            float width = site.Params.Width, depth = site.Params.Depth, height = site.Params.Height;
            // The ridge runs into the walls something stands against; on a building that stands free, along its long side.
            bool alongX = terraced ? againstEnds : width >= depth;
            float along = alongX ? width : depth, across = alongX ? depth : width;
            float verge = terraced ? 0.0f : 0.3f;
            ulong seed = unchecked((ulong)(uint)site.Seed);
            double D(int i) => WorldHash.Unit01(i, 0, Salt, seed);
            int zone = Mathf.Clamp((int)site.Zone, 0, HouseShareByZone.Length - 1);

            if (site.Params.Mass == BuildingMass.Warehouse)
            {
                if (D(0) >= WarehouseShare)
                {
                    return D(7) < FlatEavesWarehouseShare ? FlatEaves(D) : Flat;
                }
                Color shedTint = ShedTints[Pick(D(5), ShedTints.Length)];
                double w = D(1);
                if (!terraced && w >= WarehouseKinds[1])
                {
                    // A shed with a lean-to along its back: the long slope of a saltbox, low.
                    float atticShed = Mathf.Min(2.5f, 0.3f * height);
                    float saltPitch = SaltboxPitch(Mathf.Lerp(8.0f, 12.0f, (float)D(2)), across, ref atticShed);
                    return new RoofShape(RoofKind.Saltbox, alongX, BackAtMin(alongX, D(6)), saltPitch, 0.35f, verge, RoofCovering.Metal, shedTint, atticShed);
                }
                if (!terraced && w >= WarehouseKinds[0])
                {
                    // A monopitch shed.
                    return new RoofShape(RoofKind.Skillion, alongX, D(6) < 0.6, ClampPitch(Mathf.Lerp(5.0f, 8.0f, (float)D(2)), across), 0.35f, verge,
                        RoofCovering.Metal, shedTint);
                }
                float shedPitch = Mathf.Lerp(6.0f, 10.0f, (float)D(2));
                return new RoofShape(RoofKind.Gable, alongX, true, ClampPitch(shedPitch, across * 0.5f), 0.35f, verge, RoofCovering.Metal, shedTint);
            }

            if (site.Params.Mass == BuildingMass.MidRise)
            {
                return D(7) < FlatEavesMidRiseShareByZone[zone] ? FlatEaves(D) : Flat;
            }
            if (site.Params.Mass != BuildingMass.LowRise)
            {
                return Flat;   // a slab, a tower: flat behind a parapet (a tower's top is TowerTop's)
            }
            bool house = across <= MaxHouseShort && along <= MaxHouseLong;
            if (!house || D(0) >= HouseShareByZone[zone])
            {
                return D(7) < FlatEavesHouseShareByZone[zone] ? FlatEaves(D) : Flat;
            }
            bool oldTown = site.Zone == ZoneKind.OldTown;
            bool downtown = site.Zone == ZoneKind.Core || site.Zone == ZoneKind.Urban;
            double[] kinds = oldTown ? OldTownKinds : (downtown ? DowntownKinds : HomesKinds);
            double k = D(1);
            RoofKind kind = terraced || k < kinds[0] ? RoofKind.Gable : (k < kinds[1] ? RoofKind.Hip : (k < kinds[2] ? RoofKind.Skillion : RoofKind.Saltbox));
            double tileShare = oldTown ? 0.85 : (downtown ? 0.4 : 0.5);
            bool tile = kind != RoofKind.Skillion && D(3) < tileShare;   // a skillion's slope is too shallow for tiles: sheet metal
            Color tint = tile ? TileTints[Pick(D(5), TileTints.Length)] : MetalTints[Pick(D(5), MetalTints.Length)];
            float eave = Mathf.Lerp(0.45f, 0.6f, (float)D(4));
            if (kind == RoofKind.Saltbox)
            {
                float attic = Mathf.Min(MaxAttic, AtticShare * height);
                float saltPitch = SaltboxPitch(Mathf.Lerp(26.0f, MaxPitch, (float)D(2)), across, ref attic);
                return new RoofShape(kind, alongX, BackAtMin(alongX, D(6)), saltPitch, eave, verge, tile ? RoofCovering.Tile : RoofCovering.Metal, tint, attic);
            }
            float pitch = kind == RoofKind.Skillion ? Mathf.Lerp(6.0f, 12.0f, (float)D(2))
                : (kind == RoofKind.Hip ? Mathf.Lerp(20.0f, 28.0f, (float)D(2)) : Mathf.Lerp(22.0f, MaxPitch, (float)D(2)));
            // A skillion's rise is over the whole span, a gable's and a hip's over half of it.
            float span = kind == RoofKind.Skillion ? across : across * 0.5f;
            pitch = ClampPitch(pitch, span);
            return new RoofShape(kind, alongX, D(6) < 0.6, pitch, eave, verge, tile ? RoofCovering.Tile : RoofCovering.Metal, tint);
        }

        /// <summary>A flat roof with eaves: a slab 0.7-0.9 m out past the walls, its fascia in one of five colours.</summary>
        private static RoofShape FlatEaves(System.Func<int, double> D)
        {
            return new RoofShape(RoofKind.FlatEaves, true, true, 0.0f, Mathf.Lerp(MinFlatEave, MaxFlatEave, (float)D(4)), 0.0f, RoofCovering.Metal,
                FasciaTints[Pick(D(5), FasciaTints.Length)]);
        }

        /// <summary>
        /// Which side a saltbox's long back slope falls to. The street door is in the north wall (+z), so with the ridge along x the back is the
        /// south (-z, the min side); with the ridge along z both slopes face a side street, and a die picks.
        /// </summary>
        private static bool BackAtMin(bool alongX, double die)
        {
            return alongX || die < 0.5;
        }

        /// <summary>
        /// A saltbox's front pitch, lowered so its ridge stays under <see cref="MaxRise"/> over the walls (the ridge rises the attic and the front
        /// slope's run), and its attic lowered so the ridge stays at least <see cref="MinSaltboxRidgeShare"/> of the span from the front wall.
        /// </summary>
        private static float SaltboxPitch(float pitch, float across, ref float attic)
        {
            pitch = Mathf.Min(pitch, MaxPitch);
            // The ridge's height over the walls is attic / 2 + (across / 2) tan p (see RiseOver): keep it under MaxRise.
            float steepest = Mathf.Atan(Mathf.Max(0.05f, MaxRise - attic * 0.5f) / Mathf.Max(0.1f, across * 0.5f)) * Mathf.Rad2Deg;
            pitch = Mathf.Max(5.0f, Mathf.Min(pitch, steepest));
            float t = Mathf.Tan(pitch * Mathf.Deg2Rad);
            attic = Mathf.Min(attic, (1.0f - 2.0f * MinSaltboxRidgeShare) * across * t);
            return pitch;
        }

        /// <summary>The pitch, lowered if need be so a rise over <paramref name="span"/> stays under <see cref="MaxRise"/>.</summary>
        private static float ClampPitch(float pitch, float span)
        {
            float steepest = Mathf.Atan(MaxRise / Mathf.Max(0.1f, span)) * Mathf.Rad2Deg;
            return Mathf.Min(pitch, Mathf.Min(steepest, MaxPitch));
        }

        private static int Pick(double die, int count)
        {
            return Mathf.Min(count - 1, (int)(die * count));
        }

        /// <summary>
        /// A saltbox's ridge, measured from its front (tall) wall across the roof's span: both slopes have the same pitch, so the back one, which
        /// starts a storey lower, runs further and the ridge sits forward of the middle (by the attic's height over twice the pitch's tangent).
        /// </summary>
        public float SaltboxRidgeFromFront(float across)
        {
            float t = Mathf.Tan(Pitch * Mathf.Deg2Rad);
            return Mathf.Max(MinSaltboxRidgeShare * across, across * 0.5f - Attic / (2.0f * t));
        }

        /// <summary>How far the roof rises from the walls' top to its ridge (a skillion's high side, a saltbox's ridge over its attic), metres, over a building of this size.</summary>
        public float RiseOver(float width, float depth)
        {
            if (!Pitched)
            {
                return 0.0f;
            }
            float across = AlongX ? depth : width;
            if (Kind == RoofKind.Saltbox)
            {
                return Attic + SaltboxRidgeFromFront(across) * Mathf.Tan(Pitch * Mathf.Deg2Rad);
            }
            float run = Kind == RoofKind.Skillion ? across : across * 0.5f;
            return run * Mathf.Tan(Pitch * Mathf.Deg2Rad);
        }
    }
}
