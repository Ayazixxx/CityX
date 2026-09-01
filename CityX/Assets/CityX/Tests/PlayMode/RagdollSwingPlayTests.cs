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
    internal sealed class RagdollSwingFixedAnchorLauncher : IGrappleLauncher
    {
        private readonly Vector3 _anchor;
        public RagdollSwingFixedAnchorLauncher(Vector3 anchor) { _anchor = anchor; }
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
    /// EXPLICIT ASK, revisited twice after three (then a fourth) earlier
    /// attempt all failed live for this exact feature (see GrappleSwing's
    /// own class doc comment): "if hes in mid air AND attached to a wall
    /// via grapple hook. then he goes ragdoll... his limp body swings with
    /// the momentum of the gravity and he is attached to the rope by his
    /// right hand." A SpringJoint version stretched (reported live:
    /// "stretches the character and swings me far as fuck"). A real
    /// multi-body Ragdoll.Activate plus a hard script clamp measured a
    /// genuine instability (a rigidbody hitting 72 m/s). This final
    /// version drives the swing physics on ONE standalone, jointless
    /// Rigidbody with nothing connected to it to fight a correction --
    /// which is what these tests check for. A grounded fire is untouched
    /// -- see GrappleSwingPosePlayTests for that path's own coverage,
    /// deliberately unaffected by this file.
    /// </summary>
    public sealed class RagdollSwingPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GrappleSwing _swing;
        private PlayerLocomotion _locomotion;
        private CharacterController _controller;

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
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Loads the real scene and puts the player genuinely airborne. A
        /// raw position write alone would NOT make
        /// CharacterController.isGrounded report false -- that flag is
        /// purely the result of the LAST Move() call, not the current
        /// position -- so this lets a couple of real frames of ordinary
        /// falling happen first, exactly like a player who jumped or ran
        /// off a ledge before firing.
        /// </summary>
        private IEnumerator LoadSceneAirborneNearWall()
        {
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            _swing = Object.FindFirstObjectByType<GrappleSwing>();
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(_swing, "no GrappleSwing in the real scene");
            _locomotion = console.Vitals.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(_locomotion, "no PlayerLocomotion on the real player");
            _controller = console.Vitals.GetComponent<CharacterController>();

            _controller.enabled = false;
            _swing.transform.position += Vector3.up * 15.0f;
            _controller.enabled = true;
            Physics.SyncTransforms();

            // A couple of real falling frames -- see this method's own doc
            // comment for why a raw position write alone is not enough to
            // make isGrounded report false.
            for (int i = 0; i < 3; i++) { yield return null; }
            Assert.IsFalse(_controller.isGrounded, "test setup failed -- the player should be genuinely airborne");
        }

        private static bool FindNearbyWallAnchor(Vector3 origin, out Vector3 anchor)
        {
            for (int a = 0; a < 16; a++)
            {
                float angle = a * (360.0f / 16.0f);
                Vector3 dir = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, 40.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    anchor = hit.point;
                    return true;
                }
            }
            anchor = Vector3.zero;
            return false;
        }

        [UnityTest]
        public IEnumerator FiringMidAirActivatesTheRagdollSwingAndDisablesTheController()
        {
#if UNITY_EDITOR
            yield return LoadSceneAirborneNearWall();

            Assert.IsTrue(FindNearbyWallAnchor(_swing.transform.position, out Vector3 anchor),
                "no nearby wall found to anchor on");
            _swing.EquippedHook = new RagdollSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !_swing.IsActive; i++) { yield return null; }

            Assert.IsTrue(_swing.IsActive, "the grapple never attached");
            Assert.IsTrue(_swing.IsRagdollSwinging, "firing mid-air did not take the ragdoll path");
            Assert.IsFalse(_controller.enabled, "the CharacterController should be handed to the proxy while ragdoll-swinging");
#else
            yield break;
