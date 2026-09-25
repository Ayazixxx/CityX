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
        /// Setback from the lot edge where that edge faces a STREET.
        ///
        /// HISTORY, BECAUSE THIS SWUNG TOO FAR AND CAME BACK. It started at
        /// +2m. The first massing render showed roughly 1:2 between street
        /// width and building height where the reference is nearer 1:8, so it
        /// went to -5m -- buildings cantilevering over the road, 14m clear.
        /// Played, that was too cramped: there was no room for a pavement or a
        /// road, only a slot.
        ///
        /// The mistake was treating one number as the whole answer. Tightness
        /// is not the same as having nowhere to stand. It is now +3m, and the
        /// canyon feeling comes from the buildings being TALL rather than from
        /// the ground being narrow -- with a real pavement built on the street
        /// side of it (see CityMeshBuilder.SidewalkWidth).
        ///
        /// Budget across a 24m street chunk:
        ///     3m setback + 4m pavement | 10m road | 4m pavement + 3m setback
        /// </summary>
        public const float StreetMargin = 3.0f;

        /// <summary>
        /// Gap where the edge faces ANOTHER LOT. Near zero, because in a dense
        /// city neighbouring buildings share a party wall.
        ///
        /// WHY THE TWO ARE DIFFERENT, WHICH THE FIRST VERSION GOT WRONG. A
        /// single margin on all four sides puts every building on its own
        /// island with a gap around it. Rendered as a plan that reads as a
        /// suburban grid of detached blocks -- and no test caught it, because
        /// every number involved was individually fine. The reference is the
        /// opposite: buildings meet along a continuous street wall, and the
        /// only open space is the street itself. Making the street gap large
        /// and the neighbour gap tiny is what turns a grid of boxes into
        /// canyons.
        /// </summary>
        public const float PartyMargin = 0.4f;

        /// <summary>
        /// How much of its lot a building fills, as a fraction.
        ///
        /// THE REASON THIS IS A FRACTION AND NOT AN ABSOLUTE RANGE. The old code
        /// rolled footprints from a fixed 9-18m range, which was fine when every
        /// lot was one chunk. With multi-chunk lots a 2x2 tower could roll a 9m
        /// footprint and become a 200m needle -- undoing the exact problem the
        /// block system was built to solve. Scaling by the lot makes a big lot
        /// produce a big building structurally, not by luck.
        ///
        /// High, and deliberately so. Footprint variety was pulling buildings
        /// away from each other and breaking the street wall; in a dense block
        /// the variety that matters is HEIGHT, which is where this generator
        /// now puts all of it.
        /// </summary>
        public const double FootprintFillMin = 0.94;

        /// <summary>
        /// Small for the same reason. A metre and a half of jitter is enough to
        /// visibly ripple a street frontage that is supposed to read as one
        /// continuous face.
        /// </summary>
        public const float MaxJitter = 0.4f;
        public const double BasementChance = 0.12;

        // --- mass ---

        // The low-rise (de_maple flavour the user asked to keep as an accent)
        // and supertall-landmark shares are now per-zone -- see
        // LowRiseShareFor and SupertallShareFor -- and are tuned so the
        // city-wide totals stay where the art direction put them (low-rise
        // 6-20% of buildings, towers a few percent, supertalls rare).

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
        public const float WarehouseMinHeight = 9.0f;
        public const float WarehouseMaxHeight = 22.0f;

        /// <summary>Flat ("") is weighted heaviest -- most rooftops should not have a prop.</summary>
        public static readonly string[] RoofTypes =
        {
            "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower"
        };

        /// <summary>
        /// One roll picks an index 0..7; each zone reads it through its own
        /// table of the SAME roof names, so the dice are drawn exactly as
        /// before and only what a number means changes. A downtown skyline
        /// bristles with antennas and parapets; an industrial roof is sheds,
        /// chimneys and water towers; a residential one is mostly bare.
        /// </summary>
        private static readonly string[][] RoofsByZone =
        {
            /* Core        */ new[] { "antenna", "antenna", "parapet", "", "antenna", "parapet", "", "water_tower" },
            /* Urban       */ new[] { "", "parapet", "antenna", "shed", "antenna", "parapet", "", "water_tower" },
            /* Residential */ new[] { "", "", "", "shed", "antenna", "chimney", "parapet", "water_tower" },
            /* OldTown     */ new[] { "", "", "chimney", "chimney", "shed", "", "parapet", "water_tower" },
            /* Industrial  */ new[] { "shed", "shed", "chimney", "chimney", "", "water_tower", "shed", "water_tower" },
            /* Outskirts   */ new[] { "", "", "", "shed", "", "chimney", "shed", "water_tower" },
        };

        /// <summary>
        /// Lower values push a zone's buildings toward the short end of their
        /// class range, higher toward the tall end -- the skyline gradient.
        /// A downtown mid-rise is at the top of its 28-85m range and an
        /// outskirts one at the bottom, so the city visibly climbs toward its
        /// cores instead of being salt-and-pepper.
        /// </summary>
        public static double HeightBiasFor(double density)
        {
            double bias = (density - 0.15) / 0.80;
            if (bias < 0.0) return 0.0;
            return bias > 1.0 ? 1.0 : bias;
        }

        /// <summary>Share of 2x2 towers that become rare landmarks, by zone -- a supertall cluster is a downtown thing.</summary>
        public static double SupertallShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.14;
                case ZoneKind.Urban: return 0.04;
                default: return 0.0;
            }
        }

        /// <summary>Share of ORDINARY single-chunk lots that get the low-rise treatment, by zone.</summary>
        public static double LowRiseShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.03;
                case ZoneKind.Urban: return 0.08;
                case ZoneKind.Residential: return 0.15;
                case ZoneKind.OldTown: return 0.35;
                case ZoneKind.Industrial: return 0.60;
                default: return 0.25;
            }
        }

        /// <summary>
        /// How much likelier (or unlikelier) a special building is in a zone.
        /// Banks cluster downtown, supermarkets and hospitals serve the
        /// neighbourhoods, and nobody builds a bank in an industrial estate.
        /// </summary>
        private static double SpecialWeight(ZoneKind zone, string preset)
        {
            switch (preset)
            {
                case "bank":
                    switch (zone)
                    {
                        case ZoneKind.Core: return 3.0;
                        case ZoneKind.Urban: return 1.8;
                        case ZoneKind.Industrial: return 0.0;
                        default: return 0.4;
                    }
                case "supermarket":
                    switch (zone)
                    {
                        case ZoneKind.Residential: return 1.6;
                        case ZoneKind.OldTown: return 1.4;
                        case ZoneKind.Core: return 0.4;
                        case ZoneKind.Industrial: return 0.2;
                        default: return 1.0;
                    }
                case "hospital":
                    switch (zone)
                    {
                        case ZoneKind.Residential: return 1.5;
                        case ZoneKind.Urban: return 1.3;
                        case ZoneKind.Core: return 0.5;
                        case ZoneKind.Industrial: return 0.2;
                        default: return 0.8;
                    }
                default:
                    return 1.0;
            }
        }

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

            LotRect lot = LotRectFor(role);
            float maxWidth = lot.Width;
            float maxDepth = lot.Depth;

            // What kind of place this lot is in, from the block's zone; and
            // how dense, read at the lot's own centre so the skyline slopes
            // smoothly toward a core instead of stepping block by block.
            ZoneKind zone = ZoneOfRole(role);
            double density = CityZones.DensityAt(role.AnchorX + role.SpanX * 0.5,
                role.AnchorY + role.SpanY * 0.5);

            // Own stream. Rolling this from the shared generator would shift
            // every later draw, silently reshuffling every ALREADY-EXISTING
            // generic building in the world the moment this feature shipped.
            string specialType = "";
            SpecialBuildingPreset preset = default;
            if (!role.IsMultiChunk)
            {
                DeterministicRandom specialRng = new DeterministicRandom(
                    unchecked((ulong)(long)chunkSeed), SpecialStream);
                specialType = PickSpecialType(specialRng, zone, out preset);
            }

            // Own stream, same reasoning: the mass mix is exactly the kind of
            // thing that gets retuned, and retuning it should not move every
            // building's jitter.
            DeterministicRandom massRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), MassStream);
            BuildingMass mass = MassFor(role, massRng, zone);

            float width;
            float depth;
            float height;
            string roof;
            int facadeIndex = 0;

            if (!string.IsNullOrEmpty(specialType))
            {
                width = Clamp((float)rng.Range(preset.WidthMin, preset.WidthMax), 1.0f, maxWidth);
                depth = Clamp((float)rng.Range(preset.DepthMin, preset.DepthMax), 1.0f, maxDepth);
                height = SnapToWholeFloors((float)rng.Range(preset.HeightMin, preset.HeightMax),
                    preset.HeightMin, preset.HeightMax);
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
                //
                // The one height draw is blended with the zone's own density
                // bias: half luck, half where in the city this is, so heights
                // vary lot to lot but the average climbs toward a core.
                HeightRange(mass, out float minHeight, out float maxHeight);
                double lift = 0.5 * rng.NextDouble() + 0.5 * HeightBiasFor(density);
                height = SnapToWholeFloors(minHeight + (maxHeight - minHeight) * (float)lift,
                    minHeight, maxHeight);

                roof = RoofsByZone[(int)zone][rng.Below(RoofTypes.Length)];
                facadeIndex = PickFacadeIndex(rng);
            }

            // Jitter is capped by whatever slack is left inside the lot rect, so
            // it can never push a building across a pavement into the road no
            // matter what footprint was rolled.
            float slackX = Math.Max(0.0f, (lot.Width - width) / 2.0f);
            float slackZ = Math.Max(0.0f, (lot.Depth - depth) / 2.0f);
            float limitX = Math.Min(MaxJitter, slackX);
            float limitZ = Math.Min(MaxJitter, slackZ);

            // Offsets are measured from the SPAN centre, not the lot centre,
            // because that is what the renderer and the bridge layout both use.
            // The lot is off-centre inside its span whenever its two margins
            // differ -- which is the normal case now.
            float offsetX = lot.CentreOffsetX + (float)rng.Range(-limitX, limitX);
            float offsetZ = lot.CentreOffsetZ + (float)rng.Range(-limitZ, limitZ);

            // Own stream, same reasoning as the special roll.
            DeterministicRandom basementRng = new DeterministicRandom(
                unchecked((ulong)(long)chunkSeed), BasementStream);
            bool hasBasement = basementRng.NextDouble() < BasementChance;

            return new BuildingParams(true, width, height, depth, offsetX, offsetZ,
                roof, facadeIndex, specialType, hasBasement, mass);
        }

        /// <summary>
        /// A lot's buildable rectangle, after margins, expressed relative to the
        /// centre of the chunks it spans.
        /// </summary>
        public readonly struct LotRect
        {
            public readonly float Width;
            public readonly float Depth;

            /// <summary>How far the lot centre sits from the span centre.</summary>
            public readonly float CentreOffsetX;
            public readonly float CentreOffsetZ;

            public LotRect(float width, float depth, float centreOffsetX, float centreOffsetZ)
            {
                Width = width;
                Depth = depth;
                CentreOffsetX = centreOffsetX;
                CentreOffsetZ = centreOffsetZ;
            }
        }

        /// <summary>
        /// The buildable rectangle inside a lot, with a pavement-sized margin on
        /// edges that face a street and a party-wall margin on edges that face a
        /// neighbouring lot.
        ///
        /// With a street period of 4 this falls out neatly: a corner lot faces
        /// streets on two sides, an edge lot on one, and the middle lot of the
        /// 3x3 faces none at all -- it is landlocked behind the others. That is
        /// the perimeter-block pattern real dense cities have, and it comes free
        /// from the grid rather than being modelled separately.
        /// </summary>
        public static LotRect LotRectFor(LotRole role)
        {
            float west = MarginTowards(role.AnchorX - 1, role.AnchorY);
            float east = MarginTowards(role.AnchorX + role.SpanX, role.AnchorY);
            float south = MarginTowards(role.AnchorX, role.AnchorY - 1);
            float north = MarginTowards(role.AnchorX, role.AnchorY + role.SpanY);

            float width = role.SpanX * CityLayout.ChunkSize - west - east;
            float depth = role.SpanY * CityLayout.ChunkSize - south - north;

            // Span centre sits at half the span; the lot centre is pushed by
            // however much the two margins differ.
            return new LotRect(width, depth, (west - east) / 2.0f, (south - north) / 2.0f);
        }

        private static float MarginTowards(int neighbourChunkX, int neighbourChunkY)
        {
            return CityLayout.IsStreetChunk(neighbourChunkX, neighbourChunkY)
                ? StreetMargin
                : PartyMargin;
        }

        /// <summary>
        /// Clear ground immediately beyond one edge of a lot, before anything
        /// the next lot might build on.
        ///
        /// The same number <see cref="LotRectFor"/> subtracts, read from
        /// outside instead of inside. Anything wanting to put geometry PAST a
        /// building -- a wing, a canopy -- needs to know how much room there
        /// actually is, and a street edge has three metres where a party edge
        /// has less than half of one.
        /// </summary>
        public static float MarginBeyond(LotRole role, int dx, int dz)
        {
            if (dx < 0)
            {
                return MarginTowards(role.AnchorX - 1, role.AnchorY);
            }
            if (dx > 0)
            {
                return MarginTowards(role.AnchorX + role.SpanX, role.AnchorY);
            }
            if (dz < 0)
            {
                return MarginTowards(role.AnchorX, role.AnchorY - 1);
            }
            return MarginTowards(role.AnchorX, role.AnchorY + role.SpanY);
        }

        /// <summary>
        /// Mass follows the footprint. Only the single-chunk case has a genuine
        /// choice in it, and that choice is the one the user asked for -- mostly
        /// taller buildings, with the de_maple low-rise as a rarer accent.
        /// </summary>
        public static BuildingMass MassFor(LotRole role, DeterministicRandom massRng, ZoneKind zone)
        {
            if (role.SpanX >= 2 && role.SpanY >= 2)
            {
                bool landmark = massRng.NextDouble() < SupertallShareFor(zone);
                if (zone == ZoneKind.Industrial)
                {
                    return BuildingMass.Warehouse;
                }
                return landmark ? BuildingMass.Supertall : BuildingMass.Tower;
            }

            if (role.IsMultiChunk)
            {
                return zone == ZoneKind.Industrial ? BuildingMass.Warehouse : BuildingMass.Slab;
            }

            return massRng.NextDouble() < LowRiseShareFor(zone)
                ? BuildingMass.LowRise
                : BuildingMass.MidRise;
        }

        /// <summary>The zone of the block a lot stands in.</summary>
        public static ZoneKind ZoneOfRole(LotRole role)
        {
            if (!CityLayout.TryGetBlockRect(role.AnchorX, role.AnchorY,
                    out int minX, out int minY, out int width, out int height))
            {
                return ZoneKind.Residential;
            }
            return CityBlocks.ZoneOfBlock(minX, minY, width, height);
        }

        /// <summary>
        /// Rounds a height to a whole number of floors, kept inside its range.
        ///
        /// WHY IT MATTERS BEYOND TIDINESS. Three separate systems assume a
        /// building is made of whole floors, and each one goes visibly wrong
        /// without this. A facade texture tiled at floor height gets a sliced
        /// row at the roof. A skybridge lands mid-window. And every interior
        /// floor slab would leave a crawlspace at the top.
        ///
        /// Rounds rather than truncates, then steps back inside the range if
        /// rounding pushed it out -- truncating would bias every building in
        /// the city half a floor short.
        /// </summary>
        public static float SnapToWholeFloors(float height, float min, float max)
        {
            float floorHeight = CellLayout.NominalFloorMetres;
            int floors = (int)Math.Round(height / floorHeight);
            if (floors < 1)
            {
                floors = 1;
            }

            float snapped = floors * floorHeight;
            if (snapped > max && floors > 1)
            {
                snapped = (floors - 1) * floorHeight;
            }
            if (snapped < min)
            {
                snapped = (float)Math.Ceiling(min / floorHeight) * floorHeight;
            }
            return snapped;
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
                case BuildingMass.Warehouse:
                    min = WarehouseMinHeight;
                    max = WarehouseMaxHeight;
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
        private static string PickSpecialType(DeterministicRandom rng, ZoneKind zone, out SpecialBuildingPreset preset)
        {
            double roll = rng.NextDouble();
            double cursor = 0.0;
            for (int i = 0; i < SpecialPresets.Length; i++)
            {
                cursor += SpecialPresets[i].Chance * SpecialWeight(zone, SpecialPresets[i].Name);
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
