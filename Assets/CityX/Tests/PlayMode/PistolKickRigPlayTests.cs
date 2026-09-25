using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The pistol's own snap-back on the REAL Player.prefab and REAL Pistol.prefab:
    /// this gun's grip carries a 180-degree roll and a rig-scaled hand, so a
    /// kick that is right on paper can still push the muzzle DOWN or the gun
    /// FORWARD. The direction is measured, not assumed: on firing, the muzzle
    /// must tip UP and the whole gun must move BACK along its own barrel.
    /// </summary>
    public sealed class PistolKickRigPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _pistolObject;
        private PistolWeapon _pistol;
        private PlayerLocomotion _locomotion;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_pistolObject != null) Object.Destroy(_pistolObject);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Build()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var pistolPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab");
            Assert.IsNotNull(playerPrefab);
            Assert.IsNotNull(pistolPrefab);

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            var reader = _player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();

            _pistolObject = Object.Instantiate(pistolPrefab, new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            _pistol = _pistolObject.GetComponent<PistolWeapon>();
            _pistol.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            Assert.IsNotNull(_pistol.MuzzleFlashObject, "the real pistol has no muzzle object");

            for (int i = 0; i < 30; i++)
            {
                _locomotion.Tick(new LocomotionInput { GunEquipped = true }, 1.0f / 60.0f);
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FiringTipsTheMuzzleUpAndShovesTheGunBackAlongItsBarrel()
        {
#if UNITY_EDITOR
            yield return Build();

            Transform muzzle = _pistol.MuzzleFlashObject.transform;
            Vector3 baselineForward = muzzle.forward;
            Vector3 baselinePosition = _pistol.transform.position;
            float baselinePitch = Mathf.Asin(Mathf.Clamp(baselineForward.y, -1.0f, 1.0f)) * Mathf.Rad2Deg;

            Assert.IsTrue(_pistol.Fire(_player.transform.position + Vector3.up * 1.6f - Vector3.forward * 3.0f,
                Vector3.forward, false, _player));
            // The kick is applied by the pistol's own Update -- one frame on.
            yield return null;

            float kickedPitch = Mathf.Asin(Mathf.Clamp(muzzle.forward.y, -1.0f, 1.0f)) * Mathf.Rad2Deg;
            Vector3 shove = _pistol.transform.position - baselinePosition;
            float alongBarrel = Vector3.Dot(shove, baselineForward);

            Assert.Greater(kickedPitch - baselinePitch, 2.0f,
                $"the muzzle did not tip up (pitch {baselinePitch:0.0} -> {kickedPitch:0.0})");
            Assert.Less(alongBarrel, -0.01f,
                $"the gun was not shoved back along its barrel (moved {alongBarrel:0.000}m along it)");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheGunSettlesBackToItsCarriedPoseAfterTheKick()
        {
#if UNITY_EDITOR
            yield return Build();

            Transform muzzle = _pistol.MuzzleFlashObject.transform;
            Vector3 baselineForward = muzzle.forward;
            Vector3 baselineLocalPosition = _pistol.transform.localPosition;
            Quaternion baselineLocalRotation = _pistol.transform.localRotation;

            _pistol.Fire(_player.transform.position + Vector3.up * 1.6f - Vector3.forward * 3.0f,
                Vector3.forward, false, _player);
            float waited = 0.0f;
            while (waited < 0.8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.Less(Vector3.Distance(_pistol.transform.localPosition, baselineLocalPosition), 0.002f,
                "the gun stayed displaced after the kick");
            // In the HAND'S space: the body animates on regardless, and the muzzle's world
            // facing moves with it -- only the gun's own carried pose must be restored.
            Assert.Less(Quaternion.Angle(_pistol.transform.localRotation, baselineLocalRotation), 1.0f,
                "the muzzle stayed tipped after the kick");
#else
            yield break;
#endif
        }
    }
}
