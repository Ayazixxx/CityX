using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The body's weight (MovementWeight): speed builds and falls over a few steps, a turn keeps its grip, and a giant takes the same time.</summary>
    public sealed class MovementWeightTests
    {
        private const float Step = 1.0f / 60.0f;

        private static float SecondsTo(Vector3 from, Vector3 target, float scale = 1.0f)
        {
            Vector3 v = from;
            float t = 0.0f;
            while ((v - target).magnitude > 0.01f && t < 10.0f)
            {
                v = MovementWeight.Step(v, target, scale, Step);
                t += Step;
            }
            return t;
        }

        [Test]
        public void ASprintBuildsInAboutHalfASecondAndStopsInUnderOne()
        {
            Vector3 sprint = Vector3.forward * 7.5f;
            Assert.AreEqual(0.54f, SecondsTo(Vector3.zero, sprint), 0.05f, "the build-up to a sprint");
            Assert.AreEqual(0.38f, SecondsTo(sprint, Vector3.zero), 0.05f, "the stop from a sprint");
            Assert.AreEqual(0.29f, SecondsTo(Vector3.zero, Vector3.forward * 4.0f), 0.05f, "the build-up to a walk");
        }

        [Test]
        public void ATurnLosesItsDriftFastAndKeepsItsPaceAlongTheNewWay()
        {
            // A right angle at a sprint with the body already facing the new way: the old speed is sideways, taken off by the grip.
            Vector3 v = Vector3.forward * 7.5f;
            Vector3 target = Vector3.right * 7.5f;
            for (float t = 0.0f; t < 0.2f; t += Step)
            {
                v = MovementWeight.Step(v, target, 1.0f, Step);
            }
            Assert.AreEqual(0.0f, v.z, 0.01f, "the body is still sliding the old way after a fifth of a second: it feels like ice");
            Assert.Greater(v.x, 2.0f);
            Assert.Less(v.x, 7.5f, "the new way's speed came at once: no weight");
        }

        [Test]
        public void GoingBackwardBrakesFirstAtTheBrakingRate()
        {
            Vector3 next = MovementWeight.Step(Vector3.forward * 5.0f, Vector3.back * 5.0f, 1.0f, 0.1f);
            Assert.AreEqual(5.0f - MovementWeight.Braking * 0.1f, next.z, 1e-3f);
        }

        [Test]
        public void AGiantTakesTheSameTimeAndWetGroundLonger()
        {
            Assert.AreEqual(SecondsTo(Vector3.zero, Vector3.forward * 4.0f), SecondsTo(Vector3.zero, Vector3.forward * 12.8f, 3.2f), 0.03f);
            Assert.Greater(SecondsTo(Vector3.zero, Vector3.forward * 4.0f, 0.6f), SecondsTo(Vector3.zero, Vector3.forward * 4.0f));
        }
    }
}
