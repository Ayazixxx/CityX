using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A ladder climber's hands and feet (owner, 2026-11-09: "make ladder climbing so make that look good with hand ik"): which rung each
    /// is on is worked out from the body's height alone, by a climber's rules.
    /// </summary>
    public sealed class LadderGaitTests
    {
        private const int Rungs = 40;

        private static readonly int Over = LadderGait.HandRungsOverFoot(BodyRig.StandingHeight);

        private static LadderGait.Hold[] At(float body) => new[]
        {
            LadderGait.Hand(body, true, Over, Rungs), LadderGait.Hand(body, false, Over, Rungs),
            LadderGait.Foot(body, true, Rungs), LadderGait.Foot(body, false, Rungs),
        };

        [Test]
        public void ThreePointsAlwaysHoldAndOnlyADiagonalPairEverSteps()
        {
            for (float body = 2.0f; body < 20.0f; body += 0.01f)
            {
                LadderGait.Hold[] h = At(body);
                bool leftHand = h[0].Lift > 0.0f, rightHand = h[1].Lift > 0.0f, leftFoot = h[2].Lift > 0.0f, rightFoot = h[3].Lift > 0.0f;
                Assert.IsFalse(leftHand && rightHand, $"both hands are off the ladder {body:F2} rungs up");
                Assert.IsFalse(leftFoot && rightFoot, $"both feet are off the ladder {body:F2} rungs up");
                Assert.IsFalse(leftHand && leftFoot, $"the left hand and the left foot step together {body:F2} rungs up");
                Assert.IsFalse(rightHand && rightFoot, $"the right hand and the right foot step together {body:F2} rungs up");
            }
        }

        [Test]
        public void EveryLimbStandsOnAWholeRungAndStepsTwoAtATimeTheLeftFootOnTheEvenOnes()
        {
            var last = new float[4];
            for (int i = 0; i < 4; i++) last[i] = float.NaN;
            for (float body = 2.0f; body < 20.0f; body += 0.005f)
            {
                LadderGait.Hold[] h = At(body);
                for (int i = 0; i < 4; i++)
                {
                    if (h[i].Lift > 0.0f) continue;
                    Assert.AreEqual(Mathf.Round(h[i].Rung), h[i].Rung, 1e-4f, $"limb {i} holds between two rungs {body:F2} rungs up");
                    if (!float.IsNaN(last[i]) && Mathf.Abs(h[i].Rung - last[i]) > 0.5f)
                    {
                        Assert.AreEqual(2.0f, h[i].Rung - last[i], 1e-3f, $"limb {i} stepped other than two rungs up");
                    }
                    last[i] = h[i].Rung;
                }
                if (h[2].Lift <= 0.0f) Assert.AreEqual(0, Mathf.RoundToInt(h[2].Rung) % 2, "the left foot is on an odd rung");
                if (h[3].Lift <= 0.0f) Assert.AreEqual(1, Mathf.RoundToInt(h[3].Rung) % 2, "the right foot is on an even rung");
            }
        }

        [Test]
        public void AHandLetsGoAndLandsBeforeTheFootItMovesWith()
        {
            int pairs = 0;
            // From a height where every limb holds, so the first thing seen is a hand letting go.
            bool handWasUp = false, footWasUp = false;
            for (float body = LadderGait.RestNear(2.0f, 0.0f, 30.0f); body < 12.0f; body += 0.002f)
            {
                // The right hand moves with the left foot.
                bool hand = LadderGait.Hand(body, false, Over, Rungs).Lift > 0.0f, foot = LadderGait.Foot(body, true, Rungs).Lift > 0.0f;
                if (hand && !handWasUp)
                {
                    Assert.IsFalse(foot, "the foot was already off its rung when its hand let go");
                    pairs++;
                }
                if (footWasUp && !foot)
                {
                    Assert.IsFalse(hand, "the foot landed while its hand was still in the air");
                }
                handWasUp = hand;
                footWasUp = foot;
            }
            Assert.Greater(pairs, 3);
        }

        [Test]
        public void TheFeetAreWithinTheLegsReachAndTheHandsWithinTheArms()
        {
            float pitch = Ladders.Pitch;
            // Standing straight the soles are at the body's feet; the hips are BodyRig.HipY over them.
            float hip = BodyRig.HipY, shoulder = BodyRig.ShoulderY;
            for (float body = 4.0f; body < 14.0f; body += 0.01f)
            {
                LadderGait.Hold[] h = At(body);
                for (int i = 2; i < 4; i++)
                {
                    if (h[i].Lift > 0.0f) continue;
                    float sole = (h[i].Rung - body) * pitch;
                    Assert.Greater(sole, 0.0f, "a foot stands lower than a straight leg reaches");
                    Assert.Less(sole + BodyRig.FootY, hip - 0.15f, "a foot stands as high as the hip");
                }
                for (int i = 0; i < 2; i++)
                {
                    if (h[i].Lift > 0.0f) continue;
                    float hand = (h[i].Rung - body) * pitch;
                    float reach = Mathf.Sqrt(BodyRig.ArmLength * BodyRig.ArmLength - PlayerLadder.Standoff * PlayerLadder.Standoff);
                    Assert.Greater(hand, shoulder - 0.1f, "a hand holds below the shoulder: the arm folds up");
                    Assert.Less(hand, shoulder + reach, "a hand holds higher than the arm reaches");
                }
            }
        }

        [Test]
        public void AClimberWhoStopsRestsWithinHalfARungWithEveryLimbHolding()
        {
            for (float body = 0.0f; body < 20.0f; body += 0.037f)
            {
                float rest = LadderGait.RestNear(body, LadderGait.Floor, 30.0f);
                Assert.LessOrEqual(Mathf.Abs(rest - body), 0.5f + 1e-4f, $"the rest from {body:F2} is further than half a rung");
                Assert.IsTrue(LadderGait.AllHold(rest, Over, Rungs), $"a limb is in the air at the rest height {rest:F2}");
            }
            // At a ladder's foot the lowest rest is on its first rungs, not half-way through a step on the floor.
            float lowest = LadderGait.RestNear(LadderGait.Floor, LadderGait.Floor, 30.0f);
            Assert.GreaterOrEqual(lowest, LadderGait.Floor);
            Assert.IsTrue(LadderGait.AllHold(lowest, Over, Rungs));
        }

        [Test]
        public void AtTheFootAFootIsOnTheFloorAndAtTheTopTheHandsShareTheLastRung()
        {
            Assert.AreEqual(LadderGait.Floor, LadderGait.Foot(LadderGait.Floor - 0.6f, false, Rungs).Rung, 1e-4f, "a foot under the lowest rung is not on the floor");
            const int few = 12;
            float high = few - 1 - Over + 3.0f;
            Assert.AreEqual(few - 1, LadderGait.Hand(high, true, Over, few).Rung, 1e-4f);
            Assert.AreEqual(few - 1, LadderGait.Hand(high, false, Over, few).Rung, 1e-4f);
        }

        [Test]
        public void ALaddersRungsStopShortOfItsTop()
        {
            var ladder = new Ladder { Foot = Vector3.zero, Normal = Vector3.forward, Height = 8.6f, TopOpen = true };
            float top = ladder.Rung(ladder.RungCount - 1).y;
            Assert.LessOrEqual(top, ladder.Height - Ladders.TopRungBelow + 1e-4f);
            Assert.Greater(top, ladder.Height - Ladders.TopRungBelow - Ladders.Pitch);
            Assert.AreEqual(ladder.Top - ladder.Normal * (Ladders.OffWall + Ladders.StandBack), ladder.StandAtTop);
        }
    }
}
