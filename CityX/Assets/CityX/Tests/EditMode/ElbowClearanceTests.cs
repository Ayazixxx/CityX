using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>An arm through the torso turns round its shoulder-to-hand line until it is out (ElbowClearance); an arm already out is left alone.</summary>
    public sealed class ElbowClearanceTests
    {
        private static readonly Vector3 Bottom = new Vector3(0.0f, 0.9f, 0.0f);
        private static readonly Vector3 Top = new Vector3(0.0f, 2.0f, 0.0f);
        private const float Radius = 0.33f;

        [Test]
        public void AnElbowInsideTheBodyIsTurnedOutWithTheShoulderAndHandKept()
        {
            // The left arm reaching across to a grip in front of the right hip, its elbow bowed back into the belly.
            var shoulder = new Vector3(-0.35f, 1.9f, 0.0f);
            var hand = new Vector3(0.2f, 1.3f, 0.5f);
            var elbow = new Vector3(-0.15f, 1.45f, 0.0f);
            Assert.Less(ElbowClearance.Clearance(shoulder, elbow, hand, Bottom, Top, Radius), 0.0f, "the test's arm should start inside");
            Vector3 moved = ElbowClearance.Elbow(shoulder, elbow, hand, Bottom, Top, Radius);
            Assert.GreaterOrEqual(ElbowClearance.Clearance(shoulder, moved, hand, Bottom, Top, Radius), 0.0f, "the elbow was not turned out of the body");
            Assert.AreEqual(Vector3.Distance(shoulder, elbow), Vector3.Distance(shoulder, moved), 0.001f, "the upper arm changed length");
            Assert.AreEqual(Vector3.Distance(hand, elbow), Vector3.Distance(hand, moved), 0.001f, "the forearm changed length");
        }

        [Test]
        public void AnArmAlreadyOutOfTheBodyIsLeftAlone()
        {
            var shoulder = new Vector3(0.35f, 1.9f, 0.0f);
            var elbow = new Vector3(0.48f, 1.45f, -0.05f);
            var hand = new Vector3(0.55f, 1.05f, 0.1f);
            Assert.AreEqual(elbow, ElbowClearance.Elbow(shoulder, elbow, hand, Bottom, Top, Radius));
        }

        [Test]
        public void OfTheWaysOutTheElbowGoesLowNotUpLikeAWing()
        {
            var shoulder = new Vector3(-0.35f, 1.9f, 0.0f);
            var hand = new Vector3(0.2f, 1.3f, 0.5f);
            Vector3 moved = ElbowClearance.Elbow(shoulder, new Vector3(-0.15f, 1.45f, 0.0f), hand, Bottom, Top, Radius);
            Assert.Less(moved.y, shoulder.y, "the elbow went up level with the shoulder");
        }
    }
}
