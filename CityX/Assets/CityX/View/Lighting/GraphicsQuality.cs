using System.Collections.Generic;
using CityX.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// Puts <see cref="GraphicsOptions"/> into force (docs/GRAPHICS_SETTINGS.md): each effect's renderer feature switched on or off, and the sun's
    /// shadows, the anti-aliasing and the resolution scale set on the render pipeline. The saved choice goes in as the game starts. The
    /// features and the pipeline are project assets, so in the editor everything goes back as it was when play stops (<see cref="Restore"/>);
    /// a change made while playing would otherwise stay in the project.
    /// </summary>
    public static class GraphicsQuality
    {
        public const string LightShaftsFeature = "CityXLightShafts";
        public const string AmbientOcclusionFeature = "ScreenSpaceAmbientOcclusion";

        /// <summary>What the shadow qualities below High draw: the sun's shadow map's side in pixels, and its cascades.</summary>
        public const int MediumShadowResolution = 2048;

        public const int LowShadowResolution = 1024;

        public const int CascadesBelowHigh = 2;

        /// <summary>The options in force now.</summary>
        public static GraphicsOptions Current { get; private set; } = new GraphicsOptions();

        private static readonly Dictionary<ScriptableRendererFeature, bool> FeaturesWere = new Dictionary<ScriptableRendererFeature, bool>();
        private static UniversalRenderPipelineAsset _pipeline;
        private static int _msaaWas, _shadowResolutionWas, _cascadesWas;
        private static float _renderScaleWas;
        private static UpscalingFilterSelection _upscalingWas;

        /// <summary>The sun's shadow map for <paramref name="quality"/>: High is the project's own (<paramref name="resolution"/>, <paramref name="cascades"/>).</summary>
        public static (int resolution, int cascades) ShadowsOf(ShadowDetail quality, int resolution, int cascades)
        {
            switch (quality)
            {
                case ShadowDetail.Low:
                    return (Mathf.Min(resolution, LowShadowResolution), Mathf.Min(cascades, CascadesBelowHigh));
                case ShadowDetail.Medium:
                    return (Mathf.Min(resolution, MediumShadowResolution), Mathf.Min(cascades, CascadesBelowHigh));
                default:
                    return (resolution, cascades);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplySaved()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= RestoreOnStop;
            UnityEditor.EditorApplication.playModeStateChanged += RestoreOnStop;
#endif
            // Tests and the builders see the project as it is made, whatever this machine's player chose.
            if (!Application.isBatchMode)
            {
                Apply(GameSettings.Load().Graphics);
            }
        }

#if UNITY_EDITOR
        private static void RestoreOnStop(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Restore();
            }
        }
#endif

        public static void Apply(GraphicsOptions options)
        {
            options = (options ?? new GraphicsOptions()).Clean();
            Current = options;
            SetFeature(LightShaftsFeature, options.LightShafts);
            SetFeature(AmbientOcclusionFeature, options.AmbientOcclusion);

            UniversalRenderPipelineAsset pipeline = Pipeline();
            if (pipeline == null)
            {
                return;
            }
            if (_pipeline != pipeline)
            {
                RestorePipeline();
                _pipeline = pipeline;
                _msaaWas = pipeline.msaaSampleCount;
                _shadowResolutionWas = pipeline.mainLightShadowmapResolution;
                _cascadesWas = pipeline.shadowCascadeCount;
                _renderScaleWas = pipeline.renderScale;
                _upscalingWas = pipeline.upscalingFilter;
            }
            (int resolution, int cascades) = ShadowsOf(options.Shadows, _shadowResolutionWas, _cascadesWas);
            pipeline.mainLightShadowmapResolution = resolution;
            pipeline.shadowCascadeCount = cascades;
            pipeline.msaaSampleCount = options.AntiAliasing;
            pipeline.renderScale = options.ResolutionScale / 100.0f;
            // Stretched with AMD's FidelityFX upscaler, which sharpens what bilinear would blur.
            pipeline.upscalingFilter = options.ResolutionScale < 100 ? UpscalingFilterSelection.FSR : _upscalingWas;
        }

        /// <summary>The project's own settings back, as they were before anything was applied.</summary>
        public static void Restore()
        {
            foreach (KeyValuePair<ScriptableRendererFeature, bool> feature in FeaturesWere)
            {
                if (feature.Key != null)
                {
                    feature.Key.SetActive(feature.Value);
                }
            }
            FeaturesWere.Clear();
            RestorePipeline();
            Current = new GraphicsOptions();
        }

        private static void RestorePipeline()
        {
            if (_pipeline == null)
            {
                return;
            }
            _pipeline.msaaSampleCount = _msaaWas;
            _pipeline.mainLightShadowmapResolution = _shadowResolutionWas;
            _pipeline.shadowCascadeCount = _cascadesWas;
            _pipeline.renderScale = _renderScaleWas;
            _pipeline.upscalingFilter = _upscalingWas;
            _pipeline = null;
        }

        private static UniversalRenderPipelineAsset Pipeline()
        {
            return (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline)
                as UniversalRenderPipelineAsset;
        }

        /// <summary>Every renderer feature called <paramref name="name"/> (the PC and the mobile renderer each have one), on or off.</summary>
        private static void SetFeature(string name, bool on)
        {
            foreach (ScriptableRendererFeature feature in Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>())
            {
                if (feature.name != name)
                {
                    continue;
                }
                if (!FeaturesWere.ContainsKey(feature))
                {
                    FeaturesWere[feature] = feature.isActive;
                }
                feature.SetActive(on);
            }
        }
    }
}
