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
