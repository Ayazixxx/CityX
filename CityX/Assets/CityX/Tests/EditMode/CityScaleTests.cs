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
        private const int Seed = 61472093;

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

            var classicWidth = new List<double>();
            var classicHeight = new List<double>();
            foreach (BuildingSite s in classic.Buildings)
            {
                classicWidth.Add(Math.Max(s.Params.Width, s.Params.Depth));
                classicHeight.Add(s.Params.Height);
            }
            var hugeWidth = new List<double>();
            var hugeHeight = new List<double>();
            foreach (BuildingSite s in huge.Buildings)
            {
                hugeWidth.Add(Math.Max(s.Params.Width, s.Params.Depth));
                hugeHeight.Add(s.Params.Height);
            }

            TestContext.WriteLine($"classic: {classic.Buildings.Length} buildings, median long side {Median(classicWidth):0} m, median height {Median(classicHeight):0} m");
            TestContext.WriteLine($"huge: {huge.Buildings.Length} buildings, median long side {Median(hugeWidth):0} m, median height {Median(hugeHeight):0} m");

            Assert.Greater(huge.Buildings.Length, classic.Buildings.Length * 0.5, "the same city, not a ghost town");
            Assert.Less(huge.Buildings.Length, classic.Buildings.Length * 2.0);
            Assert.Greater(Median(hugeWidth), Median(classicWidth) * 2.5, "buildings are much wider");
            Assert.Greater(Median(hugeHeight), Median(classicHeight) * 2.2, "and much taller");
        }

        [Test]
        public void ALargerCityStillHasASpawnAndRealRoads()
        {
            PlanWorld huge = Make(CityScale.Huge);
            Assert.IsTrue(huge.TryFindSpawn(out double x, out double z));
            Assert.Less(Math.Abs(x), 4000.0);
            Assert.Less(Math.Abs(z), 4000.0);
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
