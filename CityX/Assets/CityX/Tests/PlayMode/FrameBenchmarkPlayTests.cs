using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// HOW FAST THE REAL CITY RUNS, measured: the real saved scene, the real player running down the streets
    /// (so the city streams in around them), frame time and draw counts recorded every frame.
    ///
    /// NOT PART OF THE ORDINARY SUITE -- it needs a graphics device and takes a minute, so it does nothing
    /// unless CITYX_BENCH=1 is set:
    ///   $env:CITYX_BENCH='1'; ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FrameBenchmarkPlayTests
    /// CITYX_BENCH_W / CITYX_BENCH_H set the resolution it renders at (default 2560x1600). Results go to the
    /// test log and to CityX/Logs/Benchmark/*.txt.
    /// </summary>
    public sealed class FrameBenchmarkPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static bool Enabled => Environment.GetEnvironmentVariable("CITYX_BENCH") == "1";

        private static string Percentile(List<float> sortedMs, float p)
        {
            if (sortedMs.Count == 0) return "-";
            int index = Mathf.Clamp(Mathf.CeilToInt(p * sortedMs.Count) - 1, 0, sortedMs.Count - 1);
            return sortedMs[index].ToString("0.0");
        }

#if UNITY_EDITOR
        private sealed class Phase
        {
            public string Name;
            public readonly List<float> Frames = new List<float>();
            public readonly List<KeyValuePair<float, float>> Worst = new List<KeyValuePair<float, float>>();
            public double Draw, Batches, SetPass, Tris, Verts, Casters, Gpu, CpuMain, CpuRender;
            public int Samples, TimingSamples;
            public float Seconds;
        }

        private static readonly FrameTiming[] Timings = new FrameTiming[1];

        private static void StartProfile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            UnityEngine.Profiling.Profiler.logFile = path;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            UnityEngine.Profiling.Profiler.enabled = true;
        }

        private static void StopProfile()
        {
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = "";
        }

        private IEnumerator Measure(PlayerLocomotion locomotion, Phase phase, float seconds, bool run)
        {
            float elapsed = 0.0f;
            float yaw = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                if (run)
                {
                    // Sprint forward, turning a little every few seconds so the run crosses streets and blocks.
                    if (((int)(elapsed / 5.0f)) % 2 == 1) yaw += 40.0f * dt;
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true, YawDegrees = yaw }, Time.deltaTime);
                }
                else
                {
                    locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                }
                yield return null;
                elapsed += dt;
                phase.Frames.Add(dt * 1000.0f);
                phase.Worst.Add(new KeyValuePair<float, float>(dt * 1000.0f, elapsed));

                phase.Draw += UnityEditor.UnityStats.drawCalls;
                phase.Batches += UnityEditor.UnityStats.batches;
                phase.SetPass += UnityEditor.UnityStats.setPassCalls;
                phase.Tris += UnityEditor.UnityStats.triangles;
                phase.Verts += UnityEditor.UnityStats.vertices;
                phase.Casters += UnityEditor.UnityStats.shadowCasters;
                phase.Samples++;

                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, Timings) > 0)
                {
                    phase.Gpu += Timings[0].gpuFrameTime;
                    phase.CpuMain += Timings[0].cpuMainThreadFrameTime;
                    phase.CpuRender += Timings[0].cpuRenderThreadFrameTime;
                    phase.TimingSamples++;
                }
            }
            phase.Seconds = elapsed;
        }

        private static void Report(StringBuilder sb, Phase phase)
        {
            var sorted = new List<float>(phase.Frames);
            sorted.Sort();
            float total = 0;
            int over17 = 0, over33 = 0, over100 = 0;
            foreach (float f in phase.Frames)
            {
                total += f;
                if (f > 16.7f) over17++;
                if (f > 33.4f) over33++;
                if (f > 100.0f) over100++;
            }
            int count = Mathf.Max(1, phase.Frames.Count);
            int n = Mathf.Max(1, phase.Samples);
            sb.AppendLine($"--- {phase.Name}");
            sb.AppendLine($"Frames: {phase.Frames.Count} over {phase.Seconds:0.0}s = {phase.Frames.Count / Mathf.Max(0.01f, phase.Seconds):0} fps average");
            sb.AppendLine($"Frame ms: avg {total / count:0.0}, median {Percentile(sorted, 0.5f)}, p95 {Percentile(sorted, 0.95f)}, p99 {Percentile(sorted, 0.99f)}, worst {(sorted.Count > 0 ? sorted[sorted.Count - 1] : 0):0.0}");
            sb.AppendLine($"Under 60fps: {over17} frames ({100.0f * over17 / count:0.0}%), over 33ms: {over33}, over 100ms: {over100}");
            sb.AppendLine($"Per frame (Unity stats): draw calls {phase.Draw / n:0}, batches {phase.Batches / n:0}, setpass {phase.SetPass / n:0}, triangles {phase.Tris / n / 1000.0:0}k, vertices {phase.Verts / n / 1000.0:0}k, shadow casters {phase.Casters / n:0}");
            if (phase.TimingSamples > 0)
            {
                int t = phase.TimingSamples;
                sb.AppendLine($"Frame timing (ms): GPU {phase.Gpu / t:0.0}, CPU main thread {phase.CpuMain / t:0.0}, CPU render thread {phase.CpuRender / t:0.0}");
            }
            else
            {
                sb.AppendLine("Frame timing: not available in this run");
            }
            phase.Worst.Sort((a, b) => b.Key.CompareTo(a.Key));
            var worst = new StringBuilder("Worst frames (ms @ second): ");
            for (int i = 0; i < Mathf.Min(8, phase.Worst.Count); i++)
            {
                worst.Append($"{phase.Worst[i].Key:0}@{phase.Worst[i].Value:0.0}s  ");
            }
            sb.AppendLine(worst.ToString());
        }
