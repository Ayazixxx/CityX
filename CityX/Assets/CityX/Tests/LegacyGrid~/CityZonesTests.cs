using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The zone map: where the dense cores are and what each stretch of city
    /// is used for. Everything that shapes a block (street spacing, massing,
    /// height, parks, roofs, landmarks) asks it, so it has to be a pure,
    /// smooth, well-spread function -- and it has to put the player
    /// downtown, since (0,0) is where every new game starts.
    /// </summary>
    public class CityZonesTests
    {
        [Test]
        public void DensityStaysInsideZeroToOne()
        {
            for (int x = -800; x <= 800; x += 3)
            {
                for (int y = -800; y <= 800; y += 3)
                {
                    double d = CityZones.DensityAt(x + 0.5, y + 0.5);
                    Assert.GreaterOrEqual(d, 0.0, $"({x},{y})");
                    Assert.LessOrEqual(d, 1.0, $"({x},{y})");
                    double c = CityZones.CharacterAt(x + 0.5, y + 0.5);
                    Assert.GreaterOrEqual(c, 0.0);
                    Assert.Less(c, 1.0);
                }
            }
        }

        [Test]
        public void TheSpawnPointIsDowntown()
        {
            Assert.AreEqual(ZoneKind.Core, CityZones.ZoneAt(0.5, 0.5), "a new game must start in a downtown core");
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Assert.AreEqual(ZoneKind.Core, CityZones.ZoneAt(x + 0.5, y + 0.5),
                        $"({x},{y}) is not core -- the spawn area should be solidly downtown");
                }
            }
        }

        [Test]
        public void TheSameQuestionAlwaysGetsTheSameAnswer()
        {
            var first = new List<double>();
            for (int x = -50; x < 50; x++)
            {
                for (int y = -50; y < 50; y++)
                {
                    first.Add(CityZones.DensityAt(x * 1.7, y * 2.3));
                }
            }

            // Ask in a different order after touching unrelated places.
            CityZones.DensityAt(123456.0, -98765.0);
            int index = first.Count - 1;
            for (int x = 49; x >= -50; x--)
            {
                for (int y = 49; y >= -50; y--)
                {
                    Assert.AreEqual(first[index], CityZones.DensityAt(x * 1.7, y * 2.3), 0.0, $"({x},{y})");
                    index--;
                }
            }
        }

        /// <summary>No cliffs: neighbouring chunks never differ wildly, so a core has a real slope to its edge.</summary>
        [Test]
        public void DensityChangesSmoothlyFromChunkToChunk()
        {
            double worst = 0.0;
            for (int x = -600; x < 600; x += 2)
            {
                for (int y = -600; y < 600; y += 2)
                {
                    double here = CityZones.DensityAt(x + 0.5, y + 0.5);
                    worst = System.Math.Max(worst, System.Math.Abs(here - CityZones.DensityAt(x + 1.5, y + 0.5)));
                    worst = System.Math.Max(worst, System.Math.Abs(here - CityZones.DensityAt(x + 0.5, y + 1.5)));
                }
            }
            TestContext.WriteLine($"steepest step between neighbouring chunks: {worst:0.000}");
            Assert.Less(worst, 0.12, "density has a cliff -- a core would end in a wall");
        }

        /// <summary>
        /// Every zone exists and none swamps the city. The bands are wide on
        /// purpose (regions are big and the sample finite) but they rule out
        /// the failures that matter: a city that is all core, or has none.
        /// </summary>
        [Test]
        public void EveryZoneAppearsAndNoneSwampsTheCity()
        {
            var counts = new Dictionary<ZoneKind, int>();
            int total = 0;
            for (int x = -1500; x < 1500; x += 5)
            {
                for (int y = -1500; y < 1500; y += 5)
                {
                    ZoneKind z = CityZones.ZoneAt(x + 0.5, y + 0.5);
                    counts.TryGetValue(z, out int n);
                    counts[z] = n + 1;
                    total++;
                }
            }

            foreach (ZoneKind z in System.Enum.GetValues(typeof(ZoneKind)))
            {
                double share = counts.GetValueOrDefault(z) / (double)total;
                TestContext.WriteLine($"{z}: {share:P1}");
                Assert.Greater(share, 0.02, $"{z} is almost absent from the city");
                Assert.Less(share, 0.65, $"{z} swamps the city");
            }

            Assert.Less(counts.GetValueOrDefault(ZoneKind.Core) / (double)total, 0.15, "the whole city is downtown");
        }

        /// <summary>A city has many centres, spread across the map -- not one at the origin and nothing else.</summary>
        [Test]
        public void ManyCoresAreScatteredAcrossTheWorld()
        {
            int cellsWithACore = 0;
            int cells = 0;
            for (int cellX = -5; cellX <= 5; cellX++)
            {
                for (int cellY = -5; cellY <= 5; cellY++)
                {
                    cells++;
                    bool found = false;
                    for (int dx = -40; dx <= 40 && !found; dx += 4)
                    {
                        for (int dy = -40; dy <= 40 && !found; dy += 4)
                        {
                            found = CityZones.ZoneAt(cellX * CityZones.CentreCellChunks + dx + 0.5,
                                cellY * CityZones.CentreCellChunks + dy + 0.5) == ZoneKind.Core;
                        }
                    }
                    if (found)
                    {
                        cellsWithACore++;
                    }
                }
            }
            TestContext.WriteLine($"{cellsWithACore}/{cells} lattice cells contain a downtown core");
            Assert.Greater(cellsWithACore / (double)cells, 0.4, "cores are too rare -- the city has one downtown and then nothing");
            Assert.Less(cellsWithACore / (double)cells, 0.98, "every cell has a core -- they no longer read as distinct centres");
        }

        [Test]
        public void ZoneOfADensityAndCharacterFollowsTheThresholds()
        {
            Assert.AreEqual(ZoneKind.Core, CityZones.ZoneFor(0.9, 0.5));
            Assert.AreEqual(ZoneKind.Urban, CityZones.ZoneFor(0.6, 0.5));
            Assert.AreEqual(ZoneKind.OldTown, CityZones.ZoneFor(0.6, 0.05));
            Assert.AreEqual(ZoneKind.Residential, CityZones.ZoneFor(0.3, 0.5));
            Assert.AreEqual(ZoneKind.OldTown, CityZones.ZoneFor(0.3, 0.1));
            Assert.AreEqual(ZoneKind.Industrial, CityZones.ZoneFor(0.3, 0.95));
            Assert.AreEqual(ZoneKind.Outskirts, CityZones.ZoneFor(0.1, 0.3));
            Assert.AreEqual(ZoneKind.Industrial, CityZones.ZoneFor(0.1, 0.9));
        }
    }
}
