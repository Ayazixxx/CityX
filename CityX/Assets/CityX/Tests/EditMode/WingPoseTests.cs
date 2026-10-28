using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How a wing's bones turn (docs/WINGS.md): open, the wing reaches out; folded, it stands up behind the shoulder with the hand pointing
    /// down; the stroke lifts and lowers the tip; the feathers fan open and closed and twist against the stroke; the left is the right's mirror.
    /// </summary>
    public sealed class WingPoseTests
    {
        /// <summary>Where the wingtip ends up for a pose, in the wing root's frame (X out, Y up, Z forward), on a right wing of unit bones.</summary>
        private static Vector3 Tip(in WingPose.Arm arm)
        {
            Vector3 elbow = arm.Humerus * Vector3.right;
            Vector3 wrist = elbow + arm.Humerus * arm.Forearm * Vector3.right;
            return wrist + arm.Humerus * arm.Forearm * arm.Hand * Vector3.right;
        }

        [Test]
        public void OpenTheWingReachesOutAndFoldedItStandsBehindTheShoulderHandDown()
        {
            Vector3 open = Tip(WingPose.ArmPose(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            Assert.Greater(open.x, 2.4f, "open, the tip should be far out to the side");
            Vector3 folded = Tip(WingPose.ArmPose(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            Assert.Less(folded.x, 1.2f, "folded, the tip should be drawn in to the body");
            Assert.Less(folded.z, -0.3f, "folded, the wing should lie behind the back");
            Vector3 elbow = WingPose.ArmPose(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f).Humerus * Vector3.right;
            Assert.Greater(elbow.y, 0.6f, "folded, the elbow should stand up above the shoulder");
            WingPose.Arm foldedArm = WingPose.ArmPose(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            Vector3 handDirection = foldedArm.Humerus * foldedArm.Forearm * foldedArm.Hand * Vector3.right;
            Assert.Less(handDirection.y, -0.3f, "folded, the hand (and its primaries) should point down");
        }

        [Test]
        public void TheStrokeLiftsAndLowersTheTipMoreAtTheTipThanTheShoulder()
        {
            Vector3 up = Tip(WingPose.ArmPose(0.0f, 0.0f, 0.0f, 30.0f, 13.5f, 10.5f));
            Vector3 level = Tip(WingPose.ArmPose(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            Vector3 down = Tip(WingPose.ArmPose(0.0f, 0.0f, 0.0f, -30.0f, -13.5f, -10.5f));
            Assert.Greater(up.y, level.y + 1.0f);
            Assert.Less(down.y, level.y - 1.0f);
            Vector3 elbowUp = WingPose.ArmPose(0.0f, 0.0f, 0.0f, 30.0f, 0.0f, 0.0f).Humerus * Vector3.right;
            Assert.Greater(up.y - level.y, elbowUp.y * 1.5f, "the tip should rise further than the elbow: the wing whips");
        }

        [Test]
        public void AGlideSweepsTheHandBackAndATuckDrawsTheWholeWingBack()
        {
            Vector3 level = Tip(WingPose.ArmPose(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            Vector3 glide = Tip(WingPose.ArmPose(0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f));
            Vector3 tuck = Tip(WingPose.ArmPose(0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f));
            Assert.Less(glide.z, level.z - 0.3f, "gliding, the tip should be swept back");
            Assert.Less(tuck.z, glide.z, "tucked, further back still");
            Assert.Less(tuck.x, level.x - 0.3f, "tucked, the wing is drawn in");
        }

        [Test]
        public void TheFeathersFanOpenInFlightCloseFoldedAndTwistAgainstTheStroke()
        {
            Assert.AreEqual(1.0f, WingPose.SpreadShare(0.0f, 0.0f, 0.0f), 1e-4f);
            Assert.Less(WingPose.SpreadShare(1.0f, 0.0f, 0.0f), 0.2f, "folded, the fan closes");
            Assert.Less(WingPose.SpreadShare(0.0f, 1.0f, 0.0f), 0.8f, "gliding, it closes a little");
            Vector3 still = WingPose.FeatherPose(60.0f, 1.0f, 1.0f, 0.0f) * Vector3.right;
            Assert.AreEqual(Mathf.Cos(60.0f * Mathf.Deg2Rad), still.x, 1e-3f, "fanned back by its built spread");
            Assert.Less(still.z, -0.8f, "a feather points back from the wing");
            // Through the downstroke (the stroke moving down: negative) the trailing edge (-Z) should trail UP.
            Vector3 trailing = WingPose.FeatherPose(60.0f, 1.0f, 1.0f, -300.0f) * Vector3.back;
            Assert.Greater(trailing.y, 0.1f, "on the downstroke the vane should bend up behind the stroke");
            Vector3 tipTwist = WingPose.FeatherPose(0.0f, 1.0f, 1.0f, -100.0f) * Vector3.back;
            Vector3 rootTwist = WingPose.FeatherPose(0.0f, 1.0f, 0.0f, -100.0f) * Vector3.back;
            Assert.Greater(tipTwist.y, rootTwist.y, "feathers twist more toward the wingtip");
        }

        [Test]
        public void TheLeftWingIsTheRightsMirror()
        {
            WingPose.Arm arm = WingPose.ArmPose(0.3f, 0.2f, 0.0f, 25.0f, 10.0f, 6.0f);
            Vector3 right = Tip(arm);
            var mirrored = new WingPose.Arm { Humerus = WingPose.Mirror(arm.Humerus), Forearm = WingPose.Mirror(arm.Forearm), Hand = WingPose.Mirror(arm.Hand) };
            Vector3 leftElbow = mirrored.Humerus * Vector3.left;
            Vector3 leftWrist = leftElbow + mirrored.Humerus * mirrored.Forearm * Vector3.left;
            Vector3 left = leftWrist + mirrored.Humerus * mirrored.Forearm * mirrored.Hand * Vector3.left;
            Assert.AreEqual(-right.x, left.x, 1e-4f);
            Assert.AreEqual(right.y, left.y, 1e-4f);
            Assert.AreEqual(right.z, left.z, 1e-4f);
        }
    }
}
