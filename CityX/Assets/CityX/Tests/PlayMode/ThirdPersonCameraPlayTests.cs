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
    }
}
