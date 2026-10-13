using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The whole body's lean from its motion (Character.BodyLean): into acceleration, braking and turns, a little into speed.</summary>
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
        public void SettingOffLeansForwardMoreThanRunningSteady()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * Mathf.Min(7.5f, i * 0.3f), 25);   // speeding up
            float settingOff = Vector3.Dot(lean.Tilt, Vector3.forward);
            Run(lean, i => Vector3.forward * 7.5f, 120);                     // a steady sprint
            float steady = Vector3.Dot(lean.Tilt, Vector3.forward);
            // Toned down 2026-10-13: a sprint leans a couple of degrees, never anything like the thirteen it used to.
            Assert.Greater(steady, 1.5f, "a sprint should lean forward a little");
            Assert.Less(steady, 4.0f, "a sprint leans too far (the owner found the old lean too apparent)");
            Assert.Greater(settingOff, steady, "the push off leans further than the steady run");
            Assert.LessOrEqual(lean.Degrees, BodyLean.MaxLeanDegrees + 1e-3f);
        }

        [Test]
        public void AHillLeansTheBodyIntoItAndTheCapStillHolds()
        {
            var lean = new BodyLean();
            // Walking up a 20 degree hill at 3 m/s: the slope asks for 7 degrees forward on top of the speed lean.
            Vector3 slope = Vector3.forward * (20.0f * BodyLean.UphillLeanFraction);
            for (int i = 0; i < 120; i++)
            {
                lean.Step(Vector3.forward * 3.0f, true, Dt, slope);
            }
            float forward = Vector3.Dot(lean.Tilt, Vector3.forward);
            Assert.Greater(forward, 3.0f * BodyLean.SpeedLeanDegreesPerMetrePerSecond + 5.0f, "the hill did not lean the body into it");
            Assert.LessOrEqual(lean.Degrees, BodyLean.MaxLeanDegrees * 2.0f + 1e-3f);
            // Downhill: a little back.
            var down = new BodyLean();
            for (int i = 0; i < 120; i++)
            {
                down.Step(Vector3.forward * 3.0f, true, Dt, Vector3.forward * (-20.0f * BodyLean.DownhillLeanFraction));
            }
            Assert.Less(Vector3.Dot(down.Tilt, Vector3.forward), 3.0f * BodyLean.SpeedLeanDegreesPerMetrePerSecond, "going downhill should lean back from the speed lean");
        }

        [Test]
        public void StoppingHardLeansBack()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * 7.5f, 120);
            Run(lean, i => Vector3.forward * Mathf.Max(0.0f, 7.5f - (i + 1) * 0.6f), 10);
            Assert.Less(Vector3.Dot(lean.Tilt, Vector3.forward), 0.0f, "braking from a sprint should lean back");
        }

        [Test]
        public void ATurnLeansIntoItsInside()
        {
            var lean = new BodyLean();
            // Running a circle turning left: the inside of the turn is to the left of the way the body goes.
            float yawRate = 120.0f;
            Run(lean, i => Quaternion.Euler(0, -yawRate * i * Dt, 0) * Vector3.forward * 6.0f, 90);
            Vector3 heading = Quaternion.Euler(0, -yawRate * 89 * Dt, 0) * Vector3.forward;
            Vector3 left = Vector3.Cross(heading, Vector3.up);
            Assert.Greater(Vector3.Dot(lean.Tilt, left), 2.0f, "turning left should lean left, into the turn");
        }

        [Test]
        public void InTheAirOrStoodStillTheBodyIsUpright()
        {
            var lean = new BodyLean();
            Run(lean, i => Vector3.forward * 7.5f, 60);
            Run(lean, i => Vector3.forward * 7.5f, 90, grounded: false);
            Assert.Less(lean.Degrees, 0.1f, "airborne, the lean should be gone");
            Run(lean, i => Vector3.zero, 120);
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
    }
}
