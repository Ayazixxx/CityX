using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Obstacle/altitude avoidance, against real colliders -- PlayMode
    /// because it raycasts, same reasoning NpcSteeringPlayTests already
    /// documents: an EditMode test has no physics scene for those casts to
    /// hit, and every one would come back clear.
    /// </summary>
    public sealed class FlyingNpcSteeringPlayTests
    {
        private GameObject _ground;
        private GameObject _building;
        private GameObject _body;
        private FlyingNpcSteering _steering;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Flyer");
            _steering = new FlyingNpcSteering();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            if (_ground != null)
            {
                Object.DestroyImmediate(_ground);
            }
            if (_building != null)
            {
                Object.DestroyImmediate(_building);
            }
        }

        private void GroundAt(float y)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, y - 0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        private void BuildingAt(Vector3 centre, Vector3 size)
        {
            _building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _building.transform.position = centre;
            _building.transform.localScale = size;
        }

        [UnityTest]
        public IEnumerator OpenSkyAtCruiseAltitudeIsNotAvoiding()
        {
            GroundAt(0.0f);
            _body.transform.position = new Vector3(0.0f, FlyingNpcSteering.CruiseAltitudeAboveSurface, 0.0f);
            yield return new WaitForFixedUpdate();

            Vector3 velocity = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsFalse(_steering.IsAvoiding);
            Assert.AreEqual(0.0f, velocity.y, 0.001f, "already at cruise altitude with nothing ahead -- it should hold, not climb or sink");
            Assert.AreEqual(FlyingNpcSteering.CruiseSpeed, velocity.magnitude, 0.01f);
        }

        [UnityTest]
        public IEnumerator ABuildingDirectlyAheadIsClimbedOverNotFlownInto()
        {
            GroundAt(0.0f);
            _body.transform.position = new Vector3(0.0f, FlyingNpcSteering.CruiseAltitudeAboveSurface, 0.0f);
            BuildingAt(new Vector3(0.0f, 25.0f, 15.0f), new Vector3(20.0f, 50.0f, 20.0f));
            yield return new WaitForFixedUpdate();

            Vector3 velocity = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsTrue(_steering.IsAvoiding, "a building directly ahead was not noticed at all");
            Assert.AreEqual(FlyingNpcSteering.ClimbRate, velocity.y, 0.001f,
                "it should be climbing hard to clear the building, not sinking or holding");
        }

        [UnityTest]
        public IEnumerator TooCloseToTheGroundClimbsEvenWithNothingAhead()
        {
            GroundAt(0.0f);
            _body.transform.position = new Vector3(0.0f, FlyingNpcSteering.MinAltitudeAboveSurface - 2.0f, 0.0f);
            yield return new WaitForFixedUpdate();

            Vector3 velocity = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsTrue(_steering.IsAvoiding, "flying dangerously close to the ground was not treated as unsafe");
            Assert.AreEqual(FlyingNpcSteering.ClimbRate, velocity.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator SettlingFromAboveCruiseEasesDownGently()
        {
            GroundAt(0.0f);
            _body.transform.position = new Vector3(0.0f, FlyingNpcSteering.CruiseAltitudeAboveSurface + 20.0f, 0.0f);
            yield return new WaitForFixedUpdate();

            Vector3 velocity = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsFalse(_steering.IsAvoiding, "well above the ground with nothing ahead is not a danger, just high");
            Assert.AreEqual(-FlyingNpcSteering.CruiseAdjustRate, velocity.y, 0.001f,
                "too high with nothing wrong should ease back down gently, not snap or hold");
        }

        [UnityTest]
        public IEnumerator NoGroundFoundAtAllIsAssumedSafeRatherThanClimbedForever()
        {
            // No _ground at all -- the downward ray finds nothing.
            _body.transform.position = new Vector3(0.0f, 500.0f, 0.0f);
            yield return new WaitForFixedUpdate();

            Vector3 velocity = _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.IsFalse(_steering.IsAvoiding,
                "a missing surface below should not be treated as being too close to the ground");
            Assert.AreEqual(0.0f, velocity.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator HeightAboveSurfaceReadsTheRealDistanceToTheRealGround()
        {
            GroundAt(0.0f);
            _body.transform.position = new Vector3(0.0f, 50.0f, 0.0f);
            yield return new WaitForFixedUpdate();

            _steering.Steer(_body.transform, Vector3.forward, 0.016f);

            Assert.AreEqual(50.0f, _steering.HeightAboveSurface, 0.1f);
        }
    }
}
