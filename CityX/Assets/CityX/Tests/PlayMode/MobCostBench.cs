using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: WHAT THE WORLD'S ENEMIES COST A FRAME (docs/MOB_SPAWNING.md, docs/PERFORMANCE.md). The game's own city, the player stood
    /// in a street away from the start until both budgets have filled; then a frame is timed with the enemies alive, with all of them lying
    /// dead, and with none in the world, each over the same number of frames from the same place. A profile of the first two is recorded
    /// (Logs/Benchmark/profile-mobs.raw, profile-corpses.raw: read them with ProfileReport); the numbers go to Logs/Benchmark/mobs-cost.txt.
    /// The window is the test runner's small one, so this is the processor's side of them (scripts, posing, physics, sending the draws),
    /// not the graphics card's. Explicit; run by name with -Graphics:
    ///   ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.MobCostBench
    /// </summary>
    [Explicit("a measurement; needs a graphics device")]
    public sealed class MobCostBench
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int FramesTimed = 600;
        private const int FramesToSettle = 120;

        /// <summary>Time.captureFramerate for the clock to follow real time (the tear-down puts the tests' steady clock back).</summary>
        private const int RealTime = 0;

        private Scene _scene;
        private CityScale _scaleBefore;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Debug.unityLogger.logEnabled = true;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;   // the tests after this one run on the steady clock again
            CityScale.Active = _scaleBefore;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator WhatTheWorldsEnemiesCostAFrame()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            PlayerInputReader reader = Object.FindFirstObjectByType<PlayerInputReader>();
            reader.enabled = false;
            GameObject player = reader.gameObject;
            player.GetComponent<PlayerVitals>().InfiniteHealth = true;
            var spawner = Object.FindFirstObjectByType<MobSpawner>();
            Assert.IsNotNull(spawner, "the scene has no MobSpawner -- rebuild the massing scene");
            PlanWorld plan = CurrentWorld.Instance.Plan;
            console.Submit("/time noon");
            console.Cycle.Running = false;

            // A street away from the start (nothing appears round Old Guy's house), as MobSpawnCapture stands in.
            plan.TryFindSpawn(out double startX, out double startZ);
            Vector3 street = default;
            double nearest = double.MaxValue;
            foreach (EdgeGeometry g in plan.Geometry.Edges)
            {
                if (g == null || g.Edge == null || !g.Edge.Alive || g.Edge.Bridge) continue;
                double x = g.A.X + g.Dir.X * g.Length * 0.5, z = g.A.Y + g.Dir.Y * g.Length * 0.5;
                double away = System.Math.Sqrt((x - startX) * (x - startX) + (z - startZ) * (z - startZ));
                if (away >= 280.0 && away < nearest && MobHabitats.At(plan, x, z, out _) == MobHabitat.Outdoors)
                {
                    nearest = away;
                    street = new Vector3((float)x, (float)plan.GroundHeight(x, z), (float)z);
                }
            }
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = street + Vector3.up;
            controller.enabled = true;

            // The city builds round the player and its enemies appear: 45 seconds of the game's own time, at a fixed step so it is the same every run.
            Time.captureFramerate = 60;
            float began = Time.time;
            while (Time.time < began + 45.0f)
            {
                yield return null;
            }
            Time.captureFramerate = RealTime;   // a frame is timed as long as it really takes
            spawner.Spawning = false;   // what is alive now is what is timed: nothing new appears
            // Silent while it measures, as the frame benchmark is: a console log with its stack trace costs the main thread milliseconds.
            Debug.unityLogger.logEnabled = false;

            var alive = new List<(GameObject Body, MobHabitat Habitat)>();
            spawner.CopyAlive(alive);
            int outdoors = spawner.AliveIn(MobHabitat.Outdoors), indoors = spawner.AliveIn(MobHabitat.Indoors);
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "Benchmark");
            Directory.CreateDirectory(folder);
            var timed = new List<float>();

            // Each is timed with the profiler off (recording costs half a millisecond a frame itself), then recorded.
            yield return Settle();
            yield return TimeFrames(timed);
            string withThem = Summary(timed, out float aliveMs);
            StartProfile(Path.Combine(folder, "profile-mobs.raw"));
            yield return Settle();
            yield return Settle();
            StopProfile();

            // Every one of them killed where it stands; the bodies lie where they fall.
            int corpses = 0;
            foreach ((GameObject body, MobHabitat _) in alive)
            {
                if (body != null && body.TryGetComponent(out IDamageable life) && life.IsAlive)
                {
                    life.ApplyHit(1.0e6f, player.transform.position, 0.0f);
                    corpses++;
                }
            }
            float fell = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < fell + 5.0f)
            {
                yield return null;   // the bodies fall and come to rest
            }
            yield return TimeFrames(timed);
            string lyingDead = Summary(timed, out float deadMs);
            StartProfile(Path.Combine(folder, "profile-corpses.raw"));
            yield return Settle();
            yield return Settle();
            StopProfile();

            foreach ((GameObject body, MobHabitat _) in alive)
            {
                if (body != null)
                {
                    Object.Destroy(body);
                }
            }
            yield return Settle();
            yield return TimeFrames(timed);
            string withNone = Summary(timed, out float noneMs);

            var report = new StringBuilder();
            report.AppendLine($"The world's enemies, a frame (main thread, the editor, a small window), {FramesTimed} frames each:");
            report.AppendLine($"  {alive.Count} alive ({outdoors} out of doors, {indoors} indoors): {withThem}");
            report.AppendLine($"  {corpses} of them lying dead:                      {lyingDead}");
            report.AppendLine($"  none in the world:                           {withNone}");
            report.AppendLine($"  each one alive costs about {(alive.Count > 0 ? (aliveMs - noneMs) / alive.Count : 0.0f):0.00} ms a frame, each lying dead about {(corpses > 0 ? (deadMs - noneMs) / corpses : 0.0f):0.00} ms");
            File.WriteAllText(Path.Combine(folder, "mobs-cost.txt"), report.ToString());
            Debug.unityLogger.logEnabled = true;
            Debug.Log("CityX MOBS COST:\n" + report);
            Assert.Greater(alive.Count, 0, "nothing appeared round the player in 45 seconds");
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static IEnumerator Settle()
        {
            for (int i = 0; i < FramesToSettle; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator TimeFrames(List<float> into)
        {
            into.Clear();
            for (int i = 0; i < FramesTimed; i++)
            {
                yield return null;
                into.Add(Time.unscaledDeltaTime * 1000.0f);
            }
        }

        private static string Summary(List<float> frames, out float median)
        {
            frames.Sort();
            float sum = 0.0f;
            foreach (float ms in frames)
            {
                sum += ms;
            }
            median = frames[frames.Count / 2];
            return $"median {median:0.00} ms, average {sum / frames.Count:0.00}, 95th percentile {frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.95f))]:0.00}";
        }

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
#endif
    }
}
