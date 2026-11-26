using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: how long the loading screens take and how fast the character select runs (owner, 2026-11-26: "see what you can do to lower
    /// loading screen times, as well as framerate on the character select screen"). Opens the title until its city is in, puts the character
    /// select up and times its frames; then opens the world and times it until the player stands. Each part is recorded as a Unity profile
    /// (Logs/bench-*.raw, read with ProfileReport) and the numbers are written to Logs/load-select-bench.txt. Explicit; needs -Graphics.
    /// The load times are real; the frame times are not what a player sees: a batch run's scene cameras draw nothing unless told to (0 draw
    /// calls), so they count the scripts alone. What a frame costs to draw is measured in a built game (PlayerFrameBenchmark).
    /// </summary>
    [Explicit("a benchmark; needs a graphics device")]
    public sealed class LoadAndSelectBench
    {
        private const string WorldScene = "Assets/CityX/Scenes/CityMassing.unity";

        private static string Logs => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs");

        private static void StartProfile(string name)
        {
            string path = Path.Combine(Logs, $"bench-{name}.raw");
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

        [UnityTest]
        public IEnumerator MeasureTheLoadsAndTheCharacterSelect()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 0;   // real time: this measures it
            var report = new StringBuilder();

            // --- the title, until its city is in
            StartProfile("title-load");
            var clock = Stopwatch.StartNew();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(CharacterCreatorPlayTests.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            Scene title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            SceneManager.SetActiveScene(title);
            var stage = Object.FindFirstObjectByType<TitleStage>();
            var screen = Object.FindFirstObjectByType<TitleScreen>();
            double longest = 0.0, last = clock.Elapsed.TotalSeconds;
            while (stage != null && !stage.Placed && clock.Elapsed.TotalSeconds < 60.0)
            {
                yield return null;
                longest = System.Math.Max(longest, clock.Elapsed.TotalSeconds - last);
                last = clock.Elapsed.TotalSeconds;
            }
            StopProfile();
            report.AppendLine($"title: city in after {clock.Elapsed.TotalSeconds:F2} s, longest frame {longest * 1000.0:F0} ms");

            // --- the character select, standing a while, then timed
            screen.Show(TitleScreen.Panel.CharacterSelect);
            for (float t = 0.0f; t < 3.0f; t += Time.unscaledDeltaTime) yield return null;
            StartProfile("select");
            yield return Frames("character select", report);
            StopProfile();
            report.AppendLine(Census());
            yield return SceneManager.UnloadSceneAsync(title);
            yield return null;

            // --- the world, until the player stands
            StartProfile("world-load");
            clock.Restart();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(WorldScene, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            double firstFrame = clock.Elapsed.TotalSeconds;
            Scene world = SceneManager.GetSceneByPath(WorldScene);
            SceneManager.SetActiveScene(world);
            var player = Object.FindFirstObjectByType<PlayerInputReader>();
            var body = player != null ? player.GetComponent<PlayerLocomotion>() : null;
            longest = 0.0;
            last = clock.Elapsed.TotalSeconds;
            while (body != null && !body.IsGrounded && clock.Elapsed.TotalSeconds < 60.0)
            {
                yield return null;
                longest = System.Math.Max(longest, clock.Elapsed.TotalSeconds - last);
                last = clock.Elapsed.TotalSeconds;
            }
            for (int i = 0; i < 30; i++) yield return null;   // what streams in just after
            StopProfile();
            report.AppendLine($"world: first frame after {firstFrame:F2} s, player standing after {last:F2} s, longest frame after the first {longest * 1000.0:F0} ms");
            yield return Frames("world, standing", report);
            yield return SceneManager.UnloadSceneAsync(world);

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            File.WriteAllText(Path.Combine(Logs, "load-select-bench.txt"), report.ToString());
            Debug.Log("CityX load/select bench:\n" + report);
#else
            yield break;
#endif
        }

        private static IEnumerator Frames(string what, StringBuilder report)
        {
            var times = new System.Collections.Generic.List<float>();
            double draws = 0.0, tris = 0.0, batches = 0.0;
            for (float t = 0.0f; t < 4.0f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                times.Add(Time.unscaledDeltaTime * 1000.0f);
#if UNITY_EDITOR
                draws += UnityEditor.UnityStats.drawCalls;
                tris += UnityEditor.UnityStats.triangles;
                batches += UnityEditor.UnityStats.batches;
#endif
            }
            times.Sort();
            float mean = 0.0f;
            foreach (float ms in times) mean += ms;
            mean /= Mathf.Max(1, times.Count);
            report.AppendLine($"{what}: {times.Count} frames, mean {mean:F1} ms ({1000.0f / mean:F0} fps), median {times[times.Count / 2]:F1}, " +
                              $"95% {times[(int)(times.Count * 0.95f)]:F1}, worst {times[times.Count - 1]:F1}; draws {draws / times.Count:F0}, " +
                              $"batches {batches / times.Count:F0}, triangles {tris / times.Count / 1e6:F2}M");
        }

        private static string Census()
        {
            int meshes = 0, skinned = 0, particles = 0, lights = 0, shadowLights = 0, cameras = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is SkinnedMeshRenderer) skinned++;
                else if (r is ParticleSystemRenderer) particles++;
                else meshes++;
            }
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (!l.isActiveAndEnabled) continue;
                lights++;
                if (l.shadows != LightShadows.None) shadowLights++;
            }
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c.isActiveAndEnabled) cameras++;
            }
            return $"  on screen at the select: {meshes} mesh renderers, {skinned} skinned, {particles} particle systems, {lights} lights ({shadowLights} casting shadows), {cameras} cameras";
        }
    }
}
