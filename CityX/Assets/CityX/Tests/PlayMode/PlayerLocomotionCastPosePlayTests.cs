using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The fireball cast's own raised-right-hand pose (PlayerLocomotion.
    /// SetCastPose/ApplyCastingPose). Needs the real Player.prefab and a
    /// real yielded frame, the same reason PlayerLocomotionHipsPlayTests
    /// does: LateUpdate is where the offset is actually applied, and there
    /// is no RightUpperArm/RightHand bone on a bare GameObject with no
    /// Animator/rig.
    ///
    /// CHECKS BONE POSITION, NOT A RENDER -- confirmed directly while
    /// tuning CastRaiseDegrees that a static Edit Mode Camera.Render() does
    /// not reflect a skinned mesh's pose at all (Unity only refreshes skin
    /// buffers on a real player-loop frame tick), so this asserts on the
    /// same hand-position math that tuning actually used, which Unity keeps
    /// synchronously correct regardless of skinning.
    /// </summary>
    public sealed class PlayerLocomotionCastPosePlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        private const float StepSeconds = 1.0f / 60.0f;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator CastingRaisesTheRightHandUpAndOutRatherThanAcrossTheBody()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");

            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Assert.IsNotNull(hand, "Player.prefab's Animator has no RightHand bone mapped");
            Assert.IsNotNull(hips, "Player.prefab's Animator has no Hips bone mapped");

            var idle = new LocomotionInput();

            // Settle into a real idle pose first, matching
            // PlayerLocomotionHipsPlayTests' own reasoning for not
            // asserting against the very first, still-blending-in frame.
            for (int step = 0; step < 10; step++)
            {
                _locomotion.Tick(idle, StepSeconds);
                yield return null;
            }

            Vector3 handBeforeRelative = hand.position - hips.position;

            _locomotion.SetCastPose(1.0f);
            _locomotion.Tick(idle, StepSeconds);
            yield return null;

            Vector3 delta = (hand.position - hips.position) - handBeforeRelative;
            float up = Vector3.Dot(delta, hips.up);
            float right = Vector3.Dot(delta, hips.right);

            Assert.Greater(up, 0.3f, $"a full cast should visibly lift the hand -- moved {up:0.000}m up");
            Assert.Greater(right, 0.0f,
                $"the raised right hand should swing out to its OWN side, not cross the body -- moved {right:0.000}m right");

            Object.Destroy(_player);
            _player = null;
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ZeroFractionLeavesTheArmWhereLocomotionAlonePutIt()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();

            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            var idle = new LocomotionInput();

            for (int step = 0; step < 10; step++)
            {
                _locomotion.Tick(idle, StepSeconds);
                yield return null;
            }

            Vector3 before = hand.position - hips.position;

            // Never cast at all -- SetCastPose defaults to 0 and is never
            // called here, matching a normal frame where PlayerInputReader
            // never presses 1.
            _locomotion.Tick(idle, StepSeconds);
            yield return null;

            Vector3 after = hand.position - hips.position;
            Assert.Less(Vector3.Distance(before, after), 0.01f,
                "the hand moved even though nothing ever cast a fireball");

            Object.Destroy(_player);
            _player = null;
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
