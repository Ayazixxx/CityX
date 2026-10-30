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
            // A palm or a toe just off the face, and a stepping limb lifts a little more.
            float offWall = Mathf.Max(ClimbLimbStepper.HandOffWall, ClimbLimbStepper.FootOffWall);
            Assert.Less(run.FarthestOffWall, offWall + ClimbLimbStepper.LiftMetres + 0.01f, "a hold was nowhere near the wall");
        }

        // ---- the second pass: holds a climber could really use, and sideways ------------------------------------------------------------------

        private static Run Shuffle(Vector2 travel, float seconds, out float handsCrossed, out float feetCrossed, out float straightestArm)
        {
            var stepper = new ClimbLimbStepper();
            var run = new Run();
            handsCrossed = float.MinValue;   // the most the left hand was ever right of the right hand: negative all the way means they never crossed
            feetCrossed = float.MinValue;
            straightestArm = 0.0f;
            Vector3 moveWorld = ClimbLimbStepper.TravelDirection(Normal, travel);
            Vector3 offset = Vector3.zero;
            const float dt = 1.0f / 60.0f;
            var wasStepping = new bool[4];
            for (float t = 0.0f; t < seconds; t += dt)
            {
                offset += moveWorld * (1.8f * dt);
                ClimbLimbStepper.Body body = BodyAt(0.0f);
                body.LeftShoulder += offset; body.RightShoulder += offset; body.LeftHip += offset; body.RightHip += offset;
                stepper.Tick(dt, body, WallPoint, Normal, travel);
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
                    if (limb == ClimbLimbStepper.Limb.LeftHand || limb == ClimbLimbStepper.Limb.RightHand)
                    {
                        straightestArm = Mathf.Max(straightestArm, reach);
                    }
                    float offWall = Vector3.Dot(target - WallPoint, Normal);
                    run.DeepestIntoWall = Mathf.Min(run.DeepestIntoWall, offWall);
                    run.FarthestOffWall = Mathf.Max(run.FarthestOffWall, offWall);
                }
                // The right hand stays right of the left, the right foot right of the left: no crossing, however far the shuffle goes.
                handsCrossed = Mathf.Max(handsCrossed, stepper.Anchor(ClimbLimbStepper.Limb.LeftHand).x - stepper.Anchor(ClimbLimbStepper.Limb.RightHand).x);
                feetCrossed = Mathf.Max(feetCrossed, stepper.Anchor(ClimbLimbStepper.Limb.LeftFoot).x - stepper.Anchor(ClimbLimbStepper.Limb.RightFoot).x);
            }
            return run;
        }

        [Test]
        public void ShufflingSidewaysEveryLimbStepsAlongTheWallAndNothingCrosses()
        {
            foreach (float side in new[] { 1.0f, -1.0f })
            {
                Run run = Shuffle(new Vector2(side, 0.0f), 5.0f, out float handsCrossed, out float feetCrossed, out _);
                for (int i = 0; i < 4; i++)
                {
                    Assert.Greater(run.Steps[i], 2, $"limb {(ClimbLimbStepper.Limb)i} did not keep stepping while shuffling {(side > 0 ? "right" : "left")}");
                }
                Assert.Less(handsCrossed, 0.0f, "the hands crossed while shuffling sideways");
                Assert.Less(feetCrossed, 0.0f, "the feet crossed while shuffling sideways");
                Assert.Less(run.WorstReach, 1.10f, $"a limb was asked to reach {run.WorstReach:F2} of its length shuffling sideways");
                Assert.GreaterOrEqual(run.DeepestIntoWall, 0.0f, "a sideways hold went into the wall");
                Assert.IsFalse(run.BothPairsStepped, "both pairs stepped at once while shuffling");
                Assert.IsFalse(run.BothHandsStepped, "both hands let go together while shuffling");
            }
        }

        [Test]
        public void ADiagonalClimbStepsToo()
        {
            Run run = Shuffle(new Vector2(0.7f, 0.7f), 5.0f, out float handsCrossed, out _, out _);
            for (int i = 0; i < 4; i++)
            {
                Assert.Greater(run.Steps[i], 2, $"limb {(ClimbLimbStepper.Limb)i} did not step on a diagonal climb");
            }
            Assert.Less(handsCrossed, 0.0f, "the hands crossed on a diagonal");
            Assert.Less(run.WorstReach, 1.10f);
        }

        [Test]
        public void TheArmsStayBentReachingUpAndTheFeetPushFromBesideTheHips()
        {
            // The first pass hung the hands 0.62 arm lengths over the shoulders -- a straight arm over the head. A climber's arm is bent.
            Shuffle(Vector2.up, 6.0f, out _, out _, out float straightestArm);
            Assert.Less(straightestArm, 0.92f, $"an arm went {straightestArm:F2} of its length: straight over the head again");
            var stepper = new ClimbLimbStepper();
            ClimbLimbStepper.Body body = BodyAt(0.0f);
            foreach (ClimbLimbStepper.Limb foot in new[] { ClimbLimbStepper.Limb.LeftFoot, ClimbLimbStepper.Limb.RightFoot })
            {
                Vector3 ideal = stepper.Ideal(foot, body, WallPoint, Normal, Vector2.up);
                float below = RootOf(foot, body).y - ideal.y;
                Assert.Less(below, 0.25f * body.LegLength, "going up, a foot's hold is far below the hip: a straight, dangling leg instead of a push");
                Assert.Greater(below, 0.0f, "a foot's hold is above the hip");
            }
            // Climbing down, the feet lead: they reach well below the hips, and the hands stay about at the shoulders.
            Vector3 footDown = stepper.Ideal(ClimbLimbStepper.Limb.LeftFoot, body, WallPoint, Normal, Vector2.down);
            Assert.Greater(body.LeftHip.y - footDown.y, 0.6f * body.LegLength, "climbing down, the feet should reach down first");
            Vector3 handDown = stepper.Ideal(ClimbLimbStepper.Limb.LeftHand, body, WallPoint, Normal, Vector2.down);
            Assert.Less(Mathf.Abs(handDown.y - body.LeftShoulder.y), 0.25f * body.ArmLength, "climbing down, the hands should hold about shoulder height");
        }

        [Test]
        public void ChangingDirectionReplantsTheHoldsOnePairAtATime()
        {
            var stepper = new ClimbLimbStepper();
            const float dt = 1.0f / 60.0f;
            float y = 0.0f;
            for (float t = 0.0f; t < 2.0f; t += dt)
            {
                y += 2.5f * dt;
                stepper.Tick(dt, BodyAt(y), WallPoint, Normal, Vector2.up);
            }
            // Now shuffle right from where it is: the holds were laid out for going up, so they are all "behind" along the new direction.
            Vector3 offset = Vector3.zero;
            int stepsStarted = 0;
            var was = new bool[4];
            bool everBoth = false;
            for (float t = 0.0f; t < 1.0f; t += dt)
            {
                offset += Vector3.right * (1.8f * dt);
                ClimbLimbStepper.Body body = BodyAt(y);
                body.LeftShoulder += offset; body.RightShoulder += offset; body.LeftHip += offset; body.RightHip += offset;
                stepper.Tick(dt, body, WallPoint, Normal, Vector2.right);
                if (stepper.PairStepping(0) && stepper.PairStepping(1)) everBoth = true;
                for (int i = 0; i < 4; i++)
                {
                    bool s = stepper.IsStepping((ClimbLimbStepper.Limb)i);
                    if (s && !was[i]) stepsStarted++;
                    was[i] = s;
                }
            }
            Assert.GreaterOrEqual(stepsStarted, 4, "a change of direction did not re-plant the holds");
            Assert.IsFalse(everBoth, "both pairs re-planted at once: nothing was holding the weight");
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
