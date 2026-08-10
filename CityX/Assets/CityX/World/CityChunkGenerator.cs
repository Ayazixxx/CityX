using System;

namespace CityX.World
{
    /// <summary>
    /// Decides what stands on a given lot, from its seed alone.
    ///
    /// Ported from city_chunk.gd's <c>building_params</c>. Pure: no state, no
    /// side effects, no engine calls. Two processes with the same seed get
    /// the same city, which is the entire reason no geometry ever crosses the
    /// network.
    ///
    /// THE THING MOST LIKELY TO BE BROKEN BY A FUTURE EDIT: the ORDER of draws
    /// from the shared generator. Because the world is fully determined by
    /// the seed, inserting one extra draw here does not add a feature -- it
    /// reshuffles the width, height, roof and facade of every building in the
    /// city, everywhere, retroactively. Players' claimed buildings would move.
    ///
    /// The original solves this by giving genuinely optional rolls their own
    /// independently salted streams, and that structure is carried over
    /// deliberately rather than tidied away. See SpecialStream/BasementStream.
    /// </summary>
    public static class CityChunkGenerator
    {
        // --- lot ---
        public const double LotBuildingChance = 0.9;
        public const float LotMargin = 3.0f;
        public const float MinFootprint = 9.0f;
        public const float MaxFootprint = 18.0f;
        public const float MinHeight = 8.0f;
        public const float MaxHeight = 96.0f;
        public const float MaxJitter = 1.5f;
        public const double BasementChance = 0.12;

        /// <summary>Flat ("") is weighted heaviest -- most rooftops should not have a prop.</summary>
        public static readonly string[] RoofTypes =
        {
            "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower"
        };

        /// <summary>Relative weights of the facade colour variants; index only, colours live in the renderer.</summary>
        public static readonly double[] FacadeWeights = { 3.0, 3.0, 3.0, 2.0, 1.5, 0.75 };

        /// <summary>
        /// Declaration ORDER matters: _pick_special_building_type walks these
        /// accumulating chances, so reordering them changes which type a given
        /// seed produces even though every individual chance is unchanged.
        /// </summary>
        public static readonly SpecialBuildingPreset[] SpecialPresets =
        {
            new SpecialBuildingPreset("supermarket", 0.03, 15.0f, 18.0f, 11.0f, 15.0f, 8.0f, 11.0f, ""),
            new SpecialBuildingPreset("bank", 0.025, 11.0f, 15.0f, 11.0f, 15.0f, 12.0f, 22.0f, "parapet"),
            new SpecialBuildingPreset("police", 0.02, 10.0f, 14.0f, 10.0f, 14.0f, 10.0f, 16.0f, "antenna"),
            new SpecialBuildingPreset("hospital", 0.015, 14.0f, 18.0f, 12.0f, 16.0f, 24.0f, 45.0f, ""),
        };

        /// <summary>
        /// Salts for the independent streams. Carried across from the original
        /// as the same constants so the intent stays greppable across both
        /// codebases; the numbers are ASCII for "SPEC" and "BASE".
        /// </summary>
        private const ulong SpecialStream = 0x53504543UL;
        private const ulong BasementStream = 0x42415345UL;

        /// <summary>
        /// Replays a lot's building from its seed alone -- how a chunk
        /// inspects a neighbour it will never generate.
        /// </summary>
        public static BuildingParams ParamsFor(int chunkSeed, int coordX, int coordY, float chunkSize)
        {
            // Street chunks consume NO draws at all. This is not an
            // optimisation: taking a draw here would shift every seeded
            // sequence downstream of a street chunk, and streets are 7 of
            // every 16 lots, so it would rewrite most of the city.
            if (CityLayout.IsStreetChunk(coordX, coordY))
            {
                return BuildingParams.Empty;
            }

            DeterministicRandom rng = new DeterministicRandom(unchecked((ulong)(long)chunkSeed));

            if (rng.NextDouble() >= LotBuildingChance)
            {
                // Occasionally a lot is left as a plaza or gap.
                return BuildingParams.Empty;
            }

            float maxFootprint = Math.Min(MaxFootprint, chunkSize - LotMargin * 2.0f);

            // Own stream. Rolling this from the shared generator would shift
            // every later draw, silently reshuffling every ALREADY-EXISTING
            // generic building in the world the moment this feature shipped.
            DeterministicRandom specialRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), SpecialStream);
            string specialType = PickSpecialType(specialRng, out SpecialBuildingPreset preset);

