using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE PARACHUTE THROUGH THE REAL INPUT (owner, 2026-11-19; docs/PARACHUTE.md): the real scene's player, the parachute put on with the dev
    /// console, lifted high over the city and let fall, with a simulated keyboard: Space early does nothing, Space after two seconds of falling
    /// opens the canopy, A turns it.
    /// </summary>
    public sealed class ParachuteRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FallingHighOverTheCitySpaceOpensItOnlyAfterTwoSecondsAndATurnsIt()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Object.FindFirstObjectByType<DevConsoleView>().Submit("/equip parachute");
            yield return null;
            var chute = input.EquippedParachute as Parachute;
            Assert.IsNotNull(chute, "/equip parachute did not put one on");

            locomotion.transform.position += Vector3.up * 320.0f;
            Physics.SyncTransforms();
            yield return Wait(1.0f);
            yield return Tap(_keyboard.spaceKey);
            Assert.IsFalse(chute.DrivesBody, "Space after a second of falling opened it");
            yield return Wait(1.5f);
            Assert.AreEqual(ChuteState.Freefall, chute.Flight.State, "after two seconds and more of falling the body is not spread out to open it");
            yield return Tap(_keyboard.spaceKey);
            Assert.AreEqual(ChuteState.Opening, chute.Flight.State, "Space in freefall did not open it");
            yield return Wait(ParachuteFlight.OpeningSeconds + 0.3f);
            Assert.AreEqual(ChuteState.Open, chute.Flight.State);
            Assert.IsTrue(chute.Canopy.Showing, "no canopy over the real player");
            Assert.That(-locomotion.Velocity.y, Is.InRange(2.5f, 5.5f), "the real player does not sink as a canopy does");

            float heading = locomotion.transform.eulerAngles.y;
            Press(_keyboard.aKey);
            yield return Wait(1.0f);
            Release(_keyboard.aKey);
            yield return null;
            Assert.Less(Mathf.DeltaAngle(heading, locomotion.transform.eulerAngles.y), -25.0f, "A does not turn the real player's canopy left");
#else
            yield break;
#endif
        }
    }
}
