using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The title opens at once (owner, 2026-11-11: "the title screen takes quiet long to boot"): its menu is up on its first frames over black,
    /// the city, the skyline and the rooftop come on once the city's plan is worked out on a worker (TitleStage), and the black fades away.
    /// </summary>
    public sealed class TitleBootPlayTests
    {
        private Scene _title;

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        [UnityTest]
        public IEnumerator TheMenuIsUpAtOnceAndTheCityComesInBehindTheBlack()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(CharacterCreatorPlayTests.ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            var stage = Object.FindFirstObjectByType<TitleStage>();
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(stage, "the title has no stage");
            Assert.IsNotNull(stage.Curtain, "the title has no black to open behind: rebuild the title scene");
            Assert.IsNotNull(title, "the title has no menu");
            Assert.IsTrue(title.isActiveAndEnabled, "the menu is not up on the title's first frames");
            if (!stage.Placed)
            {
                Assert.AreEqual(1.0f, stage.Curtain.alpha, 1e-4f, "the city is seen before it is there");
                Assert.IsFalse(stage.City.gameObject.activeSelf, "the city streamer is on before its plan is ready: it would work it out on this frame");
                Assert.IsFalse(stage.Rooftop.gameObject.activeSelf, "the rooftop is up before it has a roof to stand on");
            }

            float realStart = Time.realtimeSinceStartup;
            while (!stage.Placed && Time.realtimeSinceStartup - realStart < 20.0f)
            {
                yield return null;
            }
            Assert.IsTrue(stage.Placed, "the city never came in");
            Assert.IsTrue(stage.City.gameObject.activeSelf);
            Assert.IsTrue(stage.Rooftop.gameObject.activeSelf);
            for (float waited = 0.0f; stage.Curtain.alpha > 0.0f && waited < TitleStage.CurtainFadeSeconds + 1.0f; waited += Time.deltaTime)
            {
                yield return null;
            }
            Assert.AreEqual(0.0f, stage.Curtain.alpha, 1e-4f, "the black never faded away");
#else
            yield break;
#endif
        }
    }
}
