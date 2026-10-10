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
            Assert.Greater(steady, 3.0f, "a sprint should lean forward a little");
            Assert.Greater(settingOff, steady, "the push off leans further than the steady run");
            Assert.LessOrEqual(lean.Degrees, BodyLean.MaxLeanDegrees + 1e-3f);
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
