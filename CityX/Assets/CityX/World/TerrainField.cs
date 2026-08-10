using System;

namespace CityX.World
{
    /// <summary>
    /// Ground height anywhere in the world, as a pure function of coordinate.
    ///
    /// WHY THIS EXISTS BEFORE ANY BUILDING GEOMETRY. Every building sits on the
    /// ground, so the ground has to be decided first or every piece of geometry
    /// written against it has to be rewritten. It is also the one property the
    /// two deferred systems -- rail routes and biomes -- both depend on, so
    /// getting it right here is what makes deferring them safe.
    ///
    /// WHY TERRACED RATHER THAN SMOOTH. The art direction is Chongqing, and the
    /// thing that actually makes Chongqing read as Chongqing is not that the
    /// buildings are tall -- it is that there is NO SINGLE GROUND LEVEL. Blocks
    /// step up hillsides on flat platforms separated by retaining walls, so the
    /// same building is entered on floor 1 from one street and floor 20 from
    /// another. Smooth rolling terrain cannot produce that; it produces
    /// buildings on slopes, which is a different city entirely.
    ///
    /// So the height field is continuous noise QUANTISED to whole terrace
    /// steps. The quantisation is not a rounding convenience -- it is the
    /// feature. Every flat platform and every retaining wall in the world is a
    /// direct consequence of it.
    ///
    /// WHY AN INSTANCE AND NOT A STATIC CLASS. The world seed has to salt the
    /// noise, and the alternative -- a static class with a settable seed -- is
    /// exactly the global mutable state that <see cref="DeterministicRandom"/>
    /// exists to avoid. Construct one per world, pass it down.
    ///
    /// DETERMINISM. Integer hashing with defined overflow, then double-precision
    /// +, -, * and / only. No transcendentals, no platform maths library, no
    /// engine calls -- so a Linux shard and a Windows client compute the same
    /// ground, which is the whole reason geometry never crosses the network.
    /// </summary>
    public sealed class TerrainField
    {
        /// <summary>Height of one terrace step, in metres.</summary>
        /// <remarks>
        /// Deliberately a whole multiple of <see cref="CellLayout.NominalFloorMetres"/>
        /// (2 floors of 3.2m). That is what makes "you enter on floor 1 here and
        /// floor 7 there" an exact statement rather than an approximate one, and
        /// it means a retaining wall always lines up with a floor slab instead of
        /// cutting one in half.
        /// </remarks>
        public const float TerraceStep = CellLayout.NominalFloorMetres * 2.0f;

        /// <summary>Lowest ground in the world. Sea level, and the origin's own floor.</summary>
        public const float SeaLevel = 0.0f;

        /// <summary>
        /// Total vertical relief between the lowest and highest ground.
        ///
        /// 72m is a little over 22 floors, chosen so the reference case -- one
        /// block entered on floor 1 and floor 22 -- is actually reachable rather
        /// than merely aspirational.
        /// </summary>
        public const float TotalRelief = 72.0f;

        /// <summary>
        /// The octaves, as (wavelength in metres, weight). Weights sum to 1 so
        /// the summed noise stays in [0,1) and the height range is provable
        /// rather than measured.
        ///
        /// BOTH WAVELENGTHS ARE LONG COMPARED TO A 24m CHUNK, ON PURPOSE. A
        /// short octave would vary meaningfully between neighbouring chunks,
        /// and since the field is quantised at chunk granularity that variation
        /// does not read as detail -- it reads as adjacent platforms flickering
        /// up and down a step at random, a checkerboard of pointless retaining
        /// walls. Terrain detail finer than a city block is detail this design
        /// cannot express, so it is not generated.
        ///
        /// ADDING AN OCTAVE INVALIDATES <see cref="MaxSlope"/>. That bound is
        /// derived from exactly these numbers; a third octave has to be put
        /// through the same derivation, and the bound test will catch it if it
        /// is not.
        /// </summary>
        private static readonly double[] OctaveWavelengths = { 384.0, 144.0 };
        private static readonly double[] OctaveWeights = { 0.7, 0.3 };

        /// <summary>
        /// Provable upper bound on the steepness of the CONTINUOUS field, in
        /// metres of rise per metre travelled along an axis.
        ///
        /// Derivation: one octave of value noise interpolated with smoothstep
        /// has lattice values in [0,1) and s'(t) = 6t(1-t), which peaks at 1.5.
        /// So that octave's slope is at most 1.5/wavelength per unit amplitude.
        /// Summing over the octaves and scaling by the relief:
        ///
        ///     MaxSlope = TotalRelief * 1.5 * SUM(weight_i / wavelength_i)
        ///              = 72 * 1.5 * (0.7/384 + 0.3/144)
        ///              = 0.421875 m/m
        ///
        /// Which is the property that matters: over one 24m chunk the ground can
        /// rise at most 10.125m, so two neighbouring platforms can differ by at
        /// most TWO terrace steps -- a 12.8m wall at the absolute worst, and
        /// almost always zero or one. Bounded, dramatic, and never a cliff of
        /// unknown height dropped between two chunks.
        ///
        /// Computed here rather than written as a literal so the comment above
        /// and the constant below cannot drift apart.
        /// </summary>
        public static readonly double MaxSlope = ComputeMaxSlope();

