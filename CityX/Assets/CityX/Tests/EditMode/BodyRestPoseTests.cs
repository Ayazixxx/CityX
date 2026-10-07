using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The body is modelled the way the Godot character is: arms HANGING, each arm a capsule from the shoulder to the full hand target (not the wrist), and bound
    /// that way even though the live skeleton starts as a T-pose. Before this the arms were modelled out sideways and stopped at the wrist, so every arm was 15%
    /// short and every clip bent a shoulder through 77 degrees of skin.
    /// </summary>
    public sealed class BodyRestPoseTests
    {
        [Test]
        public void EachArmCapsuleRunsFromTheShoulderToTheFullHandTarget()
        {
            List<FusedBodyShape.Segment> limbs = BodyRigBuilder.LimbCapsules();
            FusedBodyShape.Segment left = limbs.Find(s => s.Bone == HumanBodyBones.LeftUpperArm);
            FusedBodyShape.Segment right = limbs.Find(s => s.Bone == HumanBodyBones.RightUpperArm);

            Assert.Less(Vector3.Distance(left.A, new Vector3(-BodyRig.ShoulderX, BodyRig.ShoulderY, 0.0f)), 1e-4f);
            Assert.Less(Vector3.Distance(left.B, new Vector3(-BodyRig.HandX, BodyRig.HandY, 0.0f)), 1e-4f, "the left arm stops short of fused_body.gd's HAND_X, HAND_Y");
            Assert.Less(Vector3.Distance(right.B, new Vector3(BodyRig.HandX, BodyRig.HandY, 0.0f)), 1e-4f, "the right arm stops short of fused_body.gd's HAND_X, HAND_Y");
            Assert.AreEqual(BodyRig.ArmLength, Vector3.Distance(left.A, left.B), 1e-4f, "the arm is not the original's length");
        }

        [Test]
        public void TheArmsHangDownAndOutwardNotSidewaysLikeATPose()
        {
            FusedBodyShape.Segment left = BodyRigBuilder.LimbCapsules().Find(s => s.Bone == HumanBodyBones.LeftUpperArm);
            Assert.Less(left.B.y, left.A.y - 0.5f, "the modelled arm does not hang");
            Assert.Less(left.B.x, left.A.x, "the left arm should angle away from the body as it goes down");
        }

        [Test]
        public void TheSkinsArmBonesSitAtTheSameJointsAsTheShapesArmsAtRest()
        {
            // The bones of the shape's arm (shoulder, elbow, wrist) are where the skin's bones are at the arms-down rest: the elbow halfway down the capsule, the
            // wrist 85% of the way (fused_body.gd's own ELBOW_T and WRIST_T).
            foreach (BodyRig.Bone bone in BodyRig.RestBones())
            {
                if (bone.Human == HumanBodyBones.LeftLowerArm)
                {
                    Assert.Less(Vector3.Distance(bone.Position, BodyRig.ShapePositionOf(HumanBodyBones.LeftLowerArm)), 1e-4f, "the forearm bone is not at the elbow of the shape");
                }
                if (bone.Human == HumanBodyBones.LeftHand)
                {
                    Vector3 wrist = Vector3.Lerp(BodyRig.ShapePositionOf(HumanBodyBones.LeftUpperArm), BodyRig.ShapePositionOf(HumanBodyBones.LeftHand), BodyRig.WristT);
                    Assert.Less(Vector3.Distance(bone.Position, wrist), 1e-4f, "the hand bone is not at the wrist of the shape");
                }
            }
        }

        [Test]
        public void TurningTheTPoseShoulderByTheArmDownAngleLandsTheLimbOnTheShapesOwnJoints()
        {
            // The live skeleton is a T-pose. Hanging it by exactly ArmDownDegrees has to land the elbow and the wrist where the hanging shape has them, or binding
            // the shape arms-down against these bones would shear every shoulder by the difference.
            GameObject root = BodyRigBuilder.BuildSkeleton("Test", out Dictionary<string, Transform> bones);
            try
            {
                foreach (float side in new[] { -1.0f, 1.0f })
                {
                    string suffix = side < 0.0f ? "_l" : "_r";
                    bones["arm" + suffix].localRotation = Quaternion.Euler(0.0f, 0.0f, -side * BodyRig.ArmDownDegrees);
                    Vector3 shoulder = new Vector3(side * BodyRig.ShoulderX, BodyRig.ShoulderY, 0.0f);
                    Vector3 hand = new Vector3(side * BodyRig.HandX, BodyRig.HandY, 0.0f);
                    Vector3 elbowAtRest = Vector3.Lerp(shoulder, hand, BodyRig.ElbowT);
                    Vector3 wristAtRest = Vector3.Lerp(shoulder, hand, BodyRig.WristT);
                    Assert.Less(Vector3.Distance(bones["forearm" + suffix].position, elbowAtRest), 0.005f, $"the {suffix} elbow does not land on the shape's elbow");
                    Assert.Less(Vector3.Distance(bones["hand" + suffix].position, wristAtRest), 0.005f, $"the {suffix} wrist does not land on the shape's wrist");
                    Assert.Less(Vector3.Distance(bones["fingertip" + suffix].position, hand), 0.005f, $"the {suffix} fingertip does not land on the shape's hand target");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheArmBonesAreBoundAtTheArmsDownPoseAndTheRestAreNot()
        {
            Matrix4x4 left = BodyRig.RestBindMatrix(HumanBodyBones.LeftUpperArm);
            Vector3 alongTheTPoseArm = left.MultiplyVector(Vector3.left);
            Vector3 hang = (BodyRig.ShapePositionOf(HumanBodyBones.LeftHand) - BodyRig.ShapePositionOf(HumanBodyBones.LeftUpperArm)).normalized;
            Assert.Greater(Vector3.Dot(alongTheTPoseArm, hang), 0.9999f, "the arm bone's bind rotation does not turn the T-pose arm onto the hanging one");

            Matrix4x4 right = BodyRig.RestBindMatrix(HumanBodyBones.RightUpperArm);
            Vector3 rightHang = (BodyRig.ShapePositionOf(HumanBodyBones.RightHand) - BodyRig.ShapePositionOf(HumanBodyBones.RightUpperArm)).normalized;
            Assert.Greater(Vector3.Dot(right.MultiplyVector(Vector3.right), rightHang), 0.9999f);

            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightLowerLeg })
            {
                Matrix4x4 bind = BodyRig.RestBindMatrix(bone);
                Assert.Greater(Vector3.Dot(bind.MultiplyVector(Vector3.right), Vector3.right), 0.9999f, $"{bone} is bound turned");
                Assert.Less(Vector3.Distance(bind.MultiplyPoint3x4(Vector3.zero), BodyRig.PositionOf(bone)), 1e-4f, $"{bone} is bound somewhere other than its own joint");
            }
        }

        [Test]
        public void TheBakedBodyIsBoundArmsDownAndItsHandsReachTheOriginalsMidThigh()
        {
            var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Assert.IsNotNull(mesh, "the body mesh asset is missing");
            Bounds bounds = mesh.bounds;
            // The hand end is at HAND_X (0.58) with the capsule's radius and a blend's worth of skin beyond it.
            Assert.Greater(bounds.max.x, BodyRig.HandX + FusedBodyShape.ArmRadius - 0.03f, "the hands do not reach where the original's do");
            Assert.Less(bounds.max.x, BodyRig.HandX + FusedBodyShape.ArmRadius + 0.1f, "the body is wider than the original's arms: T-pose?");
            Assert.Less(bounds.size.x, 1.4f, "the baked body is laid out sideways");
        }
    }
}
