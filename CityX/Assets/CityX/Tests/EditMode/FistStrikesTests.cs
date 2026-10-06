using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The procedural unarmed strikes on paper: every blow starts and ends in the guard, reaches (nearly) the full length of the arm at the moment it
    /// LANDS, goes out along its own path, and turns the torso into it the right way. These are the properties the old two-clip jab did not have.
    /// </summary>
    public sealed class FistStrikesTests
    {
        private static readonly FistKind[] All = { FistKind.Jab, FistKind.Cross, FistKind.Hook, FistKind.Uppercut, FistKind.Haymaker };

        private static Vector3 ThrowingHand(in FistFrame f, FistKind kind) => FistStrikes.UsesRightHand(kind) ? f.RightHand : f.LeftHand;

        [Test]
        public void EveryBlowStartsAndEndsInTheGuard()
        {
            foreach (FistKind kind in All)
            {
                FistFrame start = FistStrikes.Evaluate(kind, 0.0f, 1.0f);
                FistFrame end = FistStrikes.Evaluate(kind, 1.0f, 1.0f);
                Vector3 guard = FistStrikes.GuardOffset(FistStrikes.UsesRightHand(kind));
                Assert.Less(Vector3.Distance(ThrowingHand(start, kind), guard), 0.01f, $"{kind} does not start in the guard");
                Assert.Less(Vector3.Distance(ThrowingHand(end, kind), guard), 0.01f, $"{kind} does not end in the guard");
                Assert.AreEqual(0.0f, start.TwistDegrees, 0.01f, $"{kind} starts with the torso already turned");
                Assert.AreEqual(0.0f, end.TwistDegrees, 0.5f, $"{kind} ends with the torso still turned");
            }
        }

        [Test]
        public void TheFistIsAtFullReachWhenItLands()
        {
            foreach (FistKind kind in All)
            {
                FistFrame landing = FistStrikes.Evaluate(kind, FistStrikes.ContactFraction(kind), 1.0f);
                Vector3 hand = ThrowingHand(landing, kind);
                Assert.AreEqual(1.0f, landing.Extension, 0.001f, $"{kind} is not fully extended at its contact moment");
                if (kind == FistKind.Jab || kind == FistKind.Cross || kind == FistKind.Haymaker)
                {
                    Assert.Greater(hand.z, 0.95f, $"{kind} does not reach out to the target ({hand.z:F2} of an arm length forward)");
                }
                else
                {
                    Assert.Greater(hand.magnitude, 0.7f, $"{kind} lands too close to the body");
                }
            }
        }

        [Test]
        public void EveryBlowHasItsOwnPath()
        {
            // Sampled mid-way, no two blows have the hand in the same place.
            for (int i = 0; i < All.Length; i++)
            {
                for (int j = i + 1; j < All.Length; j++)
                {
                    Vector3 a = ThrowingHand(FistStrikes.Evaluate(All[i], 0.3f, 1.0f), All[i]);
                    Vector3 b = ThrowingHand(FistStrikes.Evaluate(All[j], 0.3f, 1.0f), All[j]);
                    if (FistStrikes.UsesRightHand(All[i]) != FistStrikes.UsesRightHand(All[j]))
                    {
                        // Different hands: compare mirrored.
                        b.x = -b.x;
                    }
                    Assert.Greater(Vector3.Distance(a, b), 0.05f, $"{All[i]} and {All[j]} are the same punch");
                }
            }
        }

        [Test]
        public void TheHookSweepsInAnArcFromTheSideAndTheUppercutRises()
        {
            // The hook's lead (left) hand starts far out to the left (negative x) and finishes across the front (positive x).
            FistFrame early = FistStrikes.Evaluate(FistKind.Hook, 0.22f, 1.0f);
            FistFrame landing = FistStrikes.Evaluate(FistKind.Hook, FistStrikes.ContactFraction(FistKind.Hook), 1.0f);
            Assert.Less(early.LeftHand.x, -0.5f, "the hook is not wound up out to the side");
            Assert.Greater(landing.LeftHand.x, 0.3f, "the hook does not come across the front");

            FistFrame low = FistStrikes.Evaluate(FistKind.Uppercut, 0.22f, 1.0f);
            FistFrame high = FistStrikes.Evaluate(FistKind.Uppercut, FistStrikes.ContactFraction(FistKind.Uppercut), 1.0f);
            Assert.Less(low.RightHand.y, -0.4f, "the uppercut does not drop low first");
            Assert.Greater(high.RightHand.y, 0.5f, "the uppercut does not rise under the chin");
        }

        [Test]
        public void TheTorsoTurnsIntoTheBlowTheRightWay()
        {
            // A right-handed blow turns the torso to the left (negative yaw), a left-handed one to the right.
            Assert.Less(FistStrikes.Evaluate(FistKind.Cross, FistStrikes.ContactFraction(FistKind.Cross), 1.0f).TwistDegrees, -20.0f, "the cross does not turn the hips and shoulders into it");
            Assert.Greater(FistStrikes.Evaluate(FistKind.Hook, FistStrikes.ContactFraction(FistKind.Hook), 1.0f).TwistDegrees, 20.0f);
            // And it winds up the other way first.
            Assert.Greater(FistStrikes.Evaluate(FistKind.Cross, 0.15f, 1.0f).TwistDegrees, -1.0f, "no counter-turn in the anticipation");
        }

        [Test]
        public void TheOtherHandStaysInTheGuardWhileOneThrows()
        {
            FistFrame f = FistStrikes.Evaluate(FistKind.Jab, 0.3f, 1.0f);
            Assert.GreaterOrEqual(f.RightWeight, 0.85f, "the rear hand dropped while the lead hand punched");
            Assert.Less(Vector3.Distance(f.RightHand, FistStrikes.GuardOffset(true)), 0.01f);
        }

        [Test]
        public void ContactFallsInsideTheBlowAndTimingsAreSane()
        {
            foreach (FistKind kind in All)
            {
                float contact = FistStrikes.ContactFraction(kind);
                Assert.That(contact, Is.InRange(0.3f, 0.7f), $"{kind}");
                Assert.That(FistStrikes.Seconds(kind), Is.InRange(0.25f, 0.8f), $"{kind}");
                Assert.That(FistStrikes.MoveFraction(kind), Is.InRange(0.1f, 1.0f), $"{kind}: the legs must keep walking");
                Assert.IsTrue(FistStrikes.Has(FistStrikes.NameOf(kind)), $"{kind} has no animation name");
            }
        }

        [Test]
        public void NoFrameIsEverNaN()
        {
            foreach (FistKind kind in All)
            {
                for (float t = -0.1f; t <= 1.1f; t += 0.01f)
                {
                    FistFrame f = FistStrikes.Evaluate(kind, t, 1.0f);
                    Assert.IsFalse(float.IsNaN(f.RightHand.x + f.RightHand.y + f.RightHand.z + f.LeftHand.x + f.LeftHand.y + f.LeftHand.z + f.TwistDegrees + f.LeanDegrees), $"{kind} at {t}");
                }
            }
        }

        [Test]
        public void TheUnarmedChainIsFiveDifferentPunchesEndingInTheHaymaker()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            Assert.AreEqual(5, chain.Length);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (ComboStage stage in chain)
            {
                Assert.IsTrue(FistStrikes.Has(stage.Animation), $"{stage.Animation} is not a fist strike");
                Assert.IsTrue(seen.Add(stage.Animation), $"{stage.Animation} appears twice");
            }
            Assert.IsTrue(chain[chain.Length - 1].IsFinisher);
            Assert.AreEqual(FistStrikes.HaymakerName, chain[chain.Length - 1].Animation);
        }
    }
}
