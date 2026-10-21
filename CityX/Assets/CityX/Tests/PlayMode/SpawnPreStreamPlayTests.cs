using System.Collections;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Bug report: "the thing where i fall through at the
    /// beginning is a temporary glitch but it occurs every time i enter
    /// the game." Root cause: CityStreamer's ordinary chunk building is
    /// lazy and budgeted PER FRAME (ChunkBuildBudgetPerFrame, 6 by
    /// default) -- on the very first frame the real scene exists, zero
    /// chunks have been built yet, and whichever of PlayerInputReader's
    /// own Update or CityStreamer's own Update happens to run first that
    /// frame is not something either script controls. A player whose own
    /// gravity integrates even once before the ground under their feet
    /// exists falls into empty space for a few frames -- exactly what
    /// "temporary" and "every time" together describe, not the
    /// [[spawn_clip_investigation_2026_09_04|earlier, unreproducible
    /// "falls forever"]] report. Fixed by force-building a real radius
    /// around the real player SYNCHRONOUSLY in CityStreamer.Awake -- see
    /// SpawnPreStreamBudget's own doc comment for why Awake specifically
    /// closes this rather than narrowing it.
    /// </summary>
    public sealed class SpawnPreStreamPlayTests
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
        public IEnumerator TheRealPlayersOwnVicinityIsForceStreamedBeforeOrdinaryPerFrameStreamingWouldHaveCaughtUp()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.IsNotNull(streamer, "no CityStreamer in the real saved scene");

            TestContext.WriteLine($"{streamer.LoadedChunkCount} chunks loaded after 2 real frames " +
                $"(SpawnPreStreamBudget={streamer.SpawnPreStreamBudget}, " +
                $"ChunkBuildBudgetPerFrame={streamer.ChunkBuildBudgetPerFrame})");

            // Ordinary per-frame streaming alone could only have reached
            // 2 * ChunkBuildBudgetPerFrame chunks in this same window --
            // reaching SpawnPreStreamBudget already, this early, is only
            // possible if Awake's own synchronous pre-stream actually ran.
            Assert.GreaterOrEqual(streamer.LoadedChunkCount, streamer.SpawnPreStreamBudget,
                $"only {streamer.LoadedChunkCount} chunks were loaded after 2 real frames -- the " +
                "spawn-time pre-stream never actually ran in Awake, so a player's own gravity could " +
                "still integrate before the ground under their feet exists");

            var player = Object.FindFirstObjectByType<CityX.Character.PlayerInputReader>();
            Assert.IsNotNull(player, "no PlayerInputReader in the real saved scene");
            int chunkX = Mathf.FloorToInt(player.transform.position.x / CityX.World.CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(player.transform.position.z / CityX.World.CityLayout.ChunkSize);
            Transform chunkTransform = streamer.transform.Find($"Chunk {chunkX},{chunkY}");
            Assert.IsNotNull(chunkTransform,
                $"the real player's own chunk ({chunkX},{chunkY}) was not among the force-streamed ones -- " +
                "the one chunk this fix most needs to guarantee is missing");
#else
            yield break;
#endif
        }
    }
}
