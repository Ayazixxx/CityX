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
    /// EXPLICIT ASK, revisited after three earlier full-ragdoll attempts
    /// all failed live for this exact feature (see GrappleSwing's own
    /// class doc comment): "if hes in mid air AND attached to a wall via
    /// grapple hook. then he goes ragdoll... his limp body swings with the
    /// momentum of the gravity and he is attached to the rope by his right
    /// hand." A grounded fire is untouched -- see GrappleSwingPosePlayTests
    /// for that path's own coverage, deliberately unaffected by this file.
    /// </summary>
    public sealed class RagdollSwingPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GrappleSwing _swing;
        private Ragdoll _ragdoll;
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
            _ragdoll = console.Vitals.GetComponent<Ragdoll>();
            Assert.IsNotNull(_ragdoll, "no Ragdoll on the real player");
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
        public IEnumerator FiringMidAirActivatesTheRagdollAttachedByTheRightHand()
        {
#if UNITY_EDITOR
            yield return LoadSceneAirborneNearWall();

            Assert.IsFalse(_ragdoll.IsLimp, "setup failed -- the ragdoll should not already be limp");
            Assert.IsTrue(FindNearbyWallAnchor(_swing.transform.position, out Vector3 anchor),
                "no nearby wall found to anchor on");
            _swing.EquippedHook = new RagdollSwingFixedAnchorLauncher(anchor);

            Press(_mouse.leftButton);
            for (int i = 0; i < 5 && !_swing.IsActive; i++) { yield return null; }

            Assert.IsTrue(_swing.IsActive, "the grapple never attached");
            Assert.IsTrue(_swing.IsRagdollSwinging, "firing mid-air did not take the ragdoll path");
            Assert.IsTrue(_ragdoll.IsLimp, "GrappleSwing.BeginRagdollSwing did not actually activate the ragdoll");
#else
            yield break;
#endif
        }

        /// <summary>
        /// THE ACTUAL RISK THIS WHOLE FEATURE CARRIES -- confirmed
        /// unstable three times before for a full-body jointed rig driven
        /// by a script. Ticked for a real two seconds and checked for the
        /// two concrete failure signatures those earlier attempts hit:
        /// the torso ending up absurdly far from the anchor (a joint that
        /// stretched apart) and any rigidbody's own velocity reaching
        /// implausible magnitudes (a solver that never settled). A real,
        /// bounded pendulum swing should keep the torso within roughly a
        /// rope-length-and-a-bit of the anchor the whole time and never
        /// need anything close to freefall-terminal-velocity speeds over
        /// such a short drop.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRagdolledBodySwingsWithoutStretchingOrExploding()
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
            float worstDistanceFromAnchor = 0.0f;
            float worstSpeed = 0.0f;

            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (_ragdoll.Torso == null) { continue; }

                float distance = Vector3.Distance(_ragdoll.Torso.position, anchor);
                if (distance > worstDistanceFromAnchor) worstDistanceFromAnchor = distance;

                float speed = _ragdoll.Torso.linearVelocity.magnitude;
                if (speed > worstSpeed) worstSpeed = speed;

                Assert.IsFalse(float.IsNaN(_ragdoll.Torso.position.x), $"the torso position went NaN at frame {i}");
            }

            Assert.Less(worstDistanceFromAnchor, ropeLength * 2.0f,
                $"the torso reached {worstDistanceFromAnchor:0.0}m from a {ropeLength:0.0}m rope's anchor -- " +
                "the hand joint stretched apart instead of holding");
            Assert.Less(worstSpeed, 40.0f,
                $"a ragdoll body part reached {worstSpeed:0.0} m/s -- the solver never settled");
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
            Assert.IsFalse(_ragdoll.IsLimp, "the ragdoll never stood back up on release");
            Assert.IsTrue(_controller.enabled, "the CharacterController was not handed back");
#else
            yield break;
#endif
        }
    }
}
