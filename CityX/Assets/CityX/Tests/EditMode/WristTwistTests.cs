using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The wrist's twist goes into the forearm: the hand keeps its world rotation, the forearm turns about its own length, up to a limit.</summary>
    public sealed class WristTwistTests
    {
        private Transform _lower;
        private Transform _hand;

        [SetUp]
        public void SetUp()
        {
            _lower = new GameObject("forearm").transform;
            _lower.position = Vector3.zero;
            _hand = new GameObject("hand").transform;
            _hand.SetParent(_lower, false);
            _hand.localPosition = new Vector3(0.3f, 0.0f, 0.0f);   // the forearm runs along +X
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_lower.gameObject);
        }

        [Test]
        public void TheTwistAboutAnAxisIsMeasured()
        {
            Quaternion q = Quaternion.AngleAxis(40.0f, Vector3.right);
            Assert.AreEqual(40.0f, WristTwist.TwistDegrees(q, Vector3.right), 0.01f);
            Assert.AreEqual(-40.0f, WristTwist.TwistDegrees(q, Vector3.left), 0.01f);
            // A pure swing about another axis has no twist about this one.
            Assert.AreEqual(0.0f, WristTwist.TwistDegrees(Quaternion.AngleAxis(50.0f, Vector3.up), Vector3.right), 0.01f);
        }

        [Test]
        public void TheForearmTakesTheTwistAndTheHandStaysWhereItWas()
        {
            _hand.localRotation = Quaternion.AngleAxis(60.0f, Vector3.right) * Quaternion.AngleAxis(20.0f, Vector3.up);
            Quaternion handBefore = _hand.rotation;

            float left = WristTwist.ShareWithForearm(_lower, _hand);

            Assert.AreEqual(0.0f, left, 0.01f, "twist was left in the wrist");
            Assert.Less(Quaternion.Angle(handBefore, _hand.rotation), 0.01f, "the hand's world rotation changed");
            Assert.AreEqual(60.0f, Mathf.Abs(WristTwist.TwistDegrees(_lower.rotation, Vector3.right)), 0.1f, "the forearm did not take the twist");
            Assert.AreEqual(0.0f, WristTwist.TwistDegrees(_hand.localRotation, Vector3.right), 0.1f, "the wrist still twists");
        }

        [Test]
        public void TurningThroughTheHalfTurnTheForearmDoesNotFlip()
        {
            // A backhand rolls the sword hand through 180 degrees against the forearm. Measured fresh each frame the twist reads +179 then
            // -179, and the forearm, clamped either way, flipped 200 degrees in one frame (the pose watchdog caught it at 5000+ deg/s).
            // Carried on from last frame's, it stays at its limit instead.
            float last = 0.0f;
            float previousTake = 0.0f;
            float worstJump = 0.0f;
            for (float degrees = 120.0f; degrees <= 240.0f; degrees += 5.0f)
            {
                _lower.rotation = Quaternion.identity;   // the forearm comes fresh from the IK every frame
                _hand.localRotation = Quaternion.AngleAxis(degrees, Vector3.right);
                WristTwist.ShareWithForearm(_lower, _hand, ref last);
                float take = WristTwist.TwistDegrees(_lower.rotation, Vector3.right);
                if (degrees > 120.0f)
                {
                    worstJump = Mathf.Max(worstJump, Mathf.Abs(Mathf.DeltaAngle(previousTake, take)));
                }
                previousTake = take;
            }
            Assert.Less(worstJump, 10.0f, $"the forearm jumped {worstJump:0} degrees in one step as the hand turned through the half turn");
        }

        [Test]
        public void TheForearmStopsAtItsLimitAndTheRestStaysInTheWrist()
        {
            _hand.localRotation = Quaternion.AngleAxis(150.0f, Vector3.right);
            float left = WristTwist.ShareWithForearm(_lower, _hand, 100.0f);
            Assert.AreEqual(50.0f, left, 0.1f);
            Assert.AreEqual(100.0f, WristTwist.TwistDegrees(_lower.rotation, Vector3.right), 0.1f);
            Assert.AreEqual(50.0f, WristTwist.TwistDegrees(_hand.localRotation, Vector3.right), 0.1f);
        }
    }
}
