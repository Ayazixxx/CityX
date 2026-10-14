using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// On the real rig: the fingers close round a sword in the hand that holds it, the free hand stays open, both hands close on the two-handed
    /// sword, the hands open again when it is put away -- and another player's copy (a cosmetic mount, no equip) closes its hand the same way.
    /// </summary>
    public sealed class FingerGripPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _sword;

        [TearDown]
        public void TearDown()
        {
            if (_sword != null) Object.Destroy(_sword);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void Build()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(60, 1, 60);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
#endif
        }

        private static float Fold(Transform hand, string digit)
        {
            Transform bone = hand.Find(digit);
            Assert.IsNotNull(bone, $"the hand has no {digit}: rebuild the body (CityX/Rebuild Everything Character)");
            return Quaternion.Angle(bone.localRotation, Quaternion.identity);
        }

        private IEnumerator Settle(float seconds)
        {
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                locomotion.Tick(default, Time.deltaTime);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheSwordHandClosesRoundTheCombatSwordAndTheFreeHandStaysOpen()
        {
#if UNITY_EDITOR
            Build();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            yield return Settle(0.5f);
            Assert.Less(Fold(right, "index1_r"), 1.0f, "the empty hand should be open");

            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            _sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity);
            _sword.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            yield return Settle(0.6f);

            Assert.Greater(Fold(right, "index1_r"), HandGrip.FingerProximalDegrees - 2.0f, "the sword hand's index finger did not close");
            Assert.Greater(Fold(right, "thumb1_r"), HandGrip.ThumbProximalDegrees - 2.0f, "the sword hand's thumb did not fold across");
            Assert.Greater(Fold(right.Find("little1_r"), "little2_r"), HandGrip.FingerIntermediateDegrees - 2.0f, "the little fingertip did not fold");
            Assert.Less(Fold(left, "index1_l"), 1.0f, "the free hand should stay open with a one-handed sword");
            Assert.AreEqual(1.0f, _player.GetComponent<PlayerLocomotion>().RightGrip, 0.01f);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator BothHandsCloseRoundTheGreatswordAndOpenWhenItIsPutAway()
        {
#if UNITY_EDITOR
            Build();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            yield return Settle(0.5f);

            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Sword.prefab");
            _sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity);
            SwordWeapon sword = _sword.GetComponent<SwordWeapon>();
            sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            yield return Settle(0.8f);

            Assert.Greater(Fold(right, "middle1_r"), HandGrip.FingerProximalDegrees - 2.0f, "the right hand did not close on the greatsword");
            Assert.Greater(Fold(left, "middle1_l"), HandGrip.FingerProximalDegrees - 2.0f, "the support hand did not close on the greatsword");

            sword.OnUnequip(_player);
            _sword = null;
            yield return Settle(0.6f);
            Assert.Less(Fold(right, "middle1_r"), 1.0f, "the right hand did not open after the sword was put away");
            Assert.Less(Fold(left, "middle1_l"), 1.0f, "the left hand did not open after the sword was put away");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyClosesItsHandRoundTheMountedSword()
        {
#if UNITY_EDITOR
            Build();
            Animator animator = _player.GetComponentInChildren<Animator>();
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            yield return Settle(0.3f);

            // A copy never equips: NetworkedEquipmentSync mounts a cosmetic model of what the owner holds.
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            _sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity);
            _sword.GetComponent<SwordWeapon>().MountCosmetic(_player);
            yield return Settle(0.6f);

            Assert.Greater(Fold(right, "index1_r"), HandGrip.FingerProximalDegrees - 2.0f, "the copy's hand did not close round the mounted sword");
#else
            yield break;
#endif
        }
    }
}
