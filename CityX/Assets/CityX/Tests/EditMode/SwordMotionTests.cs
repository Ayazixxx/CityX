using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The body under the sword's attacks (docs/SWORD_FULL_BODY.md; owner, 2026-11-13: "telegraphed, have directionality, and the animation
    /// involves the whole body akin to dark souls combat"): each attack's clip of the legs, the hips, the torso and the head, read the way
    /// the skeleton plays it. Each is as long as its attack, starts and ends standing ready, is wound up with the shoulders turned away
    /// from the cut, turns through it with the hips, strides with one foot planted before the blow while the other stays where it stood,
    /// and puts the weight back and then forward. No clip has an arm bone: the game poses the sword arm and the hand on the handle itself.
    /// </summary>
    public sealed class SwordMotionTests
    {
        private static readonly SwordStrikeKind[] WithClips =
        {
            SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H, SwordStrikeKind.Finisher1H, SwordStrikeKind.Overhead1H,
            SwordStrikeKind.Sweep2H, SwordStrikeKind.Reverse2H, SwordStrikeKind.Cut2H, SwordStrikeKind.Smash2H, SwordStrikeKind.Slam2H,
        };

        private static BakedMotion Motion()
        {
            BakedMotion motion = SwordStrikes.Motion;
            Assert.IsNotNull(motion, "the sword attacks' clips are missing -- build them from Blender (CityX/SourceArt/SwordMoves/sword_moves_build.py)");
            return motion;
        }

        /// <summary>The frame of an attack's clip at <paramref name="seconds"/> into it.</summary>
        private static int Frame(BakedMotion motion, SwordStrikeKind kind, float seconds)
        {
            string name = SwordStrikes.NameOf(kind);
            return motion.SegmentStart(name) + Mathf.Clamp(Mathf.RoundToInt(seconds * motion.FramesPerSecond), 0, motion.SegmentFrames(name) - 1);
        }

        private static Dictionary<string, Vector3> Joints(BakedMotion motion, SwordStrikeKind kind, float seconds, out Dictionary<string, Quaternion> turns)
        {
            int frame = Frame(motion, kind, seconds);
            turns = BakedMotionPoses.WorldTurns(motion, frame);
            return BakedMotionPoses.Joints(motion, frame, turns);
        }

        private static Dictionary<string, Vector3> Joints(BakedMotion motion, SwordStrikeKind kind, float seconds) => Joints(motion, kind, seconds, out _);

        /// <summary>How far to the right a bone faces, degrees (0 straight ahead).</summary>
        private static float YawOf(Quaternion turn)
        {
            Vector3 facing = Vector3.ProjectOnPlane(turn * Vector3.forward, Vector3.up);
            return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        }

        /// <summary>The seconds an attack is wound up by (the middle of its hold), lands at, and has followed through by.</summary>
        private static void Moments(SwordStrikeKind kind, out float wound, out float contact, out float followed)
        {
            SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
            wound = t.WindUp + t.Hold * 0.5f;
            contact = t.Contact;
            followed = t.Contact + t.Follow;
        }

        /// <summary>The foot that strides into an attack: the right in the two backhands (it passes the left), the left in every other.</summary>
        private static string StridingFoot(SwordStrikeKind kind) => kind == SwordStrikeKind.Cut1H || kind == SwordStrikeKind.Reverse2H ? "foot_r" : "foot_l";

        private static string StandingFoot(SwordStrikeKind kind) => StridingFoot(kind) == "foot_r" ? "foot_l" : "foot_r";

        [Test]
        public void EveryAttackButTheKnifesThrustHasAClipOnTheGamesBonesAsLongAsItsTiming()
        {
            BakedMotion motion = Motion();
            List<string> expected = BakedMotionPoses.BodyBonesWithoutFingers();
            expected.RemoveAll(BakedMotion.IsArmBone);
            CollectionAssert.AreEquivalent(expected, motion.Bones, "the clips' bones are not BodyRig's legs, hips, spine, neck and head");
            Assert.AreEqual(BakedMotion.RootBone, motion.Bones[0]);
            foreach (SwordStrikeKind kind in WithClips)
            {
                Assert.IsTrue(SwordStrikes.HasClip(kind), $"no clip for {kind}");
                // A clip is whole frames; its attack's seconds are within one of them.
                Assert.AreEqual(SwordStrikes.Seconds(kind), motion.SegmentSeconds(SwordStrikes.NameOf(kind)), 1.01f / motion.FramesPerSecond,
                    $"{kind}'s clip is not as long as the attack: it would land on another frame");
            }
            Assert.IsFalse(SwordStrikes.HasClip(SwordStrikeKind.Thrust1H), "the knife's thrust has a clip now: list it here and test it");
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                Assert.IsTrue(System.Array.IndexOf(WithClips, kind) >= 0 || kind == SwordStrikeKind.Thrust1H, $"{kind} is in neither list");
            }
        }

        [Test]
        public void EveryFrameIsAWholePoseAndNothingSnaps()
        {
            BakedMotion motion = Motion();
            for (int f = 0; f < motion.FrameCount; f++)
            {
                for (int b = 0; b < motion.Bones.Count; b++)
                {
                    Quaternion q = motion.RotationAt(f, b);
                    float norm = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    Assert.AreEqual(1.0f, norm, 1e-3f, $"frame {f}, {motion.Bones[b]}");
                }
                Assert.Less(motion.HipsAt(f).magnitude, 1.5f, $"frame {f}: the hips are nowhere near the body");
            }
            foreach (SwordStrikeKind kind in WithClips)
            {
                string name = SwordStrikes.NameOf(kind);
                int first = motion.SegmentStart(name);
                float worst = BakedMotionPoses.WorstStep(motion, first, first + motion.SegmentFrames(name) - 1, false, out string where);
                Debug.Log($"CityX sword clip {kind}: the most a bone turns in a frame is {worst:0} degrees ({where})");
                // (A thigh swung through its stride and the shoulders unwinding through the strike turn some twenty degrees in a frame; a knee snapping straight and locking turned forty-seven.)
                Assert.Less(worst, 30.0f, $"{kind}: {where} turns {worst:0} degrees in one frame");
            }
        }

        [Test]
        public void EveryAttackStartsAndEndsStandingReady()
        {
            BakedMotion motion = Motion();
            foreach (SwordStrikeKind kind in WithClips)
            {
                foreach (float seconds in new[] { 0.0f, SwordStrikes.Seconds(kind) })
                {
                    Dictionary<string, Vector3> joints = Joints(motion, kind, seconds, out Dictionary<string, Quaternion> turns);
                    string when = seconds <= 0.0f ? "starts" : "ends";
                    Assert.AreEqual(BodyRig.HipY - 0.08f, joints["hips"].y, 0.03f, $"{kind} {when} with the hips somewhere else than the ready stance has them");
                    Assert.Less(new Vector2(joints["hips"].x, joints["hips"].z).magnitude, 0.04f, $"{kind} {when} with the hips off its feet");
                    Assert.Less(Mathf.Abs(YawOf(turns["spine"])), 16.0f, $"{kind} {when} with the body still turned");
                    foreach (string foot in new[] { "foot_l", "foot_r" })
                    {
                        Assert.Less(Mathf.Abs(joints[foot].z), 0.16f, $"{kind} {when} with a foot out: the next attack starts from the stance");
                    }
                }
            }
        }

        /// <summary>
        /// Owner, 2026-11-13, of a draft whose clips posed the arms too: "why are his wrists all twisted and poking through his torso". The
        /// sword arm, the blade and the hand on the handle are the game's own poses (PlayerLocomotion.ApplySwordPose), which suit its hold and
        /// keep the arms out of the body; every other baked move poses its own arms.
        /// </summary>
        [Test]
        public void TheClipsLeaveTheArmsToTheGameAndNoOtherBakedMoveDoes()
        {
            BakedMotion motion = Motion();
            foreach (string bone in motion.Bones)
            {
                Assert.IsFalse(BakedMotion.IsArmBone(bone), $"the sword attacks' clips pose {bone}");
            }
            Assert.IsTrue(motion.NewPose().LeavesTheArms);
            foreach (string path in new[] { "Octaslash/OctaslashMotion", "SwordSpin/SwordSpinMotion", "StabDash/StabDashMotion", "Fists/FistMotion", "Glock/GlockReload" })
            {
                BakedMotion other = BakedMotion.Load(path);
                Assert.IsFalse(other.NewPose().LeavesTheArms, $"{path} is treated as leaving the arms: its own arm poses would be lost");
            }
            foreach (string bone in new[] { "arm_l", "forearm_r", "hand_r", "fingertip_l", "index1_r", "thumb2_l" })
            {
                Assert.IsTrue(BakedMotion.IsArmBone(bone), bone);
            }
            foreach (string bone in new[] { "hips", "spine", "neck", "head", "thigh_l", "shin_r", "foot_l" })
            {
                Assert.IsFalse(BakedMotion.IsArmBone(bone), bone);
            }
        }

        /// <summary>Which way each cut travels: its body is wound up turned the other way (+1 the shoulders turned to the right first, a cut from the right).</summary>
        private static readonly (SwordStrikeKind kind, int woundTo)[] Ways =
        {
            (SwordStrikeKind.Slash1H, 1), (SwordStrikeKind.Cut1H, -1), (SwordStrikeKind.Finisher1H, 1),
            (SwordStrikeKind.Sweep2H, 1), (SwordStrikeKind.Reverse2H, -1), (SwordStrikeKind.Cut2H, 1),
        };

        [Test]
        public void EveryCutsBodyIsWoundUpAwayFromItAndEndsTurnedTheWayItWent()
        {
            BakedMotion motion = Motion();
            foreach ((SwordStrikeKind kind, int woundTo) in Ways)
            {
                Moments(kind, out float wound, out _, out float followed);
                Joints(motion, kind, wound, out Dictionary<string, Quaternion> coiled);
                Joints(motion, kind, followed, out Dictionary<string, Quaternion> through);
                Assert.Greater(woundTo * YawOf(coiled["spine"]), 40.0f, $"{kind}: wound up, the shoulders are not turned away from the cut: nothing to see coming");
                Assert.Less(woundTo * YawOf(through["spine"]), -35.0f, $"{kind}: followed through, the shoulders have not turned the way the cut went");
            }
        }

        [Test]
        public void TheWholeBodyCoilsAgainstTheCutAndTurnsThroughItWithTheHeadKeptAhead()
        {
            BakedMotion motion = Motion();
            foreach (SwordStrikeKind kind in WithClips)
            {
                Moments(kind, out float wound, out float contact, out float followed);
                Joints(motion, kind, wound, out Dictionary<string, Quaternion> coiled);
                Joints(motion, kind, followed, out Dictionary<string, Quaternion> through);
                bool overhead = kind == SwordStrikeKind.Overhead1H || kind == SwordStrikeKind.Smash2H || kind == SwordStrikeKind.Slam2H;
                if (!overhead)
                {
                    float turned = Mathf.Abs(Mathf.DeltaAngle(YawOf(coiled["spine"]), YawOf(through["spine"])));
                    Assert.Greater(turned, 80.0f, $"{kind}: the shoulders turn only {turned:0} degrees from the wind-up to the follow-through: an arm's cut, not a body's");
                    float hips = Mathf.Abs(Mathf.DeltaAngle(YawOf(coiled["hips"]), YawOf(through["hips"])));
                    Assert.Greater(hips, 45.0f, $"{kind}: the hips turn only {hips:0} degrees through the cut");
                    Assert.Less(hips, turned, $"{kind}: the hips turn further than the shoulders");
                }
                else
                {
                    // Straight down: the body arched back under the blade, then folded forward after it.
                    Dictionary<string, Vector3> up = Joints(motion, kind, wound), down = Joints(motion, kind, followed);
                    Assert.Less(up["neck"].z - up["hips"].z, -0.05f, $"{kind}: wound up, the body is not arched back under the blade");
                    Assert.Greater(down["neck"].z - down["hips"].z, 0.35f, $"{kind}: the body does not fold forward after the blow");
                    Assert.Less(down["hips"].y, up["hips"].y - 0.15f, $"{kind}: the hips do not sink into the blow");
                }
                foreach (float seconds in new[] { wound, contact, followed })
                {
                    Joints(motion, kind, seconds, out Dictionary<string, Quaternion> turns);
                    Assert.Less(Mathf.Abs(YawOf(turns["head"])), 25.0f, $"{kind}, {seconds:0.00} s: the head turns away from what is ahead");
                }
            }
        }

        [Test]
        public void TheWeightGoesBackInTheWindUpAndForwardForTheBlow()
        {
            BakedMotion motion = Motion();
            foreach (SwordStrikeKind kind in WithClips)
            {
                Moments(kind, out float wound, out float contact, out _);
                Assert.Less(Joints(motion, kind, wound)["hips"].z, -0.05f, $"{kind}: the weight does not go back as it is wound up");
                Assert.Greater(Joints(motion, kind, contact)["hips"].z, 0.03f, $"{kind}: the weight does not go forward as it lands");
            }
        }

        [Test]
        public void OneFootStridesAndIsPlantedBeforeTheBlowWhileTheOtherStaysWhereItStood()
        {
            BakedMotion motion = Motion();
            foreach (SwordStrikeKind kind in WithClips)
            {
                Moments(kind, out _, out float contact, out _);
                string striding = StridingFoot(kind), standing = StandingFoot(kind);
                float stood = Joints(motion, kind, 0.0f)[standing].z;
                for (float seconds = 0.0f; seconds <= contact - 0.05f; seconds += 0.03f)
                {
                    // The ankle's place on the ground: where it is in the clip, and how far the game has stepped the body by then.
                    float onTheGround = Joints(motion, kind, seconds)[standing].z + SwordStrikes.Stepped(kind, seconds);
                    // (Rolling up onto its ball, the ankle goes a few centimetres forward over toes that have not moved.)
                    Assert.AreEqual(stood, onTheGround, 0.08f, $"{kind}, {seconds:0.00} s: the standing foot slides {onTheGround - stood:0.00} m along the ground");
                }
                Dictionary<string, Vector3> landing = Joints(motion, kind, contact);
                Assert.Less(landing[striding].y, BodyRig.FootY + 0.06f, $"{kind} lands with the striding foot still in the air");
                // (A backhand\x27s right foot passes the left and lands 30 cm ahead of it; the other cuts\x27 left foot strides out 65 cm ahead of the right.)
                Assert.Greater(landing[striding].z - landing[standing].z, 0.2f, $"{kind} lands with the feet together: no stride under the blow");
                float before = Joints(motion, kind, contact - 0.04f)[striding].z + SwordStrikes.Stepped(kind, contact - 0.04f);
                float at = landing[striding].z + SwordStrikes.Stepped(kind, contact);
                Assert.AreEqual(before, at, 0.03f, $"{kind}: the striding foot is still moving as the blow lands");
            }
        }

        [Test]
        public void TheStepIsTakenBetweenTheWindUpAndTheEndOfTheLiveWindow()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                float total = SwordStrikes.Seconds(kind);
                Assert.AreEqual(0.0f, SwordStrikes.Stepped(kind, 0.0f), 1e-5f, $"{kind} steps the body before it has begun");
                Assert.AreEqual(0.0f, SwordStrikes.Stepped(kind, total * SwordStrikes.ActiveStart(kind) * SwordStrikes.StepStartShare), 1e-4f,
                    $"{kind} steps the body through the first of its wind-up");
                Assert.AreEqual(SwordStrikes.StepMetres(kind), SwordStrikes.Stepped(kind, total * SwordStrikes.ActiveEnd(kind)), 1e-4f,
                    $"{kind} has not stepped the body its stride by the end of its live window");
                Assert.AreEqual(SwordStrikes.StepMetres(kind), SwordStrikes.Stepped(kind, total), 1e-4f, $"{kind} goes on stepping the body through its recovery");
            }
        }
    }
}
