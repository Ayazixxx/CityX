using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The cast pose (PlayerLocomotion.Casting.cs; owner, 2026-11-15: "when you are holding a 1 handed weapon and use a projectile spell like
    /// fireball or ice shards, it comes out of your hand that isnt holding a weapon so typically your left ... he just lifs his left arm up and
    /// faces the palm of his hand toward the target. make the right hand unarmed projectile magic cast also face the palm of the hand toward the
    /// target"): on the real Player.prefab, read off the bones after real frames (LateUpdate poses them).
    /// </summary>
    public sealed class PlayerLocomotionCastPosePlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _sword;
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
            if (_sword != null) Object.Destroy(_sword);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Spawn(bool swordInHand)
        {
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            if (swordInHand)
            {
                _sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"),
                    new Vector3(30.0f, 1.0f, 30.0f), Quaternion.identity);
                _sword.GetComponent<SwordWeapon>().OnEquip(_player, ItemInstanceData.None);
            }
            yield return Frames(30, 0.0f, Vector3.zero);   // into a settled idle first
        }

        private IEnumerator Frames(int count, float cast, Vector3 aim)
        {
            for (int step = 0; step < count; step++)
            {
                _locomotion.SetCastPose(cast, aim);
                _locomotion.Tick(new LocomotionInput(), StepSeconds);
                yield return null;
            }
        }

        private static Vector3 Palm(Transform hand) => -hand.up;   // the hand bone's -Y is the palm (HandGrip)

        [UnityTest]
        public IEnumerator UnarmedTheRightHandComesUpWithItsPalmToTheTargetAndTheSpellLeavesFromIt()
        {
            yield return Spawn(false);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 rightBefore = right.position, leftBefore = left.position;
            Vector3 aim = (_player.transform.forward + Vector3.up * 0.15f).normalized;
            Assert.IsFalse(_locomotion.CastsWithLeftHand, "an empty right hand casts from the left");

            yield return Frames(12, 1.0f, aim);

            Assert.Greater(right.position.y - rightBefore.y, 0.4f, "the right hand did not come up");
            Assert.Greater(Vector3.Dot(Palm(right), aim), 0.85f, "the right palm does not face where the spell goes");
            Assert.Greater(Vector3.Dot(right.right, Vector3.up), 0.7f, "the right hand's fingers do not point up");
            Assert.Less((left.position - leftBefore).magnitude, 0.1f, "the left hand moved for a cast from the right");
            Vector3 palm = right.position + right.right * PlayerLocomotion.PalmOffsetMetres * (_locomotion.WorldHeight / BodyRig.StandingHeight);
            Assert.Less(Vector3.Distance(_locomotion.CastPalmPoint(aim), palm), 0.2f, "the spell does not leave from the palm");
        }

        [UnityTest]
        public IEnumerator WithASwordInTheRightHandTheLeftHandCastsItsPalmToTheTarget()
        {
            yield return Spawn(true);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.IsTrue(_locomotion.CastsWithLeftHand, "a sword in the right hand still casts from the right");
            Vector3 leftBefore = left.position;
            Vector3 aim = _player.transform.forward;

            yield return Frames(12, 1.0f, aim);

            Assert.Greater(left.position.y - leftBefore.y, 0.4f, "the left hand did not come up");
            Assert.Greater(Vector3.Dot(Palm(left), aim), 0.85f, "the left palm does not face where the spell goes");
            Assert.Greater(Vector3.Dot(-left.right, Vector3.up), 0.7f, "the left hand's fingers do not point up");   // the left hand's +X runs back up the arm
            Assert.Less(Vector3.Dot(left.position - _player.transform.position, _player.transform.right), 0.0f, "the left hand crossed to the right");
            Vector3 palm = left.position - left.right * PlayerLocomotion.PalmOffsetMetres * (_locomotion.WorldHeight / BodyRig.StandingHeight);
            Assert.Less(Vector3.Distance(_locomotion.CastPalmPoint(aim), palm), 0.2f, "the spell does not leave from the left palm");
        }

        [UnityTest]
        public IEnumerator AimingHigherRaisesTheHandHigher()
        {
            yield return Spawn(false);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            yield return Frames(12, 1.0f, _player.transform.forward);
            float level = right.position.y;
            yield return Frames(12, 1.0f, (_player.transform.forward + Vector3.up).normalized);
            Assert.Greater(right.position.y - level, 0.2f, "aiming up did not raise the hand");
        }

        [UnityTest]
        public IEnumerator ZeroFractionLeavesTheArmWhereLocomotionAlonePutIt()
        {
            yield return Spawn(false);
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 before = hand.position - hips.position;
            yield return Frames(1, 0.0f, Vector3.zero);
            Assert.Less(Vector3.Distance(before, hand.position - hips.position), 0.01f, "the hand moved even though nothing was cast");
        }
    }
}
