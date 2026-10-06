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
    /// The unarmed fighting through the REAL input dispatch (a simulated mouse and keyboard in the real scene). Reported: the fists "feel really horrible":
    /// the feet froze, the punch came late, the animations were weak. What is checked here is what the player feels:
    ///  * a click throws the punch ON THE PRESS (it used to wait for the release, to tell a tap from a hold);
    ///  * the legs keep walking and the body keeps moving during a punch, and steps into it;
    ///  * the body turns to the enemy near where the camera looks (punch magnetism) instead of punching the way it used to face;
    ///  * holding the button on and letting go throws the haymaker.
    /// </summary>
    public sealed class RealInputPunchPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GameObject _dummy;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_dummy != null) Object.Destroy(_dummy);
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

        private struct Rig
        {
            public PlayerInputReader Input;
            public PlayerLocomotion Locomotion;
            public MeleeAttacker Melee;
            public FistPoseDriver Fists;
        }

        private IEnumerator Stage(System.Action<Rig> done)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            yield return Wait(0.3f);
            done(new Rig
            {
                Input = input,
                Locomotion = locomotion,
                Melee = input.GetComponent<MeleeAttacker>(),
                Fists = input.GetComponent<FistPoseDriver>(),
            });
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AClickThrowsThePunchOnThePressNotTheRelease()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Assert.IsNotNull(rig.Fists, "the player has no fist pose driver (it is added by PlayerAnimationDriver)");
            Assert.IsFalse(rig.Melee.IsSwinging, "setup: already swinging");

            Press(_mouse.leftButton);
            yield return null;
            yield return null;
            Assert.IsTrue(rig.Melee.IsSwinging, "the punch waited for the button to come up");
            Assert.IsTrue(rig.Fists.IsStriking || rig.Fists.Engage > 0.0f, "the fists did not move on the press");
            Assert.AreEqual(FistStrikes.JabName, rig.Melee.CurrentStage.Animation, "the opening punch is the jab");
            Release(_mouse.leftButton);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePlayerKeepsWalkingAndStepsIntoAPunch()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Vector3 start = rig.Locomotion.transform.position;
            Press(_keyboard.wKey);
            yield return Wait(0.4f);   // moving at pace
            Vector3 beforePunch = rig.Locomotion.transform.position;

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            float swingStart = Time.time;
            Vector3 at = rig.Locomotion.transform.position;
            while (rig.Melee.IsSwinging && Time.time - swingStart < 1.0f)
            {
                yield return null;
            }
            float moved = Vector3.Distance(new Vector3(rig.Locomotion.transform.position.x, 0, rig.Locomotion.transform.position.z), new Vector3(at.x, 0, at.z));
            Release(_keyboard.wKey);
            Assert.Greater(Time.time - swingStart, 0.1f, "the punch was over at once");
            Assert.Greater(moved, 0.25f, $"the body was rooted during the punch (moved {moved:F2} m)");
            Assert.Greater(Vector3.Distance(beforePunch, start), 0.5f, "setup: the player was not walking before the punch");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePunchTurnsTheBodyToTheEnemyNearWhereTheCameraLooks()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            Transform cam = Camera.main.transform;
            Vector3 aim = cam.forward;
            aim.y = 0.0f;
            aim.Normalize();
            // An enemy 40 degrees to the right of the camera's aim, 2 m away, at chest height.
            Vector3 toEnemy = Quaternion.Euler(0.0f, 40.0f, 0.0f) * aim;
            _dummy = new GameObject("Dummy");
            _dummy.transform.position = rig.Locomotion.transform.position + toEnemy * 2.0f + Vector3.up * 1.2f;
            var capsule = _dummy.AddComponent<CapsuleCollider>();
            capsule.height = 2.0f;
            _dummy.AddComponent<SparringDummy>();
            Physics.SyncTransforms();
            yield return null;

            // The body faces away from the enemy at first.
            rig.Locomotion.transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0.0f, -80.0f, 0.0f) * aim);
            Press(_mouse.leftButton);
            yield return Wait(0.35f);
            Release(_mouse.leftButton);

            Vector3 facing = rig.Locomotion.transform.forward;
            facing.y = 0.0f;
            float off = Vector3.Angle(facing, toEnemy);
            Assert.Less(off, 12.0f, $"the body is {off:F0} degrees off the enemy after the punch");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HoldingTheButtonAndLettingGoThrowsTheHaymakerAfterTheJab()
        {
#if UNITY_EDITOR
            Rig rig = default;
            yield return Stage(r => rig = r);
            var thrown = new System.Collections.Generic.List<string>();
            rig.Melee.SwingStarted += stage => thrown.Add(stage.Animation);

            Press(_mouse.leftButton);
            yield return Wait(PlayerInputReader.HeavyPunchHoldSeconds + 0.1f);
            Release(_mouse.leftButton);
            yield return Wait(1.2f);

            Assert.GreaterOrEqual(thrown.Count, 2, "holding then releasing did not follow the jab");
            Assert.AreEqual(FistStrikes.JabName, thrown[0]);
            Assert.AreEqual(FistStrikes.HaymakerName, thrown[thrown.Count - 1], "the held punch did not come out as the haymaker");
#else
            yield break;
#endif
        }
    }
}
