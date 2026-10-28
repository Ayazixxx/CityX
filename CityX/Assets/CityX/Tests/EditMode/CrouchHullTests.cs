using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Source's crouch rules (CrouchHull; owner, 2026-10-27: "make it work like it does in source games with the crouch jump as well"):
    /// 0.4 s down and 0.2 s up on the ground, eased; instant in the air with the feet lifted; no standing up without room; a third of the speed.
    /// </summary>
    public sealed class CrouchHullTests
    {
        private const float Step = 1.0f / 60.0f;

        private static void Run(CrouchHull hull, bool held, bool grounded, bool room, float seconds)
        {
            for (float t = 0.0f; t < seconds - 1e-4f; t += Step)
            {
                hull.Step(held, grounded, room, Step);
            }
        }

        [Test]
        public void OnTheGroundTheDuckTakesSourcesFourTenthsOfASecondAndTheHullShrinksAtTheEnd()
        {
            var hull = new CrouchHull();
            Run(hull, true, true, true, 0.3f);
            Assert.IsFalse(hull.Ducked, "the hull shrank before the duck finished");
            Assert.AreEqual(0.75f, hull.Amount, 0.02f);
            Run(hull, true, true, true, 0.12f);
            Assert.IsTrue(hull.Ducked, "after 0.4 s the body is not ducked");
            Assert.AreEqual(1.0f, hull.Shown, 1e-4f);
        }

        [Test]
        public void StandingUpTakesATwoTenthsOfASecondAndOnlyWithRoom()
        {
            var hull = new CrouchHull();
            Run(hull, true, true, true, 0.5f);
            Run(hull, false, true, false, 1.0f);
            Assert.IsTrue(hull.Ducked, "it stood up under something");
            Assert.AreEqual(1.0f, hull.Amount, 1e-4f);
            Run(hull, false, true, true, 0.1f);
            Assert.IsFalse(hull.Ducked, "with room it did not stand");
            Assert.AreEqual(0.5f, hull.Amount, 0.02f, "half way up after 0.1 s");
            Run(hull, false, true, true, 0.1f);
            Assert.AreEqual(0.0f, hull.Amount, 1e-4f);
        }

        [Test]
        public void InTheAirTheDuckIsInstantAndTheFeetComeUpThenDropBackDown()
        {
            var hull = new CrouchHull();
            Assert.AreEqual(CrouchHull.Shift.TuckFeet, hull.Step(true, false, true, Step), "a duck in the air does not lift the feet");
            Assert.IsTrue(hull.Ducked);
            Assert.AreEqual(1.0f, hull.Shown, 1e-4f);
            Assert.AreEqual(CrouchHull.Shift.None, hull.Step(true, false, true, Step), "the feet came up twice");
            Assert.AreEqual(CrouchHull.Shift.None, hull.Step(false, false, false, Step), "the feet dropped with no room under them");
            Assert.IsTrue(hull.Ducked);
            Assert.AreEqual(CrouchHull.Shift.DropFeet, hull.Step(false, false, true, Step), "letting go in the air does not drop the feet");
            Assert.IsFalse(hull.Ducked);
            Assert.AreEqual(0.0f, hull.Shown, 1e-4f);
        }

        [Test]
        public void DuckedOnTheGroundTheBodyMovesAtAThirdOfItsSpeedAndInTheAirAtItsOwn()
        {
            var hull = new CrouchHull();
            Assert.AreEqual(1.0f, hull.SpeedFactor(true), 1e-4f);
            Run(hull, true, true, true, 0.5f);
            Assert.AreEqual(1.0f / 3.0f, hull.SpeedFactor(true), 1e-4f, "Source's duck speed crop is a third");
            Assert.AreEqual(1.0f, hull.SpeedFactor(false), 1e-4f, "the crop is for the ground only");
        }

        [Test]
        public void TheDuckIsEasedAsSourcesSimpleSpline()
        {
            Assert.AreEqual(0.0f, CrouchHull.Spline(0.0f), 1e-5f);
            Assert.AreEqual(0.5f, CrouchHull.Spline(0.5f), 1e-5f);
            Assert.AreEqual(1.0f, CrouchHull.Spline(1.0f), 1e-5f);
            Assert.Less(CrouchHull.Spline(0.1f), 0.1f, "not eased in");
            Assert.Greater(CrouchHull.Spline(0.9f), 0.9f, "not eased out");
        }

        [Test]
        public void AStepLeavesTheWalksFootPlacesDrawnInAndLowered()
        {
            Vector3 centre = new Vector3(-0.17f, 0.135f, 0.0f);
            Vector3 forward = CrouchPose.FootTarget(new Vector3(-0.17f, 0.335f, 0.4f), centre, 0.135f, false, -1.0f, 0.6f);
            Assert.AreEqual(0.4f * 0.6f, forward.z, 1e-4f, "the step is not drawn in");
            Assert.AreEqual(0.135f + 0.2f * CrouchPose.LiftShare, forward.y, 1e-4f, "the lift is not lowered");
            Vector3 still = CrouchPose.FootTarget(centre, centre, 0.135f, true, -1.0f, 1.0f);
            Assert.AreEqual(-0.17f - CrouchPose.StanceWiden, still.x, 1e-4f, "standing still the feet are not wider");
        }
    }
}
