using System.Collections.Generic;
using System.IO;
using CityX.Core;
using CityX.View;
using CityX.ViewEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.Tests
{
    /// <summary>
    /// The cel look (owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines", "players and buildings and
    /// terrain, everything"): every material is on a cel-shaded shader and nothing made later can slip out of it, CityX/Lit is URP's Lit (every
    /// property there, so a material moved onto it lost nothing), the black lines are drawn on every renderer, and the look is one switch.
    /// </summary>
    public sealed class CelShadingTests
    {
        private const string UrpLitName = "Universal Render Pipeline" + "/Lit";   // in two, or this file would be its own find
        private const string UrpLitGuid = "933532a4fcc9baf4fa0491de14d08ed7";
        private const string CodeRoot = "Assets/CityX";

        private static IEnumerable<string> FilesUnder(string extension, bool withTests)
        {
            foreach (string path in Directory.GetFiles(CodeRoot, "*" + extension, SearchOption.AllDirectories))
            {
                string unix = path.Replace('\\', '/');
                if (withTests || !unix.Contains("/Tests/"))
                {
                    yield return unix;
                }
            }
        }

        [Test]
        public void NoMaterialIsOnUrpsOwnLit()
        {
            var left = new List<string>();
            foreach (string path in FilesUnder(".mat", true))
            {
                if (File.ReadAllText(path).Contains("guid: " + UrpLitGuid))
                {
                    left.Add(path);
                }
            }
            CollectionAssert.IsEmpty(left, "these materials are still on URP's Lit, out of the cel look: " + string.Join(", ", left));
        }

        [Test]
        public void NoCodeMakesAMaterialOnUrpsOwnLit()
        {
            var left = new List<string>();
            foreach (string path in FilesUnder(".cs", true))
            {
                if (File.ReadAllText(path).Contains("\"" + UrpLitName + "\""))
                {
                    left.Add(path);
                }
            }
            CollectionAssert.IsEmpty(left, "this code asks for URP's Lit by name (use CityX.Core.LitShader): " + string.Join(", ", left));
        }

        [Test]
        public void CityXLitHasEveryPropertyOfUrpsLit()
        {
            Shader urp = Shader.Find(UrpLitName);
            Shader ours = LitShader.Find();
            Assert.IsNotNull(ours, $"no {LitShader.Name} shader");
            Assert.AreEqual(urp.GetPropertyCount(), ours.GetPropertyCount(), "CityX/Lit's properties are not URP Lit's");
            for (int i = 0; i < urp.GetPropertyCount(); i++)
            {
                int at = ours.FindPropertyIndex(urp.GetPropertyName(i));
                Assert.GreaterOrEqual(at, 0, $"CityX/Lit has no {urp.GetPropertyName(i)}");
                Assert.AreEqual(urp.GetPropertyType(i), ours.GetPropertyType(at), urp.GetPropertyName(i));
            }
            Assert.AreEqual(urp.passCount, ours.passCount, "CityX/Lit does not have URP Lit's passes");
        }

        [Test]
        public void EveryRendererDrawsTheLinesAfterTheOpaqueWorldFromItsDepthAndNormals()
        {
            foreach (string path in FullScreenPassSetup.RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                Assert.IsNotNull(data, path);
                FullScreenPassRendererFeature lines = null;
                foreach (ScriptableRendererFeature f in data.rendererFeatures)
                {
                    if (f is FullScreenPassRendererFeature full && f.name == OutlineSetup.FeatureName)
                    {
                        lines = full;
                    }
                }
                Assert.IsNotNull(lines, $"{path} has no {OutlineSetup.FeatureName} pass: run the scene builder");
                Assert.IsTrue(lines.isActive, path);
                Assert.AreEqual("CityX/Outline", lines.passMaterial.shader.name, path);
                Assert.AreEqual(FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents, lines.injectionPoint,
                    $"{path}: the lines go on the opaque world, under the glass and the air");
                Assert.IsTrue((lines.requirements & ScriptableRenderPassInput.Depth) != 0, $"{path}: the outlines need the depth");
                Assert.IsTrue((lines.requirements & ScriptableRenderPassInput.Normal) != 0, $"{path}: the folds need the normals");
            }
        }

        [Test]
        public void TheCelLookIsOneSwitchWithItsNumbersSet()
        {
            bool wasOn = CelShading.Enabled;
            try
            {
                CelShading.Set(false);
                Assert.IsFalse(Shader.IsKeywordEnabled(GlobalKeyword.Create(CelShading.Keyword)));
                CelShading.Set(true);
                Assert.IsTrue(Shader.IsKeywordEnabled(GlobalKeyword.Create(CelShading.Keyword)));
                Vector4 sun = Shader.GetGlobalVector("_CityXToonSun");
                Assert.Greater(sun.w, sun.z, "the lit side is no brighter than the shade side");
                Assert.Greater(Shader.GetGlobalVector("_CityXOutline").w, 0.0f, "the lines are off");
                Assert.Less(Shader.GetGlobalVector("_CityXOutlineFade").x, Shader.GetGlobalVector("_CityXOutlineFade").y);
                Assert.AreEqual(Color.black, Shader.GetGlobalColor("_CityXOutlineColor"), "the lines are not black");
            }
            finally
            {
                CelShading.Set(wasOn);
            }
        }
    }
}
