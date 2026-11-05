using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Escape that closes a menu page (the stats, the loadout, the book, the C menu...) is not also a pause, with the real keyboard. Both
    /// the page and the pause menu read the same press in the same frame: the page closed and the pause menu opened over the game, sometimes
    /// with no cursor.
    /// </summary>
    public sealed class EscapeClosesOnlyTheMenuPlayTests : InputTestFixture
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _page;
        private GameObject _pause;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDownScene()
        {
            foreach (GameObject go in new[] { _ground, _player, _page, _pause })
            {
                if (go != null) Object.Destroy(go);
            }
            Time.timeScale = 1.0f;
        }

        [UnityTest]
        public IEnumerator EscapeOnAPageClosesThePageAndDoesNotPause()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();

            _page = new GameObject("Page");
            var root = new GameObject("Root");
            root.transform.SetParent(_page.transform, false);
            root.SetActive(false);   // a page is built hidden
            var page = _page.AddComponent<PlaceholderPanelView>();
            page.Root = root;
            page.InputReader = reader;

            _pause = new GameObject("Pause");
            var dim = new GameObject("Dim");
            dim.transform.SetParent(_pause.transform, false);
            dim.SetActive(false);
            var pause = _pause.AddComponent<PauseMenuView>();
            pause.Dim = dim;
            yield return null;
            yield return null;

            page.Show("Book", "Nothing here yet.");
            Assert.IsTrue(page.IsOpen, "setup: the page did not open");
            Assert.IsFalse(pause.IsOpen, "setup: the game is paused");
            yield return null;
            Assert.IsTrue(reader.IsHeld, "setup: the page does not hold the keys");

            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return null;
            Assert.IsFalse(page.IsOpen, "Escape did not close the page");
            Assert.IsFalse(pause.IsOpen, "the Escape that closed the page also paused the game");

            // And with nothing open, Escape pauses as it always did.
            Press(_keyboard.escapeKey);
            yield return null;
            Release(_keyboard.escapeKey);
            yield return null;
            Assert.IsTrue(pause.IsOpen, "setup: Escape no longer pauses at all");
            pause.Close();
#else
            yield break;
#endif
        }
    }
}
