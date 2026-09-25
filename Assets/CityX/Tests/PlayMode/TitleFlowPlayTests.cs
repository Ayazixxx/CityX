using System.Collections;
using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// Walks the menus the way a person does: by pressing the buttons.
    ///
    /// THIS IS THE ONE THAT WOULD HAVE CAUGHT IT. Every button on all three
    /// screens was dead -- they were wired with onClick.AddListener from an
    /// editor script, which is not serialized, so the scene saved without a
    /// single listener on it. Nothing errored. SINGLEPLAYER simply did
    /// nothing, and that is how it was found: by pressing it.
    ///
    /// So these press the real Buttons in the real scene and check the screen
    /// actually changed. An edit-mode check that a ButtonAction exists is not
    /// enough on its own -- it says the data is there, not that pressing the
    /// button runs it.
    /// </summary>
    public class TitleFlowPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";

        private Scene _title;
        private TitleScreen _screen;

        [UnitySetUp]
        public IEnumerator OpenTheTitleScreen()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
#endif
            _title = SceneManager.GetSceneByPath(ScenePath);
            _screen = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(_screen, "the title scene has no TitleScreen");
            _screen.Show(TitleScreen.Panel.Menu);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitleScreen()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        /// <summary>
        /// Presses the button of that name that is CURRENTLY ON SCREEN -- not
        /// by calling the method it happens to be wired to, which would prove
        /// nothing about the wiring.
        ///
        /// Scoped to what is showing because several names are reused across
        /// panels: BACK and CANCEL each exist three or four times over, one per
        /// screen that needs one, and only ever one of them is up at a time.
        /// </summary>
        private void Press(string name)
        {
            Button[] showing = _title.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .Where(found => found.name == name && found.gameObject.activeInHierarchy)
                .ToArray();

            Assert.IsNotEmpty(showing,
                $"no button called {name} is on screen, so nobody could press it");
            Assert.AreEqual(1, showing.Length,
                $"{showing.Length} buttons called {name} are on screen at once");
            showing[0].onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator SingleplayerReachesTheCharacterList()
        {
            Press("SingleplayerButton");
            yield return null;

            Assert.AreEqual(TitleScreen.Panel.CharacterSelect, _screen.Showing,
                "pressing SINGLEPLAYER went nowhere");
            Assert.IsNotNull(_screen.Select);
            Assert.AreEqual(CharacterSelect.Mode.List, _screen.Select.Showing);
        }

        [UnityTest]
        public IEnumerator TheCharacterListReachesTheCreator()
        {
            Press("SingleplayerButton");
            yield return null;
            Press("NewButton");
            yield return null;

            Assert.AreEqual(TitleScreen.Panel.Creator, _screen.Showing,
                "pressing NEW CHARACTER did not open the creator");

            var creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.IsNotNull(creator,
                "the creator panel is showing but its body was never switched on");
        }

        /// <summary>
        /// Backing out of the creator with nothing painted must not stop to
        /// ask. There is nothing to lose, and a confirmation for an empty
        /// canvas is a dialog that only ever gets in the way.
        /// </summary>
        [UnityTest]
        public IEnumerator BackingOutOfAnUntouchedCreatorDoesNotAsk()
        {
            Press("SingleplayerButton");
            yield return null;
            Press("NewButton");
            yield return null;

            var overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            Assert.IsNotNull(overlays);

            Press("BackButton");
            yield return null;

            Assert.IsFalse(overlays.ConfirmPanel.activeSelf,
                "it asked whether to discard a design that was never painted");
            Assert.AreEqual(TitleScreen.Panel.CharacterSelect, _screen.Showing,
                "BACK did not return to the character list");
        }

        /// <summary>
        /// And once something HAS been painted, it must ask -- nothing is saved
        /// from the creator until a name is confirmed, so leaving quietly would
        /// throw the work away.
        /// </summary>
        [UnityTest]
        public IEnumerator BackingOutOfAPaintedCreatorAsksFirst()
        {
            Press("SingleplayerButton");
            yield return null;
            Press("NewButton");
            yield return null;

            var creator = Object.FindFirstObjectByType<CharacterCreator>();
            var overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            Assert.IsNotNull(creator);
            Assert.IsNotNull(overlays);

            creator.Colour = Color.magenta;
            creator.BrushRadiusPixels = 5;
            Vector3 chest = creator.BodyCollider.transform.TransformPoint(
                creator.BodyCollider.sharedMesh.bounds.center);
            creator.PressAt(creator.Camera.WorldToScreenPoint(chest));
            yield return null;
            Assert.IsTrue(creator.HasEdits, "the test failed to paint anything");

            Press("BackButton");
            yield return null;

            Assert.IsTrue(overlays.ConfirmPanel.activeSelf,
                "BACK threw away a painted design without asking");
            Assert.AreEqual(TitleScreen.Panel.Creator, _screen.Showing,
                "it left the creator before the question was answered");

            Press("CancelButton");
            yield return null;
            Assert.IsFalse(overlays.ConfirmPanel.activeSelf, "CANCEL did not dismiss the question");
            Assert.AreEqual(TitleScreen.Panel.Creator, _screen.Showing,
                "CANCEL left the creator anyway");
        }

        /// <summary>
        /// ENTER THE CITY opens the name prompt rather than finishing, and an
        /// empty name is refused -- otherwise somebody gets a character called
        /// nothing and no explanation.
        /// </summary>
        [UnityTest]
        public IEnumerator EnteringTheCityAsksForANameFirst()
        {
            Press("SingleplayerButton");
            yield return null;
            Press("NewButton");
            yield return null;

            var overlays = Object.FindFirstObjectByType<CreatorOverlays>();
            Assert.IsNotNull(overlays);
            Assert.IsFalse(overlays.NamePanel.activeSelf);

            Press("EnterCityButton");
            yield return null;

            Assert.IsTrue(overlays.NamePanel.activeSelf,
                "ENTER THE CITY did not ask for a name");
            Assert.AreEqual(TitleScreen.Panel.Creator, _screen.Showing,
                "it left the creator before a name was given");

            Press("StartButton");
            yield return null;
            Assert.IsTrue(overlays.NamePanel.activeSelf,
                "an empty name was accepted, which makes a character out of nothing");
        }

        [UnityTest]
        public IEnumerator EveryTopLevelPanelIsReachableAndBackable()
        {
            foreach ((string button, TitleScreen.Panel panel) in new[]
            {
                ("MultiplayerButton", TitleScreen.Panel.Multiplayer),
                ("SettingsButton", TitleScreen.Panel.Settings),
            })
            {
                _screen.Show(TitleScreen.Panel.Menu);
                yield return null;

                Press(button);
                yield return null;
                Assert.AreEqual(panel, _screen.Showing, $"{button} went nowhere");

                _screen.Back();
                yield return null;
                Assert.AreEqual(TitleScreen.Panel.Menu, _screen.Showing,
                    $"there is no way back from {panel}");
            }
        }
    }
}
