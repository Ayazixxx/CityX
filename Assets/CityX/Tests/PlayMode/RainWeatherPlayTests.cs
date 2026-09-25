using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT ASK: "add rainy weather, in console /weather rain... make it
    /// look reallllly good." /weather rain/clear already existed end to end
    /// before this; what is new is everything RainEffect now reaches beyond
    /// its own particles -- checked here through the real dev console
    /// dispatch, not a direct RainEffect call, since a real fog/light change
    /// only means anything if the real command path actually drives it.
    /// </summary>
    public sealed class RainWeatherPlayTests
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
        public IEnumerator WeatherRainActivatesParticlesAndTightensFogThenClearRestoresItExactly()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            // RenderSettings (fog included) belong to whichever scene is
            // ACTIVE, not to every loaded scene -- an additive load alone
            // leaves the empty PlayMode-test bootstrap scene's own defaults
            // (fog effectively 0) in charge, not CityMassing's own real
            // 150/380. Real gameplay never hits this: the player's own
            // scene loads as the (only, therefore active) scene. Confirmed
            // directly -- a first attempt at this test read a "clear" fog
            // start of 0 without this line, since fog never actually made it
            // to RenderSettings at all.
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console);
            RainEffect rain = console.Rain;
            Assert.IsNotNull(rain, "setup failed -- no RainEffect wired to the real console");

            float clearFogStart = RenderSettings.fogStartDistance;
            float clearFogEnd = RenderSettings.fogEndDistance;

            console.Submit("/weather rain");
            yield return null;

            Assert.IsTrue(rain.IsActive, "/weather rain did not activate the real rain particles");

            // WAITING ON THE REAL CONDITION, NOT A FIXED FRAME COUNT --
            // EXPLICIT BUG, caught directly rather than assumed away: a
            // batch-mode PlayMode run has no fixed frame rate at all (this
            // project's own tooling notes ~2500fps headless), so "300
            // frames" is nowhere near "5 real seconds" and StormStrength
            // was still only partway through its ramp when the fixed count
            // ran out. Capped, not unbounded, so a genuine regression still
            // fails the test instead of hanging it.
            // EXACTLY 1.0, not just "close" -- Mathf.MoveTowards (which
            // StormRamp is built on) snaps to its exact target the instant
            // the remaining distance is within a single step's reach,
            // rather than approaching it asymptotically, so waiting for the
            // exact value is reliable regardless of how many tiny steps a
            // very high batch-mode frame rate happens to take to get there.
            // A looser "close enough" threshold here still leaves a real gap
            // for fogStartDistance's own much wider 90-unit range to turn
            // into a visible few-tenths error against the tight tolerance
            // below (confirmed directly -- an earlier 0.001 threshold here
            // read back as a 0.5 unit fog error).
            for (int i = 0; i < 3000 && rain.StormStrength < 1.0f; i++)
            {
                yield return null;
            }
            Assert.AreEqual(1.0f, rain.StormStrength, "the storm ramp never actually finished settling in");
            // ONE MORE FRAME -- a coroutine resumes after Update but before
            // that same frame's LateUpdate (the only place fog is actually
            // written), so the instant StormStrength first reads its exact
            // target, RenderSettings.fogStartDistance still reflects the
            // PREVIOUS frame's LateUpdate, one small step short of it.
            yield return null;

            Assert.Less(RenderSettings.fogStartDistance, clearFogStart - 10.0f,
                "fog never actually tightened while it was raining");
            Assert.Less(RenderSettings.fogEndDistance, clearFogEnd - 10.0f,
                "fog never actually tightened while it was raining");

            console.Submit("/weather clear");
            yield return null;

            Assert.IsFalse(rain.IsActive, "/weather clear did not stop the real rain particles");

            for (int i = 0; i < 3000 && rain.StormStrength > 0.0f; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0.0f, rain.StormStrength, "the storm ramp never actually finished easing back out");
            yield return null; // same one-frame lag as above

            Assert.AreEqual(clearFogStart, RenderSettings.fogStartDistance, 0.01f,
                "fog start distance did not restore to its exact original value after clearing");
            Assert.AreEqual(clearFogEnd, RenderSettings.fogEndDistance, 0.01f,
                "fog end distance did not restore to its exact original value after clearing");
#else
            yield break;
#endif
        }
    }
}
