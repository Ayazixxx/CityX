using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The city at a larger scale is the classic plan drawn bigger: the same kind of city, with buildings that are wider and taller and a
    /// region that is larger, and nothing about the classic numbers changed.
    /// </summary>
    public sealed class CityScaleTests
    {
        private const int Seed = 38207461;

        private static PlanWorld Make(CityScale scale) => PlanWorld.Create(Seed, PlanConfig.For(scale));

        private static double Median(List<double> values)
        {
            values.Sort();
            return values.Count == 0 ? 0.0 : values[values.Count / 2];
        }

        [Test]
        public void TheClassicConfigIsExactlyTheOldDefault()
        {
            PlanConfig cfg = PlanConfig.For(CityScale.Classic);
            Assert.AreEqual(2500.0, cfg.Half);
            Assert.AreEqual(30.0, cfg.HillAmplitude);
            Assert.AreEqual(1500.0, cfg.HillWavelength);
            Assert.AreEqual(5.5, cfg.StreetHalfWidth);
            Assert.AreEqual(28.0, cfg.MinSegment);
            Assert.AreEqual(1.0, cfg.Scale);
            Assert.AreEqual(1.0, cfg.HeightScale);
            Assert.IsTrue(CityScale.Classic.IsClassic);
        }

        [Test]
        public void PresetsAndNumbersParse()
        {
            Assert.IsTrue(CityScale.TryParse("Huge", out CityScale huge));
            Assert.AreEqual(CityScale.Huge.Footprint, huge.Footprint);
            Assert.IsTrue(CityScale.TryParse("6,4", out CityScale custom));
            Assert.AreEqual(6.0, custom.Footprint);
            Assert.AreEqual(4.0, custom.Height);
            Assert.IsFalse(CityScale.TryParse("0.2,1", out _), "smaller than classic is not a scale up");
            Assert.IsFalse(CityScale.TryParse("nonsense", out _));
        }

        [Test]
        public void ALargerCityHasWiderTallerBuildingsInTheSamePlaces()
        {
            PlanWorld classic = Make(CityScale.Classic);
            PlanWorld huge = Make(CityScale.Huge);

            WholeBuildings(classic, out List<double> classicWidth, out List<double> classicHeight);
            WholeBuildings(huge, out List<double> hugeWidth, out List<double> hugeHeight);

            TestContext.WriteLine($"classic: {classic.Buildings.Length} buildings, median long side {Median(classicWidth):0} m, median height {Median(classicHeight):0} m");
            TestContext.WriteLine($"huge: {huge.Buildings.Length} buildings, median long side {Median(hugeWidth):0} m, median height {Median(hugeHeight):0} m");

            Assert.Greater(huge.Buildings.Length, classic.Buildings.Length * 0.5, "the same city, not a ghost town");
            Assert.Less(huge.Buildings.Length, classic.Buildings.Length * 2.0);
            Assert.Greater(Median(hugeWidth), Median(classicWidth) * 2.5, "buildings are much wider");
            Assert.Greater(Median(hugeHeight), Median(classicHeight) * 2.2, "and much taller");
        }

        /// <summary>
        /// Per building, not per volume: a building is a street-level block that is not a wing, plus the wings and volumes that follow it in the list
        /// (Massing.Compose). Its width is its main block's long side, its height from its floor to its highest roof. Counting volumes would weigh a
        /// twisted tower's sixteen short stages as sixteen short buildings.
        /// </summary>
        private static void WholeBuildings(PlanWorld world, out List<double> widths, out List<double> heights)
        {
            widths = new List<double>();
            heights = new List<double>();
            BuildingSite[] b = world.Buildings;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i].OnRoof || b[i].Wing)
                {
                    continue;
                }
                double top = b[i].Top;
                for (int j = i + 1; j < b.Length && (b[j].OnRoof || b[j].Wing); j++)
                {
                    top = Math.Max(top, b[j].Top);
                }
                widths.Add(Math.Max(b[i].Params.Width, b[i].Params.Depth));
                heights.Add(top - b[i].BaseHeight);
            }
        }

        private static double Percentile(List<double> sorted, double p) => sorted.Count == 0 ? 0.0 : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];

        [Test]
        public void AVariedCityRunsFromClassicSizedBuildingsToHugeOnes()
        {
            PlanWorld classic = Make(CityScale.Classic);
            PlanWorld varied = Make(CityScale.Varied);

            List<double> Long(PlanWorld w)
            {
                var l = new List<double>();
                foreach (BuildingSite s in w.Buildings) l.Add(Math.Max(s.Params.Width, s.Params.Depth));
                l.Sort();
                return l;
            }
            List<double> Tall(PlanWorld w)
            {
                var l = new List<double>();
                foreach (BuildingSite s in w.Buildings) l.Add(s.Params.Height);
                l.Sort();
                return l;
            }
            List<double> cl = Long(classic), vl = Long(varied), ch = Tall(classic), vh = Tall(varied);
            foreach (double p in new[] { 0.05, 0.25, 0.5, 0.75, 0.95 })
            {
                TestContext.WriteLine($"p{p * 100:0}: long side classic {Percentile(cl, p):0} varied {Percentile(vl, p):0}; height classic {Percentile(ch, p):0} varied {Percentile(vh, p):0}");
            }
            TestContext.WriteLine($"varied: {varied.Buildings.Length} buildings, {varied.Data.Nodes.Count} road nodes; classic {classic.Buildings.Length}, {classic.Data.Nodes.Count}");
            Assert.Less(Percentile(vl, 0.10), Percentile(cl, 0.50) * 1.3, "some buildings are as small as the classic city's typical one");
            Assert.Greater(Percentile(vl, 0.95), Percentile(cl, 0.95) * 2.2, "and some are far bigger");
            Assert.Greater(Percentile(vl, 0.95) / Percentile(vl, 0.10), 3.5, "a wide spread");
        }

        [Test]
        public void BigBuildingsGatherWithBigBuildingsButNotExclusively()
        {
            PlanWorld varied = Make(CityScale.Varied);
            BuildingSite[] b = varied.Buildings;

            // A grid of 200 m cells to find each building's neighbours.
            var cells = new Dictionary<long, List<int>>();
            long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
            for (int i = 0; i < b.Length; i++)
            {
                long k = Key((int)Math.Floor(b[i].CentreX / 200.0), (int)Math.Floor(b[i].CentreZ / 200.0));
                if (!cells.TryGetValue(k, out List<int> list)) cells[k] = list = new List<int>();
                list.Add(i);
            }
            double Size(int i) => Math.Max(b[i].Params.Width, b[i].Params.Depth);

            double near = 0.0, random = 0.0;
            int pairs = 0;
            var rng = new System.Random(7);
            for (int i = 0; i < b.Length; i += 3)
            {
                int cx = (int)Math.Floor(b[i].CentreX / 200.0), cy = (int)Math.Floor(b[i].CentreZ / 200.0);
                int best = -1;
                double bestD = double.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!cells.TryGetValue(Key(cx + dx, cy + dy), out List<int> list)) continue;
                        foreach (int j in list)
                        {
                            if (j == i) continue;
                            double d = (b[i].CentreX - b[j].CentreX) * (b[i].CentreX - b[j].CentreX) + (b[i].CentreZ - b[j].CentreZ) * (b[i].CentreZ - b[j].CentreZ);
                            if (d < bestD) { bestD = d; best = j; }
                        }
                    }
                }
                if (best < 0) continue;
                int other = rng.Next(b.Length);
                near += Math.Abs(Math.Log(Size(i) / Size(best)));
                random += Math.Abs(Math.Log(Size(i) / Size(other)));
                pairs++;
            }
            double ratio = (near / pairs) / (random / pairs);
            TestContext.WriteLine($"{pairs} buildings: mean size difference to the nearest neighbour {near / pairs:0.00}, to a random building {random / pairs:0.00} (ratio {ratio:0.00})");
            Assert.Less(ratio, 0.7, "districts: neighbours are much more alike than strangers");
            Assert.Greater(ratio, 0.12, "but not exclusively: neighbours still differ");
        }

        [Test]
        public void ALargerCityStillHasASpawnAndRealRoads()
        {
            PlanWorld huge = Make(CityScale.Huge);
            Assert.IsTrue(huge.TryFindSpawn(out double x, out double z));
            Assert.Less(Math.Abs(x), 6000.0);
            Assert.Less(Math.Abs(z), 6000.0);
            Assert.Greater(huge.Data.Nodes.Count, 800);
            Assert.Greater(huge.Data.Blocks.Count, 300);
            int buildingsNearSpawn = 0;
            foreach (BuildingSite s in huge.Buildings)
            {
                if (Math.Abs(s.CentreX - x) < 600.0 && Math.Abs(s.CentreZ - z) < 600.0)
                {
                    buildingsNearSpawn++;
                }
            }
            Assert.Greater(buildingsNearSpawn, 6, "there is a city around the spawn");
        }

        [Test]
        public void TowersAreStillTowersAndNothingIsAbsurd()
        {
            PlanWorld huge = Make(CityScale.Huge);
            int tall = 0, supers = 0;
            double highest = 0.0;
            foreach (BuildingSite s in huge.Buildings)
            {
                highest = Math.Max(highest, s.Params.Height);
                tall += s.Params.Mass == BuildingMass.Tower ? 1 : 0;
                supers += s.Params.Mass == BuildingMass.Supertall ? 1 : 0;
                Assert.LessOrEqual(s.Params.Height, PlanBuildings.MaxSlenderness * Math.Min(s.Params.Width, s.Params.Depth) * 3.0 + 1.0);
            }
            TestContext.WriteLine($"huge: {tall} towers, {supers} supertalls, tallest {highest:0} m");
            Assert.Greater(tall + supers, 20, "a skyline");
            Assert.Less(highest, 1500.0, "tall, not absurd");
        }
    }
}
