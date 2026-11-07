using CityX.View;
using CityX.ViewEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.Tests
{
    /// <summary>
    /// The light shafts (docs/LIGHTING.md) are on both renderers as their own half-size feature, after the city's air, and nothing else by
    /// their name is left there (they were a full-screen pass first, and both drawing would cost twice).
    /// </summary>
    public sealed class LightShaftsTests
    {
        [Test]
        public void EachRendererDrawsTheShaftsOnceAfterTheHaze()
        {
            foreach (string path in FullScreenPassSetup.RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                Assert.IsNotNull(data, path);
                int shafts = -1, haze = -1, named = 0;
                for (int i = 0; i < data.rendererFeatures.Count; i++)
                {
                    ScriptableRendererFeature feature = data.rendererFeatures[i];
                    if (feature == null) continue;
                    if (feature.name == GraphicsQuality.LightShaftsFeature)
                    {
                        named++;
                        shafts = i;
                        Assert.IsInstanceOf<LightShaftsFeature>(feature, $"{path}: the shafts are not their own half-size feature");
                        Assert.IsNotNull(((LightShaftsFeature)feature).Material, $"{path}: the shafts have no material");
                        Assert.AreEqual("CityX/LightShafts", ((LightShaftsFeature)feature).Material.shader.name);
                    }
                    if (feature.name == HazeSetup.FeatureName)
                    {
                        haze = i;
                    }
                }
                Assert.AreEqual(1, named, $"{path}: {named} features draw the light shafts (rebuild the scene)");
                Assert.Greater(shafts, haze, $"{path}: the shafts are drawn before the air they light");
            }
        }

        [Test]
        public void TheShaderTracesAndLaysOver()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LightShaftsSetup.MaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual(2, material.passCount);
            Assert.AreEqual("TRACE", material.GetPassName(0).ToUpperInvariant());
            Assert.AreEqual("LAYOVER", material.GetPassName(1).ToUpperInvariant());
        }
    }
}
