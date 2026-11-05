using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What opening and closing the map actually does to the two components
    /// that would otherwise fight it for the mouse: movement/combat input and
    /// the orbit camera's own look. Built by hand rather than by loading the
    /// scene, so this tests MapPanelView's own behaviour and fails for one
    /// reason.
    /// </summary>
    public class MapPanelViewPlayTests
    {
        private GameObject _panelObject;
        private GameObject _playerObject;
        private GameObject _cameraObject;

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
        }

        private MapPanelView MakePanel(out PlayerInputReader inputReader, out ThirdPersonCamera orbitCamera)
        {
            _playerObject = new GameObject("Player");
            _playerObject.AddComponent<CharacterController>();
            _playerObject.AddComponent<PlayerLocomotion>();
            inputReader = _playerObject.AddComponent<PlayerInputReader>();

            _cameraObject = new GameObject("Camera");
            orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            orbitCamera.Target = _playerObject.transform;

            _panelObject = new GameObject("MapPanel");
            var panel = _panelObject.AddComponent<MapPanelView>();
            panel.Root = new GameObject("Root");
            panel.Root.transform.SetParent(_panelObject.transform, false);
            // A fresh GameObject is active by default, which would make
            // IsOpen true before Open() is ever called -- Awake() does this
            // same thing via Close() in the real scene, but nothing runs
            // Awake on a component built by hand like this one.
            panel.Root.SetActive(false);
            panel.InputReader = inputReader;
            panel.OrbitCamera = orbitCamera;
            return panel;
        }

        [UnityTest]
        public IEnumerator OpeningDisablesMovementAndCameraLook()
        {
            MapPanelView panel = MakePanel(out PlayerInputReader inputReader, out ThirdPersonCamera orbitCamera);
            yield return null;

            panel.Open();
            yield return null;

            Assert.IsTrue(panel.IsOpen);
            Assert.IsFalse(inputReader.TakesKeys, "input should be disabled while the map is open");
            Assert.IsFalse(orbitCamera.enabled, "the orbit camera should be disabled while the map is open");
        }

        [UnityTest]
        public IEnumerator ClosingRestoresMovementAndCameraLook()
        {
            MapPanelView panel = MakePanel(out PlayerInputReader inputReader, out ThirdPersonCamera orbitCamera);
            yield return null;
            panel.Open();
            yield return null;

            panel.Close();
            yield return null;

            Assert.IsFalse(panel.IsOpen);
            Assert.IsTrue(inputReader.TakesKeys, "input should come back once the map closes");
            Assert.IsTrue(orbitCamera.enabled, "the orbit camera should come back once the map closes");
        }

        [UnityTest]
        public IEnumerator OpeningTwiceInARowDoesNothingExtra()
        {
            MapPanelView panel = MakePanel(out PlayerInputReader inputReader, out _);
            yield return null;

            panel.Open();
            yield return null;
            panel.Open();
            yield return null;
            panel.Close();
            yield return null;

            // Opened twice and closed once, the keys are the player's again: the second Open held nothing more.
            Assert.IsTrue(inputReader.TakesKeys);
        }
    }
}
