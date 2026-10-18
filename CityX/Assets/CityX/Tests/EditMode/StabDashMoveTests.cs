using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The stab dash's timeline against the numbers it was taken from (the Mii Swordfighter's grounded Power Thrust in Smash Ultimate, at 60 fps:
    /// docs/STAB_DASH.md), its slide against the dash whose length it copies, and its Blender animation against the GIF it was drawn from. If one
    /// of these fails, the move has drifted from its source.
    /// </summary>
    public sealed class StabDashMoveTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheStabHurtsFromFrame12ToFrame30()
        {
            Assert.AreEqual(0.0f, StabDashMove.DamageScale(11.0f * Frame), "before the first hit frame");
            Assert.Greater(StabDashMove.DamageScale(12.0f * Frame), 0.0f, "frame 12 is the first");
            Assert.Greater(StabDashMove.DamageScale(30.0f * Frame), 0.0f, "frame 30 is the last");
            Assert.AreEqual(0.0f, StabDashMove.DamageScale(31.0f * Frame), "after the last");
        }

        [Test]
        public void TheStabFadesFrom15To10InThreeBandsAndOnlyItsCleanStartLaunches()
        {
            // clean 12-14: 15%, late 15-22: 13%, later 23-30: 10%
            foreach (int frame in new[] { 12, 13, 14 })
            {
                Assert.AreEqual(1.5f, StabDashMove.DamageScale(frame * Frame), 1e-4f, $"frame {frame}");
                Assert.IsTrue(StabDashMove.IsClean(frame * Frame), $"frame {frame} is the clean hit");
            }
            foreach (int frame in new[] { 15, 18, 22 })
            {
                Assert.AreEqual(1.3f, StabDashMove.DamageScale(frame * Frame), 1e-4f, $"frame {frame}");
                Assert.IsFalse(StabDashMove.IsClean(frame * Frame), $"frame {frame} is not clean");
            }
            foreach (int frame in new[] { 23, 26, 30 })
            {
                Assert.AreEqual(1.0f, StabDashMove.DamageScale(frame * Frame), 1e-4f, $"frame {frame}");
            }
            Assert.IsFalse(StabDashMove.IsClean(11.0f * Frame), "nothing is clean before the hit window");
        }

        [Test]
        public void TheBodyIsHandedBackOnFrame59()
        {
            Assert.AreEqual(59.0f * Frame, StabDashMove.CommitSeconds, 1e-5f);
        }

        [Test]
        public void TheSlideGoesExactlyADashsLengthFromFrame11ToFrame35()
        {
            Assert.AreEqual(PlayerLocomotion.DashSpeed * PlayerLocomotion.DashSeconds, StabDashMove.SlideMetres, 1e-5f, "not a dash's length");
            Assert.AreEqual(4.8f, StabDashMove.SlideMetres, 1e-4f);
            Assert.AreEqual(0.0f, StabDashMove.SlideDistance(0.0f), 1e-5f);
            Assert.AreEqual(0.0f, StabDashMove.SlideDistance(11.0f * Frame), 1e-5f, "the body moves before the release");
            Assert.Greater(StabDashMove.SlideDistance(12.0f * Frame), 0.0f, "the body has not moved off by the first hit");
            Assert.AreEqual(4.8f, StabDashMove.SlideDistance(35.0f * Frame), 1e-4f, "the slide is not over by frame 35");
            Assert.AreEqual(4.8f, StabDashMove.SlideDistance(StabDashMove.CommitSeconds), 1e-4f);
            float last = 0.0f;
            for (float f = 0.0f; f <= 40.0f; f += 0.25f)
            {
                float now = StabDashMove.SlideDistance(f * Frame);
                Assert.GreaterOrEqual(now, last - 1e-5f, $"the slide goes back at frame {f}");
                last = now;
            }
        }

        [Test]
        public void TheSlideIsFastestOffTheMarkAndSlowsAllTheWay()
        {
            float Step(int f) => StabDashMove.SlideDistance((f + 1) * Frame) - StabDashMove.SlideDistance(f * Frame);
            Assert.Greater(Step(12) / Frame, 30.0f, "the lunge does not burst off faster than a dash's 24 m/s");
            for (int f = 12; f < 34; f++)
            {
                Assert.GreaterOrEqual(Step(f), Step(f + 1) - 1e-5f, $"the slide speeds up again at frame {f + 1}");
            }
            Assert.Less(Step(33), Step(13) / 10.0f, "the slide does not die away");
        }

        // ---- the animation, made in Blender from the GIF (CityX/SourceArt/StabDash) ----

        private static BakedMotion Motion()
        {
            BakedMotion motion = BakedMotion.Load(StabDashMove.MotionResource);
            Assert.IsNotNull(motion, "the stab dash's animation is missing -- export it from Blender (SourceArt/StabDash/stab_build_all.py)");
            return motion;
        }

        private static int FrameOf(float seconds) => Mathf.RoundToInt(seconds * 60.0f);

        [Test]
        public void TheAnimationIsOnTheGamesOwnBonesAndLastsAsLongAsTheMove()
        {
            BakedMotion motion = Motion();
            CollectionAssert.AreEquivalent(BakedMotionPoses.BodyBonesWithoutFingers(), motion.Bones, "the animation's bones are not BodyRig's");
            Assert.AreEqual(BakedMotion.RootBone, motion.Bones[0]);
            Assert.AreEqual(60, motion.FramesPerSecond);
            Assert.IsTrue(motion.HasSegment(StabDashMove.Segment), $"no '{StabDashMove.Segment}' in the animation");
            Assert.GreaterOrEqual(motion.SegmentSeconds(StabDashMove.Segment), StabDashMove.CommitSeconds - 1e-4f, "the animation ends before the move");
        }

        [Test]
        public void TheWindUpDrawsTheSwordBackInADeepWideSquat()
        {
            BakedMotion motion = Motion();
            int cocked = motion.SegmentStart(StabDashMove.Segment) + 9;
            var world = BakedMotionPoses.WorldTurns(motion, cocked);
            var joints = BakedMotionPoses.Joints(motion, cocked, world);
            Vector3 blade = BakedMotionPoses.Blade(world);
            Assert.Less(joints["hand_r"].z, joints["neck"].z - 0.3f, "the sword hand is not drawn back behind the body");
            Assert.Greater(blade.z, 0.5f, "the drawn-back blade does not point ahead");
            Assert.Less(blade.y, -0.15f, "the drawn-back blade does not point down");
            Assert.Greater(joints["hand_l"].z, joints["neck"].z + 0.3f, "the free hand is not out ahead");
            Assert.Less(joints["hips"].y, 0.55f, "the squat is not deep");
            Assert.Greater(Mathf.Abs(joints["foot_l"].z - joints["foot_r"].z), 0.7f, "the feet are not wide apart");
        }

        [Test]
        public void EveryHitFrameThrustsTheBladeStraightAheadOnAStraightArm()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(StabDashMove.Segment);
            for (int f = FrameOf(StabDashMove.HitStart); f <= FrameOf(StabDashMove.HitEnd); f++)
            {
                var world = BakedMotionPoses.WorldTurns(motion, start + f);
                var joints = BakedMotionPoses.Joints(motion, start + f, world);
                Vector3 blade = BakedMotionPoses.Blade(world);
                Assert.Less(Vector3.Angle(blade, Vector3.forward), 12.0f, $"frame {f}: the blade is not thrust straight ahead");
                Assert.Greater((joints["hand_r"] - joints["arm_r"]).magnitude, 0.8f, $"frame {f}: the sword arm is not straight");
                Assert.Greater(joints["hand_r"].z - joints["arm_r"].z, 0.75f, $"frame {f}: the sword arm is not out ahead");
            }
        }

        [Test]
        public void TheLungeIsLowWithTheBackLegStretchedOutBehindAndTheFreeArmRaised()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(StabDashMove.Segment);
            for (int f = 14; f <= 31; f++)
            {
                var world = BakedMotionPoses.WorldTurns(motion, start + f);
                var joints = BakedMotionPoses.Joints(motion, start + f, world);
                Assert.Less(joints["hips"].y, 0.5f, $"frame {f}: the lunge is not low");
                Assert.Greater(joints["neck"].z - joints["hips"].z, 0.6f, $"frame {f}: the body does not lean far into the lunge");
                Assert.Less(joints["foot_r"].z, joints["hips"].z - 0.5f, $"frame {f}: the back leg is not stretched out behind");
                Assert.Greater(joints["foot_l"].z, joints["hips"].z + 0.4f, $"frame {f}: the front foot is not out ahead");
                Assert.Greater(joints["hand_l"].y, joints["arm_l"].y + 0.1f, $"frame {f}: the free arm is not raised");
                Assert.Less(joints["hand_l"].z, joints["arm_l"].z, $"frame {f}: the free arm is not behind");
            }
        }

        [Test]
        public void ItEndsInTheSwordStanceItStartedFrom()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(StabDashMove.Segment);
            int end = start + FrameOf(StabDashMove.CommitSeconds);
            for (int b = 0; b < motion.Bones.Count; b++)
            {
                Assert.Less(Quaternion.Angle(motion.RotationAt(start, b), motion.RotationAt(end, b)), 3.0f, $"{motion.Bones[b]} ends away from the stance");
            }
            Assert.Less((motion.HipsAt(start) - motion.HipsAt(end)).magnitude, 0.02f, "the hips end away from the stance");
        }

        [Test]
        public void NothingSnapsButTheDipAndTheThrust()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(StabDashMove.Segment);
            // The dip into the squat is quick and the release and thrust (frames 9-14) as fast as Smash's, but never so far in one frame that
            // the frames between could be read the wrong way round.
            float step = BakedMotionPoses.WorstStep(motion, start, start + 9, false, out string where);
            Assert.Less(step, 60.0f, $"the wind-up snaps: {step:0} degrees at {where}");
            step = BakedMotionPoses.WorstStep(motion, start + 9, start + 14, false, out where);
            Assert.Less(step, 100.0f, $"the thrust turns a bone too far in one frame: {step:0} degrees at {where}");
            // The lunge is held; in the recovery the free arm comes down and round to the grip quickly, as the body is handed back.
            step = BakedMotionPoses.WorstStep(motion, start + 14, start + FrameOf(StabDashMove.CommitSeconds), false, out where);
            Assert.Less(step, 25.0f, $"the lunge or the recovery snaps: {step:0} degrees at {where}");
        }
    }
}