#endif

        [UnityTest]
        public IEnumerator RunDownTheStreetsAndMeasure()
        {
#if UNITY_EDITOR
            if (!Enabled)
            {
                Assert.Ignore("set CITYX_BENCH=1 to run the frame benchmark");
            }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("the frame benchmark needs a graphics device: run with -Graphics");
            }

            LogAssert.ignoreFailingMessages = true;
            UnityEditor.PlayerSettings.enableFrameTimingStats = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var locomotion = UnityEngine.Object.FindFirstObjectByType<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "no player in the real scene");
            var reader = locomotion.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;   // nobody is at the keyboard: we drive the player

            // The batch window is tiny; render the main camera into a texture the size of the real screen
            // so the pixel cost (MSAA, HDR, post-processing) is what a player at that resolution pays.
            int width = int.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_W"), out int w) ? w : 2560;
            int height = int.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_H"), out int h) ? h : 1600;
            // Optional what-ifs, on a COPY of the pipeline asset so nothing on disk is touched:
            //   CITYX_BENCH_MSAA (1/2/4/8), CITYX_BENCH_SHADOW (distance in metres, 0 = off), CITYX_BENCH_SCALE (render scale),
            //   CITYX_BENCH_CASCADES (1-4), CITYX_BENCH_SHADOWRES (shadow map size), CITYX_BENCH_QUICK=1 (standing phase only).
            var pipeline = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline)
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            string overrides = "";
            if (pipeline != null)
            {
                var copy = UnityEngine.Object.Instantiate(pipeline);
                if (int.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_MSAA"), out int msaa)) { copy.msaaSampleCount = msaa; overrides += $" MSAA={msaa}"; }
                if (float.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_SHADOW"), out float shadow)) { copy.shadowDistance = shadow; overrides += $" shadowDistance={shadow}"; }
                if (float.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_SCALE"), out float scale)) { copy.renderScale = scale; overrides += $" renderScale={scale}"; }
                if (int.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_CASCADES"), out int cascades)) { copy.shadowCascadeCount = cascades; overrides += $" cascades={cascades}"; }
                if (int.TryParse(Environment.GetEnvironmentVariable("CITYX_BENCH_SHADOWRES"), out int shadowRes)) { copy.mainLightShadowmapResolution = shadowRes; overrides += $" shadowRes={shadowRes}"; }
                QualitySettings.renderPipeline = copy;
            }

            Camera camera = Camera.main;
            RenderTexture target = null;
            if (camera != null && width > 0)
            {
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "BenchTarget" };
                camera.targetTexture = target;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), CPU: {SystemInfo.processorType} x{SystemInfo.processorCount}");
            sb.AppendLine($"Rendering at {(target != null ? $"{width}x{height}" : $"{Screen.width}x{Screen.height}")}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                          $"vSync {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}, overrides:{(overrides.Length > 0 ? overrides : " none")}");

            // Let the first ring of the city stream in before timing anything.
            for (int i = 0; i < 300; i++)
            {
                locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                yield return null;
            }

            string folderForProfiles = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Benchmark");
            Directory.CreateDirectory(folderForProfiles);
            bool profile = Environment.GetEnvironmentVariable("CITYX_BENCH_PROFILE") == "1";

            var standing = new Phase { Name = "STANDING STILL (steady state: pure render cost)" };
            if (profile) StartProfile(Path.Combine(folderForProfiles, "profile-standing.raw"));
            yield return Measure(locomotion, standing, 8.0f, run: false);
            if (profile) StopProfile();
            Report(sb, standing);

            if (Environment.GetEnvironmentVariable("CITYX_BENCH_QUICK") != "1")
            {
                var running = new Phase { Name = "SPRINTING DOWN THE STREETS (the city streams in around the player)" };
                if (profile) StartProfile(Path.Combine(folderForProfiles, "profile-running.raw"));
                yield return Measure(locomotion, running, 25.0f, run: true);
                if (profile) StopProfile();
                Report(sb, running);
            }

            if (target != null)
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Benchmark");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"bench-{DateTime.Now:yyyyMMdd-HHmmss}.txt"), sb.ToString());
            Debug.Log("CityX BENCH:\n" + sb);
#else
            yield break;
#endif
        }
    }
}
