using UnityEngine;

namespace CityX.World
{
    /// <summary>The shape of a building's roof: flat (with a parapet, every roof before 2026-10-15) or one of the pitched ones.</summary>
    public enum RoofKind
    {
        Flat = 0,

        /// <summary>Two slopes meeting at a ridge along the long side, a triangular gable wall at each end (切妻).</summary>
        Gable = 1,

        /// <summary>Four slopes: two up the long sides to a shorter ridge, a triangle up each end (寄棟).</summary>
        Hip = 2,

        /// <summary>One slope across the whole roof, from a low wall to a high one (片流れ): the modern house.</summary>
        Skillion = 3,
    }

    /// <summary>What a pitched roof is covered with: tiles (the Tile wall material, at a roof tile's size) or standing-seam metal sheet.</summary>
    public enum RoofCovering
    {
        Tile = 0,
        Metal = 1,
    }

    /// <summary>
    /// A PITCHED ROOF (2026-10-15, docs/WORLD_GACHA.md phase 2). Every roof in the city was flat behind a parapet, which is a downtown's
    /// roofscape; a town's is pitched, and the owner's model town has gables (切妻), hips (寄棟), the single slope of a modern house (片流れ)
    /// and the low metal gable of a warehouse. Most low buildings out of the centre get one, a few downtown (an old house between towers).
    ///
    /// NOT WHERE ITS EAVES WOULD MEET ANOTHER BLOCK: not a special one, not a block of a shaped building, not one something stands on. What
    /// stands against it (BuildingSite.Contacts: a terrace's neighbours, the leg of an L) may stand against two opposite walls only, and those
    /// become the gable's ends, flush, with no verge: the roof of a row house. Against walls both ways round, it keeps a flat roof.
    ///
    /// Pure: from the building's seed on a salt of its own, without allocating. The mesh is CityMeshBuilder.AddPitchedRoof's (near and far).
    /// A pitched roof is nobody's to stand on, so BuildingStairs leaves out the stair up to it (the top floor's ceiling is closed), FacadeOrnament
    /// gives it no cornice (there is no parapet for one to cap) and ApartmentFront leaves the building alone.
    /// </summary>
    public readonly struct RoofShape
    {
        public readonly RoofKind Kind;

        /// <summary>True: the ridge (or a skillion's fall) runs along the building's X, its width; false: along Z, its depth.</summary>
        public readonly bool AlongX;

        /// <summary>A skillion's low side: true the -Z (or -X) wall, false the +Z (or +X) one.</summary>
        public readonly bool LowAtMin;

        /// <summary>The slope, degrees from level.</summary>
        public readonly float Pitch;

        /// <summary>How far the eaves stand out past the walls, and a gable's verge past its end walls, metres.</summary>
        public readonly float Eave;

        public readonly float Verge;

        public readonly RoofCovering Covering;

        /// <summary>The covering's colour.</summary>
        public readonly Color Tint;

        public RoofShape(RoofKind kind, bool alongX, bool lowAtMin, float pitch, float eave, float verge, RoofCovering covering, Color tint)
        {
            Kind = kind;
            AlongX = alongX;
            LowAtMin = lowAtMin;
            Pitch = pitch;
            Eave = eave;
            Verge = verge;
            Covering = covering;
            Tint = tint;
        }

        public bool Pitched => Kind != RoofKind.Flat;

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

        private const ulong Salt = 0x50544348UL;   // "PTCH"

        /// <summary>The share of each zone's houses (low buildings) that get a pitched roof (Core, Urban, Residential, OldTown, Industrial, Outskirts).</summary>
        private static readonly double[] HouseShareByZone = { 0.1, 0.25, 0.6, 0.75, 0.0, 0.7 };

        /// <summary>The share of warehouses that get a low metal gable.</summary>
        public const double WarehouseShare = 0.45;

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

        /// <summary>
        /// False makes every roof flat, as before pitched roofs: the "before" of the comparison renders (RoofShapeCapture). The chunk builders read
        /// it on their own threads, so it is changed only between rebuilds, and never in a game (every machine must build the same city).
        /// </summary>
        public static bool PitchedRoofsOn = true;

        /// <summary>The roof a building gets. <see cref="Flat"/> for most; see the class comment for which can have a pitched one.</summary>
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
            float width = site.Params.Width, depth = site.Params.Depth;
            // The ridge runs into the walls something stands against; on a building that stands free, along its long side.
            bool alongX = terraced ? againstEnds : width >= depth;
            float along = alongX ? width : depth, across = alongX ? depth : width;
            float verge = terraced ? 0.0f : 0.3f;
            ulong seed = unchecked((ulong)(uint)site.Seed);
            double D(int i) => WorldHash.Unit01(i, 0, Salt, seed);

            if (site.Params.Mass == BuildingMass.Warehouse)
            {
                if (D(0) >= WarehouseShare)
                {
                    return Flat;
                }
                float shedPitch = Mathf.Lerp(6.0f, 10.0f, (float)D(2));
                return new RoofShape(RoofKind.Gable, alongX, true, ClampPitch(shedPitch, across * 0.5f), 0.35f, verge, RoofCovering.Metal,
                    ShedTints[Pick(D(5), ShedTints.Length)]);
            }

            if (site.Params.Mass != BuildingMass.LowRise || across > MaxHouseShort || along > MaxHouseLong)
            {
                return Flat;
            }
            int zone = Mathf.Clamp((int)site.Zone, 0, HouseShareByZone.Length - 1);
            if (D(0) >= HouseShareByZone[zone])
            {
                return Flat;
            }
            bool oldTown = site.Zone == ZoneKind.OldTown;
            double k = D(1);
            RoofKind kind = terraced ? RoofKind.Gable
                : oldTown
                    ? (k < 0.6 ? RoofKind.Gable : (k < 0.95 ? RoofKind.Hip : RoofKind.Skillion))
                    : (k < 0.5 ? RoofKind.Gable : (k < 0.8 ? RoofKind.Hip : RoofKind.Skillion));
            float pitch = kind == RoofKind.Skillion ? Mathf.Lerp(6.0f, 12.0f, (float)D(2))
                : (kind == RoofKind.Hip ? Mathf.Lerp(20.0f, 28.0f, (float)D(2)) : Mathf.Lerp(22.0f, MaxPitch, (float)D(2)));
            // A skillion's rise is over the whole span, a gable's and a hip's over half of it.
            float span = kind == RoofKind.Skillion ? across : across * 0.5f;
            pitch = ClampPitch(pitch, span);

            double tileShare = oldTown ? 0.85 : (site.Zone == ZoneKind.Core || site.Zone == ZoneKind.Urban ? 0.4 : 0.5);
            bool tile = kind != RoofKind.Skillion && D(3) < tileShare;   // a skillion's slope is too shallow for tiles: sheet metal
            Color tint = tile ? TileTints[Pick(D(5), TileTints.Length)] : MetalTints[Pick(D(5), MetalTints.Length)];
            float eave = Mathf.Lerp(0.45f, 0.6f, (float)D(4));
            return new RoofShape(kind, alongX, D(6) < 0.6, pitch, eave, verge, tile ? RoofCovering.Tile : RoofCovering.Metal, tint);
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

        /// <summary>How far the roof rises from its eaves at the walls to its ridge (or a skillion's high side), metres, over a building of this size.</summary>
        public float RiseOver(float width, float depth)
        {
            if (!Pitched)
            {
                return 0.0f;
            }
            float across = AlongX ? depth : width;
            float run = Kind == RoofKind.Skillion ? across : across * 0.5f;
            return run * Mathf.Tan(Pitch * Mathf.Deg2Rad);
        }
    }
}
