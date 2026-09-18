using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT ASK: "make an npc that is a normal white playermodel and he
    /// flys around with the jetpack in the sky. make him go around walls and
    /// stuff and not get stuck." FlyingNpcBrainTests/FlyingNpcSteeringPlayTests
    /// already prove the wander and avoidance logic in isolation; this is the
    /// gap between correct logic and a real body, in the real city, actually
    /// staying airborne -- through the real dev console, not a direct
    /// component construction, since /spawn flyer is the only way a player
    /// ever actually gets one.
    /// </summary>
    public sealed class FlyingNpcPlayTests
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
        public IEnumerator SpawnFlyerProducesARealFlyingNpcThatStaysAirborne()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console);
            Assert.IsNotNull(console.FlyingNpcPrefab, "setup failed -- no flying npc prefab wired to the real console");

            console.Submit("/spawn flyer");
            yield return null;

            var flyer = Object.FindFirstObjectByType<FlyingNpc>();
            Assert.IsNotNull(flyer, "/spawn flyer did not actually produce a FlyingNpc in the scene");

            float startY = flyer.transform.position.y;

            // A couple of real seconds' worth of frames -- long enough for
            // the steering to actually settle toward its cruise band if it
            // is going to, without pinning this to a specific frame-rate
            // assumption (a batch-mode run has no fixed one -- see this
            // session's own RainWeatherPlayTests for the exact same lesson).
            for (int i = 0; i < 600; i++)
            {
                yield return null;
            }

            Assert.Greater(flyer.transform.position.y, startY - 5.0f,
                "the flying npc lost significant altitude instead of staying airborne -- it may have fallen out of the sky");
            Assert.Less(flyer.HeightAboveSurface, FlyingNpcSteering.CruiseAltitudeAboveSurface * 3.0f,
                "the flying npc climbed to an unreasonable altitude -- it may be stuck fleeing something it can never actually clear");
#else
            yield break;
#endif
        }
    }
}
