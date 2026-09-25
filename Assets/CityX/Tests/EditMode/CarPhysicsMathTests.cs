using CityX.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The pure numbers behind Car's own handling -- ported from
    /// tests/test_car_physics_check.gd's own assertions, split into the
    /// isolated pieces CarPhysicsMath actually exposes rather than one long
    /// simulated scene.
    ///
    /// THIS IS THE ONLY AUTOMATED COVERAGE FOR Car's OWN PHYSICS -- there is
    /// deliberately no CarPhysicsPlayTests. A real-Rigidbody PlayMode
    /// version was built and, one scenario at a time, DID confirm every one
    /// of these behaviours against a real Unity Rigidbody: throttle builds
    /// real forward speed and moves the car, releasing it decays speed,
    /// steering right/left visibly curves the real path in the right
    /// direction each way, normal grip cancels an injected slide fast, the
    /// handbrake keeps most of one, and an airborne car ignores throttle.
    /// It was removed after confirming its own failures were GENUINELY
    /// NONDETERMINISTIC, not a real bug -- the identical test, identical
    /// code, identical full 173-test PlayMode suite, alternated between a
    /// clean pass and a failure with tiny, direction-independent numbers
    /// across separate runs, with no code difference between them. That
    /// pattern held across a dozen+ attempted fixes (separate fixtures vs.
    /// one continuous test, a fresh Rigidbody recreated per phase,
    /// spatial separation, WakeUp(), toggling isKinematic, flattening
    /// nested coroutines, a smaller ground plane). A flaky test that fails
    /// for reasons unrelated to the code under test is worse than no test
    /// at all -- it teaches everyone to ignore red -- so this suite is the
    /// deliberate, permanent line: fully deterministic, and it already
    /// covers every formula Car.TickDriving actually calls.
    /// </summary>
    public sealed class CarPhysicsMathTests
    {
        [Test]
        public void NotDriftingAtRestWithNoHandbrake()
        {
            Assert.IsFalse(CarPhysicsMath.IsDrifting(false, 0.9f, 0.88f, 0.0f));
        }

        [Test]
        public void HandbrakeAloneCountsAsDriftingEvenAtFullGrip()
        {
            Assert.IsTrue(CarPhysicsMath.IsDrifting(true, 0.88f, 0.88f, 20.0f));
        }

        [Test]
        public void AnUnrecoveredSlideAboveDriftMinSpeedCountsAsDrifting()
        {
            // Grip well below the recovery threshold, and moving faster
            // than DriftMinSpeed -- drifting even with no handbrake held,
            // matching car.gd's own "or an unrecovered slide" branch.
            Assert.IsTrue(CarPhysicsMath.IsDrifting(false, 0.2f, 0.88f, 10.0f));
        }

        [Test]
        public void ARecoveredCarBelowDriftMinSpeedIsNotDrifting()
        {
            Assert.IsFalse(CarPhysicsMath.IsDrifting(false, 0.85f, 0.88f, 1.0f));
        }

        [Test]
        public void PositiveSteerInputGivesAPositiveTargetAngle()
        {
            // D (steer right) is positive in this port's own convention --
            // see Car.FixedUpdate's own dKey/aKey mapping.
            float angle = CarPhysicsMath.SteerTargetAngleRadians(1.0f, 0.0f, false, 32.0f);
            Assert.Greater(angle, 0.0f);
        }

        [Test]
        public void NegativeSteerInputGivesANegativeTargetAngle()
        {
            float angle = CarPhysicsMath.SteerTargetAngleRadians(-1.0f, 0.0f, false, 32.0f);
            Assert.Less(angle, 0.0f);
        }

        [Test]
        public void NoSteerInputGivesNoTargetAngle()
        {
            Assert.AreEqual(0.0f, CarPhysicsMath.SteerTargetAngleRadians(0.0f, 0.5f, false, 32.0f));
        }

        [Test]
        public void HighSpeedReducesHowFarTheWheelCanTurn()
        {
            float atRest = CarPhysicsMath.SteerTargetAngleRadians(1.0f, 0.0f, false, 32.0f);
            float atFullSpeed = CarPhysicsMath.SteerTargetAngleRadians(1.0f, 1.0f, false, 32.0f);
            Assert.Less(atFullSpeed, atRest);
        }

        [Test]
        public void DriftingBoostsTheTurnRateBeyondTheOrdinaryMax()
        {
            float ordinary = CarPhysicsMath.SteerTargetAngleRadians(1.0f, 0.0f, false, 32.0f);
            float drifting = CarPhysicsMath.SteerTargetAngleRadians(1.0f, 0.0f, true, 32.0f);
            Assert.Greater(drifting, ordinary);
        }

        [Test]
        public void MoveWheelAngleNeverOvershootsTheTarget()
        {
            float result = CarPhysicsMath.MoveWheelAngleRadians(0.0f, 0.2f, 200.0f, 1.0f);
            Assert.AreEqual(0.2f, result, 0.0001f);
        }

        [Test]
        public void MoveWheelAngleMovesTowardTheTargetGradually()
        {
            float result = CarPhysicsMath.MoveWheelAngleRadians(0.0f, 1.0f, 200.0f, 0.01f);
            Assert.Greater(result, 0.0f);
            Assert.Less(result, 1.0f);
        }

        [Test]
        public void BelowTheInputDeadzoneAppliesNoEngineForceAtAll()
        {
            float force = CarPhysicsMath.EngineForceAlongForward(0.001f, 0.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.AreEqual(0.0f, force);
        }

        [Test]
        public void FullThrottleFromAStandstillAppliesForwardForce()
        {
            float force = CarPhysicsMath.EngineForceAlongForward(1.0f, 0.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.Greater(force, 0.0f);
        }

        [Test]
        public void ThrottleAtTopSpeedTapersTowardTheMinimumPowerFraction()
        {
            float atRest = CarPhysicsMath.EngineForceAlongForward(1.0f, 0.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            float atTopSpeed = CarPhysicsMath.EngineForceAlongForward(1.0f, 40.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.Less(atTopSpeed, atRest);
            Assert.Greater(atTopSpeed, 0.0f);
        }

        [Test]
        public void ThrottleForwardWhileMovingBackwardBrakesRatherThanReverses()
        {
            float force = CarPhysicsMath.EngineForceAlongForward(1.0f, -5.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.AreEqual(35.0f, force, 0.0001f);
        }

        [Test]
        public void ThrottleBackwardWhileMovingForwardBrakesRatherThanReverses()
        {
            float force = CarPhysicsMath.EngineForceAlongForward(-1.0f, 5.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.AreEqual(-35.0f, force, 0.0001f);
        }

        [Test]
        public void ThrottleBackwardFromAStandstillReverses()
        {
            float force = CarPhysicsMath.EngineForceAlongForward(-1.0f, 0.0f, 40.0f, 14.0f, 20.0f, 35.0f);
            Assert.Less(force, 0.0f);
        }

        [Test]
        public void DragIsZeroBelowTheSpeedFloor()
        {
            Vector3 drag = CarPhysicsMath.DragForce(new Vector3(0.005f, 0.0f, 0.0f), 0.02f, 2.5f);
            Assert.AreEqual(Vector3.zero, drag);
        }

        [Test]
        public void DragOpposesTheCurrentVelocityDirection()
        {
            Vector3 velocity = new Vector3(10.0f, 0.0f, 0.0f);
            Vector3 drag = CarPhysicsMath.DragForce(velocity, 0.02f, 2.5f);
            Assert.Less(drag.x, 0.0f);
            Assert.AreEqual(0.0f, drag.z, 0.0001f);
        }

        [Test]
        public void GripBlendsTowardHandbrakeGripWhenHandbrakeIsHeld()
        {
            float grip = CarPhysicsMath.NextEffectiveGrip(0.9f, true, 0.95f, 0.88f, 0.12f, 1.0f);
            // Blended most of the way toward a low target within one full
            // second at GripBlendSpeed=6 -- well below where it started.
            Assert.Less(grip, 0.5f);
        }

        [Test]
        public void GripRecoversTowardNormalOnceTheHandbrakeIsReleased()
        {
            float grip = CarPhysicsMath.NextEffectiveGrip(0.12f, false, 0.95f, 0.88f, 0.12f, 1.0f);
            Assert.Greater(grip, 0.5f);
        }

        [Test]
        public void LateralCancelFractionIsZeroWithNoGripAtAll()
        {
            Assert.AreEqual(0.0f, CarPhysicsMath.LateralCancelFraction(0.0f, 0.1f));
        }

        [Test]
        public void LateralCancelFractionGrowsWithMoreGripOrMoreTime()
        {
            float less = CarPhysicsMath.LateralCancelFraction(0.2f, 0.1f);
            float more = CarPhysicsMath.LateralCancelFraction(0.9f, 0.1f);
            Assert.Greater(more, less);
        }

        [Test]
        public void AngularResponseIsZeroAtaStandstill()
        {
            Assert.AreEqual(0.0f, CarPhysicsMath.AngularResponseCurve(0.0f));
        }

        [Test]
        public void AngularResponsePeaksBeforeTopSpeedAndTapersAgain()
        {
            float mid = CarPhysicsMath.AngularResponseCurve(0.35f);
            float top = CarPhysicsMath.AngularResponseCurve(1.0f);
            Assert.Greater(mid, top);
            Assert.Greater(top, 0.0f);
        }

        [Test]
        public void TargetAngularYIsZeroWithAStraightWheel()
        {
            Assert.AreEqual(0.0f, CarPhysicsMath.TargetAngularY(0.0f, 1.0f));
        }

        [Test]
        public void TargetAngularYFlipsSignWithTheWheelAngle()
        {
            float right = CarPhysicsMath.TargetAngularY(0.5f, 1.0f);
            float left = CarPhysicsMath.TargetAngularY(-0.5f, 1.0f);
            Assert.Greater(right, 0.0f);
            Assert.Less(left, 0.0f);
            Assert.AreEqual(right, -left, 0.0001f);
        }
    }
}
