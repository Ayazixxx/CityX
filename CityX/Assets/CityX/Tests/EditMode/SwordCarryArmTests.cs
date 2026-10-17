using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The one-handed sword's carry arm on its own (owner, 2026-10-17: "the sword is like spinning around make you own sword running and walking
    /// animations and make them look good"): each joint turns the way its name says, the carry points a sword held out past the thumb forward,
    /// down and out, and a gait's swing moves the hand while the blade only bobs.
    /// </summary>
    public sealed class SwordCarryArmTests
    {
        /// <summary>A sword out past the thumb: tilted toward the fingers as the default one-handed hold had it before 2026-10-17, and straight out as the owner holds it (now also the default).</summary>
        private static readonly Vector3 DefaultHold = new Vector3(0.26f, 0.0f, 0.966f).normalized;

        private static readonly Vector3 OwnersHold = new Vector3(-0.056f, -0.077f, 0.996f).normalized;

        private static Quaternion Hand(ArmAngles a)
        {
            SwordCarryArm.Solve(a, Quaternion.identity, out _, out _, out Quaternion hand);
            return hand;
        }

        private static float Elevation(Vector3 direction) => Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1.0f, 1.0f)) * Mathf.Rad2Deg;

        private static float Outward(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        [Test]
        public void AtZeroTheArmHangsStraightDownThumbForwardPalmToTheThigh()
        {
            SwordCarryArm.Solve(default, Quaternion.identity, out Quaternion upper, out Quaternion forearm, out Quaternion hand);
            foreach (Quaternion bone in new[] { upper, forearm, hand })
            {
                Assert.Less(Vector3.Angle(bone * Vector3.right, Vector3.down), 0.01f, "the arm does not hang straight down");
                Assert.Less(Vector3.Angle(bone * Vector3.forward, Vector3.forward), 0.01f, "the thumb's side does not face forward");
                Assert.Less(Vector3.Angle(bone * Vector3.down, Vector3.left), 0.01f, "the palm does not face the thigh");
            }
        }

        [Test]
        public void EachJointTurnsTheWayItsNameSays()
        {
            SwordCarryArm.Solve(new ArmAngles(30, 0, 0, 0, 0, 0, 0), Quaternion.identity, out Quaternion upper, out _, out _);
            Assert.Greater((upper * Vector3.right).z, 0.4f, "flexion does not swing the arm forward");
            SwordCarryArm.Solve(new ArmAngles(0, 30, 0, 0, 0, 0, 0), Quaternion.identity, out upper, out _, out _);
            Assert.Greater((upper * Vector3.right).x, 0.4f, "abduction does not lift the arm out from the side");
            SwordCarryArm.Solve(new ArmAngles(0, 0, 0, 60, 0, 0, 0), Quaternion.identity, out _, out Quaternion forearm, out _);
            Assert.Greater((forearm * Vector3.right).z, 0.8f, "the elbow does not bend the forearm forward");
            SwordCarryArm.Solve(new ArmAngles(0, 0, 0, 0, 40, 0, 0), Quaternion.identity, out _, out forearm, out _);
            Assert.Greater((forearm * Vector3.forward).x, 0.6f, "supination does not turn the thumb outward");
            Assert.Greater((Hand(new ArmAngles(0, 0, 0, 0, 0, 30, 0)) * Vector3.forward).y, -0.6f);
            Assert.Less((Hand(new ArmAngles(0, 0, 0, 0, 0, 30, 0)) * Vector3.forward).y, -0.4f, "ulnar deviation does not tip the thumb's side down");
            Assert.Less((Hand(new ArmAngles(0, 0, 0, 0, 0, 0, 40)) * Vector3.right).x, -0.6f, "flexing the wrist does not turn the fingers toward the palm");
            SwordCarryArm.Solve(new ArmAngles(0, 0, 30, 0, 0, 0, 0), Quaternion.identity, out upper, out _, out _);
            Assert.Greater((upper * Vector3.forward).x, 0.4f, "external rotation does not turn the arm's front outward");
        }

        [Test]
        public void TheCarriesPointTheSwordForwardDownAndClearOfTheLegs()
        {
            foreach ((string name, ArmAngles pose) in new[] { ("walk", SwordCarryArm.Walk), ("run", SwordCarryArm.Run), ("idle", SwordCarryArm.Idle) })
            {
                foreach (Vector3 hold in new[] { DefaultHold, OwnersHold })
                {
                    Vector3 blade = Hand(pose) * hold;
                    Assert.Greater(blade.z, 0.6f, $"{name}: the sword does not point forward");
                    Assert.That(Elevation(blade), Is.InRange(-45.0f, -15.0f), $"{name}: the sword is not angled forward and down");
                    Assert.That(Outward(blade), Is.InRange(10.0f, 40.0f), $"{name}: the sword is not angled out, clear of the legs");
                }
                Assert.LessOrEqual(pose.Deviation, SwordCarryArm.MaxDeviation, $"{name}: the wrist is bent past what a wrist does");
                Assert.Less(Mathf.Abs(pose.WristFlex), 10.0f, $"{name}: the wrist is folded");
            }
        }

        [Test]
        public void ASwingMovesTheHandButTheBladeOnlyBobs()
        {
            foreach ((string name, ArmAngles pose, SwordCarryArm.SwingShares shares, float swing) in new[]
            {
                ("walk", SwordCarryArm.Walk, SwordCarryArm.WalkSwing, 30.0f),
                ("run", SwordCarryArm.Run, SwordCarryArm.RunSwing, 45.0f),
            })
            {
                float highest = -90.0f, lowest = 90.0f, foremost = -9.0f, hindmost = 9.0f;
                for (int i = 0; i < 64; i++)
                {
                    float phase = i / 64.0f * 2.0f * Mathf.PI;
                    float now = swing * Mathf.Sin(phase);
                    float elbow = swing * Mathf.Sin(phase - shares.ElbowLag * 2.0f * Mathf.PI);
                    float wrist = swing * Mathf.Sin(phase - shares.WristLag * 2.0f * Mathf.PI);
                    ArmAngles a = SwordCarryArm.Swinging(pose, shares, now, elbow, wrist);
                    Assert.That(a.Deviation, Is.InRange(0.0f, SwordCarryArm.MaxDeviation), $"{name}: the wrist bends past what a wrist does");
                    Assert.GreaterOrEqual(a.Elbow, 0.0f, $"{name}: the elbow bends backward");
                    SwordCarryArm.Solve(a, Quaternion.identity, out Quaternion upper, out Quaternion forearm, out Quaternion hand);
                    Vector3 wristAt = upper * Vector3.right * 0.52f + forearm * Vector3.right * 0.37f;
                    foremost = Mathf.Max(foremost, wristAt.z);
                    hindmost = Mathf.Min(hindmost, wristAt.z);
                    float elevation = Elevation(hand * OwnersHold);
                    highest = Mathf.Max(highest, elevation);
                    lowest = Mathf.Min(lowest, elevation);
                }
                Assert.Greater(foremost - hindmost, 0.12f, $"{name}: the sword hand does not swing");
                Assert.Less(highest - lowest, 12.0f, $"{name}: the blade see-saws as the arm swings (it bobs {highest - lowest:0} degrees)");
            }
        }

        [Test]
        public void ALoopingSwingIsReadAcrossItsWrap()
        {
            float[] times = { 0.0f, 0.25f, 0.5f, 0.75f };
            float[] values = { 0.0f, 10.0f, 0.0f, -10.0f };
            Assert.AreEqual(5.0f, SwordCarryArm.SampleLooping(times, values, 1.0f, 0.125f), 1e-4f);
            Assert.AreEqual(-5.0f, SwordCarryArm.SampleLooping(times, values, 1.0f, 0.875f), 1e-4f, "the stretch from the last sample back to the first is not read");
            Assert.AreEqual(-5.0f, SwordCarryArm.SampleLooping(times, values, 1.0f, -0.125f), 1e-4f, "a time before the start does not wrap");
            Assert.AreEqual(10.0f, SwordCarryArm.SampleLooping(times, values, 1.0f, 2.25f), 1e-4f, "a later cycle is not the same");
        }
    }
}
