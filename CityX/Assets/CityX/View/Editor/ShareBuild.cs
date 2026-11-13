using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the whole game, title screen first, as a Windows player to send a friend for playing online together (owner, 2026-11-12: "i want to
    /// 1v1 my friend in a duel"; docs/PLAYING_ONLINE.md). Both players must run the same build. Zip CityX/Builds/Play and send all of it.
    ///
    ///   Unity.exe -batchmode -quit -projectPath CityX -executeMethod CityX.ViewEditor.ShareBuild.BuildGameToShare
    /// </summary>
    public static class ShareBuild
    {
        public const string OutputFolder = "Builds/Play";

        [MenuItem("CityX/Build Game To Share")]
        public static void BuildGameToShare()
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                // The scenes the project plays, in order: the title screen, then the city.
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(folder, "CityX.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"CityX: game build {report.summary.result} in {report.summary.totalTime.TotalSeconds:0}s, {report.summary.totalSize / (1024 * 1024)} MB -> {options.locationPathName}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}
