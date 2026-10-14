using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The procedural sword attacks on paper: every one starts and ends in its ready stance, winds up and holds before it strikes, is fastest at
    /// the contact (inside its live window), keeps the edge perpendicular to the blade, keeps the tip out of the ground, and the one-handed
    /// first attack is the horizontal slash the owner asked for.
    /// </summary>
    public sealed class SwordStrikesTests
    {
        /// <summary>Shoulder to wrist on the rig (the hand offsets are in these), and the blade from the wrist (the grip's own offset included).</summary>
        private static readonly float ArmLength = BodyRig.WristT * BodyRig.ArmLength;

        private static float BladeLength(SwordStrikeKind kind) => (SwordStrikes.IsOneHanded(kind) ? 1.39f : 2.1f) + 0.1f;

        [Test]
        public void NamesRoundTripAndGuardsAreKnown()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                string name = SwordStrikes.NameOf(kind);
                Assert.IsTrue(SwordStrikes.TryGet(name, out SwordStrikeKind back), name);
                Assert.AreEqual(kind, back);
                Assert.IsTrue(SwordStrikes.Has(name));
                Assert.IsFalse(SwordStrikes.TryGetGuard(name, out _), $"{name} is an attack, not a guard");
            }
            Assert.IsTrue(SwordStrikes.TryGetGuard(SwordStrikes.Guard1HName, out bool oneHanded) && oneHanded);
            Assert.IsTrue(SwordStrikes.TryGetGuard(SwordStrikes.Guard2HName, out oneHanded) && !oneHanded);
            Assert.IsTrue(SwordStrikes.Has(SwordStrikes.Guard2HName));
            Assert.IsFalse(SwordStrikes.Has("sword_attack"), "a clip's name is not a posed strike");
            Assert.IsFalse(SwordStrikes.TryGet("punch_jab", out _));
        }

        [Test]
        public void FourAttacksAreOneHandedAndFourTwoHanded()
        {
            int oneHanded = 0;
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordFrame mid = SwordStrikes.Evaluate(kind, 0.5f);
                Assert.AreEqual(SwordStrikes.IsOneHanded(kind), !mid.TwoHanded, $"{kind}'s frames disagree with its handedness");
                if (SwordStrikes.IsOneHanded(kind)) oneHanded++;
            }
            Assert.AreEqual(4, oneHanded);
            Assert.AreEqual(SwordStrikeKind.Slash1H, SwordStrikes.All[0]);
        }

        [Test]
        public void EveryAttackStartsAndEndsInItsReadyStance()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordFrame ready = SwordStrikes.Ready(SwordStrikes.IsOneHanded(kind));
                SwordFrame start = SwordStrikes.Evaluate(kind, 0.0f);
                SwordFrame end = SwordStrikes.Evaluate(kind, 1.0f);
                Assert.Less(Vector3.Distance(start.Hand, ready.Hand), 0.001f, $"{kind} does not start in the ready stance");
                Assert.Less(Vector3.Distance(end.Hand, ready.Hand), 0.001f, $"{kind} does not end in the ready stance");
                Assert.Less(Vector3.Angle(end.Blade, ready.Blade), 0.5f, $"{kind} ends with the blade elsewhere");
                Assert.AreEqual(ready.TwistDegrees, end.TwistDegrees, 0.01f, $"{kind} ends with the torso still turned");
                Assert.AreEqual(0.0f, end.HipDrop, 0.001f, $"{kind} ends with the hips still dropped");
            }
        }

        [Test]
        public void TheWindUpHoldsStillBeforeTheStrike()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                float active = SwordStrikes.ActiveStart(kind);
                // Just before the blade goes live, the hand has stopped moving (the anticipation's hold).
                SwordFrame a = SwordStrikes.Evaluate(kind, active - 0.05f);
                SwordFrame b = SwordStrikes.Evaluate(kind, active - 0.02f);
                Assert.Less(Vector3.Distance(a.Hand, b.Hand), 0.01f, $"{kind} is still winding up when it should be holding");
                Assert.Less(Vector3.Distance(SwordStrikes.Evaluate(kind, 0.0f).Hand, a.Hand) * ArmLength, 2.5f);
                Assert.Greater(Vector3.Distance(SwordStrikes.Evaluate(kind, 0.0f).Hand, a.Hand) * ArmLength, 0.25f, $"{kind} barely winds up");
            }
        }

        [Test]
        public void TheBladeIsFastestInsideItsLiveWindow()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                float blade = BladeLength(kind);
                float seconds = SwordStrikes.Seconds(kind);
                const int samples = 600;
                float fastest = 0.0f, fastestAt = 0.0f;
                Vector3 previous = SwordStrikes.TipOffset(SwordStrikes.Evaluate(kind, 0.0f), ArmLength, blade);
                for (int i = 1; i <= samples; i++)
                {
                    float t = i / (float)samples;
                    Vector3 tip = SwordStrikes.TipOffset(SwordStrikes.Evaluate(kind, t), ArmLength, blade);
                    float speed = Vector3.Distance(tip, previous) / (seconds / samples);
                    previous = tip;
                    if (speed > fastest)
                    {
                        fastest = speed;
                        fastestAt = t;
                    }
                }
                Assert.GreaterOrEqual(fastestAt, SwordStrikes.ActiveStart(kind), $"{kind} is fastest at {fastestAt:0.00}, before the blade goes live");
                Assert.LessOrEqual(fastestAt, SwordStrikes.ActiveEnd(kind), $"{kind} is fastest at {fastestAt:0.00}, after the blade stops");
                Assert.Greater(fastest, 8.0f, $"{kind}'s tip only reaches {fastest:0.0} m/s: a wave, not a cut");
                float contact = SwordStrikes.ContactFraction(kind);
                Assert.GreaterOrEqual(contact, SwordStrikes.ActiveStart(kind), $"{kind}: the contact is before the live window");
                Assert.LessOrEqual(contact, SwordStrikes.ActiveEnd(kind), $"{kind}: the contact is after the live window");
            }
        }

        [Test]
        public void TheEdgeStaysPerpendicularToTheBladeAndBothStayUnit()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                for (int i = 0; i <= 100; i++)
                {
                    SwordFrame f = SwordStrikes.Evaluate(kind, i / 100.0f);
                    Assert.AreEqual(1.0f, f.Blade.magnitude, 0.001f, $"{kind} t={i / 100.0f:0.00}: the blade is not unit");
                    Assert.AreEqual(1.0f, f.Edge.magnitude, 0.001f, $"{kind} t={i / 100.0f:0.00}: the edge is not unit");
                    Assert.AreEqual(0.0f, Vector3.Dot(f.Blade, f.Edge), 0.001f, $"{kind} t={i / 100.0f:0.00}: the edge is not perpendicular to the blade");
                }
            }
        }

        [Test]
        public void TheTipNeverGoesThroughTheGround()
        {
            // The right shoulder is 1.9 m up; the hand's offset is in arm lengths, the blade in metres, the hips' drop lowers everything.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                float lowest = float.MaxValue;
                float lowestAt = 0.0f;
                for (int i = 0; i <= 200; i++)
                {
                    SwordFrame f = SwordStrikes.Evaluate(kind, i / 200.0f);
                    float tipY = BodyRig.ShoulderY + SwordStrikes.TipOffset(f, ArmLength, BladeLength(kind)).y - f.HipDrop;
                    if (tipY < lowest)
                    {
                        lowest = tipY;
                        lowestAt = i / 200.0f;
                    }
                }
                Assert.Greater(lowest, 0.12f, $"{kind}: the tip dips to {lowest:0.00} m at t={lowestAt:0.00}");
            }
        }

        [Test]
        public void TheOneHandedFirstAttackIsAHorizontalSlashFromRightToLeft()
        {
            // Owner, 2026-10-13: "make the first attack with a one handed sword a horizontal slash".
            SwordStrikeKind kind = SwordStrikeKind.Slash1H;
            SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f);
            SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
            SwordFrame follow = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.08f);

            Assert.Greater(windup.Hand.x, 0.5f, "the slash is not wound up out to the right");
            Assert.Less(follow.Hand.x, -0.3f, "the slash does not carry through to the left");
            Assert.Less(Mathf.Abs(contact.Blade.y), 0.2f, $"the blade is not flat at the contact (y {contact.Blade.y:0.00})");
            Assert.Less(Mathf.Abs(contact.Hand.y - windup.Hand.y), 0.25f, "the hand's height changes too much for a horizontal cut");
            // The edge leads the motion: at the contact the tip is moving to the left, and the edge faces the way the tip moves ACROSS the
            // blade (a slash also draws along its own length, which no edge can face).
            Vector3 before = SwordStrikes.TipOffset(SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind) - 0.02f), ArmLength, 1.39f);
            Vector3 at = SwordStrikes.TipOffset(contact, ArmLength, 1.39f);
            Vector3 motion = (at - before).normalized;
            Vector3 across = (motion - contact.Blade * Vector3.Dot(motion, contact.Blade)).normalized;
            Assert.Less(motion.x, -0.5f, "the tip is not sweeping leftward at the contact");
            Assert.Greater(Vector3.Dot(contact.Edge, across), 0.7f, "the edge is not leading the cut: the flat of the blade arrives");
            Assert.IsFalse(contact.TwoHanded);
        }

        [Test]
        public void TheTorsoWindsUpAgainstTheCutAndUnwindsThroughIt()
        {
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut2H, SwordStrikeKind.Sweep2H })
            {
                float windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f).TwistDegrees;
                float through = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.06f).TwistDegrees;
                Assert.Greater(windup, 15.0f, $"{kind} does not turn the torso to the right in its wind-up");
                Assert.Less(through, -15.0f, $"{kind} does not unwind the torso through the cut");
            }
        }

        [Test]
        public void TheHeaviesDropTheHipsAndLeanInTheLightsLess()
        {
            float heavyDrop = 0.0f, lightDrop = 0.0f;
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                float most = 0.0f, lean = 0.0f;
                for (int i = 0; i <= 100; i++)
                {
                    SwordFrame f = SwordStrikes.Evaluate(kind, i / 100.0f);
                    most = Mathf.Max(most, f.HipDrop);
                    lean = Mathf.Max(lean, f.LeanDegrees);
                }
                if (SwordStrikes.IsHeavy(kind))
                {
                    heavyDrop = Mathf.Max(heavyDrop, most);
                    Assert.Greater(lean, 15.0f, $"{kind} does not lean into the blow");
                }
                else
                {
                    lightDrop = Mathf.Max(lightDrop, most);
                }
            }
            Assert.Greater(heavyDrop, lightDrop, "the heavies should sink lower than the lights");
            Assert.Greater(heavyDrop, 0.1f);
        }

        [Test]
        public void EveryAttackHasItsOwnPath()
        {
            for (int i = 0; i < SwordStrikes.All.Length; i++)
            {
                for (int j = i + 1; j < SwordStrikes.All.Length; j++)
                {
                    Vector3 a = SwordStrikes.Evaluate(SwordStrikes.All[i], 0.35f).Hand;
                    Vector3 b = SwordStrikes.Evaluate(SwordStrikes.All[j], 0.35f).Hand;
                    Assert.Greater(Vector3.Distance(a, b), 0.05f, $"{SwordStrikes.All[i]} and {SwordStrikes.All[j]} wind up to the same place");
                }
            }
        }

        [Test]
        public void TheFreeHandIsRaisedForOneHandedAttacksAndOnTheGripForTwoHanded()
        {
            SwordFrame one = SwordStrikes.Evaluate(SwordStrikeKind.Cut1H, 0.3f);
            Assert.IsFalse(one.TwoHanded);
            Assert.Greater(one.FreeHand.z, 0.2f, "the free hand should be raised in front");
            Assert.Greater(one.FreeHand.x, 0.0f, "the free hand should be across the body (inward)");

            SwordFrame two = SwordStrikes.Evaluate(SwordStrikeKind.Cut2H, 0.3f);
            Assert.IsTrue(two.TwoHanded);

            SwordFrame guard1 = SwordStrikes.Guard(true);
            SwordFrame guard2 = SwordStrikes.Guard(false);
            Assert.IsFalse(guard1.TwoHanded);
            Assert.IsTrue(guard2.TwoHanded);
            Assert.Greater(guard2.Blade.y, 0.7f, "the two-handed guard holds the blade up");
            Assert.Less(Mathf.Abs(guard1.Blade.x), 1.0f);
            Assert.Greater(Mathf.Abs(guard1.Blade.x), 0.5f, "the one-handed guard holds the blade across the body");
        }

        [Test]
        public void TheTwoHandedWindUpsKeepTheGripWithinTheLeftArmsReach()
        {
            // The left hand holds the grip 20 cm below the right: wherever the right hand goes, that point must be reachable from the LEFT
            // shoulder (0.7 m to the left), or the support hand comes off the handle.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                if (SwordStrikes.IsOneHanded(kind)) continue;
                float worst = 0.0f;
                for (int i = 0; i <= 100; i++)
                {
                    SwordFrame f = SwordStrikes.Evaluate(kind, i / 100.0f);
                    Vector3 rightHand = f.Hand * ArmLength;                 // from the right shoulder
                    Vector3 grip = rightHand - f.Blade * 0.2f;              // the left hand's point on the handle
                    Vector3 fromLeftShoulder = grip + Vector3.right * (2.0f * BodyRig.ShoulderX);
                    worst = Mathf.Max(worst, fromLeftShoulder.magnitude);
                }
                Assert.Less(worst, ArmLength + 0.1f, $"{kind} takes the grip {worst:0.00} m from the left shoulder");
            }
        }
    }
}
