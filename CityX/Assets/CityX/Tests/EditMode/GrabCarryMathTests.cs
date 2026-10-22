using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The grab/carry system's pure maths, ported from player.gd's
    /// _apply_carry_force/_start_carry.
    /// </summary>
    public sealed class GrabCarryMathTests
    {
        [Test]
        public void CarryTargetSitsStraightOutFromTheEye()
        {
            Vector3 target = GrabCarryMath.CarryTarget(new Vector3(0.0f, 2.0f, 0.0f), Vector3.forward, 1.5f);

            Assert.AreEqual(new Vector3(0.0f, 2.0f, 1.5f), target);
        }

        [Test]
        public void ClampCarryDistanceLeavesAnInRangeValueAlone()
        {
            Assert.AreEqual(1.5f, GrabCarryMath.ClampCarryDistance(1.5f, 1.0f, 2.5f), 0.001f);
        }

        [Test]
        public void ClampCarryDistanceClampsTooCloseAndTooFar()
        {
            Assert.AreEqual(1.0f, GrabCarryMath.ClampCarryDistance(0.2f, 1.0f, 2.5f), 0.001f,
                "grabbing something right up against the camera should not start the carry closer than the minimum");
            Assert.AreEqual(2.5f, GrabCarryMath.ClampCarryDistance(10.0f, 1.0f, 2.5f), 0.001f,
                "grabbing something at the far edge of the range should not start the carry further than the maximum");
        }

        [Test]
        public void SpringForcePullsTowardTheTargetWhenAtRest()
        {
            Vector3 force = GrabCarryMath.SpringForce(
                target: new Vector3(0.0f, 0.0f, 2.0f), currentPosition: Vector3.zero, currentVelocity: Vector3.zero,
                springK: 120.0f, dampingK: 18.0f, maxForce: 2500.0f);

            Assert.Greater(force.z, 0.0f, "the force should pull toward the target, not away from it");
            Assert.AreEqual(240.0f, force.z, 0.001f, "at rest the force should be exactly k * error");
        }

        [Test]
        public void SpringForceDampsExistingMotion()
        {
            // Already moving straight toward the target at speed -- damping
            // alone should pull the force back below the undamped value.
            Vector3 undamped = GrabCarryMath.SpringForce(
                new Vector3(0.0f, 0.0f, 2.0f), Vector3.zero, Vector3.zero, 120.0f, 18.0f, 2500.0f);
            Vector3 damped = GrabCarryMath.SpringForce(
                new Vector3(0.0f, 0.0f, 2.0f), Vector3.zero, new Vector3(0.0f, 0.0f, 5.0f), 120.0f, 18.0f, 2500.0f);

            Assert.Less(damped.z, undamped.z, "existing motion toward the target should reduce the applied force");
        }

        [Test]
        public void SpringForceNeverExceedsTheClamp()
        {
            Vector3 force = GrabCarryMath.SpringForce(
                target: new Vector3(0.0f, 0.0f, 1000.0f), currentPosition: Vector3.zero, currentVelocity: Vector3.zero,
                springK: 120.0f, dampingK: 18.0f, maxForce: 2500.0f);

            Assert.LessOrEqual(force.magnitude, 2500.001f,
                "a huge error should never blow the force past the hard clamp");
        }

        [Test]
        public void ShouldAutoDropIsFalseWhileCloseToTheTarget()
        {
            Assert.IsFalse(GrabCarryMath.ShouldAutoDrop(
                new Vector3(0.0f, 0.0f, 2.0f), new Vector3(0.0f, 0.0f, 1.5f), 4.0f));
        }

        [Test]
        public void ShouldAutoDropIsTrueOnceWedgedTooFarAway()
        {
            Assert.IsTrue(GrabCarryMath.ShouldAutoDrop(
                new Vector3(0.0f, 0.0f, 2.0f), new Vector3(0.0f, 0.0f, -10.0f), 4.0f));
        }

        [Test]
        public void TurnTowardsSpinsTheShortWayRoundAndStopsWhenThere()
        {
            Vector3 spin = GrabCarryMath.TurnTowards(Quaternion.identity, Quaternion.Euler(0.0f, 90.0f, 0.0f), 10.0f);
            Assert.AreEqual(90.0f * Mathf.Deg2Rad * 10.0f, spin.y, 0.01f);
            Vector3 back = GrabCarryMath.TurnTowards(Quaternion.identity, Quaternion.Euler(0.0f, 270.0f, 0.0f), 10.0f);
            Assert.Less(back.y, 0.0f, "270 degrees one way is 90 the other");
            Assert.AreEqual(Vector3.zero, GrabCarryMath.TurnTowards(Quaternion.identity, Quaternion.identity, 10.0f));
        }
    }
}
