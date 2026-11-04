using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The fists on the real body. The stance between blows once put the hands on the hips with the elbows out (owner's screenshot, 2026-11-03: "what the
    /// fuck is this attack position"), and every test on paper passed: only the real arms can show it. And a punch taking over from one still recovering
    /// snapped the arm that threw it from straight out to hanging, in one frame.
    /// </summary>
    public sealed class FistStancePlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private FistPoseDriver _fists;
        private Transform _rightUpper, _rightHand, _leftUpper, _leftHand;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Spawn()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _fists = _player.GetComponent<FistPoseDriver>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            _rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            yield return Frames(60);
#else
            yield break;
#endif
        }

        private IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _locomotion.Tick(default, Dt);
                yield return null;
            }
        }

        /// <summary>Where a hand is from its shoulder, in the body's own axes: what the arm is doing, whatever the hips are.</summary>
        private Vector3 HandFromShoulder(Transform upper, Transform hand)
        {
            return _player.transform.InverseTransformVector(hand.position - upper.position);
        }

        [UnityTest]
        public IEnumerator TheStanceAfterAPunchLeavesTheArmsWhereTheyHangStandingPlain()
        {
#if UNITY_EDITOR
            yield return Spawn();
            Assert.IsFalse(_locomotion.FistPoseActive, "setup: the body is not standing plain");
            Vector3 plainRight = HandFromShoulder(_rightUpper, _rightHand);
            Vector3 plainLeft = HandFromShoulder(_leftUpper, _leftHand);

            _fists.Begin(FistKind.RightStraight);
            yield return Frames(Mathf.CeilToInt(FistStrikes.Seconds(FistKind.RightStraight) / Dt) + 20);
            Assert.IsFalse(_fists.IsStriking, "setup: the punch is still going");
            Assert.IsTrue(_locomotion.FistPoseActive, "setup: the stance is not being held: nothing was tested");

            float right = Vector3.Distance(plainRight, HandFromShoulder(_rightUpper, _rightHand));
            float left = Vector3.Distance(plainLeft, HandFromShoulder(_leftUpper, _leftHand));
            Debug.Log($"CityX fist stance: the hands are {right * 100:F1} cm (right) and {left * 100:F1} cm (left) from where they hang standing plain");
            Assert.Less(right, 0.06f, "the stance moved the right hand from where the arm hangs");
            Assert.Less(left, 0.06f, "the stance moved the left hand from where the arm hangs");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePunchTakingOverLetsTheLastArmDownWithoutASnap()
        {
#if UNITY_EDITOR
            yield return Spawn();
            _fists.Begin(FistKind.RightStraight);
            for (int i = 0; i < 120 && _fists.Progress < 1.0f - MeleeAttacker.ChainCancelFraction; i++)
            {
                yield return Frames(1);
            }
            Assert.IsTrue(_fists.IsStriking, "setup: the punch ended before the next could cancel it");

            Vector3 last = HandFromShoulder(_rightUpper, _rightHand);
            _fists.Begin(FistKind.LeftStraight);
            float most = 0.0f;
            for (int i = 0; i < 15; i++)
            {
                yield return Frames(1);
                Vector3 now = HandFromShoulder(_rightUpper, _rightHand);
                most = Mathf.Max(most, Vector3.Distance(last, now));
                last = now;
            }
            Debug.Log($"CityX fist chain: the arm that threw the last punch moved at most {most * 100:F0} cm in a frame as the next took over");
            Assert.Less(most, 0.35f, "the arm that threw the last punch snapped down as the next took over");
#else
            yield break;
#endif
        }
    }
}
