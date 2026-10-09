using CityX.View;
using CityX.ViewEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.Tests
{
    /// <summary>The city's air (CityHaze): the numbers, the day/night cycle driving it instead of the old fog, and the pass on the renderer.</summary>
    public sealed class CityHazeTests
    {
        private static readonly Color Horizon = new Color(0.62f, 0.70f, 0.80f);
        private static readonly Color Top = new Color(0.25f, 0.42f, 0.75f);

        [Test]
        public void AStormThickensTheAir()
        {
            CityHaze.Settings clear = CityHaze.For(Horizon, Top, 1.0f, Color.white, 1.0f, 0.0f, 1.0f, 150.0f, 380.0f);
            CityHaze.Settings storm = CityHaze.For(Horizon, Top, 1.0f, Color.white, 1.0f, 1.0f, 0.05f, 150.0f, 380.0f);
            Assert.AreEqual(CityHaze.ClearDensity, clear.Density, 1e-6f);
            Assert.Greater(storm.Density, clear.Density * 2.0f);
        }

        [Test]
        public void ANearStreetIsClearAndAFarSkylineIsHazed()
        {
            // At street level, along the ground: a building across the street barely hazed, one at the far end of the streamed city mostly.
            float d = CityHaze.ClearDensity;
            float near = 1.0f - Mathf.Exp(-d * 30.0f);
            float far = 1.0f - Mathf.Exp(-d * 300.0f);
            Assert.Less(near, 0.06f, "the street you stand in should not look foggy");
            Assert.Greater(far, 0.3f, "a tower 300 m away should be visibly softer than one across the street");
        }

        [Test]
        public void ThePlayerWalksInClearAir()
        {
            // No haze round the player; it builds up gently, and is at full thickness well before the city's edge starts to fade.
            CityHaze.Settings clear = CityHaze.For(Horizon, Top, 1.0f, Color.white, 1.0f, 0.0f, 1.0f, 150.0f, 380.0f);
            Assert.Greater(CityHaze.ClearRadius, 15.0f, "the street round the player must be clear");
            Assert.Greater(CityHaze.FadeInEnd - CityHaze.ClearRadius, 50.0f, "the haze should fade in slowly, not start at a wall");
            Assert.Less(CityHaze.FadeInEnd, clear.EdgeStart);
            CityHaze.Push(clear, Vector3.up);
            try
            {
                Vector4 edge = Shader.GetGlobalVector("_CityXHazeEdge");
                Assert.AreEqual(CityHaze.ClearRadius, edge.z, 1e-4f);
                Assert.AreEqual(CityHaze.FadeInEnd, edge.w, 1e-4f);
            }
            finally
            {
                CityHaze.Clear();
            }
        }

        [Test]
        public void TheSunGlowsOnlyWhenItIsUp()
        {
            CityHaze.Settings night = CityHaze.For(Horizon, Top, 1.0f, new Color(1.0f, 0.8f, 0.6f), 0.0f, 0.0f, 1.0f, 150.0f, 380.0f);
            CityHaze.Settings day = CityHaze.For(Horizon, Top, 1.0f, new Color(1.0f, 0.8f, 0.6f), 1.5f, 0.0f, 1.0f, 150.0f, 380.0f);
            Assert.AreEqual(Horizon.r, night.SunColour.r, 1e-5f);
            Assert.AreEqual(Horizon.b, night.SunColour.b, 1e-5f);
            Assert.Greater(day.SunColour.r, Horizon.r + 0.2f);
        }

        [Test]
        public void TheEdgeOfTheStreamedCityIsHiddenByTheFogEnd()
        {
            foreach ((float start, float end) in new[] { (150.0f, 380.0f), (60.0f, 170.0f), (4.0f, 20.0f) })
            {
                CityHaze.Settings s = CityHaze.For(Horizon, Top, 1.0f, Color.white, 1.0f, 0.0f, 1.0f, start, end);
                Assert.AreEqual(end, s.EdgeEnd, 1e-4f, "everything past the fog end must be all haze");
                Assert.Greater(s.EdgeStart, start - 1e-4f);
                Assert.Less(s.EdgeStart, s.EdgeEnd);
            }
        }

        [Test]
        public void TheDayNightCycleDrawsTheHazeInsteadOfTheFog()
        {
            bool fog = RenderSettings.fog;
            var go = new GameObject("Cycle");
            var sunObject = new GameObject("Sun");
            try
            {
                var cycle = go.AddComponent<DayNightCycle>();
                cycle.Sun = sunObject.AddComponent<Light>();
                cycle.Sun.type = LightType.Directional;
                cycle.Apply(SkyCycle.At(SkyCycle.DaySeconds * 0.5f));
                Assert.IsFalse(RenderSettings.fog, "the built-in fog would stack on the haze");
                Assert.Greater(CityHaze.CurrentDensity, 0.0f, "the haze is off");

                cycle.HazeDrawn = false;
                cycle.Apply(SkyCycle.At(SkyCycle.DaySeconds * 0.5f));
                Assert.IsTrue(RenderSettings.fog, "without the haze the old fog must hide the city's edge");
                Assert.AreEqual(0.0f, CityHaze.CurrentDensity);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sunObject);
                CityHaze.Clear();
                RenderSettings.fog = fog;
            }
        }

        [Test]
        public void ARemovedCycleTakesItsAirWithIt()
        {
            var go = new GameObject("Cycle");
            var cycle = go.AddComponent<DayNightCycle>();
            cycle.Apply(SkyCycle.At(SkyCycle.DaySeconds * 0.5f));
            Assert.Greater(CityHaze.CurrentDensity, 0.0f);
            Object.DestroyImmediate(go);
            Assert.AreEqual(0.0f, CityHaze.CurrentDensity, "the title screen would inherit the city's haze");
        }

        [Test]
        public void EveryRendererDrawsTheHazeBeforePostProcessing()
        {
            foreach (string path in HazeSetup.RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                Assert.IsNotNull(data, path);
                FullScreenPassRendererFeature haze = null;
                foreach (ScriptableRendererFeature f in data.rendererFeatures)
                {
                    if (f is FullScreenPassRendererFeature full && f.name == HazeSetup.FeatureName)
                    {
                        haze = full;
                    }
                }
                Assert.IsNotNull(haze, $"{path} has no {HazeSetup.FeatureName} pass: run the scene builder");
                Assert.IsTrue(haze.isActive, path);
                Assert.IsNotNull(haze.passMaterial, path);
                Assert.AreEqual("CityX/Haze", haze.passMaterial.shader.name, path);
                Assert.AreEqual(FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing, haze.injectionPoint, path);
                Assert.IsTrue((haze.requirements & ScriptableRenderPassInput.Depth) != 0, $"{path}: the haze needs the depth of every pixel");
            }
        }
    }
}
