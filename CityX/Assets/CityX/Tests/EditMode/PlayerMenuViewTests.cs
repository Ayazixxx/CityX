using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// PlayerMenuView/PlayerSubmenuView driven directly through their own
    /// public Open/Close/Show/Hide, the same reasoning DialogueBox's own
    /// tests already use -- Keyboard.current is null in this EditMode
    /// context, so the C-key/Escape detection in Update() itself is not
    /// what these tests exercise; the state transitions those key presses
    /// would trigger are.
    /// </summary>
    public sealed class PlayerMenuViewTests
    {
        private GameObject _playerObject;
        private GameObject _cameraObject;
        private PlayerInputReader _inputReader;
        private ThirdPersonCamera _orbitCamera;
        private PlayerMenuCamera _menuCamera;
        private PlayerMenuView _menu;
        private PlayerSubmenuView _submenu;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player", typeof(CharacterController));
            _playerObject.transform.position = new Vector3(3.0f, 0.0f, 3.0f);
            _inputReader = _playerObject.AddComponent<PlayerInputReader>();
            _menuCamera = _playerObject.AddComponent<PlayerMenuCamera>();

            _cameraObject = new GameObject("Camera");
            _orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _orbitCamera.Target = _playerObject.transform;

            var menuRoot = new GameObject("PlayerMenuRoot");
            // A freshly created GameObject defaults to active -- in the real
            // flow Awake() calls Close() before anything else ever runs,
            // which is what establishes "starts closed" as an invariant.
            // Nothing here calls Awake() (see the class doc comment), so
            // this stands in for it directly.
            menuRoot.SetActive(false);
            _menu = menuRoot.AddComponent<PlayerMenuView>();
            _menu.Root = menuRoot;
            _menu.Player = _playerObject.transform;
            _menu.CameraTransform = _cameraObject.transform;
            _menu.InputReader = _inputReader;
            _menu.OrbitCamera = _orbitCamera;
            _menu.MenuCamera = _menuCamera;

            var submenuRoot = new GameObject("SubmenuRoot");
            submenuRoot.transform.SetParent(menuRoot.transform, false);
            // Same reasoning as menuRoot's own SetActive(false) above --
            // Show()/Hide() have no early-return guard the way Open() does,
            // but IsVisible has to start false for the "starts hidden"
            // assertion to mean anything.
            submenuRoot.SetActive(false);
            _submenu = submenuRoot.AddComponent<PlayerSubmenuView>();
            _submenu.Root = submenuRoot;
            _menu.Submenu = _submenu;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_menu.gameObject);
        }

        [Test]
        public void OpenShowsTheRootAndDisablesInputAndOrbitCamera()
        {
            _menu.Open();

            Assert.IsTrue(_menu.IsOpen);
            Assert.IsFalse(_inputReader.enabled, "movement/combat input should be disabled while the menu is up");
            Assert.IsFalse(_orbitCamera.enabled, "mouse-look should be disabled while the menu is up");
        }

        [Test]
        public void OpenCutsTheCameraToTheFrontOnFraming()
        {
            _playerObject.transform.rotation = Quaternion.Euler(0.0f, 40.0f, 0.0f);
            _menu.Open();

            Vector3 expectedPosition = PlayerMenuCameraMath.CameraPosition(
                _playerObject.transform.position, _playerObject.transform.forward);
            Assert.AreEqual(expectedPosition, _cameraObject.transform.position);

            // The camera should end up somewhere in front of the player,
            // not coincident with them -- a cheap sanity check that Begin
            // actually ran rather than leaving the camera wherever it was.
            float distance = Vector3.Distance(_cameraObject.transform.position, _playerObject.transform.position);
            Assert.Greater(distance, 0.5f);
        }

        [Test]
        public void CloseHidesTheRootAndReenablesInputAndOrbitCamera()
        {
            _menu.Open();
            _menu.Close();

            Assert.IsFalse(_menu.IsOpen);
            Assert.IsTrue(_inputReader.enabled);
            Assert.IsTrue(_orbitCamera.enabled);
        }

        [Test]
        public void CloseAlsoHidesAnOpenSubmenu()
        {
            _menu.Open();
            _submenu.Show();
            Assert.IsTrue(_submenu.IsVisible);

            _menu.Close();

            Assert.IsFalse(_submenu.IsVisible, "closing the whole menu should not leave the fly-out showing next time it opens");
        }

        [Test]
        public void SubmenuStartsHiddenAndShowHideToggleItsVisibility()
        {
            Assert.IsFalse(_submenu.IsVisible);

            _submenu.Show();
            Assert.IsTrue(_submenu.IsVisible);

            _submenu.Hide();
            Assert.IsFalse(_submenu.IsVisible);
        }

        [Test]
        public void OpeningTwiceInARowIsHarmlessAndDoesNotReenableInputEarly()
        {
            _menu.Open();
            _inputReader.enabled = false; // simulate whatever Open() already set
            _menu.Open();

            Assert.IsTrue(_menu.IsOpen);
            Assert.IsFalse(_inputReader.enabled, "a redundant Open() must not disturb the already-disabled input");
        }
    }
}
