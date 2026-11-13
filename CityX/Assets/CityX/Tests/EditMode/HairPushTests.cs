using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What worn hair is kept out of (HairPush; owner, 2026-11-12: "is there a better fix for the shirt spikes stabbing through the shirt than them bending?"): the
    /// body's own torso, shoulder bar, neck column and upper arms, as the body is built, and the hair's material asking the shader for the push.
    /// </summary>
    public sealed class HairPushTests
    {
        private static FusedBodyShape.Segment[] UpperBodyAtRest()
        {
            var pieces = new FusedBodyShape.Segment[HairPush.PiecesPerBody];
            Assert.AreEqual(HairPush.PiecesPerBody, FusedBodyShape.UpperBody(BodyRig.ShapePositionOf, FusedBodyShape.ReferenceHeight, pieces));
            return pieces;
        }

        private static void AssertSame(FusedBodyShape.Segment body, FusedBodyShape.Segment piece, string what)
        {
            Assert.Less(Vector3.Distance(body.A, piece.A), 1e-5f, $"{what} starts elsewhere than the body's");
            Assert.Less(Vector3.Distance(body.B, piece.B), 1e-5f, $"{what} ends elsewhere than the body's");
            Assert.AreEqual(body.Radius, piece.Radius, 1e-5f, $"{what} is not as thick as the body's");
        }

        [Test]
        public void ThePiecesAreTheBodysOwnTorsoShouldersAndNeck()
        {
            FusedBodyShape.Segment[] pieces = UpperBodyAtRest();
            List<FusedBodyShape.Segment> body = BodyRigBuilder.LimbCapsules();
            // LimbCapsules: the torso, the pelvis bar, the shoulder bar, the neck column, then the limbs.
            AssertSame(body[0], pieces[0], "the torso");
            AssertSame(body[2], pieces[1], "the shoulder bar");
            AssertSame(body[3], pieces[2], "the neck column");
        }

        [Test]
        public void EachUpperArmRunsFromTheArmsRootToItsElbowAsThickAsTheArm()
        {
            FusedBodyShape.Segment[] pieces = UpperBodyAtRest();
            List<FusedBodyShape.Segment> body = BodyRigBuilder.LimbCapsules();
            foreach ((int index, HumanBodyBones upper, HumanBodyBones elbow) in new[]
                     { (3, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (4, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm) })
            {
                FusedBodyShape.Segment arm = body.Find(s => s.Bone == upper);
                Assert.Less(Vector3.Distance(arm.A, pieces[index].A), 1e-5f, $"the {upper} piece does not start at the arm's root");
                Assert.Less(Vector3.Distance(BodyRig.ShapePositionOf(elbow), pieces[index].B), 1e-5f, $"the {upper} piece does not end at the elbow");
                Assert.AreEqual(arm.Radius, pieces[index].Radius, 1e-5f, $"the {upper} piece is not as thick as the arm at its root");
            }
        }

        [Test]
        public void TheHairsMaterialIsPushed()
        {
            var hair = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            Assert.IsTrue(hair.IsKeywordEnabled(HairPush.Keyword), "the hair is not pushed out of the body");
            Assert.IsTrue(hair.shader.keywordSpace.FindKeyword(HairPush.Keyword).isValid, $"{hair.shader.name} does not know {HairPush.Keyword}");
        }

        [Test]
        public void ThereIsRoomForEveryPieceOfTheNearestBodies()
        {
            Assert.AreEqual(HairPush.MaxBodies * HairPush.PiecesPerBody, HairPush.MaxPieces);
            string shader = System.IO.File.ReadAllText("Assets/CityX/View/Shaders/CityXHairPush.hlsl");
            StringAssert.Contains($"#define CITYX_HAIR_PIECES_MAX {HairPush.MaxPieces}", shader, "the shader holds a different number of pieces");
            StringAssert.Contains($"#define CITYX_BACK_BOARDS_MAX {CityX.Items.BackClip.Max}", shader, "the shader holds a different number of boards");
        }
    }
}
