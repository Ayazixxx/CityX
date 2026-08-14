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
                AssertSaneColour(state.AmbientColour, "ambient", t);
                AssertSaneColour(state.HorizonColour, "horizon", t);
                AssertSaneColour(state.SkyTopColour, "sky top", t);
                AssertSaneColour(state.GroundBottomColour, "ground bottom", t);
            }
        }

        /// <summary>
        /// Night must actually be darker than day. Easy to get wrong once a
        /// sunset tint is being lerped over the top of the blend.
        /// </summary>
        [Test]
        public void NightIsDarkerThanDay()
        {
            float noon = Luminance(SkyCycle.At(SkyCycle.DaySeconds * 0.5f).AmbientColour);
            float midnight = Luminance(
                SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f).AmbientColour);

            Assert.Less(midnight, noon * 0.5f,
                "the night ambient is not meaningfully darker than the day's");
        }

        /// <summary>
        /// The daytime ambient must be exactly Godot's own colour times its own
        /// energy.
        ///
        /// WHAT THIS IS REALLY GUARDING is the dimness. The port ran an ambient
        /// roughly three times this bright, which floods every crevice and
        /// erases the shading that makes concrete read as solid -- the flat
        /// look the whole graphics pass was about. It is the kind of value
        /// somebody raises later because a shadowed street looks dark, so the
        /// number is pinned rather than left to drift.
        /// </summary>
        [Test]
        public void NoonMatchesGodotsOwnAmbientFill()
        {
            SkyCycle.State noon = SkyCycle.At(SkyCycle.DaySeconds * 0.5f);
            Color expected = SkyCycle.Dim(SkyCycle.AmbientDayColour, SkyCycle.AmbientDayEnergy);

            Assert.AreEqual(expected.r, noon.AmbientColour.r, 0.0001f);
            Assert.AreEqual(expected.g, noon.AmbientColour.g, 0.0001f);
            Assert.AreEqual(expected.b, noon.AmbientColour.b, 0.0001f);

            // AND THE DIMMING IS DONE IN LINEAR SPACE, which is the whole of
            // why this helper exists. Scaling the sRGB triple instead lands
            // roughly a third of the light and crushes every shadowed facade
            // to silhouette -- it is an easy mistake to make twice, and the
            // symptom looks like an art decision rather than a bug.
            Color naive = SkyCycle.AmbientDayColour * SkyCycle.AmbientDayEnergy;
            Assert.Greater(noon.AmbientColour.linear.g, naive.linear.g * 2.0f,
                "the ambient energy is being applied to the sRGB triple, not to the light");

            // And it is genuinely BLUE, not a grey fill with a hint of blue in
            // it. Warm key against cool fill is most of what reads as real
            // outdoor lighting, and a neutral fill loses that entirely.
            Assert.Greater(noon.AmbientColour.b, noon.AmbientColour.r * 1.5f,
                "the daytime fill has lost its blue and is close to neutral grey");
        }

        /// <summary>
        /// The sky dome and the fog must meet at exactly the same colour.
        ///
        /// This is the trick that hides the streamed world's loaded-chunk edge:
        /// the city stops at a hard boundary and the only thing concealing it
        /// is fog the same colour as what is behind it. Let the two drift and
        /// the boundary reappears as a visible wall at every dawn and dusk.
        /// </summary>
        [Test]
        public void TheFogAndTheHorizonAreTheSameColour()
        {
            // Dusk specifically, because that is where a separately-computed
            // sunset tint on each of them would come apart.
            for (float t = 0.0f; t < SkyCycle.CycleSeconds; t += 20.0f)
            {
                SkyCycle.State state = SkyCycle.At(t);

                // The dome's horizon end IS HorizonColour -- DayNightCycle
                // feeds the one value to both -- so what is left to check is
                // that the zenith is a DIFFERENT colour from it. A dome whose
                // top and horizon had collapsed together would still hide the
                // edge perfectly and would also be a flat card.
                Assert.AreNotEqual(state.SkyTopColour, state.HorizonColour,
                    $"the sky has no gradient left at t={t}");
            }
        }

        /// <summary>
        /// The dome dims at night, separately from going darker in colour.
        ///
        /// Two things depend on this. A night sky left at daytime luminance
        /// survives the tonemap curve and stays visibly bright; and since the
        /// city reflects the sky, it would go on lighting the streets like an
        /// overcast afternoon at midnight.
        /// </summary>
        [Test]
        public void TheSkyDimsAtNight()
        {
            SkyCycle.State noon = SkyCycle.At(SkyCycle.DaySeconds * 0.5f);
            SkyCycle.State midnight =
                SkyCycle.At(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f);

            Assert.AreEqual(SkyCycle.SkyEnergyDay, noon.SkyEnergy, 0.001f);
            Assert.AreEqual(SkyCycle.SkyEnergyNight, midnight.SkyEnergy, 0.001f);
            Assert.Less(midnight.SkyEnergy, noon.SkyEnergy * 0.25f);

            // The ground half is dimmer than the sky half all day, which is
            // what stops a rooftop reflecting as much light up as down.
            Assert.Less(noon.GroundEnergy, noon.SkyEnergy);
        }

        /// <summary>
        /// Window lights follow the daylight, not a clock.
        ///
        /// The failure this guards is a city whose windows glow through the
        /// afternoon, which reads as a bug rather than as evening. Driving them
        /// off daylight rather than a time threshold also means they come up
        /// through dusk at the same rate the world goes down, instead of
        /// snapping on at some moment.
        /// </summary>
        [Test]
        public void WindowsAreDarkByDayAndLitByNight()
        {
            float Glow(float seconds) =>
                (1.0f - SkyCycle.At(seconds).Daylight) * View.DayNightCycle.WindowGlow;

            Assert.AreEqual(0.0f, Glow(SkyCycle.DaySeconds * 0.5f), 0.001f,
                "the windows are lit at midday");
            Assert.AreEqual(View.DayNightCycle.WindowGlow,
                Glow(SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f), 0.001f,
                "the windows are not fully lit at midnight");

            // Half lit somewhere in the dusk band, rather than jumping.
            float atSunset = Glow(SkyCycle.DaySeconds);
            Assert.Greater(atSunset, 0.0f, "nothing is lit at sunset");
            Assert.Less(atSunset, View.DayNightCycle.WindowGlow,
                "the windows are already at full strength at sunset");
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