            float width;
            float depth;
            float height;
            string roof;
            int facadeIndex = 0;

            if (!string.IsNullOrEmpty(specialType))
            {
                width = Clamp((float)rng.Range(preset.WidthMin, preset.WidthMax), MinFootprint, maxFootprint);
                depth = Clamp((float)rng.Range(preset.DepthMin, preset.DepthMax), MinFootprint, maxFootprint);
                height = (float)rng.Range(preset.HeightMin, preset.HeightMax);
                roof = preset.Roof;
            }
            else
            {
                width = (float)rng.Range(MinFootprint, maxFootprint);
                depth = (float)rng.Range(MinFootprint, maxFootprint);

                // Squaring a uniform sample biases it toward 0, so most
                // buildings land in the lower half of the height range while a
                // rare few still reach near the top. A flat distribution here
                // reads as "the whole skyline got taller", not "a few real
                // standouts among mostly similar buildings".
                double t = rng.NextDouble();
                height = MinHeight + (float)(t * t) * (MaxHeight - MinHeight);

                roof = RoofTypes[rng.Below(RoofTypes.Length)];
                facadeIndex = PickFacadeIndex(rng);
            }

            // Jitter is capped per axis so it can never eat into LotMargin
            // even at maximum footprint. Buildings stay near their lot centre
            // so a block reads as lined up along the street rather than
            // scattered at random depths.
            float slackX = Math.Max(0.0f, (chunkSize / 2.0f - LotMargin) - width / 2.0f);
            float slackZ = Math.Max(0.0f, (chunkSize / 2.0f - LotMargin) - depth / 2.0f);
            float limitX = Math.Min(MaxJitter, slackX);
            float limitZ = Math.Min(MaxJitter, slackZ);
            float offsetX = (float)rng.Range(-limitX, limitX);
            float offsetZ = (float)rng.Range(-limitZ, limitZ);

            // Own stream, same reasoning as the special roll.
            DeterministicRandom basementRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), BasementStream);
            bool hasBasement = basementRng.NextDouble() < BasementChance;

            return new BuildingParams(true, width, height, depth, offsetX, offsetZ,
                roof, facadeIndex, specialType, hasBasement);
        }

        /// <summary>
        /// Walks the presets accumulating their chances. Returns "" for the
        /// overwhelming majority of lots, which are ordinary buildings.
        /// </summary>
        private static string PickSpecialType(DeterministicRandom rng, out SpecialBuildingPreset preset)
        {
            double roll = rng.NextDouble();
            double cursor = 0.0;
            for (int i = 0; i < SpecialPresets.Length; i++)
            {
                cursor += SpecialPresets[i].Chance;
                if (roll < cursor)
                {
                    preset = SpecialPresets[i];
                    return preset.Name;
                }
            }
            preset = default;
            return "";
        }

        private static int PickFacadeIndex(DeterministicRandom rng)
        {
            double total = 0.0;
            for (int i = 0; i < FacadeWeights.Length; i++)
            {
                total += FacadeWeights[i];
            }

            double roll = rng.NextDouble() * total;
            double cursor = 0.0;
            for (int i = 0; i < FacadeWeights.Length; i++)
            {
                cursor += FacadeWeights[i];
                if (roll < cursor)
                {
                    return i;
                }
            }
            // Floating-point safety net, not a normal path.
            return FacadeWeights.Length - 1;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }
    }
}
