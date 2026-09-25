using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The orbit driver itself, as opposed to ClaimCutsceneMathTests' pure
    /// formulas -- what matters here is that Begin/Tick actually move the
    /// camera and that Finished fires exactly once.
    /// </summary>
    public sealed class ClaimCutsceneTests
    {
        private GameObject _object;
        private ClaimCutscene _cutscene;
        private GameObject _cameraObject;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Cutscene");
            _cutscene = _object.AddComponent<ClaimCutscene>();
            _cameraObject = new GameObject("Camera");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void BeginPlacesTheCameraAwayFromTheBuildingRightAway()
        {
            var groundCentre = new Vector3(10.0f, 0.0f, 10.0f);
            var size = new Vector3(12.0f, 20.0f, 12.0f);

            _cutscene.Begin(_cameraObject.transform, playerPosition: new Vector3(20.0f, 0.0f, 10.0f), groundCentre, size);

            float distanceFromFocus = Vector3.Distance(_cameraObject.transform.position, groundCentre + Vector3.up * (size.y * 0.5f));
            Assert.Greater(distanceFromFocus, 0.0f, "the camera should not spawn exactly on top of the focus point");
        }

        [Test]
        public void TickingToTheEndFiresFinishedExactlyOnce()
        {
            _cutscene.Begin(_cameraObject.transform, Vector3.zero, Vector3.zero, new Vector3(12.0f, 20.0f, 12.0f));
            int finishedCount = 0;
            _cutscene.Finished += () => finishedCount++;

            _cutscene.Tick(ClaimCutscene.OrbitSeconds + 1.0f);
            _cutscene.Tick(1.0f);
            _cutscene.Tick(1.0f);

            Assert.AreEqual(1, finishedCount, "Finished should not keep firing on every tick after the orbit is already done");
        }

        [Test]
        public void TheCameraActuallyMovesOverTheCourseOfTheOrbit()
        {
            _cutscene.Begin(_cameraObject.transform, new Vector3(20.0f, 0.0f, 0.0f), Vector3.zero, new Vector3(12.0f, 20.0f, 12.0f));
            Vector3 start = _cameraObject.transform.position;

            _cutscene.Tick(ClaimCutscene.OrbitSeconds * 0.5f);

            Assert.AreNotEqual(start, _cameraObject.transform.position, "halfway through a 360 orbit the camera should have moved");
        }

        [Test]
        public void TheCameraAlwaysFacesTheFocusPoint()
        {
            var groundCentre = new Vector3(5.0f, 0.0f, 5.0f);
            var size = new Vector3(12.0f, 20.0f, 12.0f);
            _cutscene.Begin(_cameraObject.transform, new Vector3(20.0f, 0.0f, 5.0f), groundCentre, size);

            _cutscene.Tick(ClaimCutscene.OrbitSeconds * 0.25f);

            Vector3 focus = groundCentre + Vector3.up * (size.y * 0.5f);
            Vector3 expectedForward = (focus - _cameraObject.transform.position).normalized;
            float dot = Vector3.Dot(_cameraObject.transform.forward, expectedForward);
            Assert.Greater(dot, 0.99f, "the camera should be looking straight at the building's midpoint");
        }

        [Test]
        public void WithNoCameraAssignedTickingDoesNotThrow()
        {
            var cutscene = new GameObject("NoCamera").AddComponent<ClaimCutscene>();
            try
            {
                cutscene.Begin(null, Vector3.zero, Vector3.zero, new Vector3(12.0f, 20.0f, 12.0f));
                Assert.DoesNotThrow(() => cutscene.Tick(1.0f));
            }
            finally
            {
                Object.DestroyImmediate(cutscene.gameObject);
            }
        }
    }
}
