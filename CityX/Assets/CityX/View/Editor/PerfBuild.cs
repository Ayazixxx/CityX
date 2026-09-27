using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the game as a real Windows player for measuring performance. The editor's frame times include
    /// its own overhead, so how fast the game really runs has to be measured in a build.
    ///
    ///   Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.PerfBuild.BuildBenchmarkPlayer
    ///   CityX/Builds/Bench/CityX.exe -cityxbench -screen-width 2560 -screen-height 1600 -screen-fullscreen 0
    ///
    /// The result is written to CityX/Builds/Bench/bench-result.txt. The benchmark code (PlayerFrameBenchmark) is
    /// compiled in only through the CITYX_BENCH define this build sets; the city scene alone is included, and the
    /// build is a release build (no profiler), with Frame Timing Stats on for the GPU/CPU split.
    /// </summary>
    public static class PerfBuild
    {
        public const string OutputFolder = "Builds/Bench";

        [MenuItem("CityX/Build Benchmark Player")]
        public static void BuildBenchmarkPlayer()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(projectRoot, OutputFolder);
            Directory.CreateDirectory(folder);

            bool timingWas = PlayerSettings.enableFrameTimingStats;
            PlayerSettings.enableFrameTimingStats = true;
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/CityX/Scenes/CityMassing.unity" },
                    locationPathName = Path.Combine(folder, "CityX.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "CITYX_BENCH" },
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"CityX: benchmark build {report.summary.result} in {report.summary.totalTime.TotalSeconds:0}s, {report.summary.totalSize / (1024 * 1024)} MB -> {options.locationPathName}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
                }
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = timingWas;
            }
        }
    }
}
