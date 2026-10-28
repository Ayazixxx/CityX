using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The flight shared by the jetpack and the wings (docs/FLIGHT.md): Space in the air takes off and the flight lasts until the body lands;
    /// nothing held is a hover; Space climbs, Shift a faster straight climb, Ctrl sinks; flying forward follows the camera up and down; the
    /// Flight Speed stat scales it all.
    /// </summary>
    public sealed class FlightModelTests
    {
        private static readonly FlightTuning Tuning = new FlightTuning(climb: 7.0f, boostClimb: 10.0f, descend: 9.0f, cruise: 10.0f, fast: 17.0f, acceleration: 12.0f);

        private static FlightInput Air(bool thrust = false, bool descend = false, bool boost = false, Vector2 move = default, float pitch = 0.0f) =>
            new FlightInput { ThrustHeld = thrust, DescendHeld = descend, BoostHeld = boost, Move = move, CameraPitchDegrees = pitch, Airborne = true };

        [Test]
        public void SpaceInTheAirTakesOffAndSpaceOnTheGroundDoesNot()
        {
            var model = new FlightModel(Tuning);
            model.Tick(new FlightInput { ThrustHeld = true, Airborne = false });
            Assert.IsFalse(model.Flying, "took off from the ground: Space there is a jump");
            model.Tick(Air(thrust: true));
            Assert.IsTrue(model.Flying);
            Assert.IsTrue(model.TookOff);
            Assert.IsTrue(model.ClimbBegan, "a flight begun with Space held is a climb from its first frame");
            model.Tick(Air(thrust: true));
            Assert.IsFalse(model.TookOff, "TookOff is one frame");
            Assert.IsFalse(model.ClimbBegan);
        }

        [Test]
        public void LettingGoHoversAndTouchingDownLands()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true));
            model.Tick(Air());
            Assert.IsTrue(model.Flying, "letting go of Space ended the flight: it should hover");
            Assert.AreEqual(0.0f, model.VerticalVelocity, 1e-4f, "a hover holds its height");
            Assert.AreEqual(Tuning.Cruise, model.HorizontalSpeed, 1e-4f, "hovering, the keys would fly at the cruise (nothing held, the body goes nowhere)");
            model.Tick(new FlightInput { Airborne = false });
            Assert.IsFalse(model.Flying);
            Assert.IsTrue(model.Landed);
        }

        [Test]
        public void SpaceClimbsShiftClimbsFasterAndCtrlSinks()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true));
            Assert.AreEqual(Tuning.Climb, model.VerticalVelocity, 1e-4f);
            model.Tick(Air(thrust: true, boost: true));
            Assert.AreEqual(Tuning.BoostClimb, model.VerticalVelocity, 1e-4f, "Shift with nothing else held is the faster climb");
            model.Tick(Air(thrust: true, boost: true, move: Vector2.right));
            Assert.AreEqual(Tuning.Climb, model.VerticalVelocity, 1e-4f, "Shift with a direction held is spent on speed, not the climb");
            model.Tick(Air(descend: true));
            Assert.AreEqual(-Tuning.Descend, model.VerticalVelocity, 1e-4f);
            Assert.IsTrue(model.Descending);
        }

        [Test]
        public void FlyingForwardFollowsTheCameraUpAndDownAndAHoverIgnoresIt()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true));
            model.Tick(Air(move: Vector2.up, pitch: -30.0f));
            Assert.Greater(model.VerticalVelocity, 0.0f, "looking up while flying forward should climb");
            Assert.AreEqual(Mathf.Sin(30.0f * Mathf.Deg2Rad) * Tuning.Cruise, model.VerticalVelocity, 1e-3f);
            model.Tick(Air(move: Vector2.up, pitch: 30.0f));
            Assert.Less(model.VerticalVelocity, 0.0f, "looking down while flying forward should dive");
            model.Tick(Air(pitch: 30.0f));
            Assert.AreEqual(0.0f, model.VerticalVelocity, 1e-4f, "hovering in place, where the camera looks must not sink the body");
            model.Tick(Air(thrust: true, move: Vector2.up, pitch: -30.0f));
            Assert.AreEqual(Tuning.Climb + Mathf.Sin(30.0f * Mathf.Deg2Rad) * Tuning.Cruise, model.VerticalVelocity, 1e-3f, "Space adds to the camera's climb");
        }

        [Test]
        public void ShiftFliesFasterAndTheFlightSpeedStatScalesEverything()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true));
            model.Tick(Air(move: Vector2.up));
            Assert.AreEqual(Tuning.Cruise, model.HorizontalSpeed, 1e-4f);
            model.Tick(Air(move: Vector2.up, boost: true));
            Assert.AreEqual(Tuning.Fast, model.HorizontalSpeed, 1e-4f);
            Assert.IsTrue(model.Boosting);
            model.Tick(Air(thrust: true, move: Vector2.up, boost: true), speedScale: 1.5f);
            Assert.AreEqual(Tuning.Fast * 1.5f, model.HorizontalSpeed, 1e-4f);
            Assert.AreEqual(Tuning.Climb * 1.5f, model.VerticalVelocity, 1e-4f);
        }

        [Test]
        public void AClimbBegunMidFlightIsNoticedOnce()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true));
            model.Tick(Air());
            model.Tick(Air());
            model.Tick(Air(thrust: true));
            Assert.IsTrue(model.ClimbBegan);
            model.Tick(Air(thrust: true));
            Assert.IsFalse(model.ClimbBegan);
        }

        [Test]
        public void StopEndsTheFlightAtOnce()
        {
            var model = new FlightModel(Tuning);
            model.Tick(Air(thrust: true, move: Vector2.up));
            model.Stop();
            Assert.IsFalse(model.Flying);
            Assert.AreEqual(0.0f, model.HorizontalSpeed);
            Assert.AreEqual(0.0f, model.VerticalVelocity);
        }
    }
}
