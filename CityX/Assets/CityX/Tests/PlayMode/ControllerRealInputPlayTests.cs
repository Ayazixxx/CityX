using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The game played on a controller, through the real input dispatch in the real scene (owner, 2026-11-25: "make it playable on
    /// controller"; docs/CONTROLLER.md): the left stick walks, the right stick turns the camera, A jumps, the right trigger punches, and the
    /// keyboard and mouse still work beside it.
    /// </summary>
    public sealed class ControllerRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Gamepad _pad;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            _pad = InputSystem.AddDevice<Gamepad>();
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

        private IEnumerator Stage(System.Action<PlayerInputReader> done)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            yield return Wait(0.3f);
            done(input);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheLeftStickWalksAndTheKeyboardStillDoes()
        {
            PlayerInputReader input = null;
            yield return Stage(r => input = r);
            Transform body = input.transform;

            Vector3 start = body.position;
            Set(_pad.leftStick, new Vector2(0.0f, 1.0f));
            yield return Wait(1.0f);
            Set(_pad.leftStick, Vector2.zero);
            float onStick = Vector3.Distance(Flat(start), Flat(body.position));
            Assert.Greater(onStick, 2.0f, $"the left stick pushed forward a second moved the body {onStick:0.00} m");
            yield return Wait(0.5f);

            start = body.position;
            Press(_keyboard.wKey);
            yield return Wait(1.0f);
            Release(_keyboard.wKey);
            float onKeys = Vector3.Distance(Flat(start), Flat(body.position));
            Assert.Greater(onKeys, 2.0f, $"W held a second with a controller plugged in moved the body {onKeys:0.00} m");
            Assert.AreEqual(onKeys, onStick, onKeys * 0.25f, "the stick all the way is not the keyboard's pace");
        }

        [UnityTest]
        public IEnumerator TheRightStickTurnsTheCamera()
        {
            PlayerInputReader input = null;
            yield return Stage(r => input = r);
            var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            float before = camera.transform.eulerAngles.y;
            Set(_pad.rightStick, new Vector2(1.0f, 0.0f));
            yield return Wait(0.5f);
            Set(_pad.rightStick, Vector2.zero);
            float turned = Mathf.Abs(Mathf.DeltaAngle(before, camera.transform.eulerAngles.y));
            Assert.Greater(turned, 60.0f, $"the right stick held over half a second turned the camera {turned:0} degrees");
        }

        [UnityTest]
        public IEnumerator AJumpsAndTheRightTriggerPunches()
        {
            PlayerInputReader input = null;
            yield return Stage(r => input = r);
            var locomotion = input.GetComponent<PlayerLocomotion>();
            var melee = input.GetComponent<MeleeAttacker>();

            float ground = input.transform.position.y, highest = ground;
            Press(_pad.buttonSouth);
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                highest = Mathf.Max(highest, input.transform.position.y);
            }
            Release(_pad.buttonSouth);
            Assert.Greater(highest - ground, 0.5f, "A did not jump");
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Wait(0.5f);

            Set(_pad.rightTrigger, 1.0f);
            yield return null;
            yield return null;
            Assert.IsTrue(melee.IsSwinging, "the right trigger did not throw a punch");
            Set(_pad.rightTrigger, 0.0f);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
