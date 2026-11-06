using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sword spin's timeline against the numbers it was taken from (Link's Spin Attack in Smash Ultimate, at 60 fps -- see
    /// docs/SWORD_SPIN.md): if one of these fails, the move has drifted from its source.
    /// </summary>
    public sealed class SwordSpinMoveTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheWholeAnimationIs91Frames()
        {
            Assert.AreEqual(91.0f * Frame, SwordSpinMove.AnimationSeconds, 1e-5f);
            Assert.AreEqual(91.0f * Frame, SwordSpinMove.CommitSeconds(SpinKind.Air), 1e-5f);
        }

        [Test]
        public void TheGroundSpinIsInterruptibleAtFrame74()
        {
            Assert.AreEqual(74.0f * Frame, SwordSpinMove.CommitSeconds(SpinKind.Ground), 1e-5f);
        }

        /// <summary>Owner, 2026-11-05: "make it hit 4 times so 4 times the damage number pops up"; on the ground the fourth is a plain blow.</summary>
        [Test]
        public void TheGroundSpinHitsFourTimesOnceATurnAndNeverThrows()
        {
            var pulses = SwordSpinMove.GroundPulses;
            int[] frames = { 7, 15, 26, 38 };
            Assert.AreEqual(frames.Length, pulses.Length, "four blows");
            float total = 0.0f;
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.AreEqual(frames[i] * Frame, pulses[i].Time, 1e-5f, $"blow {i}");
                Assert.IsFalse(pulses[i].Final, $"ground blow {i} throws: on the ground none does");
                total += pulses[i].DamageScale;
            }
            Assert.AreEqual(1.4f, total, 1e-4f, "the four together are not what its one clean hit was");
            Assert.AreSame(pulses, SwordSpinMove.Pulses(SpinKind.Ground));
        }

        [Test]
        public void ChargingAddsUpToSixtyPercentOverSixtyFrames()
        {
            Assert.AreEqual(1.0f, SwordSpinMove.ChargeMultiplier(0.0f), 1e-5f);
            Assert.AreEqual(1.3f, SwordSpinMove.ChargeMultiplier(30.0f * Frame), 1e-5f);
            Assert.AreEqual(1.6f, SwordSpinMove.ChargeMultiplier(60.0f * Frame), 1e-5f);
            Assert.AreEqual(1.6f, SwordSpinMove.ChargeMultiplier(120.0f * Frame), 1e-5f, "charge past the maximum counts for nothing");
        }

        /// <summary>Owner, 2026-11-05: four blows; in the air the fourth knocks back and throws limp.</summary>
        [Test]
        public void TheAirSpinHitsFourTimesAndTheLastThrows()
        {
            var pulses = SwordSpinMove.AirPulses;
            int[] frames = { 8, 16, 31, 48 };
            Assert.AreEqual(frames.Length, pulses.Length, "four blows");
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.AreEqual(frames[i] * Frame, pulses[i].Time, 1e-5f, $"blow {i}");
                Assert.AreEqual(i == frames.Length - 1, pulses[i].Final, $"blow {i} final");
            }
            Assert.Greater(pulses[3].DamageScale, pulses[0].DamageScale, "the throwing blow is the strong one");
            Assert.AreSame(pulses, SwordSpinMove.Pulses(SpinKind.Air));
        }

        [Test]
        public void TheAirSpinsBlowsAreInOrderAndInsideItsActiveTime()
        {
            var pulses = SwordSpinMove.AirPulses;
            for (int i = 1; i < pulses.Length; i++)
            {
                Assert.Greater(pulses[i].Time, pulses[i - 1].Time, $"blow {i} is not after blow {i - 1}");
            }
            Assert.LessOrEqual(pulses[pulses.Length - 1].Time, SwordSpinMove.AirActiveSeconds);
        }

        [Test]
        public void LandingFromTheAirSpinCostsThirtyFrames()
        {
            Assert.AreEqual(30.0f * Frame, SwordSpinMove.AirLandingLagSeconds, 1e-5f);
        }

        [Test]
        public void AChargedSpinCarriesOnFromItsCockedFrame()
        {
            Assert.AreEqual(4.0f * Frame, SwordSpinMove.ChargeFrameSeconds, 1e-5f, "Smash charges on frames 3-4");
            Assert.AreEqual(0.0f, SwordSpinMove.StartSeconds(SpinKind.Ground, 0.0f), 1e-5f, "a spin that never charged winds up from the start");
            Assert.AreEqual(2.0f * Frame, SwordSpinMove.StartSeconds(SpinKind.Ground, 2.0f * Frame), 1e-5f, "a short charge has wound up that far");
            Assert.AreEqual(SwordSpinMove.ChargeFrameSeconds, SwordSpinMove.StartSeconds(SpinKind.Ground, 1.0f), 1e-5f);
            Assert.AreEqual(0.0f, SwordSpinMove.StartSeconds(SpinKind.Air, 1.0f), 1e-5f, "the air spin is never charged");
            Assert.AreEqual(3.0f * Frame, SwordSpinMove.GroundPulses[0].Time - SwordSpinMove.ChargeFrameSeconds, 1e-5f,
                "a charge let go lands its first blow three frames later");
        }

        // ---- the animation, made in Blender from Smash's hitbox GIFs (CityX/SourceArt/SwordSpin) ----

        private static BakedMotion Motion()
        {
            BakedMotion motion = BakedMotion.Load(SwordSpinMove.MotionResource);
            Assert.IsNotNull(motion, "the sword spin's animation is missing -- export it from Blender (SourceArt/SwordSpin/spin_build_all.py)");
            return motion;
        }

        private static int FrameOf(float seconds) => Mathf.RoundToInt(seconds * 60.0f);

        /// <summary>Which way the body faces on a frame, degrees about up (Unity's: positive is to the right).</summary>
        private static float Facing(BakedMotion motion, int frame)
        {
            Vector3 forward = BakedMotionPoses.WorldTurns(motion, frame)["hips"] * Vector3.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        /// <summary>How far the body has turned to the LEFT by each frame of a segment since its first, unwrapped.</summary>
        private static float[] TurnedLeft(BakedMotion motion, string segment)
        {
            int start = motion.SegmentStart(segment);
            int count = motion.SegmentFrames(segment);
            var turned = new float[count + 1];
            float last = Facing(motion, start);
            for (int f = 1; f <= count; f++)
            {
                float now = Facing(motion, start + f);
                turned[f] = turned[f - 1] - Mathf.DeltaAngle(last, now);
                last = now;
            }
            return turned;
        }

        /// <summary>The blade on a frame, in the thrust grip the spin is posed with and every sword is held in while it plays.</summary>
        private static Vector3 BladeOn(BakedMotion motion, int frame) =>
            BakedMotionPoses.Blade(BakedMotionPoses.WorldTurns(motion, frame), SwordWeapon.ThrustBladeAxisInHand);

        [Test]
        public void TheAnimationIsOnTheGamesOwnBonesAndLastsAsLongAsTheMove()
        {
            BakedMotion motion = Motion();
            CollectionAssert.AreEquivalent(BakedMotionPoses.BodyBonesWithoutFingers(), motion.Bones, "the animation's bones are not BodyRig's");
            Assert.AreEqual(BakedMotion.RootBone, motion.Bones[0]);
            Assert.AreEqual(60, motion.FramesPerSecond);
            foreach (string segment in new[] { SwordSpinMove.GroundSegment, SwordSpinMove.ChargeSegment, SwordSpinMove.AirSegment })
            {
                Assert.IsTrue(motion.HasSegment(segment), $"no '{segment}' in the animation");
            }
            // Played until the body is handed back, never reaching the next segment's first frame.
            Assert.Greater(motion.SegmentSeconds(SwordSpinMove.GroundSegment), SwordSpinMove.CommitSeconds(SpinKind.Ground) + Frame);
            Assert.GreaterOrEqual(motion.SegmentSeconds(SwordSpinMove.AirSegment), SwordSpinMove.CommitSeconds(SpinKind.Air));
        }

        [Test]
        public void TheGroundSpinWindsUpToTheRightThenTurnsFourTimesToTheLeftAndEndsFacingAhead()
        {
            BakedMotion motion = Motion();
            float[] turned = TurnedLeft(motion, SwordSpinMove.GroundSegment);
            int cocked = FrameOf(SwordSpinMove.ChargeFrameSeconds);
            Assert.Less(turned[cocked], -20.0f, "the wind-up does not turn the body to the right, cocking the sword");
            for (int f = cocked + 1; f <= 49; f++)
            {
                Assert.GreaterOrEqual(turned[f] - turned[f - 1], -0.5f, $"the spin turns back at frame {f}");
            }
            // The lunge it ends in turns the hips a little to the right of ahead, as a lunge does.
            Assert.AreEqual(1440.0f, turned[49], 15.0f, "not four turns to the left, ending facing ahead");
            Assert.AreEqual(1440.0f, turned[FrameOf(SwordSpinMove.GroundCommitSeconds)], 15.0f, "not facing ahead when the body is handed back");
            // Fastest at the start (Smash's first turn takes 8 frames, the third 12): the speed of frames 7-15 against 27-35.
            Assert.Greater(turned[15] - turned[7], (turned[35] - turned[27]) * 1.2f, "the spin does not start fast and slow down");
        }

        [Test]
        public void TheFirstGroundBlowCutsStraightAheadAndTheSwordArmStaysOutLowAndLevelThroughTheSpin()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(SwordSpinMove.GroundSegment);
            int first = FrameOf(SwordSpinMove.GroundPulses[0].Time);
            Assert.Greater(BladeOn(motion, start + first).z, 0.8f, "the first blow does not cut straight ahead");
            for (int f = first; f <= FrameOf(SwordSpinMove.GroundPulses[SwordSpinMove.GroundPulses.Length - 1].Time); f++)
            {
                var world = BakedMotionPoses.WorldTurns(motion, start + f);
                var joints = BakedMotionPoses.Joints(motion, start + f, world);
                Vector3 blade = BakedMotionPoses.Blade(world, SwordWeapon.ThrustBladeAxisInHand);
                // Owner, 2026-10-18: "i want it either horizontally or slightly upward" (it pointed down with the combat sword).
                float rise = Mathf.Asin(Mathf.Clamp(blade.y, -1.0f, 1.0f)) * Mathf.Rad2Deg;
                Assert.That(rise, Is.InRange(-3.0f, 25.0f), $"frame {f}: the blade is not swept level or a little up ({rise:0} degrees)");
                Assert.Greater((joints["hand_r"] - joints["arm_r"]).magnitude, 0.8f, $"frame {f}: the sword arm is not straight out");
                Vector3 outward = Vector3.ProjectOnPlane(joints["hand_r"] - joints["hips"], Vector3.up).normalized;
                Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(blade, Vector3.up).normalized, outward), 0.7f,
                    $"frame {f}: the blade does not carry on out from the body");
                Assert.Less(joints["hips"].y, 0.66f, $"frame {f}: the spin is not crouched");
                Vector3 stance = joints["foot_l"] - joints["foot_r"];
                Assert.Greater(new Vector2(stance.x, stance.z).magnitude, 0.6f, $"frame {f}: the feet are not wide apart");
            }
        }

        [Test]
        public void TheChargeHoldsTheCockedPoseWithTheSwordBehindAndLoopsOnItself()
        {
            BakedMotion motion = Motion();
            int cocked = motion.SegmentStart(SwordSpinMove.GroundSegment) + FrameOf(SwordSpinMove.ChargeFrameSeconds);
            int hold = motion.SegmentStart(SwordSpinMove.ChargeSegment);
            for (int b = 0; b < motion.Bones.Count; b++)
            {
                Assert.Less(Quaternion.Angle(motion.RotationAt(cocked, b), motion.RotationAt(hold, b)), 3.0f,
                    $"{motion.Bones[b]}: the held charge is not the cocked pose the spin carries on from");
            }
            Assert.Less(BladeOn(motion, hold).z, -0.5f, "the charge does not cock the sword behind");
            float step = BakedMotionPoses.WorstStep(motion, hold, hold + motion.SegmentFrames(SwordSpinMove.ChargeSegment) - 1, true, out string where);
            Assert.Less(step, 4.0f, $"the held charge snaps (or jumps where it loops): {step:0} degrees at {where}");
        }

        [Test]
        public void TheAirSpinsBlowsLandWithTheBladeAheadOrBehindAndTheLastSwingsItUp()
        {
            BakedMotion motion = Motion();
            int start = motion.SegmentStart(SwordSpinMove.AirSegment);
            var pulses = SwordSpinMove.AirPulses;
            for (int i = 0; i < pulses.Length - 1; i++)
            {
                int frame = FrameOf(pulses[i].Time);
                float ahead = BladeOn(motion, start + frame).z;
                Assert.Greater(ahead, 0.7f, $"blow {i} (frame {frame}) lands with the blade ahead, but it is not ahead");
            }
            Assert.Less(BladeOn(motion, start + 45).y, 0.0f, "the launching cut does not start low");
            Assert.Greater(BladeOn(motion, start + 51).y, 0.8f, "the launching cut does not end overhead");
        }

        [Test]
        public void TheAirSpinTurnsFasterThanTheGroundSpinAndEndsFacingAhead()
        {
            BakedMotion motion = Motion();
            float[] air = TurnedLeft(motion, SwordSpinMove.AirSegment);
            float[] ground = TurnedLeft(motion, SwordSpinMove.GroundSegment);
            Assert.Greater(air[38] - air[8], (ground[37] - ground[7]) * 1.1f, "the air spin is not the faster one");
            int end = motion.SegmentStart(SwordSpinMove.AirSegment) + FrameOf(SwordSpinMove.AirCommitSeconds);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0.0f, Facing(motion, end))), 10.0f, "the air spin does not end facing ahead");
        }

        [Test]
        public void NothingSnapsOutsideTheWhirl()
        {
            BakedMotion motion = Motion();
            int ground = motion.SegmentStart(SwordSpinMove.GroundSegment);
            int air = motion.SegmentStart(SwordSpinMove.AirSegment);
            // The whirl turns the body up to 60 degrees a frame, never so far that the frames between could be read the wrong way round.
            float step = BakedMotionPoses.WorstStep(motion, ground, ground + 49, false, out string where);
            Assert.Less(step, 120.0f, $"the ground spin turns a bone too far in one frame: {step:0} degrees at {where}");
            step = BakedMotionPoses.WorstStep(motion, air, air + 52, false, out where);
            Assert.Less(step, 120.0f, $"the air spin turns a bone too far in one frame: {step:0} degrees at {where}");
            // The held lunge and the lowered blade are slow; at the end the arms come back to the guard quickly, as the body is handed back
            // (Octaslash's follow-through allows as much).
            step = BakedMotionPoses.WorstStep(motion, ground + 50, ground + FrameOf(SwordSpinMove.GroundCommitSeconds), false, out where);
            Assert.Less(step, 18.0f, $"the ground spin's ending snaps: {step:0} degrees at {where}");
            step = BakedMotionPoses.WorstStep(motion, air + 58, air + FrameOf(SwordSpinMove.AirCommitSeconds), false, out where);
            Assert.Less(step, 18.0f, $"the air spin's ending snaps: {step:0} degrees at {where}");
        }
    }
}
