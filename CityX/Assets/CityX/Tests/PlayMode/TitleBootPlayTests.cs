using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The title opens at once (owner, 2026-11-11: "the title screen takes quiet long to boot") on black with a word that it is loading, over its
    /// menu too (owner, 2026-11-12: "it starts with just the buttons"); the city, the skyline and the rooftop come on once the city's plan is
    /// worked out on a worker (TitleStage), and the black fades away off the whole title at once. Logs how long that took.
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
        public IEnumerator TheTitleOpensOnBlackSayingItIsLoadingAndComesInWhole()
        {
#if UNITY_EDITOR
            float loadStart = Time.realtimeSinceStartup;
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
                Assert.AreEqual(stage.Curtain.transform.parent.childCount - 1, stage.Curtain.transform.GetSiblingIndex(),
                    "the black is not over the menu: the title shows only its buttons while it loads (rebuild the title scene)");
                Assert.IsTrue(stage.Curtain.blocksRaycasts, "the hidden menu can be clicked while the title loads");
                Assert.IsNotNull(stage.Loading, "nothing says the title is loading");
                Assert.IsFalse(stage.City.gameObject.activeSelf, "the city streamer is on before its plan is ready: it would work it out on this frame");
                Assert.IsFalse(stage.Rooftop.gameObject.activeSelf, "the rooftop is up before it has a roof to stand on");
            }

            float realStart = Time.realtimeSinceStartup, longestFrame = 0.0f, last = Time.realtimeSinceStartup;
            while (!stage.Placed && Time.realtimeSinceStartup - realStart < 20.0f)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.realtimeSinceStartup - last);
                last = Time.realtimeSinceStartup;
            }
            Debug.Log($"CityX title boot: placed {Time.realtimeSinceStartup - loadStart:F2} s after the scene was asked for, longest frame {longestFrame * 1000.0f:F0} ms");
            Assert.IsTrue(stage.Placed, "the city never came in");
            Assert.IsTrue(stage.City.gameObject.activeSelf);
            Assert.IsTrue(stage.Rooftop.gameObject.activeSelf);
            Assert.IsFalse(stage.Curtain.blocksRaycasts, "the menu does not answer once the city is there");
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
