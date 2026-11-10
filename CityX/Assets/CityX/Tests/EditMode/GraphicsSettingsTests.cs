using System.IO;
using CityX.Core;
using CityX.UI;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.Tests
{
    /// <summary>
    /// The graphics settings (docs/GRAPHICS_SETTINGS.md): the presets, the menu's rows, the saved file, and the options put into force on the
    /// render pipeline and its effects (and taken back off).
    /// </summary>
    public sealed class GraphicsSettingsTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            GameSettings.DirectoryOverride = _directory;
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsQuality.Restore();
            GameSettings.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void ANewGameIsTheHighPresetTheGameAsItWasMade()
        {
            var fresh = new GraphicsOptions();
            Assert.AreEqual(GraphicsPreset.High, fresh.Preset);
            Assert.AreEqual(100, fresh.ResolutionScale);
            Assert.IsTrue(fresh.LightShafts && fresh.AmbientOcclusion && fresh.Reflections);
            Assert.AreEqual(ShadowDetail.High, fresh.Shadows);
        }

        [Test]
        public void EachPresetIsKnownAgainAndOneChangeMakesItCustom()
        {
            for (var preset = GraphicsPreset.Low; preset < GraphicsPreset.Custom; preset++)
            {
                Assert.AreEqual(preset, GraphicsOptions.Of(preset).Preset);
            }
            GraphicsOptions low = GraphicsOptions.Of(GraphicsPreset.Low);
            Assert.IsFalse(low.LightShafts || low.AmbientOcclusion || low.Reflections, "LOW still draws an effect it could drop");
            Assert.AreEqual(1, low.AntiAliasing);

            GraphicsOptions changed = GraphicsOptions.Of(GraphicsPreset.High);
            changed.LightShafts = false;
            Assert.AreEqual(GraphicsPreset.Custom, changed.Preset);
            changed = GraphicsOptions.Of(GraphicsPreset.High);
            changed.Reflections = false;
            Assert.AreEqual(GraphicsPreset.Custom, changed.Preset, "the reflections are no part of what makes a preset");
        }

        [Test]
        public void APresetLeavesTheResolutionScaleAsThePlayerSetIt()
        {
            var options = new GraphicsOptions { ResolutionScale = 70 };
            options.Use(GraphicsPreset.Low);
            Assert.AreEqual(70, options.ResolutionScale);
            Assert.AreEqual(GraphicsPreset.Low, options.Preset, "the resolution scale is no part of a preset");
        }

        [Test]
        public void ValuesTheMenuDoesNotOfferSnapToOnesItDoes()
        {
            GraphicsOptions clean = new GraphicsOptions { AntiAliasing = 8, ResolutionScale = 55, Shadows = (ShadowDetail)9 }.Clean();
            Assert.AreEqual(4, clean.AntiAliasing);
            Assert.AreEqual(50, clean.ResolutionScale);
            Assert.AreEqual(ShadowDetail.High, clean.Shadows);
        }

        [Test]
        public void ASettingsFileFromBeforeGraphicsLoadsAsTheHighPreset()
        {
            File.WriteAllText(GameSettings.Path_, "{\"MasterVolume\": 0.5, \"MaxFps\": 60}");
            GameSettingsData loaded = GameSettings.Load();
            Assert.AreEqual(60, loaded.MaxFps, "setup: the old file was not read");
            Assert.IsNotNull(loaded.Graphics);
            Assert.AreEqual(GraphicsPreset.High, loaded.Graphics.Preset);
            Assert.AreEqual(100, loaded.Graphics.ResolutionScale);
        }

        [Test]
        public void TheGraphicsAreSavedAndReadBack()
        {
            var data = new GameSettingsData { Graphics = GraphicsOptions.Of(GraphicsPreset.Medium) };
            data.Graphics.ResolutionScale = 85;
            Assert.IsTrue(GameSettings.Save(data));
            GraphicsOptions loaded = GameSettings.Load().Graphics;
            Assert.AreEqual(GraphicsPreset.Medium, loaded.Preset);
            Assert.AreEqual(85, loaded.ResolutionScale);
        }

        [Test]
        public void EveryRowStepsRoundAllItsChoicesBothWays()
        {
            for (int row = 0; row < GraphicsMenu.Rows.Length; row++)
            {
                GraphicsMenu.Row stepped = GraphicsMenu.Rows[row];
                var options = new GraphicsOptions();
                int start = stepped.Get(options);
                for (int step = 0; step < stepped.Steps; step++)
                {
                    Assert.AreNotEqual("?", GraphicsMenu.ValueOf(options, row), $"{stepped.Name} shows a value it has no name for");
                    GraphicsMenu.Step(options, row, 1);
                }
                Assert.AreEqual(start, stepped.Get(options), $"{stepped.Name} did not come round to where it started");
                GraphicsMenu.Step(options, row, -1);
                GraphicsMenu.Step(options, row, 1);
                Assert.AreEqual(start, stepped.Get(options), $"{stepped.Name}: back and forward again is not where it was");
                Assert.IsFalse(string.IsNullOrEmpty(stepped.Description), $"{stepped.Name} has no description");
            }
        }

        [Test]
        public void ThePresetRowSetsTheOthersAndShowsCustomWhenTheyMatchNone()
        {
            var options = new GraphicsOptions();
            GraphicsMenu.Step(options, GraphicsMenu.PresetRow, 1);   // HIGH wraps round to LOW
            Assert.AreEqual(GraphicsPreset.Low, options.Preset);
            Assert.IsFalse(options.LightShafts);
            Assert.AreEqual("LOW", GraphicsMenu.ValueOf(options, GraphicsMenu.PresetRow));

            options.LightShafts = true;
            Assert.AreEqual("CUSTOM", GraphicsMenu.ValueOf(options, GraphicsMenu.PresetRow));
            GraphicsMenu.Step(options, GraphicsMenu.PresetRow, -1);   // from CUSTOM the arrows land on a preset
            Assert.AreNotEqual(GraphicsPreset.Custom, options.Preset);
        }

        [Test]
        public void ShadowQualitiesBelowHighDrawSmallerShadowMapsWithFewerCascades()
        {
            Assert.AreEqual((4096, 4), GraphicsQuality.ShadowsOf(ShadowDetail.High, 4096, 4));
            Assert.AreEqual((2048, 2), GraphicsQuality.ShadowsOf(ShadowDetail.Medium, 4096, 4));
            Assert.AreEqual((1024, 2), GraphicsQuality.ShadowsOf(ShadowDetail.Low, 4096, 4));
            Assert.AreEqual((1024, 1), GraphicsQuality.ShadowsOf(ShadowDetail.Medium, 1024, 1), "a quality never draws more than the project's own");
        }

        [Test]
        public void TheLowPresetSwitchesTheEffectsOffAndRestoreBringsTheProjectBack()
        {
            var renderer = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            ScriptableRendererFeature shafts = renderer.rendererFeatures.Find(f => f != null && f.name == GraphicsQuality.LightShaftsFeature);
            ScriptableRendererFeature occlusion = renderer.rendererFeatures.Find(f => f != null && f.name == GraphicsQuality.AmbientOcclusionFeature);
            Assert.IsNotNull(shafts, "no light shafts on the PC renderer: rebuild the scene");
            Assert.IsNotNull(occlusion, "no ambient occlusion on the PC renderer");
            var pipeline = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline)
                as UniversalRenderPipelineAsset;
            Assert.IsNotNull(pipeline);
            int msaa = pipeline.msaaSampleCount, resolution = pipeline.mainLightShadowmapResolution, cascades = pipeline.shadowCascadeCount;
            bool shaftsWere = shafts.isActive, occlusionWas = occlusion.isActive;

            var low = GraphicsOptions.Of(GraphicsPreset.Low);
            low.ResolutionScale = 50;
            GraphicsQuality.Apply(low);
            Assert.IsFalse(shafts.isActive, "the light shafts are still drawn");
            Assert.IsFalse(occlusion.isActive, "the ambient occlusion is still drawn");
            Assert.AreEqual(1, pipeline.msaaSampleCount);
            Assert.AreEqual(GraphicsQuality.ShadowsOf(ShadowDetail.Low, resolution, cascades),
                (pipeline.mainLightShadowmapResolution, pipeline.shadowCascadeCount));
            Assert.AreEqual(0.5f, pipeline.renderScale, 1e-4f);
            Assert.AreEqual(UpscalingFilterSelection.FSR, pipeline.upscalingFilter);

            GraphicsQuality.Restore();
            Assert.AreEqual(shaftsWere, shafts.isActive);
            Assert.AreEqual(occlusionWas, occlusion.isActive);
            Assert.AreEqual((msaa, resolution, cascades), (pipeline.msaaSampleCount, pipeline.mainLightShadowmapResolution, pipeline.shadowCascadeCount));
            Assert.AreEqual(1.0f, pipeline.renderScale, 1e-4f);
        }
    }
}
