using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Wraith's pose (WraithPose, pure): swinging arms out of step and lagging down the arm, the lean, the bank, the look, the flinch.</summary>
    public sealed class WraithPoseTests
    {
        private static WraithPose.Result At(float time, float speed = 0.0f, float turnRate = 0.0f, float hurt = 0.0f)
        {
            return WraithPose.Compute(new WraithPose.Input(time, speed, turnRate, false, Vector3.zero, hurt));
        }

        private static WraithPose.Result Looking(Vector3 target)
        {
            return WraithPose.Compute(new WraithPose.Input(0.0f, 0.0f, 0.0f, true, target, 0.0f));
        }

        [Test]
        public void HangingStillTheArmsSwingOutOfStepWithinTheirSwingHeldALittleOut()
        {
            for (float t = 0.0f; t < WraithPose.ArmSwingSeconds; t += 0.05f)
            {
                WraithPose.Result pose = At(t);
                Assert.LessOrEqual(Mathf.Abs(pose.ArmLeft.Pitch), WraithPose.ArmSwingDegrees + 1e-3f, "the left arm swung too far");
                Assert.AreEqual(-pose.ArmLeft.Pitch, pose.ArmRight.Pitch, 1e-3f, "the arms swing together, not out of step");
                Assert.AreEqual(WraithPose.ArmSpreadDegrees, pose.ArmLeft.Roll, 1e-3f);
                Assert.AreEqual(-WraithPose.ArmSpreadDegrees, pose.ArmRight.Roll, 1e-3f);
                Assert.AreEqual(0.0f, pose.Spine.Pitch + pose.Hips.Pitch, 1e-3f, "hanging still it leans");
            }
            Assert.AreEqual(WraithPose.ArmSwingDegrees, At(WraithPose.ArmSwingSeconds * 0.25f).ArmLeft.Pitch, 1e-3f, "the swing's top is not the swing");
        }

        [Test]
        public void TheJointsLagDownTheArm()
        {
            float armPeak = PeakTime(p => p.ArmLeft.Pitch);
            float forearmPeak = PeakTime(p => p.ForearmLeft.Pitch);
            float handPeak = PeakTime(p => p.HandLeft.Pitch);
            float lag = WraithPose.JointLag * WraithPose.ArmSwingSeconds;
            Assert.AreEqual(lag, Mathf.Repeat(forearmPeak - armPeak, WraithPose.ArmSwingSeconds), 0.06f, "the forearm does not lag the arm");
            Assert.AreEqual(lag * 2.0f, Mathf.Repeat(handPeak - armPeak, WraithPose.ArmSwingSeconds), 0.06f, "the hand does not lag further");
            Assert.AreEqual(WraithPose.ArmSwingDegrees * WraithPose.ForearmShare, At(forearmPeak).ForearmLeft.Pitch, 0.1f);
        }

        private static float PeakTime(System.Func<WraithPose.Result, float> of)
        {
            float best = float.MinValue, at = 0.0f;
            for (float t = 0.0f; t < WraithPose.ArmSwingSeconds; t += 0.005f)
            {
                float value = of(At(t));
                if (value > best)
                {
                    best = value;
                    at = t;
                }
            }
            return at;
        }

        [Test]
        public void DriftingItLeansForwardAndItsArmsTrailBack()
        {
            float leanSum = 0.0f, armSum = 0.0f, stillArmSum = 0.0f;
            int samples = 0;
            for (float t = 0.0f; t < WraithPose.ArmSwingSeconds; t += WraithPose.ArmSwingSeconds / 64.0f)
            {
                WraithPose.Result moving = At(t, WraithDrift.CruiseSpeed);
                leanSum += moving.Spine.Pitch + moving.Hips.Pitch;
                armSum += moving.ArmLeft.Pitch;
                stillArmSum += At(t).ArmLeft.Pitch;
                samples++;
            }
            Assert.AreEqual(WraithPose.LeanDegrees, leanSum / samples, 1e-3f, "at cruising speed it does not lean its whole lean");
            Assert.AreEqual(-WraithPose.ArmTrailDegrees, armSum / samples, 0.05f, "drifting, the arms do not trail back");
            Assert.AreEqual(0.0f, stillArmSum / samples, 0.05f, "still, the arms hang off centre");
            Assert.AreEqual(WraithPose.LeanDegrees * 0.5f, At(0.0f, WraithDrift.CruiseSpeed * 0.5f).Spine.Pitch + At(0.0f, WraithDrift.CruiseSpeed * 0.5f).Hips.Pitch,
                1e-3f, "the lean does not follow the speed");
        }

        [Test]
        public void TurningItBanksIntoTheTurn()
        {
            WraithPose.Result right = At(0.0f, 0.0f, WraithDrift.TurnDegreesPerSecond);
            WraithPose.Result left = At(0.0f, 0.0f, -WraithDrift.TurnDegreesPerSecond);
            Assert.AreEqual(-WraithPose.BankDegrees, right.Spine.Roll + right.Hips.Roll, 1e-3f, "turning right it does not bank right");
            Assert.AreEqual(WraithPose.BankDegrees, left.Spine.Roll + left.Hips.Roll, 1e-3f, "turning left it does not bank left");
            Assert.AreEqual(-WraithPose.BankDegrees, At(0.0f, 0.0f, WraithDrift.TurnDegreesPerSecond * 5.0f).Spine.Roll + At(0.0f, 0.0f, WraithDrift.TurnDegreesPerSecond * 5.0f).Hips.Roll,
                1e-3f, "a hard turn banks it past its limit");
        }

        [Test]
        public void ItLooksAtWhoIsNearNoFurtherThanItsNeckTurns()
        {
            Assert.AreEqual(45.0f, Looking(new Vector3(5.0f, 0.0f, 5.0f)).Head.Yaw, 1e-3f, "it does not look right at someone ahead to its right");
            Assert.AreEqual(-WraithPose.LookYawLimit, Looking(new Vector3(-5.0f, 0.0f, -1.0f)).Head.Yaw, 1e-3f, "it turned its head right round");
            Assert.AreEqual(-WraithPose.LookPitchLimit, Looking(new Vector3(0.0f, 5.0f, 1.0f)).Head.Pitch, 1e-3f, "it looked straight up over its limit");
            Assert.AreEqual(-30.0f, Looking(new Vector3(0.0f, 1.0f, Mathf.Sqrt(3.0f))).Head.Pitch, 1e-3f, "it does not look up at someone above");
            WraithPose.Result idle = At(WraithPose.HeadIdleSeconds * 0.25f);
            Assert.AreEqual(WraithPose.HeadIdleYawDegrees, idle.Head.Yaw, 1e-3f, "with nobody near its head does not wander");
        }

        [Test]
        public void HitItJerksBackAndFlingsItsArmsUpThenSettles()
        {
            WraithPose.Result hit = At(0.0f, 0.0f, 0.0f, 1.0f);
            Assert.AreEqual(-WraithPose.FlinchDegrees, hit.Spine.Pitch + hit.Hips.Pitch, 1e-3f, "hit, it does not jerk back");
            Assert.AreEqual(-WraithPose.FlinchArmDegrees, hit.ArmLeft.Pitch, 1e-3f, "hit, its arms do not fly up");
            WraithPose.Result half = At(0.0f, 0.0f, 0.0f, 0.5f);
            Assert.AreEqual(-WraithPose.FlinchDegrees * 0.5f, half.Spine.Pitch + half.Hips.Pitch, 1e-3f, "the flinch does not settle");
            Assert.AreEqual(0.0f, At(0.0f).Spine.Pitch + At(0.0f).Hips.Pitch, 1e-3f);
        }
    }
}
