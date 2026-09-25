using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The rules of rain (World/Weather.cs). Every number the visuals and the
    /// gameplay read is pinned here, including the ORDERINGS the research says
    /// matter: wetness rises fast and falls slowly, sheen fades before
    /// darkness, far thunder is quieter, later and duller.
    /// </summary>
    public sealed class WeatherTests
    {
        // --- intensity words ---

        [Test]
        public void PlainRainMeansAFullDownpourAsItAlwaysDid()
        {
            Assert.IsTrue(Weather.TryParseIntensity(null, out float none));
            Assert.AreEqual(Weather.HeavyRain, none);
            Assert.IsTrue(Weather.TryParseIntensity("", out float empty));
            Assert.AreEqual(Weather.HeavyRain, empty);
        }

        [Test]
        public void IntensityWordsAndNumbersParse()
        {
            Assert.IsTrue(Weather.TryParseIntensity("light", out float light));
            Assert.AreEqual(Weather.LightRain, light);
            Assert.IsTrue(Weather.TryParseIntensity("Moderate", out float moderate));
            Assert.AreEqual(Weather.ModerateRain, moderate);
            Assert.IsTrue(Weather.TryParseIntensity("HEAVY", out float heavy));
            Assert.AreEqual(Weather.HeavyRain, heavy);
            Assert.IsTrue(Weather.TryParseIntensity("0.4", out float number));
            Assert.AreEqual(0.4f, number, 1e-6f);
        }

        [TestCase("banana")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.5")]
        public void NonsenseIntensityIsRefused(string text)
        {
            Assert.IsFalse(Weather.TryParseIntensity(text, out _));
        }

        [Test]
        public void MoreIntensityMeansMoreOfEverythingItDrives()
        {
            float previousFlux = 0.0f, previousMood = 0.0f, previousVolume = 0.0f;
            foreach (float i in new[] { 0.1f, 0.25f, 0.55f, 0.8f, 1.0f })
            {
                Assert.Greater(Weather.DropFlux(i), previousFlux);
                Assert.Greater(Weather.StormTarget(i), previousMood);
                Assert.Greater(Weather.RainVolume(i), previousVolume);
                previousFlux = Weather.DropFlux(i);
                previousMood = Weather.StormTarget(i);
                previousVolume = Weather.RainVolume(i);
            }
            Assert.AreEqual(0.0f, Weather.DropFlux(0.0f));
            Assert.AreEqual(0.0f, Weather.StormTarget(0.0f));
            Assert.AreEqual(0.0f, Weather.RainVolume(0.0f));
        }

        [Test]
        public void FullIntensityIsExactlyOneSoTheStormRampCanSnapToIt()
        {
            Assert.AreEqual(1.0f, Weather.StormTarget(1.0f), 0.0f);
        }

        [Test]
        public void AFullDownpourKeepsTheDropDensityTheOriginalEffectHad()
        {
            // 900 drops alive over a 40x40m box, each falling 18m at ~22m/s.
            float original = 900.0f / (18.0f / 22.0f) / (40.0f * 40.0f);
            Assert.AreEqual(original, Weather.DropFlux(1.0f), original * 0.15f);
        }

        // --- wetness ---

        [Test]
        public void HeavyRainSoaksTheCityInSecondsAndItDriesOutOverMinutes()
        {
            float wet = 0.0f;
            for (float t = 0.0f; t < 10.0f; t += 0.1f)
            {
                wet = Weather.StepWetness(wet, 1.0f, 0.1f);
            }
            Assert.Greater(wet, 0.95f, "ten seconds of a downpour should all but soak the city");

            float drying = 1.0f;
            for (float t = 0.0f; t < 10.0f; t += 0.1f)
            {
                drying = Weather.StepWetness(drying, 0.0f, 0.1f);
            }
            Assert.Greater(drying, 0.9f, "ten seconds after the rain stops the city should still be soaked");

            Assert.Greater(Weather.DrySeconds, Weather.SoakSeconds * 8.0f, "wetness has to fall MUCH more slowly than it rises");
        }

        [Test]
        public void WetnessReachesExactlyZeroAgainWhenItHasDried()
        {
            float wet = 1.0f;
            for (int i = 0; i < 20000 && wet > 0.0f; i++)
            {
                wet = Weather.StepWetness(wet, 0.0f, 0.1f);
            }
            Assert.AreEqual(0.0f, wet, "the ground materials are restored from this value, so it must land on zero exactly");
        }

        [Test]
        public void ADrizzleOnlyEverDampensWhereADownpourSoaks()
        {
            float drizzle = 0.0f, downpour = 0.0f;
            for (int i = 0; i < 3000; i++)
            {
                drizzle = Weather.StepWetness(drizzle, Weather.LightRain, 0.1f);
                downpour = Weather.StepWetness(downpour, Weather.HeavyRain, 0.1f);
            }
            Assert.Less(drizzle, 0.7f);
            Assert.Greater(drizzle, 0.3f);
            Assert.AreEqual(1.0f, downpour, 1e-4f);
        }

        [Test]
        public void EasingTheRainOffFromADownpourDriesTheCityDownTowardTheNewCeiling()
        {
            float wet = 1.0f;
            for (int i = 0; i < 6000; i++)
            {
                wet = Weather.StepWetness(wet, Weather.LightRain, 0.1f);
            }
            Assert.AreEqual(Weather.WetnessCeiling(Weather.LightRain), wet, 1e-4f);
        }

        [Test]
        public void SheenLeavesADryingSurfaceBeforeItsDarkness()
        {
            Assert.AreEqual(0.0f, Weather.SheenFactor(0.0f));
            Assert.AreEqual(1.0f, Weather.SheenFactor(1.0f));
            foreach (float w in new[] { 0.2f, 0.5f, 0.8f })
            {
                Assert.Less(Weather.SheenFactor(w), w, $"at wetness {w} the shine should have dropped out faster than the darkening");
            }
        }

        [Test]
        public void RipplesNeedStandingWaterAndThickenWithTheRain()
        {
            Assert.AreEqual(0.0f, Weather.RippleChance(0.2f, 1.0f), "damp ground makes no rings");
            Assert.AreEqual(0.0f, Weather.RippleChance(0.35f, 1.0f));
            Assert.Greater(Weather.RippleChance(1.0f, 1.0f), 0.3f);
            Assert.Greater(Weather.RippleChance(1.0f, 1.0f), Weather.RippleChance(0.7f, 1.0f));
            Assert.Greater(Weather.RippleChance(1.0f, 1.0f), Weather.RippleChance(1.0f, 0.2f));
        }

        // --- the player's body ---

        [Test]
        public void StandingInARealDownpourSoaksABodyInSecondsAndCoverDriesItFaster()
        {
            float wet = 0.0f;
            for (int i = 0; i < 70; i++)
            {
                wet = Weather.StepBodyWetness(wet, 1.0f, 0.0f, 0.1f);
            }
            Assert.AreEqual(1.0f, wet, 1e-4f, "seven seconds of a downpour should soak a body");

            float open = 1.0f, covered = 1.0f;
            for (int i = 0; i < 100; i++)
            {
                open = Weather.StepBodyWetness(open, 0.0f, 0.0f, 0.1f);
                covered = Weather.StepBodyWetness(covered, 0.0f, 1.0f, 0.1f);
            }
            Assert.Less(covered, open, "a body dries faster under a roof than in the open air");
            Assert.Greater(open, 0.8f, "and not instantly in either");
        }

        [Test]
        public void ARainShowerSoaksABodySlowerThanADownpour()
        {
            float shower = Weather.StepBodyWetness(0.0f, 0.25f, 0.0f, 1.0f);
            float downpour = Weather.StepBodyWetness(0.0f, 1.0f, 0.0f, 1.0f);
            Assert.Less(shower, downpour);
        }

        // --- what wetness does to gameplay ---

        [Test]
        public void WetGroundCostsTractionDownToAFloorAndDryGroundCostsNone()
        {
            Assert.AreEqual(1.0f, Weather.GroundTraction(0.0f));
            Assert.AreEqual(Weather.WetTractionFloor, Weather.GroundTraction(1.0f), 1e-6f);
            Assert.Greater(Weather.GroundTraction(0.3f), Weather.GroundTraction(0.8f));
            Assert.Greater(Weather.WetTractionFloor, 0.3f, "wet ground is slippery, not ice");
        }

        [Test]
        public void AWetWallCostsStaminaAndSlipsButADryOneDoesNot()
        {
            Assert.AreEqual(1.0f, Weather.WallStaminaDrain(0.0f));
            Assert.AreEqual(2.0f, Weather.WallStaminaDrain(1.0f));
            Assert.AreEqual(0.0f, Weather.WallSlipSpeed(0.0f));
            Assert.AreEqual(Weather.WetWallSlipSpeed, Weather.WallSlipSpeed(1.0f), 1e-6f);
            Assert.Less(Weather.WetWallSlipSpeed, 2.5f, "slippery, not a free fall -- a climber can still make progress");
        }

        [Test]
        public void RainWeakensAFlameButNeverPutsItOutAltogether()
        {
            Assert.AreEqual(1.0f, Weather.FlameStrength(0.0f));
            Assert.AreEqual(0.5f, Weather.FlameStrength(1.0f), 1e-6f);
            Assert.Greater(Weather.FlameStrength(1.0f), 0.0f);
        }

        [Test]
        public void PeopleHurryInTheRainUpToAPointAndSeekCoverOnlyOnceItIsReallyFalling()
        {
            Assert.AreEqual(1.0f, Weather.PedestrianHurry(0.0f));
            Assert.AreEqual(1.6f, Weather.PedestrianHurry(1.0f), 1e-6f);
            Assert.Greater(Weather.ShelterSeekThreshold, Weather.LightRain * 0.9f - 0.0f, "a drizzle is at the edge of being ignored");
            Assert.Less(Weather.ShelterSeekThreshold, Weather.ModerateRain);
        }

        // --- the storm overhead ---

        [Test]
        public void ThunderTravelsAtTheSpeedOfSound()
        {
            Assert.AreEqual(0.0f, Weather.ThunderDelaySeconds(0.0f));
            Assert.AreEqual(1.0f, Weather.ThunderDelaySeconds(343.0f), 1e-4f);
            Assert.AreEqual(2.0f * Weather.ThunderDelaySeconds(400.0f), Weather.ThunderDelaySeconds(800.0f), 1e-4f);
            Assert.AreEqual(0.0f, Weather.ThunderDelaySeconds(-10.0f), "a negative distance is not a time machine");
        }

        [Test]
        public void FartherStrikesAreDimmerQuieterAndDuller()
        {
            float previousFlash = 2.0f, previousVolume = 2.0f, previousBrightness = 1e9f;
            foreach (float distance in new[] { 100.0f, 400.0f, 900.0f, 1500.0f, 2200.0f })
            {
                Assert.Less(Weather.FlashStrength(distance), previousFlash + 1e-6f);
                Assert.Less(Weather.ThunderVolume(distance), previousVolume + 1e-6f);
                Assert.Less(Weather.ThunderBrightnessHz(distance), previousBrightness + 1e-3f);
                previousFlash = Weather.FlashStrength(distance);
                previousVolume = Weather.ThunderVolume(distance);
                previousBrightness = Weather.ThunderBrightnessHz(distance);
            }
            Assert.AreEqual(1.0f, Weather.FlashStrength(100.0f));
            Assert.Greater(Weather.FlashStrength(3000.0f), 0.0f, "even a far strike is a flicker");
            Assert.GreaterOrEqual(Weather.ThunderVolume(50000.0f), 0.15f);
            Assert.LessOrEqual(Weather.ThunderVolume(0.0f), 1.0f);
        }

        [Test]
        public void ShelterQuietensAndMuffles()
        {
            Assert.AreEqual(1.0f, Weather.ShelteredVolume(0.0f));
            Assert.Less(Weather.ShelteredVolume(1.0f), 1.0f);
            Assert.Greater(Weather.ShelteredVolume(1.0f), 0.3f, "rain on a roof is still heard");
            Assert.AreEqual(22000.0f, Weather.ShelteredCutoffHz(0.0f));
            Assert.Less(Weather.ShelteredCutoffHz(1.0f), 2500.0f);
            Assert.Greater(Weather.ShelteredCutoffHz(0.5f), Weather.ShelteredCutoffHz(1.0f));
        }

        [Test]
        public void ThunderstormsNeedARealDownpourNotADrizzle()
        {
            Assert.Greater(Weather.ThunderstormThreshold, Weather.LightRain);
            Assert.Greater(Weather.ThunderstormThreshold, Weather.ModerateRain - 0.0001f);
            Assert.LessOrEqual(Weather.ThunderstormThreshold, Weather.HeavyRain);
        }
    }
}
