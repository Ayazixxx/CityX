using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The whole body's lean from its motion (Character.BodyLean, second version): the feet's centre trails the head, so a run leans forward, a push-off
    /// overshoots, a hard stop braces back, a turn leans into itself, and in the air or stood still the body is upright.
    /// </summary>
    public sealed class BodyLeanTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static void Run(BodyLean lean, System.Func<int, Vector3> velocityAt, int frames, bool grounded = true)
        {
            for (int i = 0; i < frames; i++)
            {
                lean.Step(velocityAt(i), grounded, Dt);
            }
        }

        [Test]
        public void ASteadySprintLeansForwardModestlyAndSettingOffLeansFurther()
        {
            var lean = new BodyLean();
            float mostForward = 0.0f;
            for (int i = 0; i < 60; i++)
            {
                lean.Step(Vector3.forward * 7.5f, true, Dt);
                mostForward = Mathf.Max(mostForward, Vector3.Dot(lean.Tilt, Vector3.forward));
            }
            Run(lean, i => Vector3.forward * 7.5f, 120);
            float steady = Vector3.Dot(lean.Tilt, Vector3.forward);
            float expected = Mathf.Atan(7.5f * BodyLean.LagSeconds / BodyLean.PivotHeight) * Mathf.Rad2Deg;
            Assert.AreEqual(expected, steady, 0.3f, "a steady sprint should hold the feet a fixed lag behind the head");
            Assert.Greater(steady, 5.0f, "a sprint should lean forward");
            Assert.Less(steady, 10.0f, "a sprint leans too far");
            Assert.Greater(mostForward, steady + 0.5f, "the push off should overshoot the steady lean");
            Assert.LessOrEqual(lean.Degrees, BodyLean.MaxLeanDegrees + 1e-3f);
            Assert.AreEqual(7.5f * BodyLean.LagSeconds, Vector3.Dot(lean.FootLag, Vector3.forward), 0.01f, "the feet should trail the head by the lag");
        }

        [Test]
        public void StoppingHardSwingsTheFeetOutInFrontAndLeansBack()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * 7.5f, 120);
            float mostBack = 0.0f;
            float mostAhead = 0.0f;
            for (int i = 0; i < 60; i++)
            {
                lean.Step(Vector3.zero, true, Dt);
                mostBack = Mathf.Min(mostBack, Vector3.Dot(lean.Tilt, Vector3.forward));
                mostAhead = Mathf.Min(mostAhead, Vector3.Dot(lean.FootLag, Vector3.forward));
            }
            Assert.Less(mostBack, -1.0f, "braking from a sprint should lean back");
            Assert.Less(mostAhead, -0.03f, "the feet should overshoot to in front of the head on a hard stop");
            Run(lean, i => Vector3.zero, 180);
            Assert.Less(lean.Degrees, 0.1f, "stood still, upright again");
        }

        [Test]
        public void ATurnLeansIntoItsInside()
        {
            var lean = new BodyLean();
            // Running a circle turning left: the inside of the turn is to the left of the way the body goes; the feet trail on the outside.
            float yawRate = 120.0f;
            Run(lean, i => Quaternion.Euler(0, -yawRate * i * Dt, 0) * Vector3.forward * 6.0f, 90);
            Vector3 heading = Quaternion.Euler(0, -yawRate * 89 * Dt, 0) * Vector3.forward;
            Vector3 left = Vector3.Cross(heading, Vector3.up);
            Assert.Greater(Vector3.Dot(lean.Tilt, left), 1.0f, "turning left should lean left, into the turn");
        }

        [Test]
        public void InTheAirOrStoodStillTheBodyIsUpright()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * 7.5f, 60);
            Run(lean, i => Vector3.forward * 7.5f, 120, grounded: false);
            Assert.Less(lean.Degrees, 0.1f, "airborne, the lean should be gone");
            Run(lean, i => Vector3.zero, 180);
            Assert.Less(lean.Degrees, 0.1f, "standing still, upright");
        }

        [Test]
        public void TheLeanTipsTheTopTowardItsDirection()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * 7.5f, 120);
            Vector3 top = lean.Rotation() * Vector3.up;
            Assert.Greater(top.z, 0.03f, "leaning forward should move the head forward of the feet");
        }

        [Test]
        public void AHillLeansTheBodyIntoItAndTheCapStillHolds()
        {
            var lean = new BodyLean();
            // Walking up a 20 degree hill at 3 m/s: the slope asks for 7 degrees forward on top of the run lean.
            Vector3 slope = Vector3.forward * (20.0f * BodyLean.UphillLeanFraction);
            for (int i = 0; i < 180; i++)
            {
                lean.Step(Vector3.forward * 3.0f, true, Dt, slope);
            }
            float runLean = Mathf.Atan(3.0f * BodyLean.LagSeconds / BodyLean.PivotHeight) * Mathf.Rad2Deg;
            Assert.Greater(Vector3.Dot(lean.Tilt, Vector3.forward), runLean + 5.0f, "the hill did not lean the body into it");
            Assert.LessOrEqual(lean.Degrees, BodyLean.MaxLeanDegrees * 2.0f + 1e-3f);
            var down = new BodyLean();
            for (int i = 0; i < 180; i++)
            {
                down.Step(Vector3.forward * 3.0f, true, Dt, Vector3.forward * (-20.0f * BodyLean.DownhillLeanFraction));
            }
            Assert.Less(Vector3.Dot(down.Tilt, Vector3.forward), runLean, "going downhill should lean back from the run lean");
        }
    }
}
