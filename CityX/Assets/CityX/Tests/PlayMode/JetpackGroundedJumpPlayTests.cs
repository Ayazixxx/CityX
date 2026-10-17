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
    /// EXPLICIT BUG REPORT: "when you press space on the floor with jetpack
    /// on it just does boost right away but make it so you can still jump
    /// with jetpack on and but when you hold space mid air then it
    /// activates." Through the REAL PlayerInputReader.Update() dispatch,
    /// with a real simulated Space key (InputTestFixture, the same pattern
    /// RagdollDuringSwingPlayTests already establishes) -- the actual fix
    /// lives entirely in that dispatch, which none of JetpackTests'/
    /// JetpackRealScenePlayTests' own direct Jetpack.Tick calls ever
    /// exercise.
    /// </summary>
    public sealed class JetpackGroundedJumpPlayTests : InputTestFixture
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
        public IEnumerator PressingSpaceOnTheGroundStillJumpsInsteadOfThrusting()
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

            // The real player spawns slightly above the ground and falls
            // the first few frames -- wait for it to actually settle before
            // testing grounded-only behaviour.
            for (int i = 0; i < 300 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");

            console.Submit("/equip jetpack");
            yield return null;
            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "setup failed -- jetpack never equipped");
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- no longer grounded after equipping");

            Press(_keyboard.spaceKey);
            yield return null;

            Assert.Greater(locomotion.Velocity.y, 0.0f,
                "pressing space while grounded with the jetpack equipped should still jump");
            Assert.IsFalse(jetpack.FlameEffect.isEmitting,
                "the jetpack's own thrust fired while still standing on the ground");

            Release(_keyboard.spaceKey);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HoldingSpaceOnceAirborneActivatesTheJetpack()
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

            for (int i = 0; i < 300 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");

            console.Submit("/equip jetpack");
            yield return null;
            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "setup failed -- jetpack never equipped");

            // Jump to get airborne first -- the same real path a player
            // would actually take, not a teleport.
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            for (int i = 0; i < 30 && locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsFalse(locomotion.IsGrounded, "setup failed -- the jump never actually left the ground");

            Press(_keyboard.spaceKey);
            for (int i = 0; i < 10 && !jetpack.FlameEffect.isEmitting; i++)
            {
                yield return null;
            }

            Assert.IsTrue(jetpack.FlameEffect.isEmitting,
                "holding space once airborne should activate the jetpack's own thrust");

            Release(_keyboard.spaceKey);
#else
            yield break;
#endif
        }

        /// <summary>
        /// EXPLICIT BUG REPORT: "after i crash and recover sometimes i am
        /// stuck at an angle." Root cause was right here in the real
        /// dispatch, not in Jetpack.Tick itself: EquippedJetpack.Tick used
        /// to be called ONLY while `!_locomotion.IsGrounded`, and
        /// Tick/TickTilt is the ONLY thing that ever eases the body's own
        /// lean back toward level (see TickTilt's own doc comment).
        /// The instant the player became grounded again -- whether by
        /// landing softly or by standing back up from a crash ragdoll --
        /// Tick simply stopped being called at all, freezing whatever lean
        /// the spine had at that exact moment forever. This test reproduces
        /// the whole thing through the real dispatch: build up a real,
        /// nonzero lean while genuinely flying, land, and confirm the lean
        /// actually eases back to upright rather than staying stuck.
        /// </summary>
        [UnityTest]
        public IEnumerator LandingWithAnExistingLeanStraightensItBackUpright()
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

            for (int i = 0; i < 300 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            Vector3 homePosition = locomotion.transform.position;

            console.Submit("/equip jetpack");
            yield return null;
            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "setup failed -- jetpack never equipped");

            // Jump to get airborne first, the same real path a player would
            // actually take.
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            for (int i = 0; i < 30 && locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsFalse(locomotion.IsGrounded, "setup failed -- the jump never actually left the ground");

            // Fly forward while looking slightly UP, not down -- builds a
            // real, genuine pitch lean (cameraPitch + SupermanPitchOffset,
            // comfortably past the 40-degree check below) while ClimbVelocityFor's
            // own -Sin(cameraPitch) keeps the body actually CLIMBING instead
            // of diving straight back into the ground before the lean has
            // any time to build (a downward-looking camera was tried first
            // and immediately defeated the setup -- the real player dove
            // into the ground within a couple of frames, landed, and the
            // fix under test eased the barely-started lean straight back to
            // zero before the assertion ever ran).
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
                "nothing real to straighten back out");

            // EACH RELEASE GETS ITS OWN FRAME -- confirmed necessary: queuing
            // Release(spaceKey) immediately followed by Release(wKey) with no
            // yield between them silently lost the Space release (still read
            // isPressed==true a frame later), which then kept the real body
            // climbing at a flat, un-decaying Jetpack.ThrustSpeed indefinitely
            // (a hover reads moveInput.y<=0 once W actually released, so
            // ClimbVelocityFor's own !flyingForward branch returns the flat
            // hover climb speed rather than the sin-based dive/climb one) --
            // this is a test-harness input-queueing quirk, not a real
            // gameplay bug; confirmed via a direct diagnostic assertion that
            // failed exactly this way before this fix.
            Release(_keyboard.wKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return null;
            Assert.IsFalse(_keyboard.spaceKey.isPressed, "diagnostic -- Space did not actually register as released");
            Assert.IsFalse(jetpack.FlameEffect.isEmitting, "diagnostic -- thrust is still active after releasing Space");

            // TELEPORT BACK ABOVE THE ALREADY-CONFIRMED-SOLID SPAWN GROUND,
            // NOT A NATURAL FALL -- the real city is a real procedurally
            // built hillside with real drop-offs (and, per a separate,
            // still-open bug report, possibly real gaps in its floor); even
            // the brief 25-frame hold above was enough to drift the real
            // body somewhere that took well over 15 real seconds to fall
            // back to ground, twice, at two different hold durations. That
            // is a real-world/terrain concern, not what this test exists to
            // check -- this test is only about whether the LEAN straightens
            // out once grounded, so it removes the traversal gamble by
            // dropping the body from a small, known-safe height directly
            // above the exact spot it started from. Same disable/set/
            // enable/SyncTransforms sequence PlayerLocomotion's own
            // RecoverFromFallingThroughTheWorld already uses for exactly
            // this kind of CharacterController-safe teleport.
            var controller = locomotion.GetComponent<CharacterController>();
            controller.enabled = false;
            locomotion.transform.position = homePosition + Vector3.up * 3.0f;
            controller.enabled = true;
            Physics.SyncTransforms();

            // SECONDS, NOT FRAMES. The body keeps the upward speed it had while flying, and falling back down
            // takes simulated time; a batch run's frames are only a few milliseconds each (cheaper the lighter
            // the city is to stream), so a fixed count of frames covered too little time to come back down.
            for (float waited = 0.0f; waited < 20.0f && !locomotion.IsGrounded; waited += Time.deltaTime)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded,
                $"setup failed -- never actually landed. Started the drop at Y={homePosition.y + 3.0f:0.00} " +
                $"(home Y was {homePosition.y:0.00}), now sitting at Y={locomotion.transform.position.y:0.00}");

            // A few more frames on the ground for TickTilt's own SmoothDamp
            // to actually settle (TiltSmoothTime * ~3, comfortably rounded
            // up) -- this is the part that used to never happen at all.
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.Less(jetpack.CurrentTilt.x, 1.0f,
                $"the body is still leaning {jetpack.CurrentTilt.x:0.0} degrees after landing and settling -- " +
                "it never straightened back out, it is stuck at an angle");
#else
            yield break;
#endif
        }
    }
}
