using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Whether Old Guy would be seen moving (OldGuy.IsSeenBy, IsInCameraFrustum): he steps into his den only where the player's camera sees
    /// neither where he stands nor where he goes (TryMoveUnseen).
    /// </summary>
    public sealed class OldGuyStagingTests
    {
        private GameObject _cameraObject;
        private Camera _camera;
        private GameObject _wall;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        private Camera MakeCamera(Vector3 position, Quaternion rotation)
        {
            _cameraObject = new GameObject("Camera");
            _cameraObject.transform.position = position;
            _cameraObject.transform.rotation = rotation;
            _camera = _cameraObject.AddComponent<Camera>();
            return _camera;
        }

        [Test]
        public void IsInCameraFrustumIsTrueForAPointDirectlyAhead()
        {
            Camera camera = MakeCamera(Vector3.zero, Quaternion.identity);
            Assert.IsTrue(OldGuy.IsInCameraFrustum(camera, new Vector3(0.0f, 0.0f, 10.0f)));
        }

        [Test]
        public void IsInCameraFrustumIsFalseForAPointBehindTheCamera()
        {
            Camera camera = MakeCamera(Vector3.zero, Quaternion.identity);
            Assert.IsFalse(OldGuy.IsInCameraFrustum(camera, new Vector3(0.0f, 0.0f, -10.0f)),
                "a point behind the camera must never read as visible, whatever its x/y happen to be");
        }

        [Test]
        public void IsInCameraFrustumIsFalseForAPointWellOffToTheSide()
        {
            Camera camera = MakeCamera(Vector3.zero, Quaternion.identity);
            Assert.IsFalse(OldGuy.IsInCameraFrustum(camera, new Vector3(500.0f, 0.0f, 10.0f)),
                "a point far outside the horizontal field of view should not read as visible");
        }

        [Test]
        public void APointAheadWithNothingInTheWayIsSeen()
        {
            Camera camera = MakeCamera(Vector3.zero, Quaternion.identity);
            Assert.IsTrue(OldGuy.IsSeenBy(camera, new Vector3(0.0f, 0.0f, 20.0f)));
        }

        [Test]
        public void APointBehindAWallIsNotSeen()
        {
            Camera camera = MakeCamera(Vector3.zero, Quaternion.identity);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0.0f, 0.0f, 10.0f);
            _wall.transform.localScale = new Vector3(20.0f, 5.0f, 1.0f);
            Physics.SyncTransforms();

            Assert.IsFalse(OldGuy.IsSeenBy(camera, new Vector3(0.0f, 0.0f, 20.0f)), "a wall between should hide it");
        }
    }
}
