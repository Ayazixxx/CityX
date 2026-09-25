using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    internal sealed class RagdollDuringSwingFixedAnchorLauncher : IGrappleLauncher
    {
        private readonly Vector3 _anchor;
        public RagdollDuringSwingFixedAnchorLauncher(Vector3 anchor) { _anchor = anchor; }
        public bool FindAnchor(Vector3 origin, Vector3 direction, float maxRange, out Vector3 anchor)
        {
            anchor = _anchor;
            return true;
        }
        public void ShowRope(Vector3 anchor) { }
        public void UpdateRope(Vector3 anchor) { }
        public void HideRope() { }
    }

    /// <summary>
    /// ORIGINAL BUG REPORT: "the ragdolled body with arm attached to
    /// grapple gun fully [stretched]". A melee finisher landing mid-swing
    /// (PlayerVitals.TriggerFinisherKnockdown) or dying mid-swing both call
    /// Ragdoll.Activate() while GrappleSwing.IsActive can still be true.
    /// Early fixes (see grapple_swing_deprioritized memory) kept
    /// progressively refining the hand-off's ORDERING so the two systems
    /// stopped fighting over the same skeleton -- but the hand-off itself
    /// still read as a glitch. EXPLICIT, REPEATED ASK settled it instead:
    /// "remove the ragdoll function completely and revert back to him just
    /// being still" -- Ragdoll.Activate now never ragdolls out of an active
    /// swing at all, it just releases the rope and leaves the character
    /// standing under ordinary Animator/PlayerLocomotion control. This test
    /// now checks THAT: the swing releases, and the body never goes limp.
    /// </summary>
    public sealed class RagdollDuringSwingPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // See LocomotionAnimatorStabilityPlayTests's own TearDown
            // comment for why this is set and why it must always be
            // cleared, pass or fail. EXPLICIT BUG, found live while
            // diagnosing an apparent PoseDistortionWatchdog violation in
            // this exact test: this file predates that fix and never got
            // it, so the swing's own real-time-based reach-pose
            // interpolation (RotateTowards using the real, uncapped
            // Time.deltaTime PlayerInputReader's live dispatch passes into
            // Tick) was exposed to batch mode's own irregular real frame
            // timing -- a single unusually-large Time.deltaTime frame lets
            // the swing legitimately rotate further in that one frame,
            // which a rate calculation elsewhere (dividing by a
            // DIFFERENT, smaller Time.deltaTime sample) can misread as an
            // implausible snap. Confirmed directly: a temporary diagnostic
            // showed the "worst" bone's own before/after values were an
            // entirely ordinary partial-to-further-along point along the
            // swing's own known Z-axis reach interpolation (see
            // RightUpperArmReachDegrees's own doc comment), not a glitch.
            Time.captureDeltaTime = 0.0f;

            if (_scene.IsValid() && _scene.isLoaded)
            {
                // Same "Destroying assets is not permitted" / RenderTexture
                // noise every other real-scene PlayMode test in this file
                // group already tolerates -- see GrappleSwingRealInputPlayTests'
                // own doc comment.
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RagdollingWhileSwingingReleasesTheSwingButNeverGoesLimp()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            // Realistic gameplay pace, not batch mode's own uncapped real
            // frame timing -- see this class's own TearDown comment for
            // the full story.
            Time.captureDeltaTime = 1.0f / 60.0f;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var swing = Object.FindFirstObjectByType<GrappleSwing>();
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(swing, "no GrappleSwing in the real scene");
            var ragdoll = console.Vitals.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "no Ragdoll on the real player");

            // Well above and clear of the junction's own surrounding
            // buildings -- see this test's own doc comment update: the real
            // spawn point's height changed (CityMassingSceneBuilder now
            // spawns on the actual tilted street surface, ContinuousHeightAt,
            // instead of the old flat quantised pad), which shifted this
            // fixed offset's real-world target enough to clip a nearby
            // rooftop and auto-release the swing before the 20-frame check
            // below ever got to run.
            Vector3 anchor = swing.transform.position + new Vector3(15.0f, 60.0f, 10.0f);
            swing.EquippedHook = new RagdollDuringSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !swing.IsActive; i++) { yield return null; }

            Assert.IsTrue(swing.IsActive, "the grapple never attached");

            // Short pre-trigger window, and bailing out of the loop the
            // instant IsActive flips -- a real swing can auto-release on
            // its own (see GrappleSwing.AttachGraceSeconds), and this test
            // needs to catch it still genuinely attached, not after it has
            // already let go on its own.
            Press(_keyboard.wKey);
            for (int i = 0; i < 20 && swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(swing.IsActive, "the swing auto-released before the ragdoll trigger could even fire");

            // The same trigger PlayerVitals.TriggerFinisherKnockdown uses --
            // a finisher (or death) landing while the rope is still out.
            ragdoll.Activate(new Vector3(3.0f, 4.0f, 0.0f));
            Release(_keyboard.wKey);

            Assert.IsFalse(swing.IsActive,
                "GrappleSwing did not release when Ragdoll activated mid-swing");
            Assert.IsFalse(ragdoll.IsLimp,
                "a knockdown/death mid-swing should release the rope and leave the character " +
                "standing, not ragdoll -- see this class's own doc comment");

            // Hold for a while -- confirms it STAYS not-limp rather than
            // ragdolling on some later frame (e.g. a delayed Animator
            // re-enable accidentally re-triggering the old hand-off).
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Assert.IsFalse(ragdoll.IsLimp, "the character should still be standing 60 frames later");
#else
            yield break;
#endif
        }
    }
}
