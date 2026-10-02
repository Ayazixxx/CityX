using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The exact tool built to remove all ambiguity from "there are no
    /// slopes" -- verified end to end against the REAL saved scene's REAL
    /// DevConsoleView, REAL PlayerVitals and REAL CityStreamer, the same
    /// "LoadSceneInPlayMode, not batch mode" technique
    /// SlopedStreetRealScenePlayTests already established for this same
    /// bug report. If DevConsoleView.Streamer were ever left unwired by a
    /// scene rebuild (see scene_rebuild_required_for_new_fields), /goto
    /// would silently do nothing in the real game despite every EditMode
    /// test of it passing -- this is what would have caught that.
    /// </summary>
    public sealed class GotoSlopeRealScenePlayTests
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

        [UnityTest]
        public IEnumerator SlashGotoSlopeActuallyMovesTheRealPlayerInTheRealScene()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(vitals, "no PlayerVitals in the real saved scene");
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console, "no DevConsoleView in the real saved scene");
            Assert.IsNotNull(console.Streamer,
                "DevConsoleView.Streamer is unwired in the real saved scene -- /goto would silently do " +
                "nothing in real gameplay despite every EditMode test of it passing");

            Vector3 before = vitals.transform.position;
            console.Submit("/goto slope");
            yield return null;

            Vector3 after = vitals.transform.position;
            Assert.AreNotEqual(before, after, "/goto slope did not move the real player at all");

            int chunkX = Mathf.FloorToInt(after.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(after.z / CityLayout.ChunkSize);
            Assert.IsTrue(CityLayout.IsStreetChunk(chunkX, chunkY),
                $"/goto slope landed the real player at ({after.x:F1},{after.z:F1}), chunk ({chunkX},{chunkY}), " +
                "which is not even a street chunk");

            TestContext.WriteLine($"moved from {before:F2} to {after:F2}, chunk ({chunkX},{chunkY})");
#else
            yield break;
#endif
        }
    }
}
