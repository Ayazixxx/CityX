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
    /// snapped the arm that threw it from straight out to hanging, in one frame. And the blows' clips on the real body (2026-11-09): a punch
    /// lands at full reach over a real stride with the fists closed, its rear foot where it stood, and a body stopped short draws its feet
    /// in instead of sliding them.
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

        /// <summary>
        /// Throws a blow and plays it to <paramref name="share"/> of its length, the driver stepped a sixtieth of a second a frame. The body
        /// is carried into it as the game carries it (MeleeAttacker.CarryIntoPunch), or left where it stands, as one stopped by whoever it
        /// is hitting is.
        /// </summary>
        private IEnumerator Throw(FistKind kind, float share, bool carried)
        {
            var controller = _player.GetComponent<CharacterController>();
            _fists.Begin(kind);
            _fists.enabled = false;
            float done = 0.0f;
            for (float elapsed = 0.0f; elapsed < share * FistStrikes.Seconds(kind); elapsed += Dt)
            {
                _locomotion.Tick(default, Dt);
                if (carried)
                {
                    float wanted = FistStrikes.Carried(kind, elapsed + Dt);
                    controller.Move(_player.transform.forward * (wanted - done));
                    done = wanted;
                }
                _fists.Step(Dt);
                _fists.enabled = false;
                yield return null;
            }
        }

        private static float OnTheGround(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        [UnityTest]
        public IEnumerator APunchLandsAtFullReachOverARealStrideWithTheFistsClosedAndTheRearFootWhereItStood()
        {
#if UNITY_EDITOR
            yield return Spawn();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Assert.Less(_locomotion.RightGrip + _locomotion.LeftGrip, 0.01f, "setup: the hands are not open standing plain");
            Vector3 rearStood = rightFoot.position;

            yield return Throw(FistKind.RightStraight, FistStrikes.ContactFraction(FistKind.RightStraight), carried: true);
            Assert.Greater(_fists.ClipWeight, 0.99f, "setup: the blow's clip does not have the body when it lands");

            Vector3 fist = _player.transform.InverseTransformPoint(_rightHand.position);
            float reach = Vector3.Distance(_rightUpper.position, _rightHand.position);
            float stride = _player.transform.InverseTransformPoint(leftFoot.position).z - _player.transform.InverseTransformPoint(rightFoot.position).z;
            float slid = OnTheGround(rightFoot.position, rearStood);
            Debug.Log($"CityX fist clip: the right straight lands {fist.z:F2} m ahead of the body, the arm {reach:F2} m long, the feet {stride:F2} m apart, the rear foot {slid * 100:F0} cm from where it stood");
            Assert.Greater(fist.z, 1.0f, "the fist lands beside the body, not ahead of it");
            Assert.Greater(reach, 0.85f, "the punch lands with the arm bent");
            Assert.Greater(stride, 0.3f, "the punch lands with the feet together: no stride");
            Assert.Less(slid, 0.12f, "the rear foot slid along the ground as the body was carried past it");
            Assert.Greater(_locomotion.RightGrip, 0.8f, "the punch is thrown with an open hand");
            Assert.Greater(_locomotion.LeftGrip, 0.8f, "the other hand is open: a fighter's hands are both fists");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABodyStoppedShortDrawsItsFeetInInsteadOfSlidingThem()
        {
#if UNITY_EDITOR
            yield return Spawn();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot), rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 rearStood = rightFoot.position;

            // Not carried at all: nose to nose with whoever it hits, the body has nowhere to go.
            yield return Throw(FistKind.RightStraight, FistStrikes.ContactFraction(FistKind.RightStraight), carried: false);

            float slid = OnTheGround(rightFoot.position, rearStood);
            float lead = _player.transform.InverseTransformPoint(leftFoot.position).z;
            Debug.Log($"CityX fist clip, stopped short: the stride is {_locomotion.FistStride:F2} of the clip's, the rear foot {slid * 100:F0} cm from where it stood, the lead foot {lead:F2} m ahead");
            Assert.Less(_locomotion.FistStride, 0.1f, "a body that went nowhere still strides the clip's whole stride");
            Assert.Less(slid, 0.12f, "the rear foot slid back along the ground under a body standing still");
            Assert.Less(lead, 0.2f, "the lead foot strode on without the body");
#else
            yield break;
#endif
        }
    }
}
