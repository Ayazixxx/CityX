using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The procedural unarmed strikes on paper, Souls-style (docs/MELEE_COMBOS.md): every blow starts and ends with the arms DOWN in the ready stance
    /// (no boxing guard), reaches (nearly) the full length of the arm at the moment it LANDS, goes out along its own path, turns the torso into it
    /// the right way, and puts the other foot forward.
    /// </summary>
    public sealed class FistStrikesTests
    {
        private static readonly FistKind[] All = { FistKind.RightStraight, FistKind.LeftStraight, FistKind.Overhand, FistKind.Uppercut };

        private static Vector3 ThrowingHand(in FistFrame f, FistKind kind) => FistStrikes.UsesRightHand(kind) ? f.RightHand : f.LeftHand;

        [Test]
        public void EveryBlowStartsAndEndsWithTheArmDownInTheReadyStance()
        {
            foreach (FistKind kind in All)
            {
                FistFrame start = FistStrikes.Evaluate(kind, 0.0f, 1.0f);
                FistFrame end = FistStrikes.Evaluate(kind, 1.0f, 1.0f);
                Vector3 ready = FistStrikes.ReadyOffset(FistStrikes.UsesRightHand(kind));
                Assert.Less(Vector3.Distance(ThrowingHand(start, kind), ready), 0.01f, $"{kind} does not start in the ready stance");
                Assert.Less(Vector3.Distance(ThrowingHand(end, kind), ready), 0.01f, $"{kind} does not end in the ready stance");
                Assert.AreEqual(0.0f, start.TwistDegrees, 0.01f, $"{kind} starts with the torso already turned");
                Assert.AreEqual(0.0f, end.TwistDegrees, 0.5f, $"{kind} ends with the torso still turned");
                Assert.AreEqual(FistStrikes.ReadyHipDrop, end.HipDrop, 0.005f, $"{kind} ends with the hips still sunk");
            }
        }

        /// <summary>No guard: the fists rest down at the sides, below the shoulders, not up at the face.</summary>
        [Test]
        public void TheReadyStanceHasTheArmsDown()
        {
            FistFrame ready = FistStrikes.Ready(1.0f);
            Assert.Less(ready.RightHand.y, -0.6f, "the right fist is not down at the side");
            Assert.Less(ready.LeftHand.y, -0.6f, "the left fist is not down at the side");
            Assert.Greater(ready.RightHand.x, 0.0f, "the right fist crosses in front of the body instead of hanging at its side");
            Assert.Less(ready.LeftHand.x, 0.0f, "the left fist crosses in front of the body instead of hanging at its side");
            Assert.Greater(ready.HipDrop, 0.0f, "the ready stance does not crouch at all");
            Assert.AreEqual(0.0f, ready.StanceLead, 0.001f, "the ready stance is not square");
            Assert.Greater(ready.Footwork, 0.99f, "the feet are not planted in the ready stance");
        }

        [Test]
        public void TheFistIsAtFullReachWhenItLands()
        {
            foreach (FistKind kind in All)
            {
                FistFrame landing = FistStrikes.Evaluate(kind, FistStrikes.ContactFraction(kind), 1.0f);
                Vector3 hand = ThrowingHand(landing, kind);
                Assert.AreEqual(1.0f, landing.Extension, 0.001f, $"{kind} is not fully extended at its contact moment");
                if (kind == FistKind.Uppercut)
                {
                    Assert.Greater(hand.magnitude, 0.6f, $"{kind} lands too close to the body");
                }
                else if (kind == FistKind.Overhand)
                {
                    Assert.Greater(hand.magnitude, 0.9f, $"{kind} is not at full stretch when it lands ({hand.magnitude:F2} of an arm length)");
                }
                else
                {
                    Assert.Greater(hand.z, 0.9f, $"{kind} does not reach out to the target ({hand.z:F2} of an arm length forward)");
                }
            }
        }

        /// <summary>The two straights are the same punch mirrored (Souls' alternating lights); the heavies are their own paths.</summary>
        [Test]
        public void TheStraightsMirrorEachOtherAndTheHeaviesHaveTheirOwnPaths()
        {
            for (float t = 0.0f; t <= 1.0f; t += 0.1f)
            {
                FistFrame right = FistStrikes.Evaluate(FistKind.RightStraight, t, 1.0f);
                FistFrame left = FistStrikes.Evaluate(FistKind.LeftStraight, t, 1.0f);
                Vector3 mirrored = new Vector3(-left.LeftHand.x, left.LeftHand.y, left.LeftHand.z);
                Assert.Less(Vector3.Distance(right.RightHand, mirrored), 0.001f, $"the straights are not mirror images at {t:F1}");
                Assert.AreEqual(-right.TwistDegrees, left.TwistDegrees, 0.01f, $"the straights turn the torso differently at {t:F1}");
            }
            FistKind[] distinct = { FistKind.RightStraight, FistKind.Overhand, FistKind.Uppercut };
            for (int i = 0; i < distinct.Length; i++)
            {
                for (int j = i + 1; j < distinct.Length; j++)
                {
                    Vector3 a = FistStrikes.Evaluate(distinct[i], 0.3f, 1.0f).RightHand;
                    Vector3 b = FistStrikes.Evaluate(distinct[j], 0.3f, 1.0f).RightHand;
                    Assert.Greater(Vector3.Distance(a, b), 0.05f, $"{distinct[i]} and {distinct[j]} are the same punch");
                }
            }
        }

        /// <summary>A straight is drawn back to the hip, not up to the face, before it goes out.</summary>
        [Test]
        public void AStraightIsDrawnBackToTheHipFirst()
        {
            FistFrame wound = FistStrikes.Evaluate(FistKind.RightStraight, 0.22f, 1.0f);
            Assert.Less(wound.RightHand.y, -0.3f, "the fist is not drawn back low");
            Assert.Less(wound.RightHand.z, 0.0f, "the fist is not drawn back behind the shoulder");
        }

        [Test]
        public void TheOverhandComesDownFromHighAndTheUppercutRises()
        {
            FistFrame high = FistStrikes.Evaluate(FistKind.Overhand, 0.45f, 1.0f);
            FistFrame landing = FistStrikes.Evaluate(FistKind.Overhand, FistStrikes.ContactFraction(FistKind.Overhand), 1.0f);
            Assert.Greater(high.RightHand.y, 0.6f, "the overhand is not wound up high over the shoulder");
            Assert.Less(high.RightHand.z, 0.0f, "the overhand is not wound back");
            Assert.Less(landing.RightHand.y, -0.3f, "the overhand does not come down below the shoulder");
            Assert.Greater(landing.LeanDegrees, 15.0f, "the whole body does not go into the overhand");

            FistFrame low = FistStrikes.Evaluate(FistKind.Uppercut, 0.26f, 1.0f);
            FistFrame up = FistStrikes.Evaluate(FistKind.Uppercut, FistStrikes.ContactFraction(FistKind.Uppercut), 1.0f);
            Assert.Less(low.RightHand.y, -0.4f, "the uppercut does not drop low first");
            Assert.That(up.RightHand.y, Is.InRange(0.2f, 0.45f), "the uppercut does not rise to the chin (and no higher: it went over the head)");
            Assert.Greater(low.HipDrop, up.HipDrop, "the uppercut does not dip the hips before it rises");
        }

        [Test]
        public void TheTorsoTurnsIntoTheBlowTheRightWay()
        {
            // A right-handed blow turns the torso to the left (negative yaw), a left-handed one to the right.
            Assert.Less(FistStrikes.Evaluate(FistKind.RightStraight, FistStrikes.ContactFraction(FistKind.RightStraight), 1.0f).TwistDegrees, -20.0f,
                "the right straight does not turn the hips and shoulders into it");
            Assert.Greater(FistStrikes.Evaluate(FistKind.LeftStraight, FistStrikes.ContactFraction(FistKind.LeftStraight), 1.0f).TwistDegrees, 20.0f);
            // And it winds up the other way first.
            Assert.Greater(FistStrikes.Evaluate(FistKind.RightStraight, 0.15f, 1.0f).TwistDegrees, 1.0f, "no counter-turn in the anticipation");
            // And leans well into it.
            Assert.Greater(FistStrikes.Evaluate(FistKind.RightStraight, FistStrikes.ContactFraction(FistKind.RightStraight), 1.0f).LeanDegrees, 10.0f);
        }

        /// <summary>The step into a punch is with the OTHER foot: the hips turn into the blow over the front foot.</summary>
        [Test]
        public void TheOtherFootIsForwardForEachBlow()
        {
            foreach (FistKind kind in All)
            {
                FistFrame f = FistStrikes.Evaluate(kind, 0.3f, 1.0f);
                float expected = FistStrikes.UsesRightHand(kind) ? -1.0f : 1.0f;
                Assert.AreEqual(expected, f.StanceLead, 0.001f, $"{kind} has the wrong foot forward");
                Assert.Greater(f.StanceDepth, 0.3f, $"{kind} does not step out");
                Assert.Greater(f.Footwork, 0.99f, $"{kind} leaves the feet to the walk clip");
                Assert.Greater(f.HipDrop, FistStrikes.ReadyHipDrop * 0.5f, $"{kind} stands bolt upright");
            }
        }

        [Test]
        public void TheOtherHandStaysDownWhileOneThrows()
        {
            FistFrame f = FistStrikes.Evaluate(FistKind.RightStraight, 0.3f, 1.0f);
            Assert.GreaterOrEqual(f.LeftWeight, 0.85f, "the other hand was let go while one punched");
            Assert.Less(Vector3.Distance(f.LeftHand, FistStrikes.ReadyOffset(false)), 0.01f, "the other hand came up");
        }

        [Test]
        public void ContactFallsInsideTheBlowAndTimingsAreSane()
        {
            foreach (FistKind kind in All)
            {
                float contact = FistStrikes.ContactFraction(kind);
                Assert.That(contact, Is.InRange(0.3f, 0.7f), $"{kind}");
                Assert.That(FistStrikes.Seconds(kind), Is.InRange(0.4f, 0.8f), $"{kind}: each punch is a commitment, not a snap");
                Assert.That(FistStrikes.MoveFraction(kind), Is.InRange(0.1f, 1.0f), $"{kind}: the legs must keep walking");
                Assert.IsTrue(FistStrikes.Has(FistStrikes.NameOf(kind)), $"{kind} has no animation name");
            }
            Assert.Greater(FistStrikes.Seconds(FistKind.Overhand), FistStrikes.Seconds(FistKind.RightStraight), "the overhand's wind-up is no tell");
        }

        [Test]
        public void NoFrameIsEverNaN()
        {
            foreach (FistKind kind in All)
            {
                for (float t = -0.1f; t <= 1.1f; t += 0.01f)
                {
                    FistFrame f = FistStrikes.Evaluate(kind, t, 1.0f);
                    Assert.IsFalse(float.IsNaN(f.RightHand.x + f.RightHand.y + f.RightHand.z + f.LeftHand.x + f.LeftHand.y + f.LeftHand.z
                        + f.TwistDegrees + f.LeanDegrees + f.HipDrop), $"{kind} at {t}");
                }
            }
        }

        /// <summary>Souls' two-hit string: right, left, and the left closes the chain; the heavies are outside it.</summary>
        [Test]
        public void TheUnarmedChainIsARightThenALeftStraight()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            Assert.AreEqual(2, chain.Length);
            Assert.AreEqual(FistStrikes.RightStraightName, chain[0].Animation);
            Assert.AreEqual(FistStrikes.LeftStraightName, chain[1].Animation);
            Assert.IsFalse(chain[0].IsFinisher);
            Assert.IsTrue(chain[1].IsFinisher, "the left straight does not close the chain");
            Assert.IsFalse(chain[1].KnocksDown, "a light knocks players down");
            Assert.AreEqual(FistStrikes.OverhandName, CombatTuning.HeavyPunch.Animation);
            Assert.IsTrue(CombatTuning.HeavyPunch.KnocksDown, "the overhand is no knockdown");
            Assert.AreEqual(FistStrikes.UppercutName, CombatTuning.UppercutPunch.Animation);
        }

        /// <summary>
        /// Two hits are guaranteed, the third is not (docs/MELEE_COMBOS.md): the first straight's stun outlasts the gap to the second's contact, and
        /// the second's does not outlast its own recovery.
        /// </summary>
        [Test]
        public void TwoPunchesAreGuaranteedAndTheThirdIsNot()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            float first = FistStrikes.Seconds(FistKind.RightStraight);
            float second = FistStrikes.Seconds(FistKind.LeftStraight);
            float firstContact = first * FistStrikes.ContactFraction(FistKind.RightStraight);
            float earliestSecondStart = first * (1.0f - MeleeAttacker.ChainCancelFraction);
            float secondContact = earliestSecondStart + second * FistStrikes.ContactFraction(FistKind.LeftStraight);
            float gap = secondContact - firstContact;
            Assert.GreaterOrEqual(CombatTuning.HitstunFor(chain[0]), gap, $"the first straight's stun ({CombatTuning.HitstunFor(chain[0]):F2} s) does not cover the gap to the second ({gap:F2} s)");

            float secondRecovery = second * (1.0f - FistStrikes.ContactFraction(FistKind.LeftStraight));
            Assert.Less(CombatTuning.HitstunFor(chain[1]), secondRecovery, "the second straight's stun locks the target through the recovery: a third would be guaranteed");
        }

        /// <summary>The whole string does less than a sword's and costs stamina (the "too op" of 2026-11-03).</summary>
        [Test]
        public void TheFistsAreWeakerThanASwordAndCostStamina()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            float total = 0.0f;
            foreach (ComboStage stage in chain)
            {
                total += CombatTuning.DamageFor(stage, 0);
                Assert.LessOrEqual(CombatTuning.DamageFor(stage, 0), CombatTuning.PunchDamage * 0.5f, $"{stage.Animation} hits as hard as a sword");
            }
            Assert.AreEqual(CombatTuning.PunchDamage, total, 0.001f, "the two straights together are one sword blow");
            Assert.AreEqual(CombatTuning.PunchDamage, CombatTuning.DamageFor(CombatTuning.HeavyPunch, 0), 0.001f, "the overhand is one sword blow");
            Assert.Greater(CombatTuning.PunchStaminaCost, 0.0f);
            Assert.Greater(CombatTuning.HeavyPunchStaminaCost, CombatTuning.PunchStaminaCost);
            Assert.Less(MeleeAttacker.FistReachMetres, 2.0f, "the fists reach as far as a sword");
        }
    }
}
