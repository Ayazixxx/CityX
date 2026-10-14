using System.Collections.Generic;
using CityX.Character;
using CityX.CharacterEditor;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The fingers fold round a grip (owner, 2026-10-13: "can you make the fingers on the model grip the swords as well"): the rig has two bones per
    /// digit chained under each hand, and HandGrip's folds go toward the palm on both hands, the thumb across it.
    /// </summary>
    public sealed class HandGripTests
    {
        [Test]
        public void TheRigHasTwoBonesPerDigitChainedUnderEachHand()
        {
            var byName = new Dictionary<string, BodyRig.Bone>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                byName[bone.Name] = bone;
            }
            foreach (string suffix in new[] { "_l", "_r" })
            {
                foreach (FusedBodyShape.Digit digit in FusedBodyShape.Digits)
                {
                    Assert.IsTrue(byName.TryGetValue(digit.Name + "1" + suffix, out BodyRig.Bone root), $"no {digit.Name}1{suffix}");
                    Assert.IsTrue(byName.TryGetValue(digit.Name + "2" + suffix, out BodyRig.Bone tip), $"no {digit.Name}2{suffix}");
                    Assert.AreEqual("hand" + suffix, root.Parent, "a finger's root should hang off the hand");
                    Assert.AreEqual(root.Name, tip.Parent, "a finger's tip should hang off its root");
                    Assert.IsTrue(BodyRig.IsFinger(root.Human) && BodyRig.IsFinger(tip.Human));
                    Assert.AreEqual(root.Human, BodyRig.FingerParent(tip.Human), "the tip's humanoid parent is not the root");
                    Assert.AreEqual(suffix == "_l" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, BodyRig.FingerParent(root.Human));
                    Assert.AreEqual(suffix == "_l" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, BodyRig.HandOf(tip.Human));
                    // The tip bone is further from the wrist than the root: the chain runs outward.
                    Vector3 wrist = BodyRig.PositionOf(suffix == "_l" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    Assert.Greater(Vector3.Distance(tip.Position, wrist), Vector3.Distance(root.Position, wrist), $"{digit.Name}{suffix} runs inward");
                }
            }
            int fingers = 0;
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (BodyRig.IsFinger(bone.Human)) fingers++;
            }
            Assert.AreEqual(20, fingers);
        }

        [Test]
        public void TheLeftHandsFingersRunTheOtherWayAlongItsArm()
        {
            // T-pose: the right arm runs +X, the left -X; the fingers continue each arm.
            Vector3 rightRoot = BodyRig.PositionOf(HumanBodyBones.RightIndexProximal);
            Vector3 rightTip = BodyRig.PositionOf(HumanBodyBones.RightIndexIntermediate);
            Vector3 leftRoot = BodyRig.PositionOf(HumanBodyBones.LeftIndexProximal);
            Vector3 leftTip = BodyRig.PositionOf(HumanBodyBones.LeftIndexIntermediate);
            Assert.Greater(rightTip.x, rightRoot.x, "the right index finger should run outward (+X)");
            Assert.Less(leftTip.x, leftRoot.x, "the left index finger should run outward (-X)");
            Assert.AreEqual(rightRoot.y, leftRoot.y, 1e-4f);
            Assert.AreEqual(rightRoot.z, leftRoot.z, 1e-4f, "the thumb's side is forward on both hands");
        }

        [Test]
        public void TheFingersAreNotInTheHumanoidAvatarButTheSkeletonHasThem()
        {
            // The project's own avatar asset (built by BodyRigBuilder.BuildAvatar on every character rebuild).
            var avatar = UnityEditor.AssetDatabase.LoadAssetAtPath<Avatar>("Assets/CityX/Character/CityXAvatar.asset");
            Assert.IsNotNull(avatar, "no avatar asset -- rebuild the character (CityX/Rebuild Everything Character)");
            Assert.IsTrue(avatar.isValid, "the avatar is not valid");
            Assert.IsTrue(avatar.isHuman);
            bool fingerInSkeleton = false;
            foreach (SkeletonBone bone in avatar.humanDescription.skeleton)
            {
                fingerInSkeleton |= bone.name == "index1_r";
            }
            Assert.IsTrue(fingerInSkeleton, "the avatar's skeleton has no finger bones -- rebuild the character");
            foreach (HumanBone human in avatar.humanDescription.human)
            {
                Assert.IsFalse(human.boneName.StartsWith("index") || human.boneName.StartsWith("thumb") || human.boneName.StartsWith("middle")
                    || human.boneName.StartsWith("ring") || human.boneName.StartsWith("little"), $"{human.boneName} is humanoid-mapped: a clip could wave it about");
            }

            GameObject root = BodyRigBuilder.BuildSkeleton("Test", out Dictionary<string, Transform> bones);
            try
            {
                Assert.IsTrue(bones.ContainsKey("index1_r") && bones.ContainsKey("thumb2_l"), "the skeleton has no finger bones");
                Assert.AreEqual(bones["hand_r"], bones["index1_r"].parent);
                Assert.AreEqual(bones["index1_r"], bones["index2_r"].parent);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FingerCurlFoldsTowardThePalmOnBothHands()
        {
            // The right hand's fingers run +X, the left hand's -X; the palm is -Y on both.
            Vector3 rightFolded = HandGrip.FingerCurl(true, 60.0f) * Vector3.right;
            Vector3 leftFolded = HandGrip.FingerCurl(false, 60.0f) * Vector3.left;
            Assert.Less(rightFolded.y, -0.5f, $"the right finger folds to {rightFolded.ToString("0.00")}, not toward the palm");
            Assert.Less(leftFolded.y, -0.5f, $"the left finger folds to {leftFolded.ToString("0.00")}, not toward the palm");
            Assert.Greater(rightFolded.x, 0.3f, "the right finger should still point outward");
            Assert.Less(leftFolded.x, -0.3f, "the left finger should still point outward");
        }

        [Test]
        public void TheThumbFoldsAcrossThePalm()
        {
            Vector3 folded = HandGrip.ThumbCurl(40.0f) * Vector3.forward;
            Assert.Less(folded.y, -0.5f, $"the thumb folds to {folded.ToString("0.00")}, not toward the palm");
        }

        [Test]
        public void OpenIsTheModelledHandAndClosedFoldsEveryBone()
        {
            var digits = new Transform[HandGrip.BonesPerHand];
            var hand = new GameObject("hand").transform;
            try
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    digits[i] = new GameObject("d" + i).transform;
                    digits[i].SetParent(hand, false);
                }
                HandGrip.Apply(digits, true, 0.0f);
                foreach (Transform d in digits)
                {
                    Assert.Less(Quaternion.Angle(d.localRotation, Quaternion.identity), 0.01f, "an open hand should be the modelled one");
                }
                HandGrip.Apply(digits, true, 1.0f);
                for (int i = 0; i < digits.Length; i++)
                {
                    Assert.Greater(Quaternion.Angle(digits[i].localRotation, Quaternion.identity), 20.0f, $"bone {i} did not fold");
                }
                HandGrip.Apply(digits, true, 0.5f);
                Assert.AreEqual(HandGrip.FingerProximalDegrees * 0.5f, Quaternion.Angle(digits[0].localRotation, Quaternion.identity), 0.01f, "half closed is half the fold");
            }
            finally
            {
                Object.DestroyImmediate(hand.gameObject);
            }
        }
    }
}
