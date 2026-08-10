namespace CityX.World
{
    /// <summary>
    /// What kind of building this is, in the sense that matters to a skyline.
    ///
    /// Mass follows mostly from FOOTPRINT rather than being rolled freely,
    /// because the two are not independent in real cities and pretending they
    /// are is what produces needles. A 2x2 lot gets a tower because it is the
    /// only lot that can carry one.
    /// </summary>
    public enum BuildingMass
    {
        /// <summary>The de_maple flavour -- 7-20m, deliberately uncommon.</summary>
        LowRise,

        /// <summary>The workhorse. 28-85m on a single lot.</summary>
        MidRise,

        /// <summary>Long, thin and tall. The blank concrete face of the brutalist reference.</summary>
        Slab,

        /// <summary>110-210m on a 2x2 lot.</summary>
        Tower,

        /// <summary>Rare landmark, 240-320m. Chongqing's own top out around 470m.</summary>
        Supertall,
    }

    /// <summary>
    /// Everything about one lot's building that is decided purely by dice.
    ///
    /// Split out from geometry building for the same reason the Godot
    /// original splits it: a chunk needs to know what its NEIGHBOUR built
    /// without that neighbour existing or ever being generated. Chunks
    /// generate independently, so both ends of a skybridge have to agree
    /// about it from their own side with no shared state to consult.
    ///
    /// A struct, not a class: this gets computed for neighbours constantly
    /// during streaming, and a per-query heap allocation on a chunk-streaming
    /// path is exactly the kind of thing that produces the frame hitches the
    /// Godot build spent a task hunting down.
    /// </summary>
    public readonly struct BuildingParams
    {
        public readonly bool HasBuilding;
        public readonly float Width;
        public readonly float Height;
        public readonly float Depth;
        public readonly float OffsetX;
        public readonly float OffsetZ;
        public readonly string Roof;
        public readonly int FacadeIndex;
        public readonly string SpecialType;
        public readonly bool HasBasement;
        public readonly BuildingMass Mass;

        public static readonly BuildingParams Empty =
            new BuildingParams(false, 0, 0, 0, 0, 0, "", 0, "", false, BuildingMass.LowRise);

        public BuildingParams(bool hasBuilding, float width, float height, float depth,
            float offsetX, float offsetZ, string roof, int facadeIndex, string specialType,
            bool hasBasement, BuildingMass mass)
        {
            HasBuilding = hasBuilding;
            Width = width;
            Height = height;
            Depth = depth;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            Roof = roof;
            FacadeIndex = facadeIndex;
            SpecialType = specialType;
            HasBasement = hasBasement;
            Mass = mass;
        }

        public bool IsSpecial => !string.IsNullOrEmpty(SpecialType);

        /// <summary>
        /// Height over the narrower footprint axis. The number that decides
        /// whether something reads as a tower or as a mast.
        /// </summary>
        public float Slenderness => Height / (Width < Depth ? Width : Depth);
    }

    /// <summary>
    /// A rare landmark building type. Chances are each checked against an
    /// independent roll and deliberately do NOT sum to 1 -- whatever is left
    /// over is an ordinary generic building.
    /// </summary>
    public readonly struct SpecialBuildingPreset
    {
        public readonly string Name;
        public readonly double Chance;
        public readonly float WidthMin, WidthMax;
        public readonly float DepthMin, DepthMax;
        public readonly float HeightMin, HeightMax;
        public readonly string Roof;

        public SpecialBuildingPreset(string name, double chance, float widthMin, float widthMax,
            float depthMin, float depthMax, float heightMin, float heightMax, string roof)
        {
            Name = name;
            Chance = chance;
            WidthMin = widthMin;
            WidthMax = widthMax;
            DepthMin = depthMin;
            DepthMax = depthMax;
            HeightMin = heightMin;
            HeightMax = heightMax;
            Roof = roof;
        }
    }
}
