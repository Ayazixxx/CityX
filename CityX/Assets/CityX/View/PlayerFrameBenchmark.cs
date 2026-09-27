#if CITYX_BENCH
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The frame benchmark, INSIDE A BUILT PLAYER -- the editor's numbers include its own overhead (rendering
    /// submitted on the main thread, profiler hooks), so the real answer to "how fast is the game" has to come
    /// from the game itself.
    ///
    /// COMPILED ONLY WHEN CITYX_BENCH IS DEFINED (the benchmark build sets it; see PerfBuild), so a normal build
    /// contains none of this. Started by the -cityxbench command-line flag: settles the city, times a phase
    /// standing still and a phase sprinting down the streets, writes bench-result.txt beside the game and quits.
    /// </summary>
    public sealed class PlayerFrameBenchmark : MonoBehaviour
    {
        private static bool HasFlag(string flag)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (HasFlag("-cityxbench"))
            {
                new GameObject("FrameBenchmark").AddComponent<PlayerFrameBenchmark>();
            }
        }

        private sealed class Phase
        {
            public string Name;
            public readonly List<float> Frames = new List<float>();
            public double Gpu, CpuMain, CpuRender;
            public int TimingSamples;
            public float Seconds;
        }

        private static readonly FrameTiming[] Timings = new FrameTiming[1];

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        /// <summary>
        /// What-ifs from the command line, applied to a COPY of the pipeline asset and to the streamer, so the
        /// real settings on disk are never touched: -bench-msaa N, -bench-shadow METRES, -bench-shadowres PIXELS,
        /// -bench-cascades N, -bench-scale S, -bench-radius CHUNKS, -bench-threads N, -bench-standing SECONDS, -bench-running SECONDS.
        /// </summary>
        private static string ApplyOverrides()
        {
            var text = new StringBuilder();
            var pipeline = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline)
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                var copy = UnityEngine.Object.Instantiate(pipeline);
                if (int.TryParse(Arg("-bench-msaa"), out int msaa)) { copy.msaaSampleCount = msaa; text.Append($" MSAA={msaa}"); }
                if (float.TryParse(Arg("-bench-shadow"), out float shadow)) { copy.shadowDistance = shadow; text.Append($" shadowDistance={shadow}"); }
                if (int.TryParse(Arg("-bench-shadowres"), out int shadowRes)) { copy.mainLightShadowmapResolution = shadowRes; text.Append($" shadowRes={shadowRes}"); }
                if (int.TryParse(Arg("-bench-cascades"), out int cascades)) { copy.shadowCascadeCount = cascades; text.Append($" cascades={cascades}"); }
                if (float.TryParse(Arg("-bench-scale"), out float scale)) { copy.renderScale = scale; text.Append($" renderScale={scale}"); }
                QualitySettings.renderPipeline = copy;
            }
            var streamer = FindFirstObjectByType<CityStreamer>();
            if (streamer != null)
            {
                if (int.TryParse(Arg("-bench-radius"), out int radius)) { streamer.RadiusChunks = radius; text.Append($" radius={radius}"); }
                if (int.TryParse(Arg("-bench-threads"), out int threads)) { streamer.GeneratorThreads = threads; text.Append($" threads={threads}"); }
                if (int.TryParse(Arg("-bench-fullradius"), out int fullRadius)) { streamer.FullDetailRadiusChunks = fullRadius; text.Append($" fullRadius={fullRadius}"); }
                text.Append($" [streamer radius {streamer.RadiusChunks}, full detail radius {streamer.FullDetailRadiusChunks}, generator threads {streamer.GeneratorThreads}]");
            }
            return text.Length == 0 ? " none" : text.ToString();
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return null;
            yield return null;

            PlayerLocomotion locomotion = null;
            for (int i = 0; i < 600 && locomotion == null; i++)
            {
                locomotion = FindFirstObjectByType<PlayerLocomotion>();
                yield return null;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"PLAYER BUILD  GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), CPU {SystemInfo.processorType} x{SystemInfo.processorCount}");
            sb.AppendLine($"Screen {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, targetFrameRate {Application.targetFrameRate}");
            if (locomotion == null)
            {
                sb.AppendLine("no player found");
                Finish(sb);
                yield break;
            }
            sb.AppendLine("Overrides:" + ApplyOverrides());

            var reader = locomotion.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;   // nobody is at the keyboard: drive the player

            // This build holds the city scene alone, with no network manager, so the animator sync would throw
            // an exception every frame (thousands in the log). Not part of the game as played: switch it off.
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour.GetType().Name == "NetworkedAnimatorSync")
                {
                    behaviour.enabled = false;
                }
            }

            // Let the first ring of the city stream in before timing anything.
            float settle = 0.0f;
            while (settle < 6.0f)
            {
                locomotion.Tick(new LocomotionInput(), Time.deltaTime);
                settle += Time.unscaledDeltaTime;
                yield return null;
            }

            var standing = new Phase { Name = "STANDING STILL" };
            yield return Measure(locomotion, standing, float.TryParse(Arg("-bench-standing"), out float standingSeconds) ? standingSeconds : 10.0f, run: false);
            Report(sb, standing);
            Census(sb, "(standing)");

            var running = new Phase { Name = "SPRINTING DOWN THE STREETS (the city streams in around the player)" };
            yield return Measure(locomotion, running, float.TryParse(Arg("-bench-running"), out float runningSeconds) ? runningSeconds : 30.0f, run: true);
            Report(sb, running);
            Census(sb, "(after sprinting)");

            Finish(sb);
        }

        private static IEnumerator Measure(PlayerLocomotion locomotion, Phase phase, float seconds, bool run)
        {
            float elapsed = 0.0f;
            float yaw = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                if (run)
                {
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

        private static string Percentile(List<float> sortedMs, float p)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(p * sortedMs.Count) - 1, 0, sortedMs.Count - 1);
            return sortedMs[index].ToString("0.0");
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
            sb.AppendLine($"--- {phase.Name}");
            sb.AppendLine($"Frames: {phase.Frames.Count} over {phase.Seconds:0.0}s = {phase.Frames.Count / Mathf.Max(0.01f, phase.Seconds):0} fps average");
            sb.AppendLine($"Frame ms: avg {total / count:0.0}, median {Percentile(sorted, 0.5f)}, p95 {Percentile(sorted, 0.95f)}, p99 {Percentile(sorted, 0.99f)}, worst {sorted[sorted.Count - 1]:0.0}");
            sb.AppendLine($"Under 60fps: {over17} frames ({100.0f * over17 / count:0.0}%), over 33ms: {over33}, over 100ms: {over100}");
            if (phase.TimingSamples > 0)
            {
                int t = phase.TimingSamples;
                sb.AppendLine($"Frame timing (ms): GPU {phase.Gpu / t:0.0}, CPU main thread {phase.CpuMain / t:0.0}, CPU render thread {phase.CpuRender / t:0.0}");
            }
        }

        /// <summary>
        /// What the renderer is being asked to draw: every active mesh renderer in the scene and the ones inside
        /// the camera's frustum, with their triangles and submesh draws. Frame time that follows the amount of
        /// geometry rather than the resolution says where to cut.
        /// </summary>
        private static void Census(StringBuilder sb, string label)
        {
            Camera camera = Camera.main;
            Plane[] planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            int renderers = 0, visibleRenderers = 0, draws = 0, visibleDraws = 0, transparentDraws = 0;
            long triangles = 0, visibleTriangles = 0, vertices = 0, visibleVertices = 0;
            var shadowCasters = 0;
            int emptyDraws = 0;
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;

                long tris = 0;
                int submeshes = Mathf.Min(mesh.subMeshCount, renderer.sharedMaterials.Length);
                int nonEmpty = 0;
                for (int s = 0; s < submeshes; s++)
                {
                    long count = mesh.GetIndexCount(s);
                    tris += count / 3;
                    if (count > 0) nonEmpty++;
                }
                emptyDraws += submeshes - nonEmpty;
                bool visible = planes == null || GeometryUtility.TestPlanesAABB(planes, renderer.bounds);

                renderers++;
                draws += submeshes;
                triangles += tris;
                vertices += mesh.vertexCount;
                if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
                if (visible)
                {
                    visibleRenderers++;
                    visibleDraws += submeshes;
                    visibleTriangles += tris;
                    visibleVertices += mesh.vertexCount;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (visible && material != null && material.renderQueue >= 3000) transparentDraws++;
                }
            }
            sb.AppendLine($"Census {label}: {emptyDraws} empty submesh draws; {renderers} mesh renderers ({shadowCasters} cast shadows), {draws} submesh draws, {triangles / 1000}k triangles, {vertices / 1000}k vertices");
            sb.AppendLine($"  in the camera frustum: {visibleRenderers} renderers, {visibleDraws} draws ({transparentDraws} transparent), {visibleTriangles / 1000}k triangles, {visibleVertices / 1000}k vertices");
        }

        private static void Finish(StringBuilder sb)
        {
            string path = Path.Combine(Application.dataPath, "..", "bench-result.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("CityX BENCH:\n" + sb);
            Application.Quit();
        }
    }
}
#endif
