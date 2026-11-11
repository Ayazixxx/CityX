using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The one move out of every wall a body's parts are in (WallClearance; owner, 2026-11-10: "stop at contact with the wall").</summary>
    public sealed class WallClearanceTests
    {
        private const float Squeeze = -0.5f;

        [Test]
        public void OneContactIsLeftByExactlyItsWayOut()
        {
            Vector3 push = WallClearance.Combined(new[] { new Vector3(-0.12f, 0.0f, 0.0f) }, 1, Squeeze);
            Assert.AreEqual(new Vector3(-0.12f, 0.0f, 0.0f), push);
        }

        [Test]
        public void TwoContactsAlongOneWallAreLeftByTheDeeper()
        {
            Vector3 push = WallClearance.Combined(new[] { new Vector3(-0.05f, 0, 0), new Vector3(-0.12f, 0, 0) }, 2, Squeeze);
            Assert.AreEqual(-0.12f, push.x, 1e-5f, "the arm and the hand in one wall did not move the body as far as the deeper of them");
        }

        [Test]
        public void InACornerTheBodyGoesOutOfBothWalls()
        {
            Vector3 push = WallClearance.Combined(new[] { new Vector3(-0.1f, 0, 0), new Vector3(0, 0, -0.08f) }, 2, Squeeze);
            Assert.AreEqual(-0.1f, push.x, 1e-5f);
            Assert.AreEqual(-0.08f, push.z, 1e-5f);
        }

        /// <summary>Kept off by each frame's arms alone, the body went in and out 13 cm a stride as the near arm swung out and in.</summary>
        [Test]
        public void AWallIsKeptOffAsFarAsTheWidestSwingNeededAndLetBackOnlySlowly()
        {
            var standoff = new WallStandoff();
            var needs = new Vector3[4];
            var root = Vector3.zero;
            // The arm swung out 12 cm into a wall to the right.
            Assert.AreEqual(1, standoff.Needs(root, new[] { new Vector3(-0.12f, 0, 0) }, 1, 0.0f, 1.0f, needs));
            Assert.AreEqual(-0.12f, needs[0].x, 1e-5f);
            root = new Vector3(-0.12f, 0, 0);

            // It swings back in: no contact, and the walk carries the body toward the wall; it is let only a little way back.
            root.x += 0.10f;
            int count = standoff.Needs(root, new Vector3[0], 0, 0.1f, 1.0f, needs);
            Assert.AreEqual(1, count, "the wall was forgotten as soon as the arm swung away from it");
            Assert.AreEqual(-(0.10f - WallStandoff.EasesInMetresPerSecond * 0.1f), needs[0].x, 1e-4f, "the body was let back too far");

            // Untouched for longer than a stride, the wall is forgotten.
            standoff.Needs(root, new Vector3[0], 0, WallStandoff.ForgetSeconds + 0.1f, 1.0f, needs);
            Assert.AreEqual(0, standoff.Count);
        }

        [Test]
        public void AWallTheBodyWalksAwayFromIsForgotten()
        {
            var standoff = new WallStandoff();
            var needs = new Vector3[4];
            standoff.Needs(Vector3.zero, new[] { new Vector3(-0.1f, 0, 0) }, 1, 0.0f, 1.0f, needs);
            Assert.AreEqual(0, standoff.Needs(new Vector3(-0.1f - WallStandoff.ClearOf - 0.05f, 0, 0), new Vector3[0], 0, 0.02f, 1.0f, needs));
            Assert.AreEqual(0, standoff.Count, "a wall left behind is still remembered");
        }

        [Test]
        public void SqueezedFromBothSidesTheBodyIsNotShaken()
        {
            Vector3 push = WallClearance.Combined(new[] { new Vector3(-0.1f, 0, 0), new Vector3(0.07f, 0, 0) }, 2, Squeeze);
            Assert.AreEqual(Vector3.zero, push, "a gap narrower than the arms pushed the body one way and then the other");
        }
    }
}
