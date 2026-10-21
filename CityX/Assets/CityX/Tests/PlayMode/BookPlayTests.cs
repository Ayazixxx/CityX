using System.Collections;
using CityX.Character;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Requested: "add books and when you press E on them it opens a
    /// page on screen with text ive prepared but just make the system for
    /// now." Through the REAL PlayerInputReader.Update()-independent Book.
    /// Update() dispatch (Book polls Keyboard directly, same as BenchSeat),
    /// with a real simulated keyboard (InputTestFixture) and a real
    /// CharacterController walked in via Move() -- same overall shape as
    /// BenchSeatPlayTests.
    ///
    /// A FAKE IBookPanel, NOT the real BookPanelView, stands in for the
    /// scene-built reading panel -- BookPanelView only exists once
    /// CityMassingSceneBuilder has built the whole massing scene, and
    /// loading that whole scene (the way RagdollDuringSwingPlayTests does)
    /// is a much heavier, slower fixture than this test needs: the actual
    /// claim under test is "Book finds an IBookPanel and calls Show with
    /// the right title/body, from the right range" -- a bare recording
    /// fake proves that exactly as directly as the real panel would.
    /// </summary>
    public sealed class BookPlayTests : InputTestFixture
    {
        private sealed class FakeBookPanel : MonoBehaviour, IBookPanel
        {
            public int ShowCount;
            public string LastTitle;
            public string LastBody;

            public void Show(string title, string body)
            {
                ShowCount++;
                LastTitle = title;
                LastBody = body;
            }
        }

        private GameObject _ground;
        private GameObject _player;
        private GameObject _bookObject;
        private GameObject _panelObject;
        private Book _book;
        private FakeBookPanel _panel;
        private CharacterController _controller;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _bookObject = new GameObject("Book");
            _bookObject.transform.position = new Vector3(0.0f, 0.0f, 6.0f);
            _book = _bookObject.AddComponent<Book>();
            _book.Title = "Test Title";
            _book.Body = "Test body text.";
            _book.Build(null);

            _panelObject = new GameObject("FakeBookPanel");
            _panel = _panelObject.AddComponent<FakeBookPanel>();
        }

        // [UnityTearDown], not the plain [TearDown] InputTestFixture already
        // declares -- see BenchSeatPlayTests' own identical comment on why
        // shadowing that name breaks device cleanup between tests.
        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_bookObject != null) Object.DestroyImmediate(_bookObject);
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_ground != null) Object.DestroyImmediate(_ground);
            yield break;
        }

        private void LoadPlayer(Vector3 position)
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, position, Quaternion.identity);
            _controller = _player.GetComponent<CharacterController>();
            Assert.IsNotNull(_controller, "Player.prefab has no CharacterController");
#endif
        }

        /// <summary>Same fixed-step walk BenchSeatPlayTests.WalkToBench uses, and for the same reason.</summary>
        private IEnumerator WalkToBook()
        {
            Vector3 toward = (_bookObject.transform.position - _player.transform.position).normalized;
            for (int step = 0; step < 120; step++)
            {
                float remaining = Vector3.Distance(_player.transform.position, _bookObject.transform.position);
                if (_book.HasNearbyPlayer && remaining <= 1.5f)
                {
                    yield break;
                }
                _controller.Move(toward * (4.0f * Time.fixedDeltaTime));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator WalkingUpAndPressingEOpensTheBookPanelWithItsOwnText()
        {
#if UNITY_EDITOR
            LoadPlayer(new Vector3(0.0f, 0.0f, 3.0f));
            yield return WalkToBook();

            Assert.IsTrue(_book.HasNearbyPlayer,
                "walking the real CharacterController up to the real book's real trigger " +
                "should have fired OnTriggerEnter");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);

            Assert.AreEqual(1, _panel.ShowCount, "pressing E next to the book should open the reading panel");
            Assert.AreEqual("Test Title", _panel.LastTitle);
            Assert.AreEqual("Test body text.", _panel.LastBody);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TooFarAwayPressingEDoesNothing()
        {
#if UNITY_EDITOR
            LoadPlayer(new Vector3(0.0f, 0.0f, 0.0f));
            yield return null;

            Assert.IsFalse(_book.HasNearbyPlayer, "setup failed -- player should not start near the book");

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);

            Assert.AreEqual(0, _panel.ShowCount, "pressing E far from the book should not open anything");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
