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
        /// A third, much-larger-wavelength noise sample -- not another
        /// octave of relief, but an ENVELOPE that scales the two octaves
        /// above up or down by place. EXPLICIT ASK: "not every part of the
        /// world should be on a hill, there should be some large areas of
        /// the map that are flat like multiple blocks large... make the
        /// steepness and flat parts come in clusters."
        ///
        /// ~2700m is many districts wide (CityLayout.DistrictChunks worth
        /// of chunks is 576m), so a "flat" or "hilly" patch reads as a
        /// whole region of the city, not per-block noise -- the same
        /// "wavelengths long compared to a chunk" reasoning
        /// OctaveWavelengths' own comment gives, just one level up in
        /// scale.
        /// </summary>
        private const double RegionWavelength = 2700.0;

        /// <summary>
        /// A salt distinct from every OctaveWavelengths index (which use
        /// 1, 2, ...) and from RegionFlatnessFactor's own internal choices
        /// below -- an independent lattice, not a resample of the terrain
        /// octaves at a third scale.
        /// </summary>
        private const ulong RegionSalt = 1000UL;

        /// <summary>
        /// RegionFlatnessFactor's raw [0,1) noise is remapped through
        /// Smoothstep between these two thresholds -- below
        /// RegionFlatThreshold is fully flat, above RegionHillyThreshold is
        /// fully hilly, and the gap between them is where a cluster
        /// gradually eases from one into the other rather than meeting at
        /// a hard seam. The gap is deliberately wide (0.3 of the whole
        /// [0,1) range) so the transition band itself spans a meaningful
        /// distance rather than reading as a sudden edge.
        /// </summary>
        private const double RegionFlatThreshold = 0.35;

        private const double RegionHillyThreshold = 0.65;

        /// <summary>
        /// Even a "flat" region keeps this fraction of the ordinary terrain
        /// amplitude -- a perfectly billiard-table plane reads as a bug,
        /// not a place; real flat ground still has gentle undulation.
        ///
        /// RAISED FROM 0.12: EXPLICIT BUG REPORT, "it seems there are no
        /// sloped roads now." The world's fixed spawn point (chunk (0,0),
        /// every new game) happened to land deep inside one of
        /// RegionWavelength's own flat clusters -- at 0.12 the nearest real
        /// sloped street to spawn was 266m away, easily read as "none at
        /// all" during ordinary early play. 0.4 still keeps a flat region
        /// visibly calmer than a hilly one (well under half amplitude), but
        /// pulls that same nearest slope in to roughly 60m -- see
        /// DiagnoseSlopedStreetInRealScene's own "N sloped ramps... closest
        /// is Xm away" log line, the tool this was actually measured with.
        /// Provably free to change on its own: MaxSlope's own derivation
        /// already assumes flatness=1 (fully hilly) for its worst case, so
        /// this constant does not appear in EnvelopeSlope or TerrainSlope
        /// at all -- see MaxSlope's own doc comment.
        /// </summary>
        private const double MinFlatnessFactor = 0.4;

        /// <summary>
        /// Provable upper bound on the steepness of the CONTINUOUS field, in
        /// metres of rise per metre travelled along an axis.
        ///
        /// Derivation: one octave of value noise interpolated with smoothstep
        /// has lattice values in [0,1) and s'(t) = 6t(1-t), which peaks at 1.5.
        /// So that octave's slope is at most 1.5/wavelength per unit amplitude.
        /// Summing over the octaves and scaling by the relief:
        ///
        ///     TerrainSlope = TotalRelief * 1.5 * SUM(weight_i / wavelength_i)
        ///                  = 72 * 1.5 * (0.7/384 + 0.3/144)
        ///                  = 0.421875 m/m
        ///
        /// RegionFlatnessFactor multiplies the octave sum by a value in
        /// [MinFlatnessFactor, 1] wherever it is 1 this term vanishes and
        /// TerrainSlope alone bounds the field exactly as before, but the
        /// envelope itself varies in space too (across the transition band
        /// between RegionFlatThreshold and RegionHillyThreshold), and that
        /// variation multiplies the noise's own [-0.5, 0.5) centred swing
        /// (see ContinuousHeightAt's own "centred around its own mean" step).
        /// The envelope's own worst-case slope, by the identical smoothstep
        /// derivation above, is bounded by
        ///
        ///     EnvelopeSlope = TotalRelief * 0.5 * 1.5 /
        ///                     (RegionWavelength * (RegionHillyThreshold - RegionFlatThreshold))
        ///
        /// (0.5 for the centred swing's own half-range, RegionWavelength
        /// standing in for "wavelength", and the threshold gap standing in
        /// for "how much of one lattice cell the whole transition is
        /// stretched across", since Smoothstep's own s'(t) assumes t spans
        /// a full [0,1) already). Summing the two keeps this a true,
        /// provable upper bound rather than a guess -- verified numerically
        /// by TerrainFieldTests.ContinuousFieldNeverExceedsItsDerivedSlopeBound.
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

            // Flattened AROUND ITS OWN MEAN (0.5), not toward SeaLevel --
            // scaling sum directly toward 0 would pull every flat region
            // down to sea level instead of flattening it wherever it
            // already happens to sit. A flat plateau can be high, low, or
            // anywhere in between; only its own local RELIEF shrinks.
            double flatness = RegionFlatnessFactor(worldX, worldZ);
            double centred = 0.5 + (sum - 0.5) * flatness;
            return SeaLevel + TotalRelief * centred;
        }

        /// <summary>
        /// A version of ContinuousHeightAt averaged over several samples
        /// spread along one axis, for a structure engineered to a gentler
        /// vertical profile than the raw terrain -- continuing the exact use
        /// case ContinuousHeightAt's own doc comment already names ("a road
        /// ramp, a rail viaduct"): such a structure travels in one direction
        /// and, in reality, never chases every short bump the ground makes,
        /// only the broader trend.
        ///
        /// Averaging along ONE axis rather than a 2D blur matches that a
        /// viaduct/highway only travels one way -- the cross-axis coordinate
        /// stays exact, only the along-axis neighbourhood is sampled.
        /// windowMetres should be chosen relative to OctaveWavelengths (see
        /// that field's own doc comment): a window comparable to or larger
        /// than an octave's wavelength damps that octave's contribution
        /// substantially, which is the whole point -- shorter wavelengths
        /// average toward their own local mean over the window, longer ones
        /// pass through relatively unaffected.
        /// </summary>
        public double SmoothedHeightAt(double worldX, double worldZ, bool alongX,
            double windowMetres, int sampleCount)
        {
            double sum = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                double t = sampleCount == 1 ? 0.0 : (double)i / (sampleCount - 1) - 0.5;
                double offset = t * windowMetres;
                double sx = alongX ? worldX + offset : worldX;
                double sz = alongX ? worldZ : worldZ + offset;
                sum += ContinuousHeightAt(sx, sz);
            }
            return sum / sampleCount;
        }

        /// <summary>
        /// How much of the ordinary terrain relief applies here, in
        /// [MinFlatnessFactor, 1.0] -- 1.0 is a fully hilly region, near
        /// MinFlatnessFactor is a fully flat one, smoothly blended across
        /// the transition band between RegionFlatThreshold and
        /// RegionHillyThreshold. See RegionWavelength's own doc comment for
        /// why "large multi-block clusters" falls out of just using a much
        /// longer wavelength than the terrain octaves themselves.
        ///
        /// Public for the same reason ContinuousHeightAt is: a diagnostic,
        /// a future biome system, or a capture tool that wants to know
        /// "how hilly is it here" without re-deriving this from height
        /// samples should be able to ask directly.
        /// </summary>
        public double RegionFlatnessFactor(double worldX, double worldZ)
        {
            double raw = ValueNoise(worldX, worldZ, RegionWavelength, RegionSalt);
            double t = (raw - RegionFlatThreshold) / (RegionHillyThreshold - RegionFlatThreshold);
            t = Math.Max(0.0, Math.Min(1.0, t));
            double smooth = Smoothstep(t);
            return MinFlatnessFactor + (1.0 - MinFlatnessFactor) * smooth;
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

        /// <summary>
        /// The highest and lowest pad under a multi-chunk building.
        ///
        /// A 2x2 tower can straddle a terrace step, and which of the two heights
        /// it uses is a real decision rather than a rounding one. The ground
        /// floor goes at the HIGHEST pad -- put it at the lowest and the uphill
        /// corner is buried, which is a hole in the world rather than a stylistic
        /// choice. The lowest comes back too because the gap between them is
        /// exactly the podium or plinth that fills the downhill side, and that
        /// stepped podium is a large part of what the hillside blocks in the
        /// reference photos actually are.
        /// </summary>
        public void PadRangeForSpan(int anchorX, int anchorY, int spanX, int spanY,
            out float highest, out float lowest)
        {
            highest = float.MinValue;
            lowest = float.MaxValue;
            for (int dx = 0; dx < spanX; dx++)
            {
                for (int dy = 0; dy < spanY; dy++)
                {
                    float pad = PadHeightFor(anchorX + dx, anchorY + dy);
                    if (pad > highest) highest = pad;
                    if (pad < lowest) lowest = pad;
                }
            }
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
            double terrainSlope = TotalRelief * 1.5 * sum;

            // See MaxSlope's own doc comment for the derivation -- the
            // flatness envelope varies in space too and contributes its
            // own, smaller, slope term on top of the terrain octaves'.
            double envelopeSlope = TotalRelief * 0.5 * 1.5 /
                (RegionWavelength * (RegionHillyThreshold - RegionFlatThreshold));

            return terrainSlope + envelopeSlope;
        }
    }
}
