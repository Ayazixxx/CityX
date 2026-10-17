using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT: "im still getting up at an angle when i
    /// ragdoll." This came right after a previous fix
    /// (JetpackGroundedJumpPlayTests.LandingWithAnExistingLeanStraightensItBackUpright)
    /// that made Jetpack.Tick run unconditionally once grounded -- but a
    /// ragdoll never reaches that dispatch at all: PlayerInputReader's own
    /// ragdoll branch returns before ever calling Jetpack.Tick (see
    /// StopThrustEffects' own doc comment), so whatever lean the body had at
    /// the exact instant of the crash stayed frozen through the entire limp
    /// ragdoll AND the whole "GettingUp" clip -- several real seconds longer
    /// than the earlier fix's own landing case. See Jetpack.ResetTilt's own
    /// doc comment for the actual fix. Driven through the real
    /// PlayerInputReader dispatch, not a direct Jetpack.Tick call, since
    /// that dispatch is exactly where the bug lived.
    /// </summary>
    public sealed class JetpackRagdollPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
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

        [UnityTest]
        public IEnumerator RagdollingWithAnExistingLeanResetsItImmediatelyInsteadOfFreezingItThroughTheWholeStandUp()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            var locomotion = input.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(console);
            Assert.IsNotNull(input);
            var ragdoll = console.Vitals.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "no Ragdoll on the real player");

            for (int i = 0; i < 300 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");

            console.Submit("/equip jetpack");
            yield return null;
            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "setup failed -- jetpack never equipped");

            // Jump to get airborne, then build a real, genuine lean -- same
            // technique as JetpackGroundedJumpPlayTests' own landing test:
            // looking slightly UP so ClimbVelocityFor's own -Sin(pitch)
            // keeps the body actually climbing instead of diving straight
            // back into the ground before the lean has any time to build.
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            for (int i = 0; i < 30 && locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsFalse(locomotion.IsGrounded, "setup failed -- the jump never actually left the ground");

            input.OrbitCamera.Pitch = -20.0f;
            Press(_keyboard.spaceKey);
            yield return null;
            Press(_keyboard.wKey);
            for (int i = 0; i < 25; i++)
            {
                yield return null;
            }
            Assert.Greater(jetpack.CurrentTilt.x, 40.0f,
                $"setup failed -- the body never actually leaned ({jetpack.CurrentTilt.x:0.0} degrees), " +
                "nothing real to reset");

            // The same real trigger a wall crash's own knockdown uses
            // (PlayerVitals.TriggerCrashKnockdown -> Ragdoll.Activate) --
            // called directly rather than needing a real wall in the right
            // place at the right moment, the same technique
            // RagdollDuringSwingPlayTests already establishes for forcing a
            // real ragdoll on demand.
            ragdoll.Activate(new Vector3(2.0f, 3.0f, 0.0f));
            yield return null;

            Assert.IsTrue(ragdoll.IsLimp, "setup failed -- Activate did not actually ragdoll the body");
            Assert.AreEqual(0.0f, jetpack.CurrentTilt.x, 0.01f,
                "the jetpack's own lean is still sitting there the instant after ragdolling -- it will stay " +
                "frozen through the whole limp/GettingUp sequence instead of being reset the moment physics " +
                "actually took over");
#else
            yield break;
#endif
        }
    }
}
