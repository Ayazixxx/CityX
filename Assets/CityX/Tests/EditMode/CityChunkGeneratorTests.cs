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
    /// the same -- footprints fit their lots, streets stay clear, and the same
    /// seed always rebuilds the same city.
    ///
    /// Exact-match against Godot was available and rejected: it would have
    /// pinned this project to another engine's RNG internals forever to buy a
    /// property nothing actually needs. What the game needs is Unity agreeing
    /// with Unity.
    ///
    /// ONE INVARIANT WAS DELIBERATELY INVERTED. The Godot build biased heights
    /// LOW so most buildings stayed short, and this file used to have a test
    /// enforcing it. That was correct for the old look and is exactly backwards
    /// for Chongqing. It has been replaced by
    /// <see cref="MostOfTheCityIsGenuinelyTall"/> and
    /// <see cref="MassMixMatchesTheArtDirection"/>, which enforce the new
    /// direction just as strictly rather than leaving the shape untested.
    /// </summary>
    public class CityChunkGeneratorTests
    {
        private const int Seed = 20260810;

        private static int ChunkSeed(int worldSeed, int x, int y)
        {
            return worldSeed ^ (x * 73856093) ^ (y * 19349663);
        }

        /// <summary>
        /// Walks a square of chunks and yields the buildings that actually
        /// stand on them, with the role that produced each one.
        /// </summary>
        private static List<(BuildingParams p, LotRole role, int x, int y)> Sweep(int worldSeed, int extent)
        {
            CityBlocks blocks = new CityBlocks(worldSeed);
            var found = new List<(BuildingParams, LotRole, int, int)>();
            for (int x = -extent; x <= extent; x++)
            {
                for (int y = -extent; y <= extent; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(worldSeed, x, y), role);
                    if (p.HasBuilding)
                    {
                        found.Add((p, role, x, y));
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// The buildings of the WHOLE city, sampled: a 5x5 spread of windows
        /// across ~8km. Zones are regional -- a downtown is a kilometre
        /// across, an old town a few blocks -- so a share measured in one
        /// window near the origin says almost nothing about the city.
        /// </summary>
        private static List<(BuildingParams p, LotRole role, int x, int y)> SweepCity(int worldSeed)
        {
            CityBlocks blocks = new CityBlocks(worldSeed);
            var found = new List<(BuildingParams, LotRole, int, int)>();
            const int half = 24;
            foreach (int gx in new[] { -280, -140, 0, 140, 280 })
            {
                foreach (int gy in new[] { -280, -140, 0, 140, 280 })
                {
                    for (int x = gx - half; x < gx + half; x++)
                    {
                        for (int y = gy - half; y < gy + half; y++)
                        {
                            LotRole role = blocks.RoleFor(x, y);
                            BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(worldSeed, x, y), role);
                            if (p.HasBuilding)
                            {
                                found.Add((p, role, x, y));
                            }
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// The guarantee everything else rests on: no geometry crosses the
        /// network because both sides can rebuild it.
        /// </summary>
        [Test]
        public void TheSameSeedAlwaysRebuildsTheSameLot()
        {
            CityBlocks blocks = new CityBlocks(4242);
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    int seed = ChunkSeed(4242, x, y);
                    LotRole role = blocks.RoleFor(x, y);
                    BuildingParams first = CityChunkGenerator.ParamsFor(seed, role);
                    BuildingParams second = CityChunkGenerator.ParamsFor(seed, role);

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
                    Assert.AreEqual(first.Mass, second.Mass, $"({x},{y}) Mass");
                }
            }
        }

        [Test]
        public void StreetsAndCoveredChunksNeverBuild()
        {
            CityBlocks blocks = new CityBlocks(7);
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (role.Builds)
                    {
                        continue;
                    }
                    BuildingParams p = CityChunkGenerator.ParamsFor(ChunkSeed(7, x, y), role);
                    Assert.IsFalse(p.HasBuilding,
                        $"({x},{y}) is {role.Kind} but grew a building");
                }
            }
        }

        /// <summary>
        /// A building that overflows its lot would poke into the road, and since
        /// generation is per-chunk and independent, neighbours would not know to
        /// avoid it.
        /// </summary>
        [Test]
        public void EveryBuildingFitsInsideItsLotWithMarginToSpare()
        {
            var buildings = Sweep(99, 30);
            Assert.Greater(buildings.Count, 400, "not enough buildings sampled for this to mean anything");

            foreach (var (p, role, x, y) in buildings)
            {
                CityChunkGenerator.LotRect lot = CityChunkGenerator.LotRectFor(role);

                Assert.LessOrEqual(p.Width, lot.Width + 0.001f, $"({x},{y}) is wider than its lot");
                Assert.LessOrEqual(p.Depth, lot.Depth + 0.001f, $"({x},{y}) is deeper than its lot");

                // Both offsets are measured from the span centre, so the lot
                // edges are its own centre offset plus half its width.
                float reachX = System.Math.Abs(p.OffsetX - lot.CentreOffsetX) + p.Width / 2.0f;
                float reachZ = System.Math.Abs(p.OffsetZ - lot.CentreOffsetZ) + p.Depth / 2.0f;

                Assert.LessOrEqual(reachX, lot.Width / 2.0f + 0.001f,
                    $"({x},{y}) reaches {reachX:0.00}m from its lot centre, past the " +
                    $"{lot.Width / 2.0f:0.00}m lot edge -- it would poke into the street");
                Assert.LessOrEqual(reachZ, lot.Depth / 2.0f + 0.001f,
                    $"({x},{y}) reaches {reachZ:0.00}m from its lot centre, past the " +
                    $"{lot.Depth / 2.0f:0.00}m lot edge");
            }
        }

        /// <summary>
        /// The one that guards the fix this file's asymmetric margins exist for.
        ///
        /// A single margin on all four sides puts every building on its own
        /// island. Every number is individually fine and the plan reads as a
        /// suburban grid of detached blocks. What the reference actually has is
        /// a continuous street wall with the road as the only gap -- so the
        /// distance between two neighbouring buildings INSIDE a block has to be
        /// small, and the distance across a street has to be large.
        /// </summary>
        [Test]
        public void NeighboursInsideABlockNearlyTouch()
        {
            CityBlocks blocks = new CityBlocks(4242);
            float widestPartyGap = 0.0f;
            int measured = 0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (!role.Builds)
                    {
                        continue;
                    }
                    int eastChunk = x + role.SpanX;
                    if (CityLayout.IsStreetChunk(eastChunk, y))
                    {
                        continue;   // that gap is the street, and it should be wide
                    }
                    LotRole eastRole = blocks.RoleFor(eastChunk, y);
                    if (!eastRole.Builds)
                    {
                        continue;
                    }

                    BuildingParams here = CityChunkGenerator.ParamsFor(ChunkSeed(4242, x, y), role);
                    BuildingParams there = CityChunkGenerator.ParamsFor(
                        ChunkSeed(4242, eastRole.AnchorX, eastRole.AnchorY), eastRole);
                    if (!here.HasBuilding || !there.HasBuilding || here.IsSpecial || there.IsSpecial)
                    {
                        continue;
                    }

                    double hereEdge = CityLayout.SpanCentre(x, role.SpanX) + here.OffsetX + here.Width / 2.0;
                    double thereEdge = CityLayout.SpanCentre(eastRole.AnchorX, eastRole.SpanX)
                                       + there.OffsetX - there.Width / 2.0;
                    float gap = (float)(thereEdge - hereEdge);

                    measured++;
                    Assert.GreaterOrEqual(gap, -0.001f,
                        $"({x},{y}) overlaps its eastern neighbour by {-gap:0.00}m");
                    if (gap > widestPartyGap)
                    {
                        widestPartyGap = gap;
                    }
                }
            }

            TestContext.WriteLine($"{measured} party walls measured; widest gap {widestPartyGap:0.00}m");
            Assert.Greater(measured, 200, "not enough within-block neighbours sampled");
            Assert.Less(widestPartyGap, 4.0f,
                $"neighbouring buildings inside a block sit up to {widestPartyGap:0.00}m apart -- " +
                "that is a suburban grid of islands, not a street wall");
        }

        /// <summary>
        /// A big lot must actually produce a big building.
        ///
        /// This is what makes the whole block system worth having. Rolling a
        /// footprint from a FIXED range -- which is what the code did before
        /// multi-chunk lots existed -- would let a 2x2 tower lot come out 9m
        /// wide, putting a 200m needle on a 42m plot. Filling a FRACTION of the
        /// lot makes that structurally impossible rather than merely unlikely.
        /// </summary>
        [Test]
        public void BuildingsFillTheLotTheyWereGiven()
        {
            foreach (var (p, role, x, y) in Sweep(5150, 30))
            {
                if (p.IsSpecial)
                {
                    continue;   // presets are absolute by design; see their own test
                }
                CityChunkGenerator.LotRect lot = CityChunkGenerator.LotRectFor(role);

                Assert.GreaterOrEqual(p.Width / lot.Width, CityChunkGenerator.FootprintFillMin - 0.001,
                    $"({x},{y}) fills only {p.Width / lot.Width:P0} of its lot width");
                Assert.GreaterOrEqual(p.Depth / lot.Depth, CityChunkGenerator.FootprintFillMin - 0.001,
                    $"({x},{y}) fills only {p.Depth / lot.Depth:P0} of its lot depth");
            }
        }

        /// <summary>
        /// No needles. The single number that says whether the footprint work
        /// actually did its job.
        ///
        /// The bound is 9 rather than something tighter because slabs are MEANT
        /// to be slender on their thin axis -- that long narrow concrete face is
        /// the brutalist reference -- and real supertall landmarks are slimmer
        /// still. What is being ruled out is a 200m building on a 9m plot.
        /// </summary>
        [Test]
        public void NoBuildingIsANeedle()
        {
            float worst = 0.0f;
            string worstWhere = "";

            foreach (var (p, role, x, y) in Sweep(31337, 40))
            {
                if (p.Slenderness > worst)
                {
                    worst = p.Slenderness;
                    worstWhere = $"({x},{y}) {p.Mass} {p.Width:0.0}x{p.Depth:0.0}x{p.Height:0.0}m " +
                                 $"on a {role.SpanX}x{role.SpanY} lot";
                }
            }

            TestContext.WriteLine($"most slender: {worst:0.00}:1 -- {worstWhere}");
            Assert.LessOrEqual(worst, 9.0f,
                $"a building reached {worst:0.0}:1 -- {worstWhere}. That is a mast, not a tower");
        }

        /// <summary>
        /// Every building is a whole number of floors.
        ///
        /// Three systems depend on it and each fails visibly without it: a
        /// facade texture tiled at floor height gets a sliced row at the roof,
        /// a skybridge lands mid-window, and interior slabs leave a crawlspace
        /// under the roof.
        /// </summary>
        [Test]
        public void EveryBuildingIsAWholeNumberOfFloors()
        {
            foreach (var (p, _, x, y) in Sweep(4242, 35))
            {
                double floors = p.Height / CellLayout.NominalFloorMetres;
                Assert.AreEqual(System.Math.Round(floors), floors, 1e-3,
                    $"({x},{y}) is {p.Height}m, which is {floors:0.00} floors");
            }
        }

        [Test]
        public void HeightsStayInsideTheirOwnClassRange()
        {
            foreach (var (p, _, x, y) in Sweep(5, 30))
            {
                if (p.IsSpecial)
                {
                    continue;
                }
                CityChunkGenerator.HeightRange(p.Mass, out float min, out float max);
                Assert.GreaterOrEqual(p.Height, min - 0.001f, $"({x},{y}) {p.Mass} is below its class range");
                Assert.LessOrEqual(p.Height, max + 0.001f, $"({x},{y}) {p.Mass} is above its class range");
            }
        }

        /// <summary>
        /// The replacement for the old bias-low test, enforcing the art
        /// direction the user actually asked for: mostly taller buildings, with
        /// the de_maple low-rise present but uncommon.
        /// </summary>
        [Test]
        public void MostOfTheCityIsGenuinelyTall()
        {
            var buildings = SweepCity(2024);
            Assert.Greater(buildings.Count, 800, "not enough buildings sampled");

            int tall = 0;
            int veryTall = 0;
            foreach (var (p, _, _, _) in buildings)
            {
                if (p.Height >= CityChunkGenerator.MidRiseMinHeight) tall++;
                if (p.Height >= CityChunkGenerator.SlabMinHeight) veryTall++;
            }

            double tallShare = tall / (double)buildings.Count;
            double veryTallShare = veryTall / (double)buildings.Count;
            TestContext.WriteLine($"{tallShare:P1} of buildings are 28m+, {veryTallShare:P1} are 60m+");

            Assert.Greater(tallShare, 0.70,
                $"only {tallShare:P0} of buildings reach 28m -- the city is short, which is the OLD " +
                "art direction, not this one");
            Assert.Greater(veryTallShare, 0.03,
                $"only {veryTallShare:P1} of buildings reach 60m -- nothing towers over anything");
        }

        [Test]
        public void MassMixMatchesTheArtDirection()
        {
            var buildings = SweepCity(777);
            var counts = new Dictionary<BuildingMass, int>();
            foreach (var (p, _, _, _) in buildings)
            {
                counts.TryGetValue(p.Mass, out int n);
                counts[p.Mass] = n + 1;
            }

            double Share(BuildingMass m)
            {
                counts.TryGetValue(m, out int n);
                return n / (double)buildings.Count;
            }

            foreach (BuildingMass m in System.Enum.GetValues(typeof(BuildingMass)))
            {
                TestContext.WriteLine($"{m}: {Share(m):P2}");
            }

            Assert.Greater(Share(BuildingMass.MidRise), 0.60,
                "mid-rise should be the workhorse of the city");

            double lowRise = Share(BuildingMass.LowRise);
            Assert.Greater(lowRise, 0.06,
                $"low-rise is {lowRise:P1} -- the de_maple flavour has effectively vanished");
            Assert.Less(lowRise, 0.20,
                $"low-rise is {lowRise:P1} -- the user asked for these to be the uncommon ones");

            double towers = Share(BuildingMass.Tower) + Share(BuildingMass.Supertall);
            Assert.Greater(towers, 0.02, $"towers are {towers:P1} -- the skyline has nothing in it");
            Assert.Less(towers, 0.10, $"towers are {towers:P1} -- every block cannot be a landmark");

            Assert.Greater(Share(BuildingMass.Slab), 0.02, "the brutalist slab has gone missing");

            Assert.Greater(Share(BuildingMass.Supertall), 0.0,
                "no supertall appeared at all -- the rare landmark tier is unreachable");
        }

        [Test]
        public void RoofsAndFacadesStayInsideTheirTables()
        {
            foreach (var (p, _, x, y) in Sweep(808, 25))
            {
                Assert.Contains(p.Roof, CityChunkGenerator.RoofTypes, $"({x},{y}) unknown roof '{p.Roof}'");
                Assert.GreaterOrEqual(p.FacadeIndex, 0, $"({x},{y})");
                Assert.Less(p.FacadeIndex, CityChunkGenerator.FacadeWeights.Length, $"({x},{y})");
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
            var buildings = SweepCity(2024);
            var specials = new Dictionary<string, int>();
            foreach (var (p, _, _, _) in buildings)
            {
                if (p.IsSpecial)
                {
                    specials.TryGetValue(p.SpecialType, out int count);
                    specials[p.SpecialType] = count + 1;
                }
            }

            int totalSpecial = 0;
            foreach (KeyValuePair<string, int> kvp in specials)
            {
                totalSpecial += kvp.Value;
            }
            double rate = totalSpecial / (double)buildings.Count;

            // Presets sum to 9% of single lots, and single lots are ~90% of all
            // buildings, so about 8% overall.
            Assert.Greater(rate, 0.03, $"only {rate:P1} of buildings are special -- landmarks have vanished");
            Assert.Less(rate, 0.16, $"{rate:P1} of buildings are special -- the city reads as a theme park");

            foreach (SpecialBuildingPreset preset in CityChunkGenerator.SpecialPresets)
            {
                Assert.IsTrue(specials.ContainsKey(preset.Name),
                    $"'{preset.Name}' never appeared in {buildings.Count} buildings -- it is unreachable");
            }
        }

        /// <summary>
        /// A supermarket has no business taking a tower lot -- its dimensions
        /// are absolute, so it would sit marooned in the middle of a 42m plot.
        /// </summary>
        [Test]
        public void SpecialBuildingsOnlyTakeSingleChunkLots()
        {
            foreach (var (p, role, x, y) in Sweep(1234, 40))
            {
                if (p.IsSpecial)
                {
                    Assert.IsFalse(role.IsMultiChunk,
                        $"'{p.SpecialType}' at ({x},{y}) took a {role.SpanX}x{role.SpanY} lot");
                }
            }
        }

        [Test]
        public void SpecialBuildingsUseTheirPresetDimensions()
        {
            var byName = new Dictionary<string, SpecialBuildingPreset>();
            foreach (SpecialBuildingPreset preset in CityChunkGenerator.SpecialPresets)
            {
                byName[preset.Name] = preset;
            }

            foreach (var (p, _, x, y) in Sweep(1234, 40))
            {
                if (!p.IsSpecial)
                {
                    continue;
                }
                SpecialBuildingPreset preset = byName[p.SpecialType];
                Assert.GreaterOrEqual(p.Height, preset.HeightMin - 0.001f, $"{p.SpecialType} at ({x},{y}) too short");
                Assert.LessOrEqual(p.Height, preset.HeightMax + 0.001f, $"{p.SpecialType} at ({x},{y}) too tall");
                Assert.AreEqual(preset.Roof, p.Roof, $"{p.SpecialType} at ({x},{y}) has the wrong roof");
            }
        }

        [Test]
        public void BasementsAreRare()
        {
            var buildings = Sweep(616, 50);
            int basements = 0;
            foreach (var (p, _, _, _) in buildings)
            {
                if (p.HasBasement)
                {
                    basements++;
                }
            }
            double rate = basements / (double)buildings.Count;
            Assert.Greater(rate, 0.06, $"only {rate:P1} of buildings have basements -- the feature has effectively vanished");
            Assert.Less(rate, 0.20, $"{rate:P1} of buildings have basements -- they are meant to be a rare find");
        }

        /// <summary>
        /// Roughly one lot in ten is left as a plaza. Without this the city is
        /// a solid wall of buildings with no breathing room.
        ///
        /// Counts LOTS, not chunks: a 2x2 tower lot is one lot that either
        /// builds or does not, and counting its four chunks would quietly
        /// weight big lots four times.
        /// </summary>
        [Test]
        public void AboutOneLotInTenIsLeftEmpty()
        {
            CityBlocks blocks = new CityBlocks(4321);
            int lots = 0;
            int empty = 0;
            for (int x = -50; x <= 50; x++)
            {
                for (int y = -50; y <= 50; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (!role.Builds)
                    {
                        continue;
                    }
                    lots++;
                    if (!CityChunkGenerator.ParamsFor(ChunkSeed(4321, x, y), role).HasBuilding)
                    {
                        empty++;
                    }
                }
            }
            double rate = empty / (double)lots;
            Assert.Greater(rate, 0.05, $"only {rate:P1} of lots are empty -- the city has no gaps at all");
            Assert.Less(rate, 0.18, $"{rate:P1} of lots are empty -- the city is full of holes");
        }

        /// <summary>
        /// THE SKYLINE GRADIENT. Buildings of the same class stand taller the
        /// closer they are to a core, so from above the city climbs toward its
        /// downtowns instead of being salt-and-pepper. Measured on plain
        /// mid-rise lots so the class mix cannot fake it.
        /// </summary>
        [Test]
        public void MidRisesStandTallerTowardTheCore()
        {
            var sum = new Dictionary<ZoneKind, double>();
            var count = new Dictionary<ZoneKind, int>();
            foreach (var (p, role, _, _) in SweepCity(4242))
            {
                if (p.IsSpecial || p.Mass != BuildingMass.MidRise)
                {
                    continue;
                }
                ZoneKind zone = CityChunkGenerator.ZoneOfRole(role);
                sum.TryGetValue(zone, out double s);
                count.TryGetValue(zone, out int n);
                sum[zone] = s + p.Height;
                count[zone] = n + 1;
            }

            double Mean(ZoneKind z)
            {
                Assert.Greater(count.GetValueOrDefault(z), 50, $"too few {z} mid-rises sampled");
                TestContext.WriteLine($"{z}: mean mid-rise {sum[z] / count[z]:0.0}m over {count[z]}");
                return sum[z] / count[z];
            }

            Assert.Greater(Mean(ZoneKind.Core), Mean(ZoneKind.Urban) + 5.0, "downtown is not taller than the urban ring");
            Assert.Greater(Mean(ZoneKind.Urban), Mean(ZoneKind.Residential) + 5.0, "the urban ring is not taller than the residential districts");
            Assert.Greater(Mean(ZoneKind.Residential), Mean(ZoneKind.Outskirts) + 3.0, "residential is not taller than the outskirts");
        }

        /// <summary>Sheds belong to industrial zones, and industrial zones grow nothing else wide and tall.</summary>
        [Test]
        public void WarehousesOnlyGrowInIndustrialZones()
        {
            int warehouses = 0;
            foreach (var (p, role, x, y) in SweepCity(9001))
            {
                bool industrial = CityChunkGenerator.ZoneOfRole(role) == ZoneKind.Industrial;
                if (p.Mass == BuildingMass.Warehouse)
                {
                    warehouses++;
                    Assert.IsTrue(industrial, $"a warehouse stands at ({x},{y}) outside any industrial zone");
                }
                else if (industrial && role.IsMultiChunk && !p.IsSpecial)
                {
                    Assert.Fail($"({x},{y}) is a {p.Mass} on a {role.SpanX}x{role.SpanY} industrial lot -- it should be a warehouse");
                }
            }
            Assert.Greater(warehouses, 20, "no warehouses appeared -- industrial zones are not producing sheds");
        }

        /// <summary>Supertall landmarks are a downtown thing: they never rise in a residential district or the outskirts.</summary>
        [Test]
        public void SupertallsStandOnlyInTheCoreAndUrbanRing()
        {
            int supertalls = 0;
            foreach (var (p, role, x, y) in SweepCity(31))
            {
                if (p.Mass != BuildingMass.Supertall)
                {
                    continue;
                }
                supertalls++;
                ZoneKind zone = CityChunkGenerator.ZoneOfRole(role);
                Assert.IsTrue(zone == ZoneKind.Core || zone == ZoneKind.Urban,
                    $"a supertall stands at ({x},{y}) in a {zone} zone");
            }
            Assert.Greater(supertalls, 5, "no supertalls in the whole sample");
        }

        /// <summary>Banks cluster downtown; the sample must show it.</summary>
        [Test]
        public void BanksClusterDowntown()
        {
            var lots = new Dictionary<ZoneKind, int>();
            var banks = new Dictionary<ZoneKind, int>();
            foreach (var (p, role, _, _) in SweepCity(2024))
            {
                ZoneKind zone = CityChunkGenerator.ZoneOfRole(role);
                if (role.IsMultiChunk)
                {
                    continue;
                }
                lots.TryGetValue(zone, out int l);
                lots[zone] = l + 1;
                if (p.SpecialType == "bank")
                {
                    banks.TryGetValue(zone, out int b);
                    banks[zone] = b + 1;
                }
            }

            double Rate(ZoneKind z) => lots.GetValueOrDefault(z) == 0 ? 0.0 : banks.GetValueOrDefault(z) / (double)lots[z];
            TestContext.WriteLine($"bank rate: core {Rate(ZoneKind.Core):P2}, residential {Rate(ZoneKind.Residential):P2}");
            Assert.Greater(Rate(ZoneKind.Core), Rate(ZoneKind.Residential) * 2.0,
                "banks are not concentrated downtown");
            Assert.AreEqual(0, banks.GetValueOrDefault(ZoneKind.Industrial), "a bank was built in an industrial estate");
        }

        /// <summary>
        /// A multi-chunk building can straddle a terrace step. It has to sit on
        /// the HIGHEST pad it covers -- anchoring to the lowest would bury the
        /// uphill corner, which is a hole in the world rather than a style.
        /// </summary>
        [Test]
        public void MultiChunkBuildingsSitOnTheHighestGroundTheyCover()
        {
            CityBlocks blocks = new CityBlocks(Seed);
            TerrainField terrain = new TerrainField(Seed);
            int straddling = 0;
            float biggestDrop = 0.0f;

            for (int x = -80; x <= 80; x++)
            {
                for (int y = -80; y <= 80; y++)
                {
                    LotRole role = blocks.RoleFor(x, y);
                    if (!role.Builds || !role.IsMultiChunk)
                    {
                        continue;
                    }

                    terrain.PadRangeForSpan(x, y, role.SpanX, role.SpanY,
                        out float highest, out float lowest);

                    Assert.GreaterOrEqual(highest, lowest, $"({x},{y}) pad range is inverted");
                    foreach (int dx in new[] { 0, role.SpanX - 1 })
                    {
                        foreach (int dy in new[] { 0, role.SpanY - 1 })
                        {
                            Assert.LessOrEqual(terrain.PadHeightFor(x + dx, y + dy), highest + 0.001f,
                                $"({x},{y}) would be buried at corner ({dx},{dy})");
                        }
                    }

                    if (highest > lowest)
                    {
                        straddling++;
                        float drop = highest - lowest;
                        if (drop > biggestDrop)
                        {
                            biggestDrop = drop;
                        }
                    }
                }
            }

            TestContext.WriteLine($"{straddling} multi-chunk buildings straddle a terrace step; " +
                                  $"deepest podium needed: {biggestDrop}m");
            Assert.Greater(straddling, 0,
                "no multi-chunk building anywhere straddles a step -- either the terrain is flat " +
                "or the span pads are not being sampled");
        }
    }
}
