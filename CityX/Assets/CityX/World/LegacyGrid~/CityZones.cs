namespace CityX.World
{

    /// <summary>
    /// The city's macro-structure: WHERE the dense cores are and what each
    /// stretch of ground is used for. A pure function of a chunk coordinate.
    ///
    /// WHY IT EXISTS. A city is not a uniform random scatter of buildings.
    /// Parish and Muller's "Procedural Modeling of Cities" (SIGGRAPH 2001)
    /// drives everything -- road density, lot size, building height and use --
    /// from a handful of global maps, chiefly population density, with
    /// centres of activity that fall away toward the edge. Before this file
    /// the city rolled every block independently, so from above towers and
    /// low-rise sat salt-and-pepper across the whole map with no downtown,
    /// no edge and no neighbourhood character. Everything that decides what a
    /// block is (street spacing, massing, height, parks, roofs, landmarks)
    /// now asks this one field instead.
    ///
    /// HOW. Urban centres are a jittered lattice (one per 96x96-chunk cell,
    /// found by hash, so any process can answer for any place without
    /// generating anything). Density is the strongest centre's smooth falloff
    /// plus a base fabric plus low-frequency noise so a core is a lobed
    /// blotch rather than a compass-drawn disc. A second, independent noise
    /// picks the character (historic quarter vs industrial) inside a density
    /// band. Chunk (0,0) -- where the player spawns -- is always the middle
    /// of a core.
    ///
    /// SEED-INDEPENDENT, like CityLayout and for the same reason (see
    /// CityLayout.LocalStreetPeriodFor): every one of CityLayout's callers
    /// would otherwise need a world seed threaded through just to ask "is
    /// this a street". The seed-like input is <see cref="CityLayout.LayoutSeed"/>.
    ///
    /// DETERMINISM. Integer hashing, then double +, -, * and / only -- no
    /// transcendentals and no platform maths library -- so a Linux shard and
    /// a Windows client agree on every zone, the same rule TerrainField holds
    /// itself to.
    /// </summary>
    public static class CityZones
    {
        /// <summary>Chunks per side of the lattice cell that holds one urban centre.</summary>
        public const int CentreCellChunks = 96;

        private const ulong CentreSalt = 0x5A4E4354UL;      // "ZNCT"
        private const ulong LobeWideSalt = 0x5A4E4C57UL;    // "ZNLW"
        private const ulong LobeFineSalt = 0x5A4E4C46UL;    // "ZNLF"
        private const ulong CharacterSalt = 0x5A4E4348UL;   // "ZNCH"
        private const ulong WarpSaltX = 0x5A4E5758UL;       // "ZNWX"
        private const ulong WarpSaltY = 0x5A4E5759UL;       // "ZNWY"

        /// <summary>Peak-to-peak distance, in chunks, the domain warp can push a query point.</summary>
        private const double WarpChunks = 26.0;

        /// <summary>The density floor everywhere: the base urban fabric between cores.</summary>
        private const double BaseDensity = 0.24;

        /// <summary>How far a lobe of noise can push density up or down at the edge of a core.</summary>
        private const double LobeAmplitude = 0.42;

        public const double CoreThreshold = 0.72;
        public const double UrbanThreshold = 0.44;
        public const double ResidentialThreshold = 0.22;

        /// <summary>
        /// Density in [0, 1] at a chunk-space point (chunk coordinates as
        /// doubles, so a block can ask about its own centre). 1 is the heart
        /// of a core; near 0 is the empty edge between two urban clusters.
        /// </summary>
        public static double DensityAt(double chunkX, double chunkY)
        {
            // Domain warp: the point is nudged by a slow noise BEFORE its
            // distance to each centre is taken, so a core's edge follows a
            // wandering line instead of a compass-drawn circle.
            double warpedX = chunkX + (ValueNoise(chunkX, chunkY, 41.0, WarpSaltX) - 0.5) * WarpChunks;
            double warpedY = chunkY + (ValueNoise(chunkX, chunkY, 41.0, WarpSaltY) - 0.5) * WarpChunks;

            int cellX = FloorToInt((warpedX + CentreCellChunks * 0.5) / CentreCellChunks);
            int cellY = FloorToInt((warpedY + CentreCellChunks * 0.5) / CentreCellChunks);

            double best = 0.0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Centre(cellX + dx, cellY + dy, out double centreX, out double centreY,
                        out double strength, out double radius);
                    double ox = warpedX - centreX;
                    double oy = warpedY - centreY;
                    double squared = ox * ox + oy * oy;
                    double reach = radius * radius;
                    if (squared >= reach)
                    {
                        continue;
                    }
                    double t = 1.0 - squared / reach;
                    double falloff = strength * t * t;
                    if (falloff > best)
                    {
                        best = falloff;
                    }
                }
            }

            double lobes = ValueNoise(chunkX, chunkY, 34.0, LobeWideSalt) * 0.65
                         + ValueNoise(chunkX, chunkY, 13.0, LobeFineSalt) * 0.35;
            // Noise matters most at the shoulder of a core and least at its
            // peak, so the very centre stays a solid downtown.
            double shoulder = 1.0 - (best > 0.85 ? (best - 0.85) / 0.15 : 0.0);
            double density = BaseDensity + (1.0 - BaseDensity) * best
                           + (lobes - 0.5) * LobeAmplitude * shoulder;
            if (density < 0.0) return 0.0;
            return density > 1.0 ? 1.0 : density;
        }

        /// <summary>
        /// Land-use character in [0, 1): low leans historic, high leans
        /// industrial. Independent of density -- it is what tells an old town
        /// from a residential district at the same density.
        /// </summary>
        public static double CharacterAt(double chunkX, double chunkY)
        {
            return ValueNoise(chunkX, chunkY, 55.0, CharacterSalt);
        }

        public static ZoneKind ZoneFor(double density, double character)
        {
            if (density >= CoreThreshold)
            {
                return ZoneKind.Core;
            }
            if (density >= UrbanThreshold)
            {
                return character < 0.18 ? ZoneKind.OldTown : ZoneKind.Urban;
            }
            if (density >= ResidentialThreshold)
            {
                if (character < 0.26) return ZoneKind.OldTown;
                return character > 0.86 ? ZoneKind.Industrial : ZoneKind.Residential;
            }
            return character > 0.62 ? ZoneKind.Industrial : ZoneKind.Outskirts;
        }

        /// <summary>The zone at a chunk-space point.</summary>
        public static ZoneKind ZoneAt(double chunkX, double chunkY)
        {
            return ZoneFor(DensityAt(chunkX, chunkY), CharacterAt(chunkX, chunkY));
        }

        /// <summary>
        /// One cell's urban centre. Cell (0, 0) is pinned to the origin at full
        /// strength -- the spawn point is always downtown -- and every other
        /// cell jitters its centre inside the middle half of the cell so two
        /// neighbouring centres can never sit on top of one another.
        /// </summary>
        private static void Centre(int cellX, int cellY, out double centreX, out double centreY,
            out double strength, out double radius)
        {
            if (cellX == 0 && cellY == 0)
            {
                centreX = 0.0;
                centreY = 0.0;
                strength = 1.0;
                radius = 46.0;
                return;
            }

            double jitterX = WorldHash.Unit01(cellX, cellY, CentreSalt, CityLayout.LayoutSeed) - 0.5;
            double jitterY = WorldHash.Unit01(cellX, cellY, CentreSalt + 1UL, CityLayout.LayoutSeed) - 0.5;
            centreX = cellX * (double)CentreCellChunks + jitterX * CentreCellChunks * 0.5;
            centreY = cellY * (double)CentreCellChunks + jitterY * CentreCellChunks * 0.5;
            strength = 0.55 + 0.45 * WorldHash.Unit01(cellX, cellY, CentreSalt + 2UL, CityLayout.LayoutSeed);
            radius = 26.0 + 22.0 * WorldHash.Unit01(cellX, cellY, CentreSalt + 3UL, CityLayout.LayoutSeed);
        }

        /// <summary>
        /// Value noise in [0,1): hash the four surrounding lattice points and
        /// blend with smoothstep -- the same construction TerrainField uses,
        /// on its own salts so the zones do not follow the hills.
        /// </summary>
        private static double ValueNoise(double x, double y, double wavelength, ulong salt)
        {
            double sx = x / wavelength;
            double sy = y / wavelength;
            double fx = System.Math.Floor(sx);
            double fy = System.Math.Floor(sy);
            int x0 = (int)fx;
            int y0 = (int)fy;
            double ux = Smoothstep(sx - fx);
            double uy = Smoothstep(sy - fy);

            double v00 = WorldHash.Unit01(x0, y0, salt, CityLayout.LayoutSeed);
            double v10 = WorldHash.Unit01(x0 + 1, y0, salt, CityLayout.LayoutSeed);
            double v01 = WorldHash.Unit01(x0, y0 + 1, salt, CityLayout.LayoutSeed);
            double v11 = WorldHash.Unit01(x0 + 1, y0 + 1, salt, CityLayout.LayoutSeed);

            double a = v00 + (v10 - v00) * ux;
            double b = v01 + (v11 - v01) * ux;
            return a + (b - a) * uy;
        }

        private static double Smoothstep(double t)
        {
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>Floor, not a cast: a cast truncates toward zero and mirrors the lattice about the origin.</summary>
        private static int FloorToInt(double value)
        {
            return (int)System.Math.Floor(value);
        }
    }
}