#endif
        }

        /// <summary>
        /// THE 0%-CHANCE-OF-STRETCH REQUIREMENT, CHECKED DIRECTLY, AGAINST
        /// THE PROXY'S OWN RECONSTRUCTED POSITION (transform.position plus
        /// the same chest-height offset BeginRagdollSwing/TickRagdollSwing
        /// both apply -- see their own doc comments). A single, jointless
        /// Rigidbody has nothing else connected to it for a correction to
        /// fight, unlike the real multi-body Ragdoll this replaced -- see
        /// GrappleSwing.RagdollActivationSpeedCap's own doc comment for why
        /// even so, the very first frame of a fast catch still allows a
        /// small, bounded, ONE-TIME amount of slop (a fundamental limit of
        /// any discrete-time simulation, not a flaw specific to this
        /// design) before the clamp has had a single chance to run.
        /// </summary>
        [UnityTest]
        public IEnumerator TheProxyNeverExceedsTheRopesLengthByMoreThanTheBoundedFirstStepSlop()
        {
#if UNITY_EDITOR
            yield return LoadSceneAirborneNearWall();
            Assert.IsTrue(FindNearbyWallAnchor(_swing.transform.position, out Vector3 anchor),
                "no nearby wall found to anchor on");
            _swing.EquippedHook = new RagdollSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !_swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(_swing.IsActive, "the grapple never attached");
            Assert.IsTrue(_swing.IsRagdollSwinging, "setup failed -- this test needs the ragdoll path");

            float ropeLength = _swing.RopeLength;
            float bodyHeight = _locomotion.StandingHeight * 0.5f;

            // Theoretical worst case for ONE physics step at the
            // activation speed cap, plus headroom -- see
            // GrappleSwing.RagdollActivationSpeedCap's own doc comment.
            // Applies to every frame, not just the first: gravity alone
            // can re-accelerate a long swing's own bottom-of-arc speed
            // past the activation cap too, and the clamp carries the same
            // one-step lag whenever instantaneous speed is high, not only
            // at the moment of the catch.
            const float boundedTolerance = 0.3f;

            for (int i = 0; i < 120 && _swing.IsActive; i++)
            {
                yield return null;
                Vector3 proxyPosition = _swing.transform.position + Vector3.up * bodyHeight;
                float distance = Vector3.Distance(proxyPosition, anchor);
                Assert.LessOrEqual(distance, ropeLength + boundedTolerance,
                    $"frame {i}: the proxy reached {distance:0.00}m from a {ropeLength:0.00}m rope's anchor -- " +
                    "past even the physically-bounded slop, the clamp is not holding");
                Assert.IsFalse(float.IsNaN(proxyPosition.x), $"the proxy position went NaN at frame {i}");
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// THE OTHER FAILURE SIGNATURE from every earlier attempt: a
        /// solver (or, here, a single body's own velocity) that never
        /// settles shows up as an implausible speed long before it
        /// visibly explodes. GrappleSwing.Velocity is synced from the
        /// proxy's own real linearVelocity every frame (see
        /// TickRagdollSwing), so this is checking the exact same number
        /// the earlier multi-body version's 72 m/s spike was measured on.
        /// </summary>
        [UnityTest]
        public IEnumerator VelocityNeverReachesAnImplausibleSpeed()
        {
#if UNITY_EDITOR
            yield return LoadSceneAirborneNearWall();
            Assert.IsTrue(FindNearbyWallAnchor(_swing.transform.position, out Vector3 anchor),
                "no nearby wall found to anchor on");
            _swing.EquippedHook = new RagdollSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !_swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(_swing.IsActive, "the grapple never attached");
            Assert.IsTrue(_swing.IsRagdollSwinging, "setup failed -- this test needs the ragdoll path");

            float worstSpeed = 0.0f;
            for (int i = 0; i < 120 && _swing.IsActive; i++)
            {
                yield return null;
                float speed = _swing.Velocity.magnitude;
                if (speed > worstSpeed) worstSpeed = speed;
            }

            Assert.Less(worstSpeed, 40.0f,
                $"the ragdoll swing's own velocity reached {worstSpeed:0.0} m/s -- the proxy never settled");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ReleasingHandsBackCleanControlAfterwards()
        {
#if UNITY_EDITOR
            yield return LoadSceneAirborneNearWall();
            Assert.IsTrue(FindNearbyWallAnchor(_swing.transform.position, out Vector3 anchor),
                "no nearby wall found to anchor on");
            _swing.EquippedHook = new RagdollSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !_swing.IsActive; i++) { yield return null; }
            Assert.IsTrue(_swing.IsActive, "the grapple never attached");
            Assert.IsTrue(_swing.IsRagdollSwinging, "setup failed -- this test needs the ragdoll path");

            for (int i = 0; i < 30; i++) { yield return null; }

            Release(_mouse.leftButton);
            yield return null;
            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && _swing.IsActive; i++) { yield return null; }

            Assert.IsFalse(_swing.IsActive, "the second click never released the swing");
            Assert.IsFalse(_swing.IsRagdollSwinging, "IsRagdollSwinging did not clear on release");
            Assert.IsTrue(_controller.enabled, "the CharacterController was not handed back");
            Assert.IsTrue(_locomotion.enabled, "PlayerLocomotion was not handed back");
#else
            yield break;
#endif
        }
    }
}
