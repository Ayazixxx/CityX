using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The reported fix: a dead-centre orbit camera puts anything small and
    /// close directly in front of the player -- a dropped pistol on the
    /// ground being the concrete case -- right behind the character's own
    /// silhouette. ShoulderOffset shifts the camera to one side, Fortnite/
    /// GTA-style, without changing what it looks at.
    /// </summary>
    public sealed class ThirdPersonCameraPlayTests
    {
        private GameObject _target;
        private GameObject _cameraObject;
        private ThirdPersonCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject("Target");
            _cameraObject = new GameObject("Camera");
            _camera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _camera.Target = _target.transform;
            _camera.Yaw = 0.0f;
            _camera.Pitch = 0.0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_target);
            Object.Destroy(_cameraObject);
        }

        [UnityTest]
        public IEnumerator TheCameraSitsToTheRightOfTheSpineRatherThanDeadCentre()
        {
            yield return null; // let LateUpdate place it once

            // Facing straight along +Z with no pitch: "to the right of the
            // spine" is a positive world-space X, and the camera should still
            // sit behind the target along Z either way.
            Assert.Greater(_cameraObject.transform.position.x, 0.05f,
                "the camera should be offset to the right of the player's own centreline");
            Assert.Less(_cameraObject.transform.position.z, _target.transform.position.z,
                "the camera should still be behind the player, offset aside notwithstanding");
        }

        [UnityTest]
        public IEnumerator TheOffsetDoesNotChangeWhereTheCameraIsActuallyLooking()
        {
            yield return null;

            // Rotation is set directly from pitch/yaw, independent of where
            // the camera's own position sits -- the offset must never leak
            // into aim, since PistolWeapon/BottleWeapon's own look-direction
            // gate reads this transform's forward directly.
            float angle = Quaternion.Angle(Quaternion.identity, _cameraObject.transform.rotation);
            Assert.Less(angle, 0.01f, "the shoulder offset should never rotate the camera off pitch/yaw");
        }

        /// <summary>
        /// THE AIM-IN FEATURE ITSELF: PlayerInputReader sets IsAiming true
        /// for as long as right-click is held on an equipped gun (or a
        /// melee weapon's own WeaponAimInput.IsAiming), and this component's
        /// only job is to react to that flag by narrowing the field of view
        /// -- it never reads the mouse or an equipped item itself, so the
        /// isolated behaviour to prove here is just the zoom in and back out.
        /// </summary>
        [UnityTest]
        public IEnumerator AimingNarrowsTheFieldOfViewAndReleasingRestoresIt()
        {
            // A DEDICATED OBJECT, CAMERA ADDED FIRST -- matching
            // CityMassingSceneBuilder's own real order (Camera, then
            // ThirdPersonCamera on the same object). Start() reads
            // GetComponent<Camera>() once, on this component's own first
            // frame; adding Camera onto the shared _cameraObject from SetUp
            // AFTER ThirdPersonCamera was already on it risks that lookup
            // running before the Camera exists.
            var rig = new GameObject("AimRig");
            Camera cam = rig.AddComponent<Camera>();
            cam.fieldOfView = 60.0f;
            ThirdPersonCamera aimCamera = rig.AddComponent<ThirdPersonCamera>();
            aimCamera.Target = _target.transform;
            aimCamera.AimFieldOfView = 40.0f;
            aimCamera.AimZoomDegreesPerSecond = 90.0f;

            yield return null; // Start() captures the rest field of view here
            Assert.AreEqual(60.0f, cam.fieldOfView, 0.5f, "should start at rest, untouched");

            // MANY frames, not a handful -- batch mode runs uncapped at
            // roughly 2500fps (see PlayerLocomotionPlayTests's own comment
            // on the same gotcha), and LateUpdate steps by real
            // Time.deltaTime, so a short loop can finish while barely any
            // simulated time has actually passed. Mathf.MoveTowards clamps
            // exactly at the target once enough has accumulated, so a
            // generous frame count converges rather than overshooting.
            aimCamera.IsAiming = true;
            for (int step = 0; step < 3000; step++)
            {
                yield return null;
            }
            Assert.AreEqual(40.0f, cam.fieldOfView, 0.5f,
                "holding aim should narrow the field of view down to AimFieldOfView");

            aimCamera.IsAiming = false;
            for (int step = 0; step < 3000; step++)
            {
                yield return null;
            }
            Assert.AreEqual(60.0f, cam.fieldOfView, 0.5f,
                "releasing aim should widen the field of view back to whatever it started at");

            Object.Destroy(rig);
        }
    }
}
