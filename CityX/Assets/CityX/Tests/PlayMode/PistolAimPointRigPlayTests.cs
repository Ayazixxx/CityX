using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "Bullet trails ... going in the wrong direction." The tracer runs from the
    /// muzzle to the shot's end point, so it only looks right if the BARREL
    /// points along that same line. The arm used to aim at a fixed point 15m down
    /// the camera ray: at 4m a 1.4m shoulder offset is ~20 degrees, so the gun
    /// pointed well off the target while the bullet (and tracer) went to it. And
    /// hip fire never raised the arm at all. Measured on the REAL Player.prefab
    /// and Pistol.prefab: after these changes the muzzle points at the aim point.
    /// </summary>
    public sealed class PistolAimPointRigPlayTests
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

            for (int i = 0; i < 20; i++)
            {
                _locomotion.Tick(new LocomotionInput { GunEquipped = true }, 1.0f / 60.0f);
                yield return null;
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// A camera behind and to the right, looking along +Z, and a target point
        /// `metres` ahead of the CAMERA on that ray -- close enough that a fixed
        /// 15m convergence point is a visibly different direction from it.
        /// </summary>
        private LocomotionInput InputFor(Vector3 aimPoint, bool aiming, bool supplyAimPoint)
        {
            return new LocomotionInput
            {
                GunEquipped = true,
                Aiming = aiming,
                YawDegrees = 0.0f,
                AimCameraPosition = _player.transform.position + new Vector3(0.6f, 1.9f, -3.5f),
                AimCameraForward = (aimPoint - (_player.transform.position + new Vector3(0.6f, 1.9f, -3.5f))).normalized,
                GunAimPoint = aimPoint,
                HasGunAimPoint = supplyAimPoint,
            };
        }

        private IEnumerator Run(LocomotionInput input, float seconds)
        {
            float elapsed = 0.0f;
            while (elapsed < seconds)
            {
                float dt = Time.deltaTime;
                _locomotion.Tick(input, dt);
                yield return null;   // LateUpdate (ApplyGunAimPose) runs after this
                elapsed += dt;
            }
        }

        private float BarrelErrorDegrees(Vector3 aimPoint)
        {
            Transform muzzle = _pistol.MuzzleFlashObject.transform;
            return Vector3.Angle(muzzle.forward, aimPoint - muzzle.position);
        }

        [UnityTest]
        public IEnumerator AimingPointsTheBarrelAtACloseTarget()
        {
#if UNITY_EDITOR
            yield return Build();
            Vector3 target = _player.transform.position + new Vector3(0.6f, 1.4f, 3.5f);

            yield return Run(InputFor(target, aiming: true, supplyAimPoint: true), 1.0f);

            float error = BarrelErrorDegrees(target);
            Assert.Less(error, 8.0f, $"the barrel points {error:0.0} degrees off a target 3.5m ahead while aiming");
#else
            yield break;
#endif
        }

        /// <summary>
        /// THE CONTROL: with the reader NOT supplying an aim point the arm falls back
        /// to the old fixed 15m-down-the-ray point, and the barrel ends up measurably
        /// further from the same close target than with the real aim point -- so the
        /// measurement above can tell the two apart.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFixedConvergencePointLeavesTheBarrelFurtherOffACloseTarget()
        {
#if UNITY_EDITOR
            yield return Build();
            Vector3 target = _player.transform.position + new Vector3(0.6f, 1.4f, 3.5f);

            yield return Run(InputFor(target, aiming: true, supplyAimPoint: true), 1.0f);
            float withAimPoint = BarrelErrorDegrees(target);

            yield return Run(InputFor(target, aiming: true, supplyAimPoint: false), 1.0f);
            float fixedPoint = BarrelErrorDegrees(target);

            Assert.Greater(fixedPoint, withAimPoint + 3.0f,
                $"the fixed-point aim ({fixedPoint:0.0} deg) was not measurably worse than the real aim point ({withAimPoint:0.0} deg)");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HipFireRaisesTheGunAtTheTargetForAMomentThenLowersIt()
        {
#if UNITY_EDITOR
            yield return Build();
            Vector3 target = _player.transform.position + new Vector3(0.6f, 1.4f, 3.5f);
            LocomotionInput hip = InputFor(target, aiming: false, supplyAimPoint: true);

            // Not aiming, nothing fired: the gun is wherever the carry pose leaves it.
            yield return Run(hip, 0.6f);
            float carried = BarrelErrorDegrees(target);

            _locomotion.NotifyGunFired();
            yield return Run(hip, 0.4f);
            float firing = BarrelErrorDegrees(target);

            Assert.Less(firing, 8.0f, $"after a shot the barrel still pointed {firing:0.0} degrees off the target (carried pose was {carried:0.0})");
            Assert.Greater(carried, firing + 5.0f, "the fire pose made no difference over the carried pose");

            // The fire pose is momentary: half a second later it has let go.
            yield return Run(hip, PlayerLocomotion.GunFirePoseSeconds + 0.8f);
            float lowered = BarrelErrorDegrees(target);
            Assert.Greater(lowered, firing + 5.0f, "the arm never lowered after firing stopped");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FiringAlsoTurnsTheBodyToTheCameraEvenWithoutAiming()
        {
#if UNITY_EDITOR
            yield return Build();
            _player.transform.rotation = Quaternion.Euler(0.0f, 120.0f, 0.0f);
            Vector3 target = _player.transform.position + new Vector3(0.6f, 1.4f, 6.0f);
            LocomotionInput hip = InputFor(target, aiming: false, supplyAimPoint: true);

            yield return Run(hip, 0.3f);
            Assert.AreEqual(120.0f, _player.transform.eulerAngles.y, 1.0f, "setup: standing still while not aiming should not turn the body");

            _locomotion.NotifyGunFired();
            yield return Run(hip, 0.1f);

            Assert.AreEqual(0.0f, Mathf.DeltaAngle(0.0f, _player.transform.eulerAngles.y), 1.0f,
                "firing did not turn the body to face where the shot goes");
#else
            yield break;
#endif
        }
    }
}
