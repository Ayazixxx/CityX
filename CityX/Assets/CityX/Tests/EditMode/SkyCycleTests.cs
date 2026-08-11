using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The day/night cycle, sampled at any moment instead of waited out.
    ///
    /// This is the whole reason SkyCycle is a pure function rather than a
    /// _process that mutates lights: a full cycle is 34 minutes, so the Godot
    /// original could only be checked by sitting and watching it, and the
    /// interesting moments -- the two horizon crossings -- are seconds long.
    /// </summary>
    public class SkyCycleTests
    {
        /// <summary>Samples across the whole cycle, fine enough to catch dusk.</summary>
        private const int Samples = 2048;

        [Test]
        public void ADayIsTwentyMinutesAndANightIsFourteen()
        {
            Assert.AreEqual(20.0f * 60.0f, SkyCycle.DaySeconds, 0.001f);
            Assert.AreEqual(14.0f * 60.0f, SkyCycle.NightSeconds, 0.001f);
            Assert.AreEqual(34.0f * 60.0f, SkyCycle.CycleSeconds, 0.001f);
        }

        /// <summary>
        /// The sun is up for exactly the day and down for exactly the night --
        /// which is the point of splitting the two halves at different rates.
        /// Measured by sampling, so it checks the pitch function rather than
        /// restating the constants.
        /// </summary>
        [Test]
        public void TheSunIsUpForTheDayAndDownForTheNight()
        {
            int up = 0;
            for (int i = 0; i < Samples; i++)
            {
                float t = SkyCycle.CycleSeconds * i / Samples;
                if (SkyCycle.At(t).IsDaytime)
                {
                    up++;
                }
            }

            float measured = SkyCycle.CycleSeconds * up / Samples;
            Assert.AreEqual(SkyCycle.DaySeconds, measured, SkyCycle.CycleSeconds / Samples * 2.0f,
                "the sun spends the wrong share of the cycle above the horizon");
        }

        [Test]
        public void TheCycleWrapsSoTheWorldNeverRunsOutOfDays()
        {
            SkyCycle.State start = SkyCycle.At(0.0f);
            SkyCycle.State later = SkyCycle.At(SkyCycle.CycleSeconds);
            SkyCycle.State negative = SkyCycle.At(-SkyCycle.CycleSeconds);

            Assert.AreEqual(start.SunPitchDegrees, later.SunPitchDegrees, 0.01f,
                "a full cycle did not return to where it started");
            Assert.AreEqual(start.SunPitchDegrees, negative.SunPitchDegrees, 0.01f,
                "negative time does not wrap");
        }

        [Test]
        public void NoonIsFullyLitAndMidnightIsNot()
        {
            SkyCycle.State noon = SkyCycle.At(SkyCycle.DaySeconds * 0.5f);
            Assert.AreEqual(1.0f, noon.Daylight, 0.001f, "noon is not full daylight");
            Assert.AreEqual(SkyCycle.SunIntensityNoon, noon.SunIntensity, 0.001f,
                "the sun is not at full strength at noon");
            Assert.IsTrue(noon.SunCastsShadow);
            Assert.IsFalse(noon.MoonCastsShadow, "the moon casts a shadow at midday");

            SkyCycle.State midnight = SkyCycle.At(
                SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f);
            Assert.AreEqual(0.0f, midnight.Daylight, 0.001f, "midnight is not fully dark");
            Assert.AreEqual(0.0f, midnight.SunIntensity, 0.001f, "the sun still shines at midnight");
            Assert.Greater(midnight.MoonIntensity, 0.0f, "the moon gives no light at midnight");
            Assert.IsFalse(midnight.SunCastsShadow);
            Assert.IsTrue(midnight.MoonCastsShadow);
        }

        /// <summary>
        /// Never two shadows. A light at zero intensity still runs its shadow
        /// pass, so leaving both enabled renders two shadow directions blended
        /// together -- a real bug in the Godot build, reported as "there should
        /// just be one shadow".
        /// </summary>
        [Test]
        public void OnlyOneBodyEverCastsAShadow()
        {
            for (int i = 0; i < Samples; i++)
            {
                float t = SkyCycle.CycleSeconds * i / Samples;
                SkyCycle.State state = SkyCycle.At(t);

                Assert.IsFalse(state.SunCastsShadow && state.MoonCastsShadow,
                    $"both bodies cast a shadow at {t:0}s into the cycle");
            }
        }

        /// <summary>
        /// The sun and moon stay exactly half a turn apart, on one arc. If they
        /// drift, both can be above the horizon at once and the world gets a
        /// physically impossible double noon.
        /// </summary>
        [Test]
        public void TheMoonStaysOppositeTheSun()
        {
            for (int i = 0; i < Samples; i++)
            {
                float t = SkyCycle.CycleSeconds * i / Samples;
                SkyCycle.State state = SkyCycle.At(t);

                Assert.AreEqual(180.0f, state.SunPitchDegrees - state.MoonPitchDegrees, 0.001f);
                Assert.AreEqual(-state.SunElevationSine, state.MoonElevationSine, 0.001f,
                    "the two bodies are not on opposite sides of the horizon");
            }
        }

        /// <summary>
        /// The sunset glow peaks at BOTH horizon crossings -- sunrise and
        /// sunset are the same physical moment in opposite directions -- and is
        /// gone by the middle of the day and the middle of the night.
        /// </summary>
        [Test]
        public void TheGlowPeaksAtBothHorizonCrossings()
        {
            Assert.AreEqual(1.0f, SkyCycle.At(0.0f).SunsetAmount, 0.01f,
                "sunrise is not a sunset");
            Assert.AreEqual(1.0f, SkyCycle.At(SkyCycle.DaySeconds).SunsetAmount, 0.01f,
                "sunset is not a sunset");

            Assert.AreEqual(0.0f, SkyCycle.At(SkyCycle.DaySeconds * 0.5f).SunsetAmount, 0.01f,
                "noon has a sunset glow");
            Assert.AreEqual(0.0f,
                SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f).SunsetAmount, 0.01f,
                "midnight has a sunset glow");
        }

        /// <summary>
        /// Dusk has to arrive gradually. If daylight jumped, the whole world's
        /// ambient would snap between two colours in a single frame.
        /// </summary>
        [Test]
        public void DaylightChangesSmoothlyRatherThanSnapping()
        {
            float previous = SkyCycle.At(0.0f).Daylight;
            float step = SkyCycle.CycleSeconds / Samples;

            for (int i = 1; i <= Samples; i++)
            {
                float current = SkyCycle.At(step * i).Daylight;
                Assert.Less(Mathf.Abs(current - previous), 0.05f,
                    $"daylight jumped by {Mathf.Abs(current - previous):0.00} at {step * i:0}s");
                previous = current;
            }
        }

        /// <summary>
        /// Nothing may go negative, NaN, or brighter than it is meant to. A
        /// negative light intensity darkens surfaces it hits, which looks like
        /// holes in the world.
        /// </summary>
        [Test]
        public void NothingIsEverNegativeOrNotANumber()
        {
            for (int i = 0; i < Samples; i++)
            {
                float t = SkyCycle.CycleSeconds * i / Samples;
                SkyCycle.State state = SkyCycle.At(t);

                Assert.IsFalse(float.IsNaN(state.SunIntensity), $"NaN sun intensity at {t:0}s");
                Assert.GreaterOrEqual(state.SunIntensity, 0.0f, $"negative sun at {t:0}s");
                Assert.LessOrEqual(state.SunIntensity, SkyCycle.SunIntensityNoon + 0.001f,
                    $"the sun is brighter than noon at {t:0}s");

                Assert.GreaterOrEqual(state.MoonIntensity, 0.0f, $"negative moon at {t:0}s");
                Assert.LessOrEqual(state.MoonIntensity, SkyCycle.MoonIntensityPeak + 0.001f);

                Assert.GreaterOrEqual(state.Daylight, 0.0f);
                Assert.LessOrEqual(state.Daylight, 1.0f);
                Assert.GreaterOrEqual(state.SunsetAmount, 0.0f);
                Assert.LessOrEqual(state.SunsetAmount, 1.0f);

                AssertSaneColour(state.SunColour, "sun", t);
                AssertSaneColour(state.AmbientSky, "ambient sky", t);
                AssertSaneColour(state.AmbientEquator, "ambient equator", t);
                AssertSaneColour(state.AmbientGround, "ambient ground", t);
                AssertSaneColour(state.HorizonColour, "horizon", t);
            }
        }

        /// <summary>
        /// Night must actually be darker than day. Easy to get wrong once a
        /// sunset tint is being lerped over the top of the blend.
        /// </summary>
        [Test]
        public void NightIsDarkerThanDay()
        {
            float noon = Luminance(SkyCycle.At(SkyCycle.DaySeconds * 0.5f).AmbientSky);
            float midnight = Luminance(
                SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f).AmbientSky);

            Assert.Less(midnight, noon * 0.5f,
                "the night ambient is not meaningfully darker than the day's");
        }

        /// <summary>
        /// The daytime ambient must be exactly the massing scene's tuned value.
        /// The cycle was built around the look already approved rather than
        /// replacing it, and this is what pins that down.
        /// </summary>
        [Test]
        public void NoonMatchesTheAmbientTheCityWasTunedUnder()
        {
            SkyCycle.State noon = SkyCycle.At(SkyCycle.DaySeconds * 0.5f);
            Assert.AreEqual(SkyCycle.AmbientSkyDay, noon.AmbientSky);
            Assert.AreEqual(SkyCycle.AmbientEquatorDay, noon.AmbientEquator);
            Assert.AreEqual(SkyCycle.AmbientGroundDay, noon.AmbientGround);
        }

        private static void AssertSaneColour(Color c, string what, float t)
        {
            Assert.IsFalse(float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b),
                $"NaN in the {what} colour at {t:0}s");
            Assert.GreaterOrEqual(Mathf.Min(c.r, Mathf.Min(c.g, c.b)), 0.0f,
                $"negative channel in the {what} colour at {t:0}s");
            Assert.LessOrEqual(Mathf.Max(c.r, Mathf.Max(c.g, c.b)), 1.001f,
                $"the {what} colour is over-bright at {t:0}s");
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }
    }
}
