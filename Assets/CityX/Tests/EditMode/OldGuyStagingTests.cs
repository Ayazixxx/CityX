using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The two pure decision primitives behind OldGuy's out-of-view staging
    /// (see OnPlayerClaimedBuilding's own doc comment) -- HasClearRun and
    /// IsInCameraFrustum, both public static for exactly this reason,
    /// tested directly rather than through the full staging orchestration
    /// (which also needs a streamed building and a real ClaimSystem -- see
    /// OldGuyTests' own class comment on why that half stays untested here).
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
        public void HasClearRunIsTrueWithNothingInTheWay()
        {
            Assert.IsTrue(OldGuy.HasClearRun(Vector3.zero, new Vector3(0.0f, 0.0f, 20.0f)));
        }

        [Test]
        public void HasClearRunIsFalseWhenAWallBlocksIt()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0.0f, 1.3f, 10.0f);
            _wall.transform.localScale = new Vector3(20.0f, 5.0f, 1.0f);
            Physics.SyncTransforms();

            Assert.IsFalse(OldGuy.HasClearRun(Vector3.zero, new Vector3(0.0f, 0.0f, 20.0f)),
                "a wall spanning the path should block the line of sight");
        }

        [Test]
        public void HasClearRunIsTrueForTheSameStartAndEndPoint()
        {
            Assert.IsTrue(OldGuy.HasClearRun(Vector3.one * 5.0f, Vector3.one * 5.0f),
                "a zero-length run has nothing to be blocked by");
        }
    }
}
