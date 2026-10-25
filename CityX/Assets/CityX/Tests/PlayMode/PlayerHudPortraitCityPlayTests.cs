using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The HUD portrait in the real city: on an ordinary load the body stands plain when the first portrait is taken, so that shot is kept at
    /// once, as it always was (the look the owner chose). Only a body doing something else then (fists up from a punch) has it taken again.
    /// </summary>
    public sealed class PlayerHudPortraitCityPlayTests
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
        }

        [UnityTest]
        public IEnumerator OnAnOrdinaryLoadTheFirstPortraitIsKeptAtOnce()
        {
#if UNITY_EDITOR
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("the portrait needs a graphics device -- run with -Graphics");
            }
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            var hud = Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the city has no HUD");
            for (int i = 0; i < 4 && hud.PortraitsTaken == 0; i++)
            {
                yield return null;
            }
            Assert.AreEqual(1, hud.PortraitsTaken, "the first portrait was not taken as the world loaded");
            Assert.IsFalse(hud.AwaitingPlainPose, "the body was not standing plain for the first portrait of an ordinary load");
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Assert.AreEqual(1, hud.PortraitsTaken, "the portrait of an ordinary load was taken again");
#else
            yield break;
#endif
        }
    }
}
