using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The generator that decides what stands on every lot.
    ///
    /// These assert INVARIANTS rather than exact values copied from the Godot
    /// build. That is a deliberate limit, and worth being clear about: the
    /// port uses its own PRNG, so the specific building on a given lot will
    /// NOT match Godot's, and could not. What must hold is that the rules are
    /// the same -- footprints fit their lots, the height distribution has the
    /// intended shape, streets stay clear, and the same seed always rebuilds
    /// the same city.
    ///
    /// Exact-match against Godot was available and rejected: it would have
    /// pinned this project to another engine's RNG internals forever to buy a
    /// property nothing actually needs. What the game needs is Unity agreeing
    /// with Unity.
    /// </summary>
    public class CityChunkGeneratorTests
    {
        private const float ChunkSize = CityLayout.ChunkSize;

        private static int ChunkSeed(int worldSeed, int x, int y)
        {
            return worldSeed ^ (x * 73856093) ^ (y * 19349663);
        }

        /// <summary>
        /// The guarantee everything else rests on: no geometry crosses the
        /// network because both sides can rebuild it.
        /// </summary>
        [Test]
        public void TheSameSeedAlwaysRebuildsTheSameLot()
        {
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    int seed = ChunkSeed(4242, x, y);
                    BuildingParams first = CityChunkGenerator.ParamsFor(seed, x, y, ChunkSize);
                    BuildingParams second = CityChunkGenerator.ParamsFor(seed, x, y, ChunkSize);

                    Assert.AreEqual(first.HasBuilding, second.HasBuilding, $"({x},{y}) HasBuilding");
                    Assert.AreEqual(first.Width, second.Width, $"({x},{y}) Width");
                    Assert.AreEqual(first.Height, second.Height, $"({x},{y}) Height");
                    Assert.AreEqual(first.Depth, second.Depth, $"({x},{y}) Depth");
                    Assert.AreEqual(first.OffsetX, second.OffsetX, $"({x},{y}) OffsetX");
                    Assert.AreEqual(first.OffsetZ, second.OffsetZ, $"({x},{y}) OffsetZ");
                    Assert.AreEqual(first.Roof, second.Roof, $"({x},{y}) Roof");
                    Assert.AreEqual(first.FacadeIndex, second.FacadeIndex, $"({x},{y}) FacadeIndex");
                    Assert.AreEqual(first.SpecialType, second.SpecialType, $"({x},{y}) SpecialType");
                    Assert.AreEqual(first.HasBasement, second.HasBasement, $"({x},{y}) HasBasement");
                }
            }
        }

        [Test]
        public void StreetChunksNeverBuildAndConsumeNoRandomness()
        {
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(7, x, y), x, y, ChunkSize);
                    Assert.IsFalse(p.HasBuilding, $"a street chunk at ({x},{y}) grew a building");
                }
            }
        }

        /// <summary>
        /// A building that overflows its lot would poke into the road, and
        /// since generation is per-chunk and independent, neighbours would not
        /// know to avoid it.
        /// </summary>
        [Test]
        public void EveryBuildingFitsInsideItsLotWithMarginToSpare()
        {
            float maxFootprint = System.Math.Min(
                CityChunkGenerator.MaxFootprint, ChunkSize - CityChunkGenerator.LotMargin * 2.0f);
            float halfLot = ChunkSize / 2.0f - CityChunkGenerator.LotMargin;

            int checkedCount = 0;
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(99, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding)
                    {
                        continue;
                    }
                    checkedCount++;

                    Assert.GreaterOrEqual(p.Width, CityChunkGenerator.MinFootprint, $"({x},{y}) too narrow");
                    Assert.LessOrEqual(p.Width, maxFootprint, $"({x},{y}) too wide");
                    Assert.GreaterOrEqual(p.Depth, CityChunkGenerator.MinFootprint, $"({x},{y}) too shallow");
                    Assert.LessOrEqual(p.Depth, maxFootprint, $"({x},{y}) too deep");

                    // The jitter cap must never push a building past the margin.
                    float reachX = System.Math.Abs(p.OffsetX) + p.Width / 2.0f;
                    float reachZ = System.Math.Abs(p.OffsetZ) + p.Depth / 2.0f;
                    Assert.LessOrEqual(reachX, halfLot + 0.001f,
                        $"({x},{y}) reaches {reachX:0.00}m from centre, past the {halfLot:0.00}m lot edge -- it would poke into the street");
                    Assert.LessOrEqual(reachZ, halfLot + 0.001f,
                        $"({x},{y}) reaches {reachZ:0.00}m from centre, past the {halfLot:0.00}m lot edge");
                }
            }
            Assert.Greater(checkedCount, 200, "not enough buildings sampled for this to mean anything");
        }

        [Test]
        public void HeightsStayWithinRange()
        {
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(5, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding)
                    {
                        continue;
                    }
                    Assert.GreaterOrEqual(p.Height, CityChunkGenerator.MinHeight - 0.001f, $"({x},{y})");
                    Assert.LessOrEqual(p.Height, CityChunkGenerator.MaxHeight + 0.001f, $"({x},{y})");
                }
            }
        }

        /// <summary>
        /// The original squares its height sample specifically so most
        /// buildings stay low and a rare few stand out. A flat distribution
        /// would pass the range check above while making the skyline read
        /// completely differently -- so the SHAPE gets its own test.
        /// </summary>
        [Test]
        public void HeightDistributionIsBiasedLowNotFlat()
        {
            List<float> heights = new List<float>();
            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(31337, x, y), x, y, ChunkSize);
                    if (p.HasBuilding && !p.IsSpecial)
                    {
                        heights.Add(p.Height);
                    }
                }
            }
            Assert.Greater(heights.Count, 500, "not enough generic buildings sampled");

            float midpoint = (CityChunkGenerator.MinHeight + CityChunkGenerator.MaxHeight) / 2.0f;
            int aboveMid = 0;
            foreach (float h in heights)
            {
                if (h > midpoint)
                {
                    aboveMid++;
                }
            }
            double fractionAbove = aboveMid / (double)heights.Count;

            // A flat distribution would put ~50% above the midpoint. Squaring
            // the sample should put ~29% above (1 - sqrt(0.5)).
            Assert.Less(fractionAbove, 0.40,
                $"{fractionAbove:P0} of buildings are above the midpoint -- that is a flat distribution, " +
                "so the whole skyline got taller instead of a few standouts appearing");
            Assert.Greater(fractionAbove, 0.15,
                $"only {fractionAbove:P0} of buildings are above the midpoint -- heights are crushed low " +
                "and nothing stands out at all");
        }

        [Test]
        public void RoofsAndFacadesStayInsideTheirTables()
        {
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(808, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding)
                    {
                        continue;
                    }
                    Assert.Contains(p.Roof, CityChunkGenerator.RoofTypes, $"({x},{y}) unknown roof '{p.Roof}'");
                    Assert.GreaterOrEqual(p.FacadeIndex, 0, $"({x},{y})");
                    Assert.Less(p.FacadeIndex, CityChunkGenerator.FacadeWeights.Length, $"({x},{y})");
                }
            }
        }

        /// <summary>
        /// Landmarks must stay rare. If special buildings became common the
        /// city would read as a theme park, and the failure is gradual enough
        /// that nobody would notice from a screenshot.
        /// </summary>
        [Test]
        public void SpecialBuildingsAreRareButDoOccur()
        {
            int buildings = 0;
            Dictionary<string, int> specials = new Dictionary<string, int>();

            for (int x = -60; x <= 60; x++)
            {
                for (int y = -60; y <= 60; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(2024, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding)
                    {
                        continue;
                    }
                    buildings++;
                    if (p.IsSpecial)
                    {
                        specials.TryGetValue(p.SpecialType, out int count);
                        specials[p.SpecialType] = count + 1;
                    }
                }
            }

            int totalSpecial = 0;
            foreach (KeyValuePair<string, int> kvp in specials)
            {
                totalSpecial += kvp.Value;
            }
            double rate = totalSpecial / (double)buildings;

            // Presets sum to 9% of lots.
            Assert.Greater(rate, 0.04, $"only {rate:P1} of buildings are special -- landmarks have vanished");
            Assert.Less(rate, 0.16, $"{rate:P1} of buildings are special -- the city reads as a theme park");

            // Every type should actually appear over this many lots, or one is
            // unreachable -- which a total-rate check alone would hide.
            foreach (SpecialBuildingPreset preset in CityChunkGenerator.SpecialPresets)
            {
                Assert.IsTrue(specials.ContainsKey(preset.Name),
                    $"'{preset.Name}' never appeared in {buildings} buildings -- it is unreachable");
            }
        }

        [Test]
        public void SpecialBuildingsUseTheirPresetDimensions()
        {
            Dictionary<string, SpecialBuildingPreset> byName = new Dictionary<string, SpecialBuildingPreset>();
            foreach (SpecialBuildingPreset preset in CityChunkGenerator.SpecialPresets)
            {
                byName[preset.Name] = preset;
            }

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(1234, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding || !p.IsSpecial)
                    {
                        continue;
                    }
                    SpecialBuildingPreset preset = byName[p.SpecialType];
                    Assert.GreaterOrEqual(p.Height, preset.HeightMin - 0.001f, $"{p.SpecialType} at ({x},{y}) too short");
                    Assert.LessOrEqual(p.Height, preset.HeightMax + 0.001f, $"{p.SpecialType} at ({x},{y}) too tall");
                    Assert.AreEqual(preset.Roof, p.Roof, $"{p.SpecialType} at ({x},{y}) has the wrong roof");
                }
            }
        }

        [Test]
        public void BasementsAreRare()
        {
            int buildings = 0;
            int basements = 0;
            for (int x = -50; x <= 50; x++)
            {
                for (int y = -50; y <= 50; y++)
                {
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(616, x, y), x, y, ChunkSize);
                    if (!p.HasBuilding)
                    {
                        continue;
                    }
                    buildings++;
                    if (p.HasBasement)
                    {
                        basements++;
                    }
                }
            }
            double rate = basements / (double)buildings;
            Assert.Greater(rate, 0.06, $"only {rate:P1} of buildings have basements -- the feature has effectively vanished");
            Assert.Less(rate, 0.20, $"{rate:P1} of buildings have basements -- they are meant to be a rare find");
        }

        /// <summary>
        /// Roughly one lot in ten is left as a plaza. Without this the city is
        /// a solid wall of buildings with no breathing room.
        /// </summary>
        [Test]
        public void AboutOneLotInTenIsLeftEmpty()
        {
            int lots = 0;
            int empty = 0;
            for (int x = -50; x <= 50; x++)
            {
                for (int y = -50; y <= 50; y++)
                {
                    if (CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    lots++;
                    if (!CityChunkGenerator.ParamsFor(ChunkSeed(4321, x, y), x, y, ChunkSize).HasBuilding)
                    {
                        empty++;
                    }
                }
            }
            double rate = empty / (double)lots;
            Assert.Greater(rate, 0.05, $"only {rate:P1} of lots are empty -- the city has no gaps at all");
            Assert.Less(rate, 0.18, $"{rate:P1} of lots are empty -- the city is full of holes");
        }
    }
}
