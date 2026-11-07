#if CITYX_BENCH
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CityX.Character;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    // -bench-effects SECONDS: what each graphics effect costs (owner, 2026-11-07: "let me know if it is effecting performance at all, all the
    // graphics and lighting changes we have made"). Standing still, by day and then by night, the graphics card's time a frame with
    // everything on, with each effect off in turn, with all of them off, and with everything on again (how far the numbers drift).
    public sealed partial class PlayerFrameBenchmark
    {
        private sealed class Effect
        {
            public string Name;
            public Action Off;
            public Action On;
        }

        private IEnumerator MeasureEffects(StringBuilder sb, PlayerLocomotion locomotion, float seconds)
        {
            var sky = FindFirstObjectByType<DayNightCycle>();
            (string name, float time)[] times =
            {
                ("DAY (mid-morning)", SkyCycle.DaySeconds * 0.35f),
                ("NIGHT (lamps and windows lit)", SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f),
            };
            foreach ((string when, float time) in times)
            {
                if (sky != null)
                {
                    sky.Running = false;
                    sky.TimeOfDay = time;
                }
                yield return Stand(locomotion, 3.0f);
                // Each effect timed on and then off straight after, and what it costs is the pair's difference: a laptop's card slows as it
                // warms, and over a whole run that drift (up to 1.7 ms) swamped the cheap effects when each was set against one first reading.
                sb.AppendLine($"--- EFFECTS, {when}: standing still, {seconds:0} s on then {seconds:0} s off each (the graphics card's ms a frame)");
                List<Effect> effects = Effects();
                var all = new Effect
                {
                    Name = "ALL OF THE ABOVE",
                    Off = () => effects.ForEach(e => e.Off()),
                    On = () =>
                    {
                        for (int i = effects.Count - 1; i >= 0; i--) effects[i].On();
                    },
                };
                var variants = new List<Effect>(effects) { all };
                foreach (Effect variant in variants)
                {
                    (double on, float onFrame) = (0.0, 0.0f);
                    (double off, float offFrame) = (0.0, 0.0f);
                    yield return Clock(locomotion, seconds, result => (on, onFrame) = result);
                    variant.Off();
                    yield return Clock(locomotion, seconds, result => (off, offFrame) = result);
                    variant.On();
                    sb.AppendLine($"{variant.Name,-52} costs {on - off,5:0.00} ms  (on {on:0.00}, off {off:0.00}; " +
                                  $"{1000.0f / Mathf.Max(0.01f, onFrame):0} fps on, {1000.0f / Mathf.Max(0.01f, offFrame):0} off)");
                }
            }
            if (sky != null)
            {
                sky.Running = true;
            }
        }

        /// <summary>A second to settle, then the graphics card's mean ms a frame and the mean frame over <paramref name="seconds"/>, standing.</summary>
        private static IEnumerator Clock(PlayerLocomotion locomotion, float seconds, Action<(double gpu, float frame)> result)
        {
            yield return Stand(locomotion, 1.0f);
            var phase = new Phase();
            yield return Measure(locomotion, phase, seconds, Motion.Standing);
            float frame = 0.0f;
            foreach (float f in phase.Frames) frame += f;
            result((phase.Gpu / Mathf.Max(1, phase.TimingSamples), frame / Mathf.Max(1, phase.Frames.Count)));
        }

        private static IEnumerator Stand(PlayerLocomotion locomotion, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Each effect, with how to switch it off and back to what it was.</summary>
        private static List<Effect> Effects()
        {
            var effects = new List<Effect>
            {
                FeatureOff("light shafts", "CityXLightShafts"),
                FeatureOff("haze (the city's air)", "CityXHaze"),
                FeatureOff("black outlines (the cel look)", "CityXOutline"),
                FeatureOff("ambient occlusion (contact darkening)", "ScreenSpaceAmbientOcclusion"),
            };

            var camera = Camera.main != null ? Camera.main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (camera != null && camera.renderPostProcessing)
            {
                effects.Add(new Effect
                {
                    Name = "post-processing (bloom, colour grade)",
                    Off = () => camera.renderPostProcessing = false,
                    On = () => camera.renderPostProcessing = true,
                });
            }

            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                int msaa = pipeline.msaaSampleCount;
                if (msaa > 1)
                {
                    effects.Add(new Effect
                    {
                        Name = $"anti-aliasing (MSAA {msaa}x) off",
                        Off = () => pipeline.msaaSampleCount = 1,
                        On = () => pipeline.msaaSampleCount = msaa,
                    });
                }
                int resolution = pipeline.mainLightShadowmapResolution;
                int cascades = pipeline.shadowCascadeCount;
                effects.Add(new Effect
                {
                    Name = $"sun shadows {resolution} px x{cascades} -> 2048 px x2",
                    Off = () =>
                    {
                        pipeline.mainLightShadowmapResolution = Mathf.Min(resolution, 2048);
                        pipeline.shadowCascadeCount = Mathf.Min(cascades, 2);
                    },
                    On = () =>
                    {
                        pipeline.mainLightShadowmapResolution = resolution;
                        pipeline.shadowCascadeCount = cascades;
                    },
                });
            }

            effects.Add(LightShadowsOff("soft shadows -> hard", light => light.shadows == LightShadows.Soft, LightShadows.Hard));
            effects.Add(LightShadowsOff("lamp and room light shadows off",
                light => light.type != LightType.Directional && light.shadows != LightShadows.None, LightShadows.None));
            return effects;
        }

        private static Effect FeatureOff(string name, string feature)
        {
            var found = new List<ScriptableRendererFeature>();
            foreach (ScriptableRendererFeature candidate in Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>())
            {
                if (candidate.name == feature && candidate.isActive)
                {
                    found.Add(candidate);
                }
            }
            return new Effect
            {
                Name = found.Count > 0 ? name : name + " (not in this build)",
                Off = () => found.ForEach(f => f.SetActive(false)),
                On = () => found.ForEach(f => f.SetActive(true)),
            };
        }

        /// <summary>The lights <paramref name="which"/> picks (looked for when switched off: the streamer makes lamps as the city loads) set to <paramref name="to"/>.</summary>
        private static Effect LightShadowsOff(string name, Func<Light, bool> which, LightShadows to)
        {
            var changed = new List<(Light light, LightShadows was)>();
            return new Effect
            {
                Name = name,
                Off = () =>
                {
                    changed.Clear();
                    foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (which(light))
                        {
                            changed.Add((light, light.shadows));
                            light.shadows = to;
                        }
                    }
                },
                On = () =>
                {
                    foreach ((Light light, LightShadows was) in changed)
                    {
                        if (light != null) light.shadows = was;
                    }
                },
            };
        }
    }
}
#endif
