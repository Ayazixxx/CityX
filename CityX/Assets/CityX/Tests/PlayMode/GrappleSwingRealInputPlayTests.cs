using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Fixed anchor, no real model -- only the swing's own ground/velocity handling is under test here.</summary>
    internal sealed class FixedAnchorLauncher : IGrappleLauncher
    {
        private readonly Vector3 _anchor;
        public FixedAnchorLauncher(Vector3 anchor) { _anchor = anchor; }
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
    /// EVERY OTHER TEST OF THE SWING THIS PROJECT HAS -- including several
    /// added earlier in this same debugging arc -- calls GrappleSwing.Tick
    /// directly, and every one of those spawned the player already
    /// airborne. Neither of those matches how a real player actually
    /// fires: through PlayerInputReader's own real Update dispatch, most
    /// often while standing on the ground. This is the one test file that
    /// drives the REAL dispatch path with a REAL simulated mouse/keyboard
    /// (UnityEngine.InputSystem's own InputTestFixture), against the REAL
    /// saved scene -- and it is what actually reproduced a live bug report
    /// ("the character stays in place... the more you move around... the
    /// more contorted") that every Tick()-direct test had missed entirely.
    /// </summary>
    public sealed class GrappleSwingRealInputPlayTests : InputTestFixture
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
            if (_scene.IsValid() && _scene.isLoaded)
            {
                // Re-set rather than trusting it to still be true from the
                // test body -- Unity's own test framework resets
                // LogAssert.ignoreFailingMessages between the test body and
                // teardown phases. Covers unloading this specific large
                // scene, which logs its own "Destroying assets is not
                // permitted" noise from inside component teardown,
                // unrelated to this test's own logic either way.
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            // Reset unconditionally so this does not leak into whatever
            // test runs next in the same session.
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// THREE real bugs, found together because none of them showed up
        /// until the swing was fired from a REAL grounded starting
        /// position (every prior test spawned the player already
        /// airborne):
        ///
        /// 1. The grounded+low-speed auto-release had no grace period at
        ///    all -- fired on literally the first Tick after Attach, since
        ///    a swing starts at whatever velocity the player already had,
        ///    which for someone standing still is near zero.
        /// 2. GrappleSwing.Tick's own wall-stick velocity reconciliation
        ///    (added to fix a DIFFERENT bug -- see MaxSwingSpeed's own doc
        ///    comment) could not tell "genuinely stuck in every direction"
        ///    apart from "perfectly free horizontally, blocked only
        ///    downward by the ground under your own feet" -- it scaled the
        ///    WHOLE velocity vector down every grounded frame, including
        ///    the horizontal component that should have been completely
        ///    free to build into a swing.
        /// 3. Gravity was never clamped while grounded (unlike
        ///    PlayerLocomotion.Tick's own identical, proven fix for the
        ///    identical problem), so _velocity.y accumulated more and more
        ///    negative every frame spent touching the ground -- which the
        ///    rope constraint never got a chance to consume, since a
        ///    rope's own length IS the exact distance measured at Attach,
        ///    so standing still (where gravity cannot actually move you)
        ///    never lets distance cross that threshold.
        ///
        /// Checked here by firing at a real, distant, elevated anchor from
        /// a real grounded start and holding a real simulated W key,
        /// confirming the swing survives well past where it used to
        /// instantly self-destruct and that horizontal speed actually
        /// grows rather than staying pinned near zero.
        /// </summary>
        [UnityTest]
        public IEnumerator FiringFromAGroundedStartWhileHoldingForwardActuallyBuildsSwingSpeed()
        {
#if UNITY_EDITOR
            // The real scene's real camera tries to render every frame
            // automatically as part of Unity's own render loop, which
            // fails under the test suite's default -nographics run (no
            // real GPU) with an unrelated "RenderTexture.Create failed"
            // error -- nothing to do with the gameplay logic actually
            // under test here. Ignored for this whole test rather than
            // requiring every future run to pass -Graphics by hand.
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var swing = Object.FindFirstObjectByType<GrappleSwing>();
            Assert.IsNotNull(swing, "no GrappleSwing in the real scene");
            Assert.IsNotNull(Camera.main, "no Camera.main -- TryFire needs it");
            Transform playerRoot = swing.transform;

            Vector3 anchor = playerRoot.position + new Vector3(15.0f, 25.0f, 10.0f);
            swing.EquippedHook = new FixedAnchorLauncher(anchor);

            // Left-click AGAIN releases the rope in this game, so the
            // button is deliberately left held rather than
            // pressed-and-released -- releasing is not needed to fire in
            // the first place.
            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !swing.IsActive; i++)
            {
                yield return null;
            }
            Assert.IsTrue(swing.IsActive, "the real click through the real Input System never fired the grapple");

            Press(_keyboard.wKey);
            int aliveFrames = 0;
            float peakHorizontalSpeed = 0.0f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (!swing.IsActive)
                {
                    break;
                }
                aliveFrames++;
                float horizontalSpeed = new Vector2(swing.Velocity.x, swing.Velocity.z).magnitude;
                if (horizontalSpeed > peakHorizontalSpeed)
                {
                    peakHorizontalSpeed = horizontalSpeed;
                }
            }
            Release(_keyboard.wKey);

            // Old bug 1: this used to be 0 or 1 (instant release the very
            // first tick). GrappleSwing.AttachGraceSeconds (0.2s, ~12
            // frames at 60fps) is the guaranteed floor now.
            Assert.GreaterOrEqual(aliveFrames, 12,
                $"the swing only survived {aliveFrames} frames from a real grounded start -- " +
                "the grounded+low-speed auto-release grace period regressed");

            // Old bugs 2 and 3 together: with gravity unbounded and the
            // whole velocity vector getting killed every grounded frame,
            // horizontal speed from holding W never meaningfully grew.
            // SwingAccel is 16 m/s^2; even a fraction of a second of
            // unimpeded push comfortably clears 1 m/s.
            Assert.Greater(peakHorizontalSpeed, 1.0f,
                $"horizontal swing speed only ever reached {peakHorizontalSpeed:0.00} m/s while holding forward " +
                "from a grounded start -- the ground was still killing the swing's own momentum");
#else
            yield break;
#endif
        }
    }
}
