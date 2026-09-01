using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple swing's OWN GROUNDED-FIRE pose -- explicit ask: "his
    /// right hand stay attached to it but the rest of his body is
    /// ragdolling". THREE earlier versions of this used real Rigidbody/
    /// CharacterJoint physics and were all reported broken live (the body
    /// stretching apart, limbs flying off) -- see GrappleSwing's own class
    /// doc comment for why a script forcing a character through fast,
    /// arbitrary motion is a bad fit for a real constraint solver. This is
    /// a fourth, deliberately much simpler design: plain procedural bone
    /// rotation, the same technique PlayerLocomotionCastPosePlayTests
    /// already proves stable for PlayerLocomotion.ApplyCastingPose. Nothing
    /// here is physics, so there is no joint/collision failure mode left to
    /// test for -- what actually needs checking is that the Animator hands
    /// off and back cleanly, and that the bones move the way the pose is
    /// supposed to.
    ///
    /// AIRBORNE, THE SAME AS ALWAYS -- but with the Ragdoll component
    /// stripped right after spawning. See GrappleSwing.BeginRagdollSwing's
    /// own doc comment: firing while genuinely mid-air with a Ragdoll
    /// present now goes ragdoll instead (a real, if narrower, second
    /// attempt at the ask this class's own header quotes) -- and grounding
    /// these tests instead is NOT the fix, because Phase 10's own
    /// grounded+low-speed auto-release (GrappleSwing.AttachGraceSeconds)
    /// would end the swing after roughly 12 frames regardless, well before
    /// most of these tests finish ticking. Removing Ragdoll instead keeps
    /// every test here on EXACTLY the procedural path it always exercised,
    /// airborne and sustained, same as before this feature existed. See
    /// RagdollSwingPlayTests for the mid-air-with-Ragdoll path's own
    /// coverage.
    /// </summary>
    public sealed class GrappleSwingPosePlayTests
    {
        private GameObject _player;
        private GrappleSwing _swing;
        private Animator _animator;

        private const float StepSeconds = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
        }

        private void SpawnPlayer()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 20.0f, 0.0f), Quaternion.identity);
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            // See this class's own doc comment -- without a Ragdoll to find,
            // GrappleSwing.Bind's own _ragdoll resolves null and every
            // Attach here keeps going through the ordinary procedural path
            // regardless of this airborne spawn height.
            Ragdoll ragdoll = _player.GetComponent<Ragdoll>();
            if (ragdoll != null) Object.DestroyImmediate(ragdoll);

            _swing = _player.AddComponent<GrappleSwing>();
            _swing.Bind();
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
#endif
        }

        [UnityTest]
        public IEnumerator AttachingDisablesTheAnimatorAndReleasingReEnablesIt()
        {
            SpawnPlayer();
            Assert.IsTrue(_animator.enabled, "setup failed -- the animator should start enabled");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            Assert.IsFalse(_animator.enabled,
                "the animator is still posing bones the swing's own procedural pose needs to own");

            _swing.Release(false);
            Assert.IsTrue(_animator.enabled, "releasing did not hand control back to the animator");
            yield break;
        }

        /// <summary>
        /// The right arm's own job the whole time a rope is attached: ease
        /// toward its fixed reach pose. Checked by ANGLE TO THE TARGET
        /// shrinking over real ticks, not an exact value, since the exact
        /// pose is an explicit first guess (see GrappleSwing's own
        /// RightUpperArmReachPose doc comment) that is expected to be
        /// re-tuned once actually seen.
        /// </summary>
        [UnityTest]
        public IEnumerator SwingingMovesTheRightArmTowardItsOwnReachPose()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.IsNotNull(rightUpperArm, "Player.prefab's Animator has no RightUpperArm bone mapped");

            Quaternion before = rightUpperArm.localRotation;

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 30; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            float moved = Quaternion.Angle(before, rightUpperArm.localRotation);
            Assert.Greater(moved, 5.0f,
                $"the right arm barely moved ({moved:0.0} degrees) after half a second of swinging -- " +
                "it should be visibly reaching for the rope");
        }

        /// <summary>
        /// THE ACTUAL ROOT CAUSE of a live bug report ("distorted to
        /// lengths that shouldn't be possible", "the character doesn't
        /// follow the camera") that survived several other real, separately
        /// confirmed fixes -- nothing to do with the swing's own pose or
        /// velocity at all. PlayerLocomotion.ClampFeetAboveGround exists
        /// purely to fix a badly calibrated GROUNDED clip (see its own doc
        /// comment) and has no job while dangling from a rope -- left
        /// running during a real, varied swing, its own "reset to zero,
        /// recompute from the live foot position" pattern turned into a
        /// genuine runaway: confirmed directly, live, the visual rig's own
        /// offset climbed from single digits to over 1000 METRES within
        /// about 20 real frames while the actual character root stayed
        /// completely normal. The camera (correctly tracking the real
        /// root) and the visible mesh (flung a kilometre straight up) came
        /// apart -- exactly what "the character doesn't follow the camera"
        /// describes, backwards from how it reads: the camera was right
        /// the whole time. Fixed by suspending the clamp entirely for as
        /// long as GrappleSwing owns the pose (SetGroundClampSuspended),
        /// plus a hard ceiling on the correction itself as a second line of
        /// defence. Checked here with the SAME realistic, continuously
        /// varying input (sweeping yaw, alternating reel/payout) that
        /// reproduced the runaway live -- a simpler, single-direction
        /// swing never triggered it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheVisualRigNeverDriftsFromTheRootDuringARealVariedSwing()
        {
            SpawnPlayer();
            Transform animatorTransform = _animator.transform;

            _swing.Attach(_player.transform.position + new Vector3(6.0f, 8.0f, 4.0f));

            float worstOffset = 0.0f;
            for (int i = 0; i < 300; i++)
            {
                float t = i / 60.0f;
                Vector2 move = new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.5f) * 0.5f + 0.5f);
                float yaw = i * 2.0f;
                bool reel = (i / 30) % 4 == 0;
                bool payout = (i / 30) % 4 == 2;
                _swing.Tick(move, yaw, false, false, reel, payout, StepSeconds);
                yield return null;

                float offset = animatorTransform.localPosition.magnitude;
                if (offset > worstOffset)
                {
                    worstOffset = offset;
                }

                if (!_swing.IsActive)
                {
                    break;
                }
            }

            Assert.Less(worstOffset, 0.5f,
                $"the visual rig drifted {worstOffset:0.0}m from the root during a real varied swing -- " +
                "ClampFeetAboveGround's own runaway is back");
        }

        /// <summary>
        /// THE SLEEVE MESH CANNOT FOLLOW THE REACH POSE -- see HoodedCloak's
        /// own UpdateSwingHiding doc comment: a cloth tube baked and
        /// weighted for ordinary locomotion's narrow arm swing crumples into
        /// a flat, wing-shaped mess when the arm reaches this far, confirmed
        /// directly by rendering one (GrapplingHookBuilder's own
        /// CaptureSwingWithCloak) -- a live bug report ("all contorted")
        /// that survived the reach-pose fix above because the bug was never
        /// in the body at all. Checked here by watching the cloak's own worn
        /// skin renderer, not by trying to measure "does the mesh look
        /// crumpled" from a test.
        /// </summary>
        [UnityTest]
        public IEnumerator AHoodedCloakHidesItselfWhileSwingingAndReappearsOnRelease()
        {
            SpawnPlayer();
#if UNITY_EDITOR
            GameObject cloakPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Wearables/HoodedCloak.prefab");
            Assert.IsNotNull(cloakPrefab, "HoodedCloak.prefab is missing");
            var cloakInstance = Object.Instantiate(cloakPrefab);
            var cloak = cloakInstance.GetComponent<HoodedCloak>();
            cloak.OnEquip(_player, ItemInstanceData.None);

            SkinnedMeshRenderer cloakSkin = FindSkinByMesh(_player, cloak.GarmentMesh);
            Assert.IsNotNull(cloakSkin, "couldn't find the cloak's own worn skin renderer");
            Assert.IsTrue(cloakSkin.gameObject.activeSelf, "setup failed -- the cloak should start showing");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            for (int i = 0; i < 10; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                cloak.UpdateSwingHiding();
                yield return null;
            }
            Assert.IsFalse(cloakSkin.gameObject.activeSelf,
                "the cloak's own sleeve is still showing mid-swing -- it will crumple against the reach pose");

            _swing.Release(false);
            cloak.UpdateSwingHiding();
            yield return null;

            Assert.IsTrue(cloakSkin.gameObject.activeSelf, "the cloak never came back after the swing ended");

            Object.Destroy(cloakInstance);
#else
            yield break;
#endif
        }

        private static SkinnedMeshRenderer FindSkinByMesh(GameObject root, Mesh mesh)
        {
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == mesh)
                {
                    return renderer;
                }
            }
            return null;
        }

        /// <summary>
        /// AN ABSOLUTE FIXED TARGET for the reach pose (this feature's own
        /// original design) rendered live as a grotesquely stretched limb --
        /// confirmed directly with GrapplingHookBuilder.CaptureSwing against
        /// a bug report that the body "stays still and starts stretching".
        /// This rig's skin is bound relative to whatever pose a bone is
        /// already in, so overwriting it outright can land on an
        /// orientation the skin weights were never bound for. Checked here
        /// by settling the SAME reach from two DELIBERATELY DIFFERENT
        /// starting poses -- composed relative to its own captured base,
        /// both settle the identical distance from their own very different
        /// starting points (a quaternion identity: Angle(q, q*delta) is
        /// delta's own angle no matter what q is); a fixed absolute target
        /// would settle two very different distances instead, since the two
        /// starting poses were picked to be nowhere near each other.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRightArmReachSettlesRelativeToItsOwnCapturedPoseNotAFixedAbsoluteOrientation()
        {
            SpawnPlayer();
            Transform rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Vector3 anchor = _player.transform.position + new Vector3(3.0f, 6.0f, 2.0f);

            // 180 frames (a full 3 simulated seconds), not the 90 an earlier
            // version of this test used -- confirmed directly that 90 was
            // too tight a margin: RightUpperArmReachDegrees' own 150 degree
            // rotation only fully settles around frame 86 in practice
            // (RotateTowards converges more slowly than ArmPoseDegreesPerSecond's
            // own nominal rate alone would suggest once other per-frame
            // rope/velocity work is in the same loop), which occasionally
            // measured a still-mid-transition angle instead of the fully
            // settled one and made this test flaky rather than wrong.
            const int settleFrames = 180;

            rightUpperArm.localRotation = Quaternion.Euler(20.0f, -40.0f, 10.0f);
            Quaternion baseA = rightUpperArm.localRotation;
            _swing.Attach(anchor);
            for (int i = 0; i < settleFrames; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            float deltaA = Quaternion.Angle(baseA, rightUpperArm.localRotation);
            _swing.Release(false);

            rightUpperArm.localRotation = Quaternion.Euler(-35.0f, 60.0f, -25.0f);
            Quaternion baseB = rightUpperArm.localRotation;
            _swing.Attach(anchor);
            for (int i = 0; i < settleFrames; i++)
            {
                _swing.Tick(Vector2.zero, 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }
            float deltaB = Quaternion.Angle(baseB, rightUpperArm.localRotation);

            Assert.AreEqual(deltaA, deltaB, 2.0f,
                $"the settled reach pose moved {deltaA:0.0} degrees from one starting pose but {deltaB:0.0} " +
                "degrees from another very different one -- the reach target is not relative to its own captured base any more");
        }

        /// <summary>
        /// The grapple gun owns the right hand -- explicit ask: casting a
        /// fireball mid-swing should come from the LEFT hand instead, held
        /// in place rather than left flailing with the rest of the body,
        /// or a deliberate cast would read as an accidental twitch.
        /// </summary>
        [UnityTest]
        public IEnumerator CastingMidSwingHoldsTheLeftArmSteadyRatherThanLettingItFlail()
        {
            SpawnPlayer();
            var fireball = _player.AddComponent<CityX.Weapons.FireballCaster>();
            fireball.Bind();
            Transform leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Assert.IsNotNull(leftUpperArm, "Player.prefab's Animator has no LeftUpperArm bone mapped");

            _swing.Attach(_player.transform.position + new Vector3(3.0f, 6.0f, 2.0f));
            // Give the swing real sideways speed so the free-flailing left
            // arm would visibly keep moving if the cast pose were not
            // overriding it -- a swing with near-zero velocity would pass
            // this test even with the override broken, since there would
            // be nothing to flail from in the first place.
            for (int i = 0; i < 20; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            fireball.TryCast(_player.transform.position, Vector3.forward, new Collider[0]);

            // FireballCaster.Tick is never called here, so IsCasting never
            // expires on its own -- deliberately: this waits long enough
            // for the arm's own RotateTowards easing to fully SETTLE into
            // the cast pose first (a real, legitimate transition, not a
            // flail) before measuring drift, rather than mistaking that
            // settling motion for the bug this test is actually checking.
            for (int i = 0; i < 90; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            Quaternion settled = leftUpperArm.localRotation;
            for (int i = 0; i < 10; i++)
            {
                _swing.Tick(new Vector2(1.0f, 0.0f), 0.0f, false, false, false, false, StepSeconds);
                yield return null;
            }

            float driftWhileCasting = Quaternion.Angle(settled, leftUpperArm.localRotation);
            // A generous tolerance, not a tight one: some other system
            // touching this same bone (PlayerLocomotion.LateUpdate's own
            // ApplyCastingPose, in particular) can add a few degrees of its
            // own jitter here independent of GrappleSwing. What actually
            // matters is that this stays far below genuine flail motion
            // (the head/leg sway elsewhere in UpdateSwingPose swings a full
            // 20-40 degrees), not that it is perfectly motionless.
            Assert.Less(driftWhileCasting, 10.0f,
                $"the left arm moved {driftWhileCasting:0.0} degrees after settling into its cast pose -- " +
                "it should hold roughly still, not flail");
        }
    }
}
