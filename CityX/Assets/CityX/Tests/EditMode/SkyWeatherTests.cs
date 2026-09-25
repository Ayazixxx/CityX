using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A storm greys the sky, the fog and the camera background TOGETHER --
    /// the dome's horizon end is the fog colour by construction, and a storm
    /// that greyed one and not the other would put the streamed world's
    /// edge back in view. These pin the pure colour rule and that
    /// DayNightCycle applies it to all three from the one colour.
    /// </summary>
    public sealed class SkyWeatherTests
    {
        private static readonly Color ClearHorizon = SkyCycle.SkyHorizonDay;
        private static readonly Color ClearTop = SkyCycle.SkyTopDay;

        [Test]
        public void ClearWeatherLeavesTheSkyExactlyAsComputed()
        {
            Assert.AreEqual(ClearTop, SkyCycle.Weathered(ClearTop, 0.0f, 0.0f));
            Assert.AreEqual(ClearHorizon, SkyCycle.Weathered(ClearHorizon, 0.0f, 0.0f));
        }

        [Test]
        public void AFullStormTakesTheBlueOutOfTheSky()
        {
            Color grey = SkyCycle.Weathered(ClearTop, 1.0f, 0.0f);

            Assert.Less(grey.grayscale, ClearTop.grayscale, "a cloud lid is darker than a clear sky");
            float clearSpread = Mathf.Max(ClearTop.r, ClearTop.g, ClearTop.b) - Mathf.Min(ClearTop.r, ClearTop.g, ClearTop.b);
            float greySpread = Mathf.Max(grey.r, grey.g, grey.b) - Mathf.Min(grey.r, grey.g, grey.b);
            Assert.Less(greySpread, clearSpread * 0.3f, "and nearly colourless -- blue-grey, not blue");
        }

        [Test]
        public void TheStormEasesInBetweenClearAndGrey()
        {
            float clear = ClearTop.grayscale;
            float half = SkyCycle.Weathered(ClearTop, 0.5f, 0.0f).grayscale;
            float full = SkyCycle.Weathered(ClearTop, 1.0f, 0.0f).grayscale;

            Assert.Less(half, clear);
            Assert.Greater(half, full);
        }

        [Test]
        public void ASunsetAndANightBothGoToTheSameCloudLid()
        {
            // The orange of a sunset is not a colour a cloud lid lets through.
            Color sunset = SkyCycle.Weathered(SkyCycle.SunsetTint, 1.0f, 0.0f);
            Assert.AreEqual(sunset.r, sunset.b, 0.12f, "the sunset's orange should be gone under a full overcast");
        }

        [Test]
        public void ALightningFlashLightsTheWholeSkyToNearWhite()
        {
            Color dark = SkyCycle.Weathered(ClearTop, 1.0f, 0.0f);
            Color lit = SkyCycle.Weathered(ClearTop, 1.0f, 1.0f);

            Assert.Greater(lit.grayscale, dark.grayscale + 0.3f, "a flash should visibly light the sky");
            Assert.Greater(lit.b, lit.r, "cold, not warm");
        }

        [Test]
        public void ANightSkyStaysDark()
        {
            Color night = SkyCycle.Weathered(SkyCycle.SkyTopNight, 1.0f, 0.0f);
            Assert.Less(night.grayscale, 0.05f);
        }

        [Test]
        public void OutOfRangeAmountsAreClamped()
        {
            Assert.AreEqual(SkyCycle.Weathered(ClearTop, 1.0f, 1.0f), SkyCycle.Weathered(ClearTop, 5.0f, 5.0f));
            Assert.AreEqual(ClearTop, SkyCycle.Weathered(ClearTop, -1.0f, -1.0f));
        }

        [Test]
        public void DayNightCycleGreysSkyFogAndBackgroundFromOneColour()
        {
            var go = new GameObject("Cycle");
            var cameraObject = new GameObject("Camera");
            var dome = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            // Apply writes the scene's global render settings; leave them as found.
            Color oldFog = RenderSettings.fogColor;
            Color oldAmbient = RenderSettings.ambientLight;
            var oldAmbientMode = RenderSettings.ambientMode;
            try
            {
                var cycle = go.AddComponent<DayNightCycle>();
                cycle.Sky = cameraObject.AddComponent<Camera>();
                cycle.SkyDome = dome;
                SkyCycle.State noon = SkyCycle.At(SkyCycle.DaySeconds * 0.5f);

                cycle.Overcast = 0.0f;
                cycle.Apply(noon);
                Color clearFog = RenderSettings.fogColor;
                Assert.AreEqual(noon.HorizonColour.r, clearFog.r, 1e-4f, "clear: fog is the horizon");

                cycle.Overcast = 1.0f;
                cycle.Apply(noon);
                Color stormFog = RenderSettings.fogColor;
                Assert.AreEqual(stormFog, cycle.Sky.backgroundColor, "the camera background must equal the fog");
                Assert.AreEqual(stormFog, SkyCycle.Weathered(noon.HorizonColour, 1.0f, 0.0f), "and both are the weathered horizon");
                Assert.Less(stormFog.grayscale, clearFog.grayscale, "a storm's fog is darker than a clear day's");

                cycle.Overcast = 0.0f;
                cycle.Apply(noon);
                Assert.AreEqual(clearFog, RenderSettings.fogColor, "clearing restores the sky exactly");
            }
            finally
            {
                RenderSettings.fogColor = oldFog;
                RenderSettings.ambientMode = oldAmbientMode;
                RenderSettings.ambientLight = oldAmbient;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(dome);
            }
        }
    }
}
