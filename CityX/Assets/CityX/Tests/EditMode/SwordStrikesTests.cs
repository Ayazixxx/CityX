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
        public void FiveAttacksAreOneHandedAndFourTwoHanded()
        {
            int oneHanded = 0;
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordFrame mid = SwordStrikes.Evaluate(kind, 0.5f);
                Assert.AreEqual(SwordStrikes.IsOneHanded(kind), !mid.TwoHanded, $"{kind}'s frames disagree with its handedness");
                if (SwordStrikes.IsOneHanded(kind)) oneHanded++;
            }
            Assert.AreEqual(5, oneHanded);
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
                Assert.AreEqual(ready.HipTwistDegrees, end.HipTwistDegrees, 0.01f, $"{kind} ends with the hips still turned");
                Assert.AreEqual(ready.HipDrop, end.HipDrop, 0.001f, $"{kind} ends with the hips still dropped");
                Assert.AreEqual(ready.StanceDepth, end.StanceDepth, 0.001f, $"{kind} ends with the feet still in its lunge");
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

        /// <summary>
        /// One-handed, the empty hand is relaxed (owner, 2026-10-17: "i dont like the way left arm is in holding shield position after swinging.
        /// make it relaxed"): low at the side through a cut, swinging against the sword, and left to the clips in the stance and the guard.
        /// </summary>
        [Test]
        public void TheFreeHandHangsRelaxedForOneHandedAttacksAndIsOnTheGripForTwoHanded()
        {
            SwordFrame one = SwordStrikes.Evaluate(SwordStrikeKind.Cut1H, 0.3f);
            Assert.IsFalse(one.TwoHanded);
            Assert.Less(one.FreeHand.y, -0.7f, "the free hand should hang low at the side, not be held up in front");
            Assert.LessOrEqual(one.FreeHand.x, 0.0f, "the free hand should hang at its own side, not across the body");
            Assert.AreEqual(0.0f, SwordStrikes.Ready(true).FreeHandWeight, 0.001f, "the stance after a swing holds the free hand up instead of leaving it relaxed");
            Assert.AreEqual(0.0f, SwordStrikes.Guard(true).FreeHandWeight, 0.001f, "the guard holds the free hand up instead of leaving it relaxed");
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                if (kind == SwordStrikeKind.Thrust1H || kind == SwordStrikeKind.Overhead1H)   // (the chain's cuts swing it against the blade: below)
                {
                    float most = float.MinValue, least = float.MaxValue;
                    for (int i = 0; i <= 50; i++)
                    {
                        float z = SwordStrikes.Evaluate(kind, i / 50.0f).FreeHand.z;
                        most = Mathf.Max(most, z);
                        least = Mathf.Min(least, z);
                    }
                    Assert.LessOrEqual(most - least, 2.0f * SwordStrikes.FreeHandBalanceSwing + 0.001f, $"{kind}: the free hand flails");
                }
            }

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

        /// <summary>
        /// THE REBOUND (owner, 2026-10-17: "implement a stagger system for if you swing at a block"): a blade off a guard is thrown up and back,
        /// the body leaning away, then gathered back into the ready stance -- and it travels by name like a swing.
        /// </summary>
        [Test]
        public void TheReboundThrowsTheBladeBackUpAndGathersIntoTheStance()
        {
            foreach (bool oneHanded in new[] { true, false })
            {
                SwordFrame ready = SwordStrikes.Ready(oneHanded);
                SwordFrame thrown = SwordStrikes.Rebound(oneHanded, 0.35f);
                SwordFrame end = SwordStrikes.Rebound(oneHanded, 1.0f);
                string hold = oneHanded ? "1h" : "2h";
                Assert.Greater(thrown.Blade.y, 0.6f, $"{hold}: the blade is not thrown up");
                Assert.Less(thrown.Blade.z, 0.0f, $"{hold}: the blade is not thrown back");
                Assert.Greater(thrown.Hand.y, ready.Hand.y + 0.1f, $"{hold}: the hand is not knocked up");
                Assert.Less(thrown.LeanDegrees, 0.0f, $"{hold}: the body does not lean away");
                Assert.Less(Vector3.Distance(end.Hand, ready.Hand), 0.01f, $"{hold}: the rebound does not end in the ready stance");
                Assert.AreEqual(!oneHanded, thrown.TwoHanded);
            }
            Assert.IsTrue(SwordStrikes.Has(SwordStrikes.Rebound1HName));
            Assert.IsTrue(SwordStrikes.TryGetRebound(SwordStrikes.ReboundName(false), out bool one) && !one);
            Assert.IsTrue(SwordStrikes.TryGetRebound(SwordStrikes.ReboundName(true), out one) && one);
        }

        /// <summary>Owner, 2026-11-04: "really have the attackers weapon bounce off of the blocker": well above the shoulder, the body thrown back on its heels, the free arm flung out to catch it.</summary>
        [Test]
        public void TheReboundThrowsTheWholeBodyNotJustTheArm()
        {
            foreach (bool oneHanded in new[] { true, false })
            {
                SwordFrame thrown = SwordStrikes.Rebound(oneHanded, 0.35f);
                Assert.Greater(SwordStrikes.HandPoint(thrown).y, BodyRig.ShoulderY, "the hand is not knocked above the shoulder");
                Assert.Less(thrown.LeanDegrees, -12.0f, "the body is not thrown back");
                Assert.Greater(thrown.TwistDegrees, 30.0f, "the shoulders are not turned open");
                Assert.Greater(thrown.StanceDepth, SwordStrikes.Ready(oneHanded).StanceDepth + 0.2f, "the feet do not catch the body");
            }
            Assert.Greater(SwordStrikes.Rebound(true, 0.35f).FreeHand.z, 0.5f, "the free arm is not flung out");
            Assert.Less(Vector3.Distance(SwordStrikes.Rebound(true, 1.0f).FreeHand, SwordStrikes.FreeHandRelaxed), SwordStrikes.FreeHandBalanceSwing, "the free arm does not end relaxed");
        }

        [Test]
        public void AGuardThatTakesABlowIsKnockedBackAndComesBack()
        {
            foreach (bool oneHanded in new[] { true, false })
            {
                SwordFrame guard = SwordStrikes.Guard(oneHanded);
                SwordFrame start = SwordStrikes.GuardJolted(guard, 0.0f);
                SwordFrame end = SwordStrikes.GuardJolted(guard, 1.0f);
                Assert.Less(Vector3.Distance(start.Hand, guard.Hand), 0.001f, "the guard jumps as the blow lands");
                Assert.Less(Vector3.Distance(end.Hand, guard.Hand), 0.001f, "the guard does not come back");
                float most = 0.0f;
                float mostAt = 0.0f;
                for (float t = 0.0f; t <= 1.0f; t += 0.01f)
                {
                    float back = guard.Hand.z - SwordStrikes.GuardJolted(guard, t).Hand.z;
                    if (back > most)
                    {
                        most = back;
                        mostAt = t;
                    }
                }
                Assert.Greater(most, 0.2f, "the hand is hardly driven back");
                Assert.Less(mostAt, 0.3f, "the blow takes its time: it should be sudden, the recovery slower");
                SwordFrame peak = SwordStrikes.GuardJolted(guard, mostAt);
                Assert.Less(peak.Blade.z, guard.Blade.z, "the tip is not knocked back");
                Assert.Less(peak.LeanDegrees, guard.LeanDegrees - 8.0f, "the body does not take the weight of it");
            }
        }

        [Test]
        public void TheTwoHandedWindUpsKeepTheGripWithinTheLeftArmsReach()
        {
            // The left hand holds the grip 20 cm below the right: wherever the right hand goes, that point must be reachable from the LEFT
            // shoulder -- where the shoulders' turn has put it -- or the support hand comes off the handle. The carry and the guard too.
            var poses = new System.Collections.Generic.List<(string, SwordFrame)>();
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                if (SwordStrikes.IsOneHanded(kind)) continue;
                for (int i = 0; i <= 100; i++)
                {
                    poses.Add(($"{kind} t={i / 100.0f:0.00}", SwordStrikes.Evaluate(kind, i / 100.0f)));
                }
            }
            poses.Add(("ready", SwordStrikes.Ready(false)));
            poses.Add(("guard", SwordStrikes.Guard(false)));
            poses.Add(("carry", SwordStrikes.Carry(0.0f)));
            poses.Add(("carry at a sprint", SwordStrikes.Carry(1.0f)));
            float worst = 0.0f;
            string worstAt = "";
            foreach ((string label, SwordFrame f) in poses)
            {
                Vector3 grip = SwordStrikes.HandPoint(f) - f.Blade * 0.2f;   // the left hand's point on the handle
                float reach = Vector3.Distance(grip, SwordStrikes.LeftShoulder(f.TwistDegrees));
                if (reach > worst)
                {
                    worst = reach;
                    worstAt = label;
                }
            }
            Assert.Less(worst, ArmLength + 0.01f, $"{worstAt} takes the grip {worst:0.00} m from the left shoulder (the arm is {ArmLength:0.00})");
        }

        [Test]
        public void TheSwordHandIsNeverAskedToReachFurtherThanTheArm()
        {
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                for (int i = 0; i <= 100; i++)
                {
                    SwordFrame f = SwordStrikes.Evaluate(kind, i / 100.0f);
                    Assert.Less(f.Hand.magnitude, 1.0f, $"{kind} t={i / 100.0f:0.00}: the hand is {f.Hand.magnitude:0.00} arm lengths from the shoulder");
                }
            }
        }

        /// <summary>The blade's velocity ACROSS itself at <paramref name="t"/> (a slash also draws along its own length, which no edge can face), body axes.</summary>
        private static Vector3 Across(SwordStrikeKind kind, float t, float dt = 0.004f)
        {
            float blade = BladeLength(kind) * 0.6f;   // a point well out along the blade, where it cuts
            SwordFrame a = SwordStrikes.Evaluate(kind, t - dt);
            SwordFrame b = SwordStrikes.Evaluate(kind, t + dt);
            Vector3 pa = SwordStrikes.HandPoint(a) + a.Blade * blade;
            Vector3 pb = SwordStrikes.HandPoint(b) + b.Blade * blade;
            SwordFrame f = SwordStrikes.Evaluate(kind, t);
            Vector3 motion = pb - pa;
            return motion - f.Blade * Vector3.Dot(motion, f.Blade);
        }

        [Test]
        public void TheEdgeLeadsEveryCut()
        {
            // Owner, 2026-10-16: "use the spikesword as guide that one is clear which side should be hitting the enemy and it is the side with the
            // spikes". The spikes are the edge (SwordWeapon: local -X): through every attack's live window the edge must face the way the blade
            // is moving across itself -- the spikes arrive first, never the flat or the back. (Not the thrust: it leads with its point.)
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                if (kind == SwordStrikeKind.Thrust1H)
                {
                    continue;
                }
                float start = SwordStrikes.ActiveStart(kind), end = SwordStrikes.ActiveEnd(kind);
                float sum = 0.0f, worst = 1.0f, worstAt = 0.0f;
                int samples = 0;
                for (float t = start + 0.01f; t <= end - 0.01f; t += 0.01f)
                {
                    Vector3 across = Across(kind, t);
                    if (across.magnitude < 1e-4f)
                    {
                        continue;   // a thrust's held point: nothing moving across the blade
                    }
                    float lead = Vector3.Dot(SwordStrikes.Evaluate(kind, t).Edge, across.normalized);
                    sum += lead;
                    samples++;
                    if (lead < worst)
                    {
                        worst = lead;
                        worstAt = t;
                    }
                }
                Assert.Greater(samples, 3, $"{kind}: the blade never moves across itself in its live window");
                Assert.Greater(sum / samples, 0.75f, $"{kind}: on average the edge is not leading the cut ({sum / samples:0.00})");
                Assert.Greater(worst, 0.2f, $"{kind}: at t={worstAt:0.00} the edge faces away from the cut ({worst:0.00}): the flat or the back arrives");
            }
        }

        [Test]
        public void TheBladeCarriesItsSpeedThroughTheContact()
        {
            // No dead stop where the strike meets the follow-through (the slam aside: it hits the floor on purpose, and a thrust stops at full
            // stretch): the blade's turning rate just after the contact is within a third of what it was just before.
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H, SwordStrikeKind.Sweep2H, SwordStrikeKind.Reverse2H, SwordStrikeKind.Cut2H, SwordStrikeKind.Finisher1H })
            {
                float contact = SwordStrikes.ContactFraction(kind);
                float before = Vector3.Angle(SwordStrikes.Evaluate(kind, contact - 0.012f).Blade, SwordStrikes.Evaluate(kind, contact - 0.002f).Blade);
                float after = Vector3.Angle(SwordStrikes.Evaluate(kind, contact + 0.002f).Blade, SwordStrikes.Evaluate(kind, contact + 0.012f).Blade);
                Assert.Greater(before, 1.0f, $"{kind}: the blade is not turning into the contact");
                Assert.AreEqual(1.0f, after / before, 0.35f, $"{kind}: the blade turns {before:0.0} deg just before the contact and {after:0.0} just after -- it stops dead");
            }
        }

        [Test]
        public void TheTwoHandedLightChainIsAFlatForehandThenAFlatBackhand()
        {
            // Owner, 2026-10-16: "i like the two handed horizontal slashes of this linked game".
            foreach ((SwordStrikeKind kind, float sweep) in new[] { (SwordStrikeKind.Sweep2H, -1.0f), (SwordStrikeKind.Reverse2H, 1.0f) })
            {
                SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f);
                SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
                SwordFrame follow = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.04f);
                Assert.IsTrue(contact.TwoHanded);
                // Flat: the blade stays within 15 degrees of the level from the wind-up to past the contact.
                for (float t = SwordStrikes.ActiveStart(kind) - 0.03f; t <= SwordStrikes.ActiveEnd(kind) + 0.04f; t += 0.01f)
                {
                    Vector3 b = SwordStrikes.Evaluate(kind, t).Blade;
                    Assert.Less(Mathf.Abs(b.y), Mathf.Sin(15.0f * Mathf.Deg2Rad), $"{kind} t={t:0.00}: the blade is not flat (y {b.y:0.00})");
                }
                // Across the front the right way: wound up on one side, out past the other.
                Assert.Greater(-sweep * windup.Blade.x, 0.5f, $"{kind} is not wound up on the far side");
                Assert.Greater(sweep * follow.Blade.x, 0.5f, $"{kind} does not carry through to the other side");
                Assert.Greater(contact.Blade.z, 0.9f, $"{kind}'s blade is not across the front at the contact");
                // At chest height, the arms reaching out.
                Vector3 hand = SwordStrikes.HandPoint(contact);
                Assert.AreEqual(1.45f, hand.y, 0.15f, $"{kind}: the hands are not at chest height at the contact");
                Assert.Greater(hand.z, 0.55f, $"{kind}: the hands are still close to the chest at the contact");
            }
            var body = new GameObject("SwordChain");
            try
            {
                var combat = body.AddComponent<SoulsSwordCombat>();
                Assert.AreEqual(SwordStrikes.Sweep2HName, combat.Light1.Animation, "the forehand is the first light");
                Assert.AreEqual(SwordStrikes.Reverse2HName, combat.Light2.Animation, "the backhand is the second light");
                Assert.AreEqual(SwordStrikes.Cut2HName, combat.Light3.Animation, "the diagonal is the finisher");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        /// <summary>
        /// The whole body behind the first two cuts, not an arm waving a stiff one: leaning away in the wind-up with the stride already open, into the blow at the
        /// contact, and the free arm swinging against the blade. Long and out at its own side: bent, it reads as a hand on the hip, and across, in front of the crotch.
        /// </summary>
        [Test]
        public void TheFirstTwoCutsPutTheBodyBehindTheBlade()
        {
            foreach ((SwordStrikeKind kind, float armAtWindUp) in new[] { (SwordStrikeKind.Slash1H, 1.0f), (SwordStrikeKind.Cut1H, -1.0f) })
            {
                SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.StrikeStart(kind));
                SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
                SwordFrame follow = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.05f);
                Assert.Less(windup.LeanDegrees, 0.0f, $"{kind} does not lean away in its wind-up");
                Assert.Greater(contact.LeanDegrees, 10.0f, $"{kind} does not drive in over the front foot");
                Assert.Greater(windup.StanceDepth, SwordStrikes.Ready(true).StanceDepth + 0.1f, $"{kind}: the stride opens on the strike, so the foot lands after the blow");
                Assert.Greater(armAtWindUp * windup.FreeHand.z, 0.3f, $"{kind}: the free arm is not on the far side of the swing at the wind-up");
                Assert.Less(armAtWindUp * contact.FreeHand.z, -0.3f, $"{kind}: the free arm does not swing across against the blade");
                foreach (SwordFrame held in new[] { windup, contact, follow })
                {
                    Assert.Greater(held.FreeHand.magnitude, 0.9f, $"{kind}: the free arm is bent: it reads as a hand on the hip");
                    Assert.Less(held.FreeHand.x, -0.1f, $"{kind}: the free hand is across the body");
                    Assert.Less(held.FreeHand.y, -0.6f, $"{kind}: the free hand is held up, not swinging low");
                }
                Assert.Less(Vector3.Distance(SwordStrikes.Evaluate(kind, 1.0f).FreeHand, SwordStrikes.FreeHandRelaxed), SwordStrikes.FreeHandBalanceSwing,
                    $"{kind}: the free arm does not end relaxed");
                // Round from the shoulder as it crosses: straight across the chord it would fold on the way.
                for (float t = SwordStrikes.StrikeStart(kind); t < SwordStrikes.ContactFraction(kind); t += 0.005f)
                {
                    Assert.Greater(SwordStrikes.Evaluate(kind, t).FreeHand.magnitude, 0.9f, $"{kind} t={t:0.00}: the free arm folds as it swings");
                }
            }
        }

        /// <summary>The power travels up the body: halfway through the strike the hips have done most of their turn, the shoulders less, the arm less, the blade least. All arrive together.</summary>
        [Test]
        public void TheFirstTwoCutsSetOffFromTheHipsAndTheBladeComesLast()
        {
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H })
            {
                float start = SwordStrikes.StrikeStart(kind), end = SwordStrikes.ContactFraction(kind);
                SwordFrame a = SwordStrikes.Evaluate(kind, start);
                SwordFrame mid = SwordStrikes.Evaluate(kind, (start + end) * 0.5f);
                SwordFrame b = SwordStrikes.Evaluate(kind, end);
                float hips = (mid.HipTwistDegrees - a.HipTwistDegrees) / (b.HipTwistDegrees - a.HipTwistDegrees);
                float shoulders = (mid.TwistDegrees - a.TwistDegrees) / (b.TwistDegrees - a.TwistDegrees);
                float blade = Vector3.Angle(a.Blade, mid.Blade) / Vector3.Angle(a.Blade, b.Blade);
                Assert.Greater(hips, shoulders, $"{kind}: the shoulders are ahead of the hips");
                Assert.Greater(shoulders, blade, $"{kind}: the blade is ahead of the shoulders");
                Assert.AreEqual(0.125f, blade, 0.05f, $"{kind}: the blade's own whip has changed");
            }
        }

        /// <summary>One step a cut, on the foot the cut strides on, and nothing in the air for the blow. The feet catch up after it.</summary>
        [Test]
        public void EachCutOfTheChainStepsOneFootAndKeepsBothDownForTheBlow()
        {
            foreach ((SwordStrikeKind kind, FootSteps stride) in new[]
            {
                (SwordStrikeKind.Slash1H, FootSteps.OnlyLeft), (SwordStrikeKind.Cut1H, FootSteps.OnlyRight), (SwordStrikeKind.Finisher1H, FootSteps.OnlyLeft),
            })
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                float lastDeparture = (t.Contact - SwordFootwork.StepSeconds) / t.Total;
                Assert.Greater(lastDeparture, 0.0f, $"{kind} is too quick for a step to land before the blow");
                Assert.AreEqual(stride, SwordStrikes.Evaluate(kind, 0.0f).Steps, $"{kind} at the start");
                Assert.AreEqual(stride, SwordStrikes.Evaluate(kind, lastDeparture - 0.01f).Steps, $"{kind} just before the last moment a step could land in time");
                Assert.AreEqual(FootSteps.None, SwordStrikes.Evaluate(kind, lastDeparture + 0.01f).Steps, $"{kind}: a step could set off and still be in the air at the blow");
                Assert.AreEqual(FootSteps.None, SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind)).Steps, $"{kind} at the blow");
                Assert.AreEqual(FootSteps.Any, SwordStrikes.Evaluate(kind, 1.0f).Steps, $"{kind}: the feet do not catch up in the recovery");
            }
            Assert.AreEqual(FootSteps.Any, SwordStrikes.Ready(true).Steps);
            Assert.AreEqual(FootSteps.Any, SwordStrikes.Evaluate(SwordStrikeKind.Sweep2H, 0.3f).Steps, "the two-handed chain has not had this treatment yet");
        }

        /// <summary>The finisher reads as the end of the chain: from over the right shoulder down to the lower left, a longer stride and a deeper sink than either cut before it.</summary>
        [Test]
        public void TheOneHandedFinisherIsADiagonalWithTheWholeBodyBehindIt()
        {
            SwordStrikeKind kind = SwordStrikeKind.Finisher1H;
            SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f);
            SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
            Vector3 follow = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.05f).Blade;
            Assert.Greater(windup.Blade.y, 0.3f, "the blade is not raised over the shoulder");
            Assert.Less(windup.Blade.z, 0.0f, "the blade is not wound back");
            Assert.Greater(windup.Blade.x, 0.2f, "the blade is not wound back on the right");
            Assert.Less(follow.x, -0.3f, "the blade does not carry through to the left");
            Assert.Less(follow.y, -0.1f, "the blade does not carry on down: not a diagonal");
            Assert.Less(contact.StanceLead, -0.9f, "the left foot does not stride through");
            foreach (SwordStrikeKind earlier in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H })
            {
                SwordFrame other = SwordStrikes.Evaluate(earlier, SwordStrikes.ContactFraction(earlier));
                Assert.Greater(contact.StanceDepth, other.StanceDepth, $"the finisher does not stride further than {earlier}");
                Assert.Greater(contact.HipDrop, other.HipDrop, $"the finisher does not sink lower than {earlier}");
                Assert.Greater(contact.LeanDegrees, other.LeanDegrees, $"the finisher does not lean further than {earlier}");
                Assert.Greater(SwordStrikes.TimingOf(kind).Recover, SwordStrikes.TimingOf(earlier).Recover, $"the finisher does not recover slower than {earlier}");
            }
            // The free arm works against the blade: reaching ahead in the wind-up, back past the hip at the blow, and hanging relaxed again by the end.
            Assert.Greater(windup.FreeHand.z, 0.5f, "the free arm does not reach ahead in the wind-up");
            Assert.Less(contact.FreeHand.z, -0.2f, "the free arm is not whipped back at the blow");
            Assert.Less(Vector3.Distance(SwordStrikes.Evaluate(kind, 1.0f).FreeHand, SwordStrikes.FreeHandRelaxed), SwordStrikes.FreeHandBalanceSwing,
                "the free arm does not end relaxed");
        }

        [Test]
        public void TheHipsLeadTheShouldersIntoTheCut()
        {
            // The kinetic chain: at the contact the hips have already done more of their turn than the shoulders have of theirs, and every
            // swinging cut turns the hips at all (a twist at the waist alone is the corkscrew the owner saw).
            foreach (SwordStrikeKind kind in new[] { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H, SwordStrikeKind.Sweep2H, SwordStrikeKind.Reverse2H, SwordStrikeKind.Cut2H, SwordStrikeKind.Finisher1H })
            {
                SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f);
                SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
                SwordFrame follow = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveEnd(kind) + 0.04f);
                float hipsRange = follow.HipTwistDegrees - windup.HipTwistDegrees;
                float shouldersRange = follow.TwistDegrees - windup.TwistDegrees;
                Assert.Greater(Mathf.Abs(hipsRange), 30.0f, $"{kind}: the hips hardly turn ({hipsRange:0} deg)");
                float hipsDone = (contact.HipTwistDegrees - windup.HipTwistDegrees) / hipsRange;
                float shouldersDone = (contact.TwistDegrees - windup.TwistDegrees) / shouldersRange;
                Assert.Greater(hipsDone, shouldersDone, $"{kind}: at the contact the hips have done {hipsDone:P0} of their turn, the shoulders {shouldersDone:P0}");
                Assert.Less(Mathf.Abs(contact.TwistDegrees - contact.HipTwistDegrees), 25.0f, $"{kind}: the waist is wrung {contact.TwistDegrees - contact.HipTwistDegrees:0} deg at the contact");
            }
        }

        [Test]
        public void EveryAttackStepsIntoItsCut()
        {
            // The stance opens from the wind-up to the contact (the front foot strides, SwordFootwork steps it), and the feet stand in it.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordFrame windup = SwordStrikes.Evaluate(kind, SwordStrikes.ActiveStart(kind) - 0.03f);
                SwordFrame contact = SwordStrikes.Evaluate(kind, SwordStrikes.ContactFraction(kind));
                Assert.Greater(contact.StanceDepth, windup.StanceDepth + 0.1f, $"{kind} does not step into the cut");
                Assert.AreEqual(1.0f, contact.Footwork, 0.001f, $"{kind}: the feet are not in the stance during the attack");
            }
        }

        [Test]
        public void TheTwoHandedSwordIsCarriedLowAndTrailsAtASprint()
        {
            // (A one-handed sword is carried by its own clips, not posed: SwordCarryArmTests, SwordCarryPlayTests.)
            SwordFrame walk = SwordStrikes.Carry(0.0f);
            SwordFrame sprint = SwordStrikes.Carry(1.0f);
            Assert.Less(walk.Blade.y, -0.3f, "the carried blade does not point down");
            Assert.Greater(walk.Blade.z, 0.5f, "walking, the carried blade does not point ahead");
            Assert.Less(sprint.Blade.z, -0.5f, "sprinting, the blade does not trail behind");
            Assert.Less(SwordStrikes.HandPoint(walk).y, 1.45f, "the hands are not low: the old carry held them out at the shoulders");
            Assert.AreEqual(0.0f, walk.Footwork, 0.001f, "the carry should leave the legs to the walk");
            Assert.IsTrue(walk.TwoHanded);
            Assert.AreEqual(0.0f, walk.FreeHandWeight, 0.001f, "the carry does not place the free hand: the grip does");
            // The two-handed ready stance is a middle guard: the point forward at the opponent, not straight up in front of the face.
            SwordFrame ready = SwordStrikes.Ready(false);
            Assert.Greater(ready.Blade.z, 0.8f);
            Assert.AreEqual(0.35f, ready.Blade.y, 0.15f);
            Assert.Less(SwordStrikes.HandPoint(ready).y, 1.55f, "the ready hands should be at the belly, not the chin");
        }

        // ---- the phases (2026-10-23, owner: "clear anticipation -> acceleration -> impact -> recovery. Light attacks feel snappy; heavy
        // ones feel powerful. Avoid floaty or weightless swings") ----

        [Test]
        public void ALightLandsFastAndAHeavyPausesAtTheTop()
        {
            // A light's click lands in about a third of a second (the research: a long anticipation on the player's own attack reads as input
            // lag; spend the frames on the follow-through instead); a heavy pauses at the top for a fifth of a second or more -- the telegraph --
            // lands past half a second, and holds its blow.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                Assert.AreEqual(t.Total, SwordStrikes.Seconds(kind), 1e-4f);
                Assert.AreEqual(t.Contact / t.Total, SwordStrikes.ContactFraction(kind), 1e-4f, $"{kind}: the contact is not where the timing says");
                Assert.Greater(t.Settle, 0.0f, $"{kind}: the follow-through is not held at all");
                if (SwordStrikes.IsHeavy(kind))
                {
                    Assert.GreaterOrEqual(t.Hold, 0.2f, $"{kind} does not pause at the top: no telegraph");
                    Assert.GreaterOrEqual(t.Contact, 0.6f, $"{kind} lands in {t.Contact:0.00} s: a heavy should take its time");
                    Assert.GreaterOrEqual(t.Settle, 0.12f, $"{kind}'s blow is not held: no weight");
                }
                else
                {
                    bool finisher = kind == SwordStrikeKind.Cut2H || kind == SwordStrikeKind.Finisher1H;
                    Assert.LessOrEqual(t.Contact, finisher ? 0.45f : 0.35f, $"{kind} takes {t.Contact:0.00} s to land: not snappy");
                    Assert.LessOrEqual(t.Hold, 0.08f, $"{kind} hangs at the top for {t.Hold:0.00} s");
                }
            }
        }

        [Test]
        public void TheStrikeIsAWhipNotAWave()
        {
            // The blade accelerates all the way into the contact: its turning rate at the end of the strike is at least two and a half times its
            // average over the strike (the cubic ease gives three; the old square gave two, and the cuts read as floaty), and it barely moves as
            // it leaves the hold.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                if (kind == SwordStrikeKind.Thrust1H)
                {
                    continue;   // a thrust hardly turns the blade: it drives it
                }
                float start = SwordStrikes.StrikeStart(kind), contact = SwordStrikes.ContactFraction(kind);
                float total = Vector3.Angle(SwordStrikes.Evaluate(kind, start).Blade, SwordStrikes.Evaluate(kind, contact).Blade);
                float window = 0.05f * (contact - start);
                float late = Vector3.Angle(SwordStrikes.Evaluate(kind, contact - window).Blade, SwordStrikes.Evaluate(kind, contact).Blade);
                float early = Vector3.Angle(SwordStrikes.Evaluate(kind, start).Blade, SwordStrikes.Evaluate(kind, start + window).Blade);
                Assert.Greater(late / (0.05f * total), 2.5f, $"{kind}: the blade arrives at only {late / (0.05f * total):0.0}x its average rate");
                Assert.Less(early, late * 0.05f, $"{kind}: the blade leaves the hold at speed ({early:0.0} deg in the first 5%)");
            }
        }

        [Test]
        public void TheFollowThroughIsHeldAMomentBeforeTheRecovery()
        {
            // The blow's weight arriving: the pose at the end of the follow-through holds (the animator's held frame after a hit) before the
            // arm gathers back. Nothing moves across the settle.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                float followEnd = (t.Contact + t.Follow) / t.Total;
                float settleEnd = followEnd + t.Settle / t.Total;
                SwordFrame a = SwordStrikes.Evaluate(kind, followEnd + 0.001f);
                SwordFrame b = SwordStrikes.Evaluate(kind, settleEnd - 0.001f);
                Assert.Less(Vector3.Distance(a.Hand, b.Hand) * ArmLength, 0.005f, $"{kind}: the hand moves through the settle");
                Assert.Less(Vector3.Angle(a.Blade, b.Blade), 0.5f, $"{kind}: the blade turns through the settle");
                Assert.AreEqual(a.LeanDegrees, b.LeanDegrees, 0.1f, $"{kind}: the body moves through the settle");
            }
        }

        [Test]
        public void AHeavyOvershootsItsStanceAndSettlesBackALightDoesNot()
        {
            // A heavy's recovery is a weight coming to rest: the hand passes a little beyond the ready stance and comes back to it; a light's
            // goes straight there. Both end exactly in it (EveryAttackStartsAndEndsInItsReadyStance).
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                float recoverStart = (t.Contact + t.Follow + t.Settle) / t.Total;
                Vector3 ready = SwordStrikes.Evaluate(kind, 1.0f).Hand;
                Vector3 way = (ready - SwordStrikes.Evaluate(kind, recoverStart).Hand).normalized;
                float furthest = 0.0f;
                for (float u = recoverStart; u <= 1.0f; u += 0.005f)
                {
                    furthest = Mathf.Max(furthest, Vector3.Dot(SwordStrikes.Evaluate(kind, u).Hand - ready, way) * ArmLength);
                }
                if (SwordStrikes.IsHeavy(kind))
                {
                    Assert.Greater(furthest, 0.015f, $"{kind}'s hand does not overshoot the stance ({furthest * 100:0.0} cm)");
                    Assert.Less(furthest, 0.10f, $"{kind}'s hand overshoots the stance by {furthest * 100:0} cm: a flail");
                }
                else
                {
                    Assert.LessOrEqual(furthest, 0.002f, $"{kind}'s hand overshoots the stance ({furthest * 100:0.0} cm): a light settles straight");
                }
            }
        }

        [Test]
        public void AHeavysHoldCreepsBackInsteadOfFreezing()
        {
            // A moving hold (the animator's): through the pause at the top the hand draws a little further back and the lean deepens, slowly,
            // so the body is still gathering, never a statue. A light's hold is a few frames and stays still.
            foreach (SwordStrikeKind kind in SwordStrikes.All)
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                SwordFrame top = SwordStrikes.Evaluate(kind, t.WindUp / t.Total);
                SwordFrame end = SwordStrikes.Evaluate(kind, (t.WindUp + t.Hold) / t.Total);
                if (SwordStrikes.IsHeavy(kind))
                {
                    float back = (top.Hand.z - end.Hand.z) * ArmLength;
                    Assert.That(back, Is.InRange(0.02f, 0.05f), $"{kind}: the hand creeps {back * 100:0.0} cm back through the hold");
                    Assert.Less(end.LeanDegrees, top.LeanDegrees - 1.0f, $"{kind}: the lean does not deepen through the hold");
                }
                else
                {
                    Assert.Less(Vector3.Distance(top.Hand, end.Hand) * ArmLength, 0.002f, $"{kind}: a light's hold moves");
                }
            }
        }
    }
}