        /// <summary>
        /// Worst-case difference in terrace steps between two chunks that share
        /// an edge. See <see cref="MaxSlope"/> for where the number comes from.
        /// </summary>
        public static readonly int MaxNeighbourTerraceSteps =
            (int)Math.Floor(MaxSlope * CityLayout.ChunkSize / TerraceStep) + 1;

        /// <summary>Highest terrace level the field can produce.</summary>
        public static readonly int MaxTerraceLevel =
            (int)Math.Floor((double)TotalRelief / TerraceStep);

        private readonly ulong _seed;

        public TerrainField(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
        }

        /// <summary>
        /// The un-quantised height field, in metres. Guaranteed to lie in
        /// [SeaLevel, SeaLevel + TotalRelief).
        ///
        /// Public because things that follow the ground smoothly rather than
        /// standing on a platform -- a road ramp, a rail viaduct, a camera
        /// collision probe -- want the shape without the steps.
        /// </summary>
        public double ContinuousHeightAt(double worldX, double worldZ)
        {
            double sum = 0.0;
            for (int i = 0; i < OctaveWavelengths.Length; i++)
            {
                // The octave index is folded into the salt so the two octaves
                // draw from independent lattices. Without it they would be the
                // same field sampled at two scales, and their peaks would line
                // up everywhere.
                sum += OctaveWeights[i] * ValueNoise(worldX, worldZ, OctaveWavelengths[i], (ulong)(i + 1));
            }
            return SeaLevel + TotalRelief * sum;
        }

        /// <summary>
        /// Which terrace a point stands on. Level 0 is sea level.
        /// </summary>
        public int TerraceLevelAt(double worldX, double worldZ)
        {
            double height = ContinuousHeightAt(worldX, worldZ) - SeaLevel;
            return (int)Math.Floor(height / TerraceStep);
        }

        /// <summary>
        /// Ground height in metres -- the terraced one. This is what buildings,
        /// streets and the player actually stand on.
        /// </summary>
        public float HeightAt(double worldX, double worldZ)
        {
            return SeaLevel + TerraceLevelAt(worldX, worldZ) * TerraceStep;
        }

        /// <summary>
        /// The single flat height a whole chunk sits at, sampled at its centre.
        ///
        /// Sampling once per chunk rather than per-corner is what guarantees a
        /// LOT IS FLAT. A building needs one pad, not four corner heights it has
        /// to reconcile, and reconciling them is how you end up back at buildings
        /// on slopes. The step between neighbouring pads becomes the retaining
        /// wall, which is the look this is aiming for anyway.
        /// </summary>
        public float PadHeightFor(int chunkX, int chunkY)
        {
            return HeightAt(CityLayout.ChunkCentreX(chunkX), CityLayout.ChunkCentreZ(chunkY));
        }

        // --- noise ---

        /// <summary>
        /// Value noise: hash the four surrounding lattice points and blend them
        /// with smoothstep. Result in [0,1).
        /// </summary>
        private double ValueNoise(double worldX, double worldZ, double wavelength, ulong octaveSalt)
        {
            double sx = worldX / wavelength;
            double sz = worldZ / wavelength;

            // Math.Floor, NOT a cast. A cast truncates toward zero, so the
            // lattice cell for -0.5 would come out as 0 -- the same cell as
            // +0.5 -- mirroring the terrain about the origin and collapsing a
            // lattice row. This is the same sign trap CityLayout.PositiveMod
            // exists for, in its floating-point form.
            double fx0 = Math.Floor(sx);
            double fz0 = Math.Floor(sz);
            int x0 = (int)fx0;
            int z0 = (int)fz0;

            double ux = Smoothstep(sx - fx0);
            double uz = Smoothstep(sz - fz0);

            double v00 = Lattice(x0, z0, octaveSalt);
            double v10 = Lattice(x0 + 1, z0, octaveSalt);
            double v01 = Lattice(x0, z0 + 1, octaveSalt);
            double v11 = Lattice(x0 + 1, z0 + 1, octaveSalt);

            double a = v00 + (v10 - v00) * ux;
            double b = v01 + (v11 - v01) * ux;
            return a + (b - a) * uz;
        }

        /// <summary>
        /// s(t) = t^2(3 - 2t). Zero first derivative at both ends, so octaves
        /// meet at lattice lines without the visible creases straight linear
        /// interpolation leaves along every grid line.
        /// </summary>
        private static double Smoothstep(double t)
        {
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>One lattice point's value, in [0,1).</summary>
        private double Lattice(int gx, int gz, ulong octaveSalt)
        {
            return WorldHash.Unit01(gx, gz, octaveSalt, _seed);
        }

        private static double ComputeMaxSlope()
        {
            double sum = 0.0;
            for (int i = 0; i < OctaveWavelengths.Length; i++)
            {
                sum += OctaveWeights[i] / OctaveWavelengths[i];
            }
            return TotalRelief * 1.5 * sum;
        }
    }
}
