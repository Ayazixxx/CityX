using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The grapple swing's own held pose -- explicit ask: "his right hand
    /// stay attached to it but the rest of his body is ragdolling". THREE
    /// earlier versions of this used real Rigidbody/CharacterJoint physics
    /// and were all reported broken live (the body stretching apart, limbs
    /// flying off) -- see GrappleSwing's own class doc comment for why a
    /// script forcing a character through fast, arbitrary motion is a bad
    /// fit for a real constraint solver. This is a fourth, deliberately
    /// much simpler design: plain procedural bone rotation, the same
    /// technique PlayerLocomotionCastPosePlayTests already proves stable
    /// for PlayerLocomotion.ApplyCastingPose. Nothing here is physics, so
    /// there is no joint/collision failure mode left to test for -- what
    /// actually needs checking is that the Animator hands off and back
    /// cleanly, and that the bones move the way the pose is supposed to.
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
