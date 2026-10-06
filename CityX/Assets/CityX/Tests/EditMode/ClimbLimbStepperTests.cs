using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The procedural climb on paper: a body climbing a wall at the real climb speed, its four limbs planted on the wall and stepping in diagonal pairs.
    /// What the old clip got wrong (both arms waving over the head, legs bent anywhere, nothing touching the wall) is what these forbid.
    /// </summary>
    public sealed class ClimbLimbStepperTests
    {
        private static readonly Vector3 WallPoint = new Vector3(0.0f, 0.0f, 1.0f);
        private static readonly Vector3 Normal = Vector3.back;   // the wall's near face is at z = 1, facing -Z (toward the climber)

        private static ClimbLimbStepper.Body BodyAt(float y)
        {
            // Facing +Z: the body's right is +X. The climber hangs 0.38 m from the wall face.
            return new ClimbLimbStepper.Body
            {
                LeftShoulder = new Vector3(-0.26f, y + 1.7f, 0.62f),
                RightShoulder = new Vector3(0.26f, y + 1.7f, 0.62f),
                LeftHip = new Vector3(-0.12f, y + 1.0f, 0.62f),
                RightHip = new Vector3(0.12f, y + 1.0f, 0.62f),
                ArmLength = 0.88f,
                LegLength = 1.1f,
            };
        }

        private static Vector3 RootOf(ClimbLimbStepper.Limb limb, in ClimbLimbStepper.Body b)
        {
            return limb == ClimbLimbStepper.Limb.LeftHand ? b.LeftShoulder : limb == ClimbLimbStepper.Limb.RightHand ? b.RightShoulder
                : limb == ClimbLimbStepper.Limb.LeftFoot ? b.LeftHip : b.RightHip;
        }

        private static float LengthOf(ClimbLimbStepper.Limb limb, in ClimbLimbStepper.Body b)
        {
            return limb == ClimbLimbStepper.Limb.LeftHand || limb == ClimbLimbStepper.Limb.RightHand ? b.ArmLength : b.LegLength;
        }

        private sealed class Run
        {
            public int[] Steps = new int[4];
            public float WorstReach;
            public float DeepestIntoWall = float.MaxValue;
            public float FarthestOffWall;
            public bool BothHandsStepped, BothPairsStepped;
        }

        private static Run Climb(int direction, float seconds)
        {
            var stepper = new ClimbLimbStepper();
            var run = new Run();
            float y = 0.0f;
            const float dt = 1.0f / 60.0f;
            var wasStepping = new bool[4];
            for (float t = 0.0f; t < seconds; t += dt)
            {
                y += direction * 2.5f * dt;
                ClimbLimbStepper.Body body = BodyAt(y);
                stepper.Tick(dt, body, WallPoint, Normal, direction);
                if (stepper.PairStepping(0) && stepper.PairStepping(1)) run.BothPairsStepped = true;
                if (stepper.IsStepping(ClimbLimbStepper.Limb.LeftHand) && stepper.IsStepping(ClimbLimbStepper.Limb.RightHand)) run.BothHandsStepped = true;
                for (int i = 0; i < 4; i++)
                {
                    var limb = (ClimbLimbStepper.Limb)i;
                    bool stepping = stepper.IsStepping(limb);
                    if (stepping && !wasStepping[i]) run.Steps[i]++;
                    wasStepping[i] = stepping;

                    Vector3 target = stepper.Target(limb, Normal);
                    float reach = Vector3.Distance(target, RootOf(limb, body)) / LengthOf(limb, body);
                    run.WorstReach = Mathf.Max(run.WorstReach, reach);
                    float offWall = Vector3.Dot(target - WallPoint, Normal);
                    run.DeepestIntoWall = Mathf.Min(run.DeepestIntoWall, offWall);
                    run.FarthestOffWall = Mathf.Max(run.FarthestOffWall, offWall);
                }
            }
            return run;
        }

        [Test]
        public void ClimbingUpEveryLimbStepsAgainAndAgain()
        {
            Run run = Climb(1, 6.0f);
            for (int i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(run.Steps[i], 5, $"limb {(ClimbLimbStepper.Limb)i} only stepped {run.Steps[i]} times in 6 s");
            }
        }

        [Test]
        public void ClimbingDownAlsoSteps()
        {
            Run run = Climb(-1, 6.0f);
            for (int i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(run.Steps[i], 5, $"limb {(ClimbLimbStepper.Limb)i} only stepped {run.Steps[i]} times going down");
            }
        }

        [Test]
        public void NoLimbIsEverAskedToReachFartherThanItCan()
        {
            foreach (int direction in new[] { 1, -1 })
            {
                Run run = Climb(direction, 6.0f);
                Assert.Less(run.WorstReach, 1.10f, $"a limb was asked to reach {run.WorstReach:F2} of its length going {(direction > 0 ? "up" : "down")}");
            }
        }

        [Test]
        public void EveryHoldIsOnTheWallAndNeverInsideIt()
        {
            Run run = Climb(1, 6.0f);
            Assert.GreaterOrEqual(run.DeepestIntoWall, 0.0f, $"a hold was {-run.DeepestIntoWall:F2} m inside the wall");
            Assert.Less(run.FarthestOffWall, 0.2f, "a hold was nowhere near the wall");
        }

        [Test]
        public void OnlyOneDiagonalPairStepsAtATimeAndTheTwoHandsNeverStepTogether()
        {
            foreach (int direction in new[] { 1, -1 })
            {
                Run run = Climb(direction, 6.0f);
                Assert.IsFalse(run.BothPairsStepped, "both pairs stepped at once: nothing was holding the weight");
                Assert.IsFalse(run.BothHandsStepped, "both hands let go together");
            }
        }

        [Test]
        public void AHoldDoesNotMoveWhileItsLimbIsHolding()
        {
            var stepper = new ClimbLimbStepper();
            stepper.Plant(BodyAt(0.0f), WallPoint, Normal, 1);
            var before = new Vector3[4];
            for (int i = 0; i < 4; i++) before[i] = stepper.Anchor((ClimbLimbStepper.Limb)i);
            // A body that hangs still: nothing is left behind, so nothing steps and nothing moves.
            for (int f = 0; f < 120; f++)
            {
                stepper.Tick(1.0f / 60.0f, BodyAt(0.0f), WallPoint, Normal, 1);
            }
            for (int i = 0; i < 4; i++)
            {
                Assert.Less(Vector3.Distance(before[i], stepper.Anchor((ClimbLimbStepper.Limb)i)), 1e-4f, $"limb {(ClimbLimbStepper.Limb)i} moved while hanging still");
            }
        }

        [Test]
        public void ThePairsAlternate()
        {
            var stepper = new ClimbLimbStepper();
            float y = 0.0f;
            int last = -1;
            int alternations = 0;
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < 8.0f; t += dt)
            {
                y += 2.5f * dt;
                stepper.Tick(dt, BodyAt(y), WallPoint, Normal, 1);
                int pair = stepper.PairStepping(0) ? 0 : stepper.PairStepping(1) ? 1 : -1;
                if (pair >= 0 && pair != last)
                {
                    if (last >= 0)
                    {
                        alternations++;
                    }
                    last = pair;
                }
            }
            Assert.Greater(alternations, 6, "the pairs did not take turns");
        }
    }
}
