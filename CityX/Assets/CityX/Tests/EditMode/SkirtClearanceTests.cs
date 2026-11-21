using System.Collections.Generic;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A coat's skirt pushed off the legs (SkirtClearance; owner, 2026-11-21: "when swinging with pretty much any melee weapon the knee clips through the
    /// jacket"): a side's two skirt bones turned about the hip joint, or shifted where turning cannot reach, until the coat's points lie outside.
    /// </summary>
    public sealed class SkirtClearanceTests
    {
        private const int Front = 0, Back = 1, Hips = 2;
        private const float Clearance = 0.015f;

        /// <summary>A panel of one point (bind pose = world at rest, every bone's bindpose the inverse of its place at the joint, the hips' at it too).</summary>
        private static SkirtClearance.Panel PanelOf(Vector3 point, BoneWeight weight)
        {
            Matrix4x4 bind = Matrix4x4.identity;
            return SkirtClearance.Panel.Of(new[] { point }, new[] { weight }, new[] { bind, bind, bind }, Front, Back, 0.3f, 0.0f, 10);
        }

        private static Matrix4x4[] Bones() => new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity };

        private static SkirtClearance.Pose Rest => new SkirtClearance.Pose { Front = Quaternion.identity, Back = Quaternion.identity };

        /// <summary>A thigh raised out to the side, as the melee swings step: from the hip joint (the origin) out and a little down.</summary>
        private static readonly List<SkirtClearance.Limb> SideThigh = new List<SkirtClearance.Limb>
        {
            new SkirtClearance.Limb(new Vector3(0.05f, -0.02f, 0.0f), new Vector3(0.36f, -0.16f, 0.0f), 0.13f, 0.12f),
        };

        [Test]
        public void ASeamPointHalfOnEachBoneIsTurnedOutOfAThighRaisedToTheSide()
        {
            SkirtClearance.Panel panel = PanelOf(new Vector3(0.2f, -0.15f, 0.0f),
                new BoneWeight { boneIndex0 = Front, weight0 = 0.5f, boneIndex1 = Back, weight1 = 0.5f });
            Matrix4x4[] bones = Bones();
            Assert.Greater(SkirtClearance.Deepest(panel, bones, SideThigh, Clearance, out _, out _, out _), 0.02f, "the point is not in the thigh to begin with");
            SkirtClearance.Pose pose = SkirtClearance.Clear(Vector3.zero, Rest, 1.0f, panel, bones, SideThigh, Clearance, 0.2f, 0.15f, 10, out _);
            Assert.AreEqual(0.0f, SkirtClearance.Deepest(panel, bones, SideThigh, Clearance, out _, out _, out _), 1e-6f, "the seam is still in the thigh");
            Assert.Greater(Quaternion.Angle(pose.Front, Quaternion.identity), 1.0f, "the front bone did not turn");
            Assert.Greater(Quaternion.Angle(pose.Back, Quaternion.identity), 1.0f, "the back bone did not turn");
        }

        [Test]
        public void APointNearTheJointIsShiftedOutAsFarAsTheMostAllowed()
        {
            SkirtClearance.Panel panel = PanelOf(new Vector3(0.12f, -0.08f, 0.0f),
                new BoneWeight { boneIndex0 = Front, weight0 = 0.7f, boneIndex1 = Hips, weight1 = 0.3f });
            Matrix4x4[] bones = Bones();
            SkirtClearance.Pose pose = SkirtClearance.Clear(Vector3.zero, Rest, 1.0f, panel, bones, SideThigh, Clearance, 0.2f, 0.15f, 10, out _);
            Assert.Greater(pose.Shift.magnitude, 0.01f, "the side was not shifted off a point turning cannot move");
            Assert.LessOrEqual(pose.Shift.magnitude, 0.15f + 1e-5f, "the side was shifted further than the most allowed");
        }

        /// <summary>
        /// A coat cut close round the legs lies as deep in them as it was fitted without being pushed (owner, 2026-11-21, of the Midnight Coat pushed
        /// open standing still: "there is skin visible around the crotch"); a leg come further in than that is pushed against.
        /// </summary>
        [Test]
        public void OnlyALegComeFurtherInThanTheCoatWasFittedIsPushedAgainst()
        {
            var weight = new BoneWeight { boneIndex0 = Front, weight0 = 0.5f, boneIndex1 = Back, weight1 = 0.5f };
            Vector3 point = new Vector3(0.2f, -0.15f, 0.0f);
            Matrix4x4 bind = Matrix4x4.identity;
            SkirtClearance.Panel fitted = SkirtClearance.Panel.Of(new[] { point }, new[] { weight }, new[] { bind, bind, bind }, Front, Back, 0.3f, 0.0f,
                10, SideThigh, Clearance);
            Assert.Greater(fitted.Fitted[0], 0.02f, "setup: the point is not cut into the leg");
            SkirtClearance.Pose pose = SkirtClearance.Clear(Vector3.zero, Rest, 1.0f, fitted, Bones(), SideThigh, Clearance, 0.2f, 0.15f, 10, out bool moved);
            Assert.IsFalse(moved, "the coat was pushed off a leg standing where it was fitted");
            Assert.AreEqual(0.0f, Quaternion.Angle(pose.Front, Quaternion.identity), 1e-4f);

            var further = new List<SkirtClearance.Limb> { new SkirtClearance.Limb(SideThigh[0].From, SideThigh[0].To + Vector3.down * 0.05f, 0.13f, 0.12f) };
            SkirtClearance.Clear(Vector3.zero, Rest, 1.0f, fitted, Bones(), further, Clearance, 0.2f, 0.15f, 10, out moved);
            Assert.IsTrue(moved, "a leg raised further into the coat was not pushed against");
        }

        [Test]
        public void ACoatClearOfTheLegsIsLeftAsItHangs()
        {
            SkirtClearance.Panel panel = PanelOf(new Vector3(-0.3f, -0.4f, 0.0f), new BoneWeight { boneIndex0 = Front, weight0 = 1.0f });
            SkirtClearance.Pose pose = SkirtClearance.Clear(Vector3.zero, Rest, 1.0f, panel, Bones(), SideThigh, Clearance, 0.2f, 0.15f, 10, out _);
            Assert.AreEqual(0.0f, Quaternion.Angle(pose.Front, Quaternion.identity), 1e-4f);
            Assert.AreEqual(Vector3.zero, pose.Shift);
        }
    }
}
