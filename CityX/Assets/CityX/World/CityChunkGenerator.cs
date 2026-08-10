using System;

namespace CityX.World
{
    /// <summary>
    /// Decides what stands on a given lot, from its seed and its footprint.
    ///
    /// Ported from city_chunk.gd's <c>building_params</c>, then reworked for the
    /// Chongqing art direction. Pure: no state, no side effects, no engine
    /// calls. Two processes with the same seed get the same city, which is the
    /// entire reason no geometry ever crosses the network.
    ///
    /// THE THING MOST LIKELY TO BE BROKEN BY A FUTURE EDIT: the ORDER of draws
    /// from the shared generator. Because the world is fully determined by
    /// the seed, inserting one extra draw here does not add a feature -- it
    /// reshuffles the width, height, roof and facade of every building in the
    /// city, everywhere, retroactively. Players' claimed buildings would move.
    ///
    /// That is survivable TODAY only because nothing has shipped and no world
    /// has been saved. Once either is true, this file is frozen except through
    /// new salted streams. Genuinely optional rolls already get their own --
    /// see SpecialStream, MassStream, BasementStream.
    /// </summary>
    public static class CityChunkGenerator
    {
        // --- lot ---

        /// <summary>Roughly one lot in ten is left as a plaza or a gap.</summary>
        public const double LotBuildingChance = 0.9;

        /// <summary>
        /// Gap from the lot edge to the building, per side. 2m rather than the
        /// Godot build's 3m: the reference is canyons, and every metre here is
        /// a metre of sky.
        /// </summary>
        public const float LotMargin = 2.0f;

        /// <summary>
        /// How much of its lot a building fills, as a fraction.
        ///
        /// THE REASON THIS IS A FRACTION AND NOT AN ABSOLUTE RANGE. The old code
        /// rolled footprints from a fixed 9-18m range, which was fine when every
        /// lot was one chunk. With multi-chunk lots a 2x2 tower could roll a 9m
        /// footprint and become a 200m needle -- undoing the exact problem the
        /// block system was built to solve. Scaling by the lot makes a big lot
        /// produce a big building structurally, not by luck.
        /// </summary>
        public const double FootprintFillMin = 0.85;

        public const float MaxJitter = 1.5f;
        public const double BasementChance = 0.12;

        // --- mass ---

        /// <summary>
        /// Share of ORDINARY single-chunk lots that get the low-rise treatment.
        ///
        /// This is the de_maple flavour the user asked to keep as a rare
        /// accent. Single lots are about 90% of all buildings by count, so this
        /// fraction lands near 13% of the city -- present everywhere, dominant
        /// nowhere.
        /// </summary>
        public const double LowRiseShareOfSingleLots = 0.15;

        /// <summary>Share of 2x2 towers that become rare landmarks instead.</summary>
        public const double SupertallShareOfTowers = 0.06;

        public const float LowRiseMinHeight = 7.0f;
        public const float LowRiseMaxHeight = 20.0f;
        public const float MidRiseMinHeight = 28.0f;
        public const float MidRiseMaxHeight = 85.0f;
        public const float SlabMinHeight = 60.0f;
        public const float SlabMaxHeight = 130.0f;
        public const float TowerMinHeight = 110.0f;
        public const float TowerMaxHeight = 210.0f;
        public const float SupertallMinHeight = 240.0f;
        public const float SupertallMaxHeight = 320.0f;

        /// <summary>Flat ("") is weighted heaviest -- most rooftops should not have a prop.</summary>
        public static readonly string[] RoofTypes =
        {
            "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower"
        };

        /// <summary>Relative weights of the facade colour variants; index only, colours live in the renderer.</summary>
        public static readonly double[] FacadeWeights = { 3.0, 3.0, 3.0, 2.0, 1.5, 0.75 };

        /// <summary>
        /// Declaration ORDER matters: PickSpecialType walks these accumulating
        /// chances, so reordering them changes which type a given seed produces
        /// even though every individual chance is unchanged.
        ///
        /// These only roll on single-chunk lots. Their dimensions are absolute
        /// and small; a supermarket has no business taking a tower lot.
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
        /// codebases; the numbers are ASCII.
        /// </summary>
        private const ulong SpecialStream = 0x53504543UL;  // "SPEC"
        private const ulong BasementStream = 0x42415345UL; // "BASE"
        private const ulong MassStream = 0x4D415353UL;     // "MASS"

