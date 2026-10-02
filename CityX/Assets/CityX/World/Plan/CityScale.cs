using System;

namespace CityX.World.Plan
{
    /// <summary>
    /// How big the city is: the whole plan (roads, blocks, lots, the hills and rivers and the region itself) is generated at this multiple
    /// of the classic city, and the buildings grow with it.
    ///
    /// Asked (2026-10-02): "make the buildings in the city orders of magnitude larger, both wider and taller ... I want to see how this
    /// would look but still remember how the city as we have it now is configured." So the classic city is a preset (<see cref="Classic"/>, also
    /// git tag city-classic-2026-10-02) and everything else is a multiple of it. Nothing about the classic numbers was changed: Footprint 1 and
    /// Height 1 is exactly the old city, building for building.
    ///
    /// FOOTPRINT scales every length in the plan, so the layout is the classic city drawn bigger (same roads, same blocks, same ids): lots and
    /// buildings come out that much wider and deeper, the region and the terrain are that much larger. Roads widen by only its square root (a
    /// street twice as wide, not four times), so the blocks get bigger faster than the streets between them.
    /// HEIGHT scales how tall buildings are (and the thresholds that decide tower or supertall).
    ///
    /// Chosen at startup: <see cref="Active"/> defaults to <see cref="Default"/>; the environment variable CITYX_CITY_SCALE overrides it with
    /// a preset name (classic, large, huge, colossal) or "footprint,height" numbers (for example "6,4"). Tests pin it to classic.
    /// </summary>
    public readonly struct CityScale
    {
        public readonly string Name;
        public readonly double Footprint;
        public readonly double Height;

        public CityScale(string name, double footprint, double height)
        {
            Name = name;
            Footprint = footprint;
            Height = height;
        }

        /// <summary>The city as it was before scaling existed.</summary>
        public static readonly CityScale Classic = new CityScale("classic", 1.0, 1.0);

        public static readonly CityScale Large = new CityScale("large", 2.5, 2.0);
        public static readonly CityScale Huge = new CityScale("huge", 4.0, 3.0);
        public static readonly CityScale Colossal = new CityScale("colossal", 8.0, 5.0);

        /// <summary>What the game starts with unless told otherwise.</summary>
        public static readonly CityScale Default = Huge;

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

        public bool IsClassic => Footprint == 1.0 && Height == 1.0;

        /// <summary>How much wider a road is: the square root of the footprint.</summary>
        public double RoadWidth => Math.Sqrt(Footprint);

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

        public override string ToString() => $"{Name} (footprint x{Footprint:0.##}, height x{Height:0.##})";
    }
}
