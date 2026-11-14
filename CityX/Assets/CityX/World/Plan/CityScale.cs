using System;

namespace CityX.World.Plan
{
    /// <summary>
    /// How big the city is: the whole plan (roads, blocks, lots, the hills and rivers and the region itself) is generated at a multiple of the
    /// classic city, and the buildings grow with it.
    ///
    /// Asked (2026-10-02): "make the buildings in the city orders of magnitude larger ... I want to see how this would look but still remember
    /// how the city as we have it now is configured." Then: "make it so the buildings can be as small as they were before this change and as large
    /// as they can be now ... districts, so larger buildings tend to be around other larger ones, but not exclusively."
    ///
    /// So a scale is a RANGE. The classic city is footprint 1 / height 1 everywhere (<see cref="Classic"/>, also git tag city-classic-2026-10-02);
    /// <see cref="Huge"/> is 4 / 3 everywhere (what the first version of this did); <see cref="Varied"/>, the default, runs from 1 / 1 up to 4 / 3
    /// across smooth districts of a kilometre or two (<c>PlanFields.DistrictAt</c>), with every block and lot leaning a little its own way.
    ///
    /// FOOTPRINT multiplies the lengths of the plan where it applies: how far apart roads are, how big blocks and lots are, setbacks, massing.
    /// HEIGHT multiplies how tall buildings are (and the thresholds that make a tower). Terrain, rivers and the region use the largest value,
    /// and roads widen by about the square root of the middle of the range.
    ///
    /// Chosen at startup: <see cref="Active"/> defaults to <see cref="Default"/>; the environment variable CITYX_CITY_SCALE overrides it with a
    /// preset name (classic, large, huge, colossal, varied) or "footprint,height" numbers (for example "6,4"). Tests pin it to classic.
    /// </summary>
    public readonly struct CityScale
    {
        public readonly string Name;

        /// <summary>The footprint multiple at the biggest districts (and everywhere, when the scale is not varied).</summary>
        public readonly double Footprint;

        /// <summary>The height multiple at the biggest districts.</summary>
        public readonly double Height;

        /// <summary>The footprint multiple at the smallest districts (equal to <see cref="Footprint"/> when the scale is not varied).</summary>
        public readonly double MinFootprint;

        public readonly double MinHeight;

        public CityScale(string name, double footprint, double height)
            : this(name, footprint, height, footprint, height)
        {
        }

        public CityScale(string name, double footprint, double height, double minFootprint, double minHeight)
        {
            Name = name;
            Footprint = footprint;
            Height = height;
            MinFootprint = minFootprint;
            MinHeight = minHeight;
        }

        /// <summary>The city as it was before scaling existed.</summary>
        public static readonly CityScale Classic = new CityScale("classic", 1.0, 1.0);

        public static readonly CityScale Large = new CityScale("large", 2.5, 2.0);
        public static readonly CityScale Huge = new CityScale("huge", 4.0, 3.0);
        public static readonly CityScale Colossal = new CityScale("colossal", 8.0, 5.0);

        /// <summary>Districts from the classic size up to the huge: small buildings near small buildings, large near large, never exclusively.</summary>
        public static readonly CityScale Varied = new CityScale("varied", 4.0, 3.0, 1.0, 1.0);

        /// <summary>What the game starts with unless told otherwise.</summary>
        public static readonly CityScale Default = Varied;

        private static CityScale? _active;

        public static CityScale Active
        {
            get
            {
                if (!_active.HasValue)
                {
                    _active = FromEnvironment() ?? Default;
                }
                return _active.Value;
            }
            set => _active = value;
        }

        public bool IsClassic => Footprint == 1.0 && Height == 1.0 && MinFootprint == 1.0 && MinHeight == 1.0;

        /// <summary>Whether the size changes from district to district.</summary>
        public bool IsVaried => MinFootprint != Footprint || MinHeight != Height;

        /// <summary>How much wider a road is: the square root of the (geometric middle of the) footprint.</summary>
        public double RoadWidth => Math.Sqrt(Math.Sqrt(Footprint * MinFootprint));

        public static bool TryParse(string text, out CityScale scale)
        {
            scale = Classic;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            switch (text.Trim().ToLowerInvariant())
            {
                case "classic": scale = Classic; return true;
                case "large": scale = Large; return true;
                case "huge": scale = Huge; return true;
                case "colossal": scale = Colossal; return true;
                case "varied": scale = Varied; return true;
            }
            string[] parts = text.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double f)
                && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double h)
                && f >= 1.0 && f <= 20.0 && h >= 1.0 && h <= 20.0)
            {
                scale = new CityScale("custom", f, h);
                return true;
            }
            return false;
        }

        private static CityScale? FromEnvironment()
        {
            string text = Environment.GetEnvironmentVariable("CITYX_CITY_SCALE");
            return TryParse(text, out CityScale scale) ? scale : (CityScale?)null;
        }

        public override string ToString() => IsVaried
            ? $"{Name} (footprint x{MinFootprint:0.##} to x{Footprint:0.##}, height x{MinHeight:0.##} to x{Height:0.##})"
            : $"{Name} (footprint x{Footprint:0.##}, height x{Height:0.##})";
    }
}