        /// <summary>
        /// Replays a lot's building from its seed and its role alone -- how a
        /// chunk inspects a neighbour it will never generate.
        ///
        /// The role carries the footprint, so a chunk covered by someone else's
        /// tower returns nothing here without needing to be told.
        /// </summary>
        public static BuildingParams ParamsFor(int chunkSeed, LotRole role)
        {
            // Streets and covered chunks consume NO draws at all. This is not
            // an optimisation: taking a draw would shift every seeded sequence
            // downstream, and streets alone are 7 of every 16 chunks.
            if (role.Kind != LotKind.Building)
            {
                return BuildingParams.Empty;
            }

            DeterministicRandom rng = new DeterministicRandom(unchecked((ulong)(long)chunkSeed));

            if (rng.NextDouble() >= LotBuildingChance)
            {
                return BuildingParams.Empty;
            }

            float maxWidth = role.SpanX * CityLayout.ChunkSize - LotMargin * 2.0f;
            float maxDepth = role.SpanY * CityLayout.ChunkSize - LotMargin * 2.0f;

            // Own stream. Rolling this from the shared generator would shift
            // every later draw, silently reshuffling every ALREADY-EXISTING
            // generic building in the world the moment this feature shipped.
            string specialType = "";
            SpecialBuildingPreset preset = default;
            if (!role.IsMultiChunk)
            {
                DeterministicRandom specialRng = new DeterministicRandom(
                    unchecked((ulong)(long)chunkSeed), SpecialStream);
                specialType = PickSpecialType(specialRng, out preset);
            }

            // Own stream, same reasoning: the mass mix is exactly the kind of
            // thing that gets retuned, and retuning it should not move every
            // building's jitter.
            DeterministicRandom massRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), MassStream);
            BuildingMass mass = MassFor(role, massRng);

            float width;
            float depth;
            float height;
            string roof;
            int facadeIndex = 0;

            if (!string.IsNullOrEmpty(specialType))
            {
                width = Clamp((float)rng.Range(preset.WidthMin, preset.WidthMax), 1.0f, maxWidth);
                depth = Clamp((float)rng.Range(preset.DepthMin, preset.DepthMax), 1.0f, maxDepth);
                height = (float)rng.Range(preset.HeightMin, preset.HeightMax);
                roof = preset.Roof;
                mass = BuildingMass.MidRise;
            }
            else
            {
                width = maxWidth * (float)rng.Range(FootprintFillMin, 1.0);
                depth = maxDepth * (float)rng.Range(FootprintFillMin, 1.0);

                // Uniform WITHIN a class, deliberately. The old code squared a
                // uniform sample to bias every building short, which was right
                // for the previous look and is exactly backwards for this one.
                // Silhouette variety now comes from the class mix -- which is
                // tied to real footprints -- rather than from one distribution
                // trying to produce both a corner shop and a skyscraper.
                HeightRange(mass, out float minHeight, out float maxHeight);
                height = (float)rng.Range(minHeight, maxHeight);

                roof = RoofTypes[rng.Below(RoofTypes.Length)];
                facadeIndex = PickFacadeIndex(rng);
            }

            // Jitter is capped per axis so it can never eat into LotMargin even
            // at maximum footprint. Buildings stay near their lot centre so a
            // block reads as lined up along the street rather than scattered.
            float slackX = Math.Max(0.0f, (role.SpanX * CityLayout.ChunkSize / 2.0f - LotMargin) - width / 2.0f);
            float slackZ = Math.Max(0.0f, (role.SpanY * CityLayout.ChunkSize / 2.0f - LotMargin) - depth / 2.0f);
            float limitX = Math.Min(MaxJitter, slackX);
            float limitZ = Math.Min(MaxJitter, slackZ);
            float offsetX = (float)rng.Range(-limitX, limitX);
            float offsetZ = (float)rng.Range(-limitZ, limitZ);

            // Own stream, same reasoning as the special roll.
            DeterministicRandom basementRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), BasementStream);
            bool hasBasement = basementRng.NextDouble() < BasementChance;

            return new BuildingParams(true, width, height, depth, offsetX, offsetZ,
                roof, facadeIndex, specialType, hasBasement, mass);
        }

        /// <summary>
        /// Mass follows the footprint. Only the single-chunk case has a genuine
        /// choice in it, and that choice is the one the user asked for -- mostly
        /// taller buildings, with the de_maple low-rise as a rarer accent.
        /// </summary>
        public static BuildingMass MassFor(LotRole role, DeterministicRandom massRng)
        {
            if (role.SpanX >= 2 && role.SpanY >= 2)
            {
                return massRng.NextDouble() < SupertallShareOfTowers
                    ? BuildingMass.Supertall
                    : BuildingMass.Tower;
            }

            if (role.IsMultiChunk)
            {
                return BuildingMass.Slab;
            }

            return massRng.NextDouble() < LowRiseShareOfSingleLots
                ? BuildingMass.LowRise
                : BuildingMass.MidRise;
        }

        public static void HeightRange(BuildingMass mass, out float min, out float max)
        {
            switch (mass)
            {
                case BuildingMass.LowRise:
                    min = LowRiseMinHeight;
                    max = LowRiseMaxHeight;
                    return;
                case BuildingMass.Slab:
                    min = SlabMinHeight;
                    max = SlabMaxHeight;
                    return;
                case BuildingMass.Tower:
                    min = TowerMinHeight;
                    max = TowerMaxHeight;
                    return;
                case BuildingMass.Supertall:
                    min = SupertallMinHeight;
                    max = SupertallMaxHeight;
                    return;
                default:
                    min = MidRiseMinHeight;
                    max = MidRiseMaxHeight;
                    return;
            }
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
