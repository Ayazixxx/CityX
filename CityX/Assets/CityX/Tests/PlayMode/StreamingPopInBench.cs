using System.Collections;
using System.IO;
using System.Text;
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
    /// Not a test: a MEASUREMENT of the city popping in round the player (owner, 2026-10-12: "I keep witnessing buildings loading in in real
    /// time up close"). The real scene, the game's own city scale, a viewer moved through the city at the player's real speeds while the main
    /// thread is held to 60 frames a second (the time a real frame spends rendering is time the main thread cannot spend assembling chunks; the
    /// workers carry on meanwhile). Every frame counts, round the viewer: chunks not there at all (a hole: buildings appear out of nothing) and
    /// chunks still in the simplified far version where the full one belongs (a visible swap). Writes Logs/streaming-bench.txt.
    /// Explicit; run with -Filter CityX.Tests.StreamingPopInBench.
    /// </summary>
    [Explicit("a measurement, not a test")]
    public sealed class StreamingPopInBench
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        /// <summary>A hole this close (chunks, either axis) is in plain view: about 100 m.</summary>
        private const int HoleRadius = 4;

        /// <summary>A far chunk this close is the simplified version up close: about 150 m.</summary>
        private const int DetailRadius = 6;

        private CityScale _scaleBefore;
        private GameObject _viewer;

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
            if (_viewer != null) Object.Destroy(_viewer);
        }

        [UnityTest]
        public IEnumerator MeasurePopIn()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own districts (tests pin classic)
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.IsNotNull(streamer);
            streamer.Rebuild();
            CityWorld world = CurrentWorld.Instance;
            world.Plan.TryFindSpawn(out double sx, out double sz);
            _viewer = new GameObject("BenchViewer");
            streamer.Viewer = _viewer.transform;

            var report = new StringBuilder();
            report.AppendLine($"streaming bench {System.DateTime.Now:yyyy-MM-dd HH:mm}: threads {streamer.WorkerThreads}, in flight {System.Math.Max(streamer.MaxChunksInFlight, 2 * streamer.WorkerThreads)}, " +
                              $"assemble {streamer.AssembleTimeBudgetMs} ms/frame, radius {streamer.RadiusChunks}, full detail {streamer.FullDetailRadiusChunks}, scale {CityScale.Active}");

            // 1. The arrival: the viewer appears at the spawn with nothing built (a fresh world): how long until the ring round it is whole?
            var spawn = new Vector3((float)sx, (float)world.Plan.GroundHeight(sx, sz) + 2.0f, (float)sz);
            _viewer.transform.position = spawn;
            yield return Run(streamer, report, "arrival (standing)", spawn, Vector3.zero, 0.0f, 12.0f);

            // 2-4. Moving: along x then z, so it is not a lucky direction.
            yield return Run(streamer, report, "sprint 7.5 m/s", spawn, new Vector3(1.0f, 0.0f, 0.3f).normalized, 7.5f, 20.0f);
            yield return Run(streamer, report, "jetpack boost 22 m/s", spawn + new Vector3(0.0f, 30.0f, 0.0f), new Vector3(-0.3f, 0.0f, 1.0f).normalized, 22.0f, 15.0f);
            yield return Run(streamer, report, "very fast 45 m/s", spawn + new Vector3(0.0f, 60.0f, 0.0f), new Vector3(0.7f, 0.0f, -0.7f).normalized, 45.0f, 10.0f);

            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "streaming-bench.txt");
            File.AppendAllText(path, report.ToString() + "\n");
            Debug.Log(report.ToString());
            TestContext.WriteLine(report.ToString());
#else
            yield break;
#endif
        }

        /// <summary>
        /// Moves the viewer from <paramref name="start"/> along <paramref name="direction"/> at <paramref name="speed"/> for <paramref name="seconds"/>,
        /// at a held 60 frames a second, and reports the holes and far chunks round it.
        /// </summary>
        private static IEnumerator Run(CityStreamer streamer, StringBuilder report, string name, Vector3 start, Vector3 direction, float speed, float seconds)
        {
            Transform viewer = streamer.Viewer;
            viewer.position = start;
            const float dt = 1.0f / 60.0f;
            int frames = Mathf.RoundToInt(seconds / dt);
            int holeFrames = 0, worstHoles = 0, holeChunkFrames = 0, farFrames = 0, worstFar = 0;
            float nearestHole = float.MaxValue;
            int firstWholeFrame = -1;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
            {
                double frameStart = clock.Elapsed.TotalMilliseconds;
                viewer.position = start + direction * (speed * dt * f);
                yield return null;

                int cx = Mathf.FloorToInt(viewer.position.x / CityLayout.ChunkSize), cy = Mathf.FloorToInt(viewer.position.z / CityLayout.ChunkSize);
                int holes = 0, farUp = 0;
                for (int dx = -DetailRadius; dx <= DetailRadius; dx++)
                {
                    for (int dy = -DetailRadius; dy <= DetailRadius; dy++)
                    {
                        var key = new Vector2Int(cx + dx, cy + dy);
                        int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                        bool loaded = streamer.IsChunkLoaded(key);
                        if (!loaded && d <= HoleRadius)
                        {
                            holes++;
                            Vector3 c = new Vector3((key.x + 0.5f) * CityLayout.ChunkSize, viewer.position.y, (key.y + 0.5f) * CityLayout.ChunkSize);
                            float distance = Vector3.Distance(new Vector3(c.x, 0, c.z), new Vector3(viewer.position.x, 0, viewer.position.z));
                            nearestHole = Mathf.Min(nearestHole, distance);
                        }
                        else if (loaded && streamer.IsChunkFar(key))
                        {
                            farUp++;
                        }
                    }
                }
                if (holes > 0) holeFrames++;
                holeChunkFrames += holes;
                worstHoles = Mathf.Max(worstHoles, holes);
                if (farUp > 0) farFrames++;
                worstFar = Mathf.Max(worstFar, farUp);
                if (firstWholeFrame < 0 && holes == 0 && farUp == 0) firstWholeFrame = f;

                // Hold the frame to 1/60 s: the rest of a real frame is rendering, which the main thread spends not streaming.
                while (clock.Elapsed.TotalMilliseconds - frameStart < dt * 1000.0)
                {
                    System.Threading.Thread.Sleep(0);
                }
            }
            report.AppendLine($"  {name,-22}: holes within {HoleRadius} chunks in {holeFrames}/{frames} frames (worst {worstHoles} at once, {holeChunkFrames} chunk-frames, nearest {(nearestHole < float.MaxValue ? nearestHole.ToString("0") + " m" : "none")}); " +
                              $"far chunks within {DetailRadius} in {farFrames} frames (worst {worstFar}); whole after {(firstWholeFrame < 0 ? "never" : (firstWholeFrame * dt).ToString("0.00") + " s")}");
        }
    }
}
