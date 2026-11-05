using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Snow's rules, with no scene (docs/SNOW.md).</summary>
    public sealed class SnowRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            CurrentWeather.ClearSnow();
            CurrentWeather.Control = null;
            CurrentWeather.Relay = null;
        }

        [TestCase("light", SnowRules.LightSnow)]
        [TestCase("Flurry", SnowRules.LightSnow)]
        [TestCase("moderate", SnowRules.ModerateSnow)]
        [TestCase("", SnowRules.ModerateSnow)]
        [TestCase(null, SnowRules.ModerateSnow)]
        [TestCase("heavy", SnowRules.Blizzard)]
        [TestCase("blizzard", SnowRules.Blizzard)]
        [TestCase("0.4", 0.4f)]
        public void AWordOrANumberIsAnIntensity(string word, float wanted)
        {
            Assert.IsTrue(SnowRules.TryParseIntensity(word, out float intensity));
            Assert.AreEqual(wanted, intensity, 1e-4f);
        }

        [TestCase("sideways")]
        [TestCase("0")]
        [TestCase("1.5")]
        public void AnythingElseIsNot(string word)
        {
            Assert.IsFalse(SnowRules.TryParseIntensity(word, out _));
        }

        private static float After(float seconds, float cover, float intensity, float sunshine = 0.0f, float rain = 0.0f)
        {
            for (float t = 0.0f; t < seconds; t += 0.5f)
            {
                cover = SnowRules.StepCover(cover, intensity, sunshine, rain, 0.5f);
            }
            return cover;
        }

        [Test]
        public void ABlizzardBuriesTheCityInAFewMinutes()
        {
            Assert.AreEqual(1.0f, After(SnowRules.BlizzardBurySeconds + 1.0f, 0.0f, SnowRules.Blizzard), 1e-3f);
            Assert.Less(After(SnowRules.BlizzardBurySeconds * 0.5f, 0.0f, SnowRules.Blizzard), 0.6f, "it was buried at once");
        }

        [Test]
        public void AFlurryOnlyDustsItHoweverLongItLasts()
        {
            float cover = After(3600.0f, 0.0f, SnowRules.LightSnow);
            Assert.Less(cover, 0.6f);
            Assert.Greater(cover, 0.4f);
        }

        [Test]
        public void ItMeltsWhenItStopsQuickerInTheSunAndQuickerStillInTheRain()
        {
            float grey = After(60.0f, 1.0f, 0.0f);
            float sunny = After(60.0f, 1.0f, 0.0f, sunshine: 1.0f);
            float rainy = After(60.0f, 1.0f, 0.0f, rain: 1.0f);
            Assert.Less(grey, 1.0f, "it never melts");
            Assert.Less(sunny, grey, "the sun does nothing");
            Assert.Less(rainy, sunny, "the rain does nothing");
            Assert.AreEqual(0.0f, After(SnowRules.MeltSeconds + 1.0f, 1.0f, 0.0f), 1e-3f, "it does not melt away");
        }

        [Test]
        public void ALighterSnowfallLetsADeepCoverSettleDownToWhatItKeeps()
        {
            float ceiling = SnowRules.CoverCeiling(SnowRules.LightSnow);
            Assert.AreEqual(ceiling, After(SnowRules.MeltSeconds * 2.0f, 1.0f, SnowRules.LightSnow), 1e-3f);
        }

        [Test]
        public void RainMeltsItEvenWhileSomethingIsStillFalling()
        {
            Assert.Less(After(30.0f, 1.0f, SnowRules.ModerateSnow, rain: 1.0f), 1.0f);
        }

        [Test]
        public void DeepSnowDragsAtTheLegsAndGivesLessGripButADustingDoesNot()
        {
            Assert.AreEqual(1.0f, SnowRules.Pace(0.0f), 1e-4f);
            Assert.AreEqual(1.0f, SnowRules.Pace(0.25f), 1e-4f, "a dusting slowed the body");
            Assert.AreEqual(SnowRules.PaceFloor, SnowRules.Pace(1.0f), 1e-4f);
            Assert.AreEqual(1.0f, SnowRules.Traction(0.0f), 1e-4f);
            Assert.AreEqual(SnowRules.TractionFloor, SnowRules.Traction(1.0f), 1e-4f);
            Assert.Greater(SnowRules.TractionFloor, Weather.WetTractionFloor, "snow is not meant to be slicker than soaked ground");
        }

        [Test]
        public void TheFogClosesInWithTheSnowAndAClearDayKeepsItsOwn()
        {
            Assert.AreEqual(150.0f, SnowRules.FogStart(150.0f, 0.0f), 1e-4f);
            Assert.AreEqual(380.0f, SnowRules.FogEnd(380.0f, 0.0f), 1e-4f);
            Assert.AreEqual(SnowRules.BlizzardFogEnd, SnowRules.FogEnd(380.0f, 1.0f), 1e-4f);
            Assert.Less(SnowRules.FogEnd(380.0f, SnowRules.Blizzard), SnowRules.FogEnd(380.0f, SnowRules.LightSnow));
        }

        [Test]
        public void TheSnowUnderfootIsTheCoverCutOffUnderARoof()
        {
            CurrentWeather.SnowCover = 0.8f;
            Assert.AreEqual(0.8f, CurrentWeather.SnowUnderfoot(Vector3.zero), 1e-4f, "with no map the sky is open");
            CurrentWeather.SnowMap = new FixedCover(1.0f);
            Assert.AreEqual(0.0f, CurrentWeather.SnowUnderfoot(Vector3.zero), 1e-4f, "snow under a roof");
            CurrentWeather.ClearSnow();
            Assert.AreEqual(0.0f, CurrentWeather.SnowUnderfoot(Vector3.zero), 1e-4f);
        }

        [Test]
        public void FlamesFeelSnowLessThanRain()
        {
            CurrentWeather.SnowFalling = 1.0f;
            Assert.AreEqual(SnowRules.QuenchShare, CurrentWeather.QuenchingOn(Vector3.zero), 1e-4f);
            CurrentWeather.SnowMap = new FixedCover(1.0f);
            Assert.AreEqual(0.0f, CurrentWeather.QuenchingOn(Vector3.zero), 1e-4f, "snow on a flame under a roof");
        }

        private sealed class FixedCover : IRainCover
        {
            private readonly float _shelter;

            public FixedCover(float shelter)
            {
                _shelter = shelter;
            }

            public float Shelter(Vector3 worldPoint) => _shelter;
        }

        private sealed class Record : IWeatherControl, IWeatherRelay
        {
            public int Applied;
            public int Sent;

            public WeatherOrder Now => default;

            public void Apply(in WeatherOrder order) => Applied++;

            public void Send(in WeatherOrder order) => Sent++;
        }

        [Test]
        public void AnOrderGoesStraightToTheWeatherAloneAndThroughTheServerInASession()
        {
            var record = new Record();
            CurrentWeather.Control = record;
            CurrentWeather.Order(new WeatherOrder { Snow = 1.0f });
            Assert.AreEqual(1, record.Applied, "alone, nothing changed");

            CurrentWeather.Relay = record;
            CurrentWeather.Order(new WeatherOrder { Snow = 1.0f });
            Assert.AreEqual(1, record.Applied, "in a session it changed here before the server said so");
            Assert.AreEqual(1, record.Sent, "in a session the server was not told");
        }

        [Test]
        public void AFootThatStopsHasLandedOnceAndAStillOneNever()
        {
            var stepper = new FootprintStepper();
            const float step = 1.0f / 60.0f;
            const float sole = 0.16f;
            Assert.IsFalse(stepper.Step(sole, step), "landed before it had moved");
            for (int i = 0; i < 30; i++)
            {
                Assert.IsFalse(stepper.Step(sole, step), "a foot standing still keeps landing");
            }
            for (int stride = 0; stride < 4; stride++)
            {
                int landings = 0;
                for (int i = 0; i < 40; i++)
                {
                    // Half the stride in the air, a quarter of a metre at its top, half on the ground.
                    float swing = Mathf.Max(0.0f, Mathf.Sin(i / 40.0f * 2.0f * Mathf.PI));
                    if (stepper.Step(sole + swing * 0.25f, step))
                    {
                        landings++;
                    }
                }
                Assert.AreEqual(1, landings, $"stride {stride}: a step must land once");
            }
        }

        [Test]
        public void ASmallShuffleIsNoStepAndALongSlopeDoesNotStopTheSteps()
        {
            var stepper = new FootprintStepper();
            const float step = 1.0f / 60.0f;
            for (int i = 0; i < 120; i++)
            {
                Assert.IsFalse(stepper.Step(0.16f + Mathf.Sin(i * 0.2f) * 0.02f, step), "a body breathing took a step");
            }

            // The foot's whole cycle slowly rising against the body, as on a long climb: the lowest it goes keeps up.
            int landings = 0;
            for (int i = 0; i < 400; i++)
            {
                float swing = Mathf.Max(0.0f, Mathf.Sin(i / 40.0f * 2.0f * Mathf.PI));
                if (stepper.Step(0.16f + i * step * 0.03f + swing * 0.25f, step))
                {
                    landings++;
                }
            }
            Assert.AreEqual(10, landings, 1, "steps lost on a long slope");
        }

        [Test]
        public void AWallsBareEdgeIsWornAwayWhileARoofsMiddleStaysCovered()
        {
            const int cells = 8;
            const float nothing = -1.0f;
            var heights = new float[cells * cells];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = nothing;
            }
            // A roof five cells square, and a pole one cell wide.
            for (int z = 1; z <= 5; z++)
            {
                for (int x = 1; x <= 5; x++)
                {
                    heights[z * cells + x] = 30.0f;
                }
            }
            heights[7 * cells + 7] = 30.0f;
            var worn = new float[cells * cells];
            RainCover.WearEdges(heights, worn, cells);
            Assert.AreEqual(30.0f, worn[3 * cells + 3], 1e-4f, "the middle of a roof is under cover");
            Assert.AreEqual(30.0f, worn[2 * cells + 2], 1e-4f, "an inner cell of a roof is under cover");
            Assert.AreEqual(nothing, worn[1 * cells + 3], 1e-4f, "the roof's edge still covers the street along its wall");
            Assert.AreEqual(nothing, worn[7 * cells + 7], 1e-4f, "a pole keeps the snow off a whole cell");
        }

        [Test]
        public void TheFieldIsAQuadAFlakeWithRanksEvenlySpread()
        {
            Mesh mesh = FlakeField.Build(100, new System.Random(1));
            Assert.AreEqual(400, mesh.vertexCount);
            Assert.AreEqual(600, mesh.triangles.Length);
            var kinds = new System.Collections.Generic.List<Vector4>();
            mesh.GetUVs(1, kinds);
            Assert.AreEqual(0.005f, kinds[0].x, 1e-4f);
            Assert.AreEqual(0.995f, kinds[399].x, 1e-4f);
            Assert.Greater(mesh.bounds.size.x, 10000.0f, "it would be culled as the camera leaves the middle of the world");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void AFinerMapReadsWhatIsOverheadForTheShaderByItsOwnCells()
        {
            var map = new RainCover((x, z, fromY) => x < 1.0f ? 50.0f : float.NaN, 1.5f, 4, 100.0f, 0);
            map.Refresh(Vector3.zero, 0.0f);
            Assert.AreEqual(16, map.LastRefreshRays, "not a cell per cast");
            var heights = new float[16];
            map.CopyHeights(heights, -1.0f);
            int roofs = 0, none = 0;
            foreach (float height in heights)
            {
                if (height == 50.0f) roofs++;
                else if (height == -1.0f) none++;
            }
            Assert.AreEqual(12, roofs, "three columns of the four stand under the roof");
            Assert.AreEqual(4, none, "the cast that found nothing is not told as such");
            Assert.AreEqual(50.0f, map.SurfaceHeightAt(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(1.0f, map.Shelter(new Vector3(0.75f, 1.0f, 0.75f)), 1e-4f);

            map.Refresh(Vector3.zero, 1000.0f);
            Assert.AreEqual(0, map.LastRefreshRays, "its re-check budget of none was not kept");
        }
    }
}
