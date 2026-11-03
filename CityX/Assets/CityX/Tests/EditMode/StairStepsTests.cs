using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The feet on a stair on paper (Character.StairSteps): a body walks a flight of the real size with nothing but the stepper's own rules.</summary>
    public sealed class StairStepsTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float Tread = 0.36f;
        private const float Riser = 0.227f;
        private const float LegLength = 0.77f;
        private const float HipHeight = 0.9f;

        private static float StairAt(float z) => z <= 0.0f ? 0.0f : Riser * (Mathf.Floor(z / Tread) + 1.0f);

        private static bool Ground(Vector3 at, out float height)
        {
            height = StairAt(at.z);
            return Mathf.Abs(height - at.y) <= 1.2f;
        }

        /// <summary>The body at <paramref name="z"/> on the line up the flight, going up (+z) or down, facing the way it goes.</summary>
        private static StairSteps.Body BodyAt(float z, bool up, float speed)
        {
            float slope = up ? Riser / Tread : -Riser / Tread;
            float floor = z * (Riser / Tread);
            float yaw = up ? 0.0f : 180.0f;
            Vector3 right = Quaternion.Euler(0.0f, yaw, 0.0f) * Vector3.right;
            var centre = new Vector3(0.0f, floor, z);
            return new StairSteps.Body
            {
                Centre = centre,
                Along = up ? Vector3.forward : Vector3.back,
                Speed = speed,
                Slope = slope,
                Yaw = yaw,
                LeftHip = centre + Vector3.up * HipHeight - right * 0.12f,
                RightHip = centre + Vector3.up * HipHeight + right * 0.12f,
                HalfWidth = 0.12f,
                LegLength = LegLength,
                SoleHeight = 0.135f,
                BallForward = 0.2f,
                ToeTip = 0.06f,
                HeelBack = 0.3f,
                ClipLeft = centre + Vector3.up * 0.135f - right * 0.12f,
                ClipRight = centre + Vector3.up * 0.135f + right * 0.12f,
            };
        }

        /// <summary>Walks the flight, calling back each frame after the stepper has stepped.</summary>
        private static StairSteps Walk(bool up, float speed, System.Action<StairSteps> eachFrame = null)
        {
            var steps = new StairSteps();
            float z = up ? 1.0f : 40.0f;
            for (int frame = 0; frame < 90; frame++)
            {
                z += (up ? speed : -speed) * Dt;
                steps.Step(Dt, 1.0f, BodyAt(z, up, speed), Ground);
                if (frame > 15)
                {
                    eachFrame?.Invoke(steps);
                }
            }
            return steps;
        }

        [TestCase(true, 4.0f)]
        [TestCase(false, 4.0f)]
        [TestCase(true, 7.5f)]
        [TestCase(false, 7.5f)]
        public void APlantedFootNeverMoves(bool up, float speed)
        {
            var was = new Vector3?[2];
            Walk(up, speed, steps =>
            {
                for (int i = 0; i < 2; i++)
                {
                    StairSteps.Foot foot = steps.Of(i);
                    if (foot.Stepping)
                    {
                        was[i] = null;
                        continue;
                    }
                    if (was[i].HasValue)
                    {
                        Assert.AreEqual(0.0f, Vector3.Distance(was[i].Value, foot.Ball), 1e-4f, "a planted foot slid");
                    }
                    was[i] = foot.Ball;
                }
            });
        }

        [TestCase(true, 4.0f)]
        [TestCase(false, 4.0f)]
        [TestCase(true, 7.5f)]
        [TestCase(false, 7.5f)]
        public void EveryStepLandsOnATreadWithTheFootsUphillEndClearOfTheRiser(bool up, float speed)
        {
            var wasStepping = new bool[2];
            int landings = 0;
            Walk(up, speed, steps =>
            {
                for (int i = 0; i < 2; i++)
                {
                    StairSteps.Foot foot = steps.Of(i);
                    if (wasStepping[i] && !foot.Stepping)
                    {
                        landings++;
                        Assert.AreEqual(StairAt(foot.Ball.z), foot.Ball.y, 0.001f, "a foot landed off its tread");
                        Vector3 uphillEnd = foot.Ball + (up ? Vector3.forward * 0.06f : Vector3.forward * 0.3f);
                        Assert.AreEqual(StairAt(foot.Ball.z), StairAt(uphillEnd.z), 0.001f, "the foot's uphill end is in the riser");
                    }
                    wasStepping[i] = foot.Stepping;
                }
            });
            Assert.GreaterOrEqual(landings, 6, "it hardly stepped");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheFeetTakeTurns(bool up)
        {
            var wasStepping = new bool[2];
            int last = -1, turns = 0;
            Walk(up, 4.0f, steps =>
            {
                for (int i = 0; i < 2; i++)
                {
                    bool stepping = steps.Of(i).Stepping;
                    if (stepping && !wasStepping[i])
                    {
                        Assert.AreNotEqual(last, i, "the same foot stepped twice running");
                        last = i;
                        turns++;
                    }
                    wasStepping[i] = stepping;
                }
            });
            Assert.GreaterOrEqual(turns, 6);
        }

        [Test]
        public void AFootLeftBehindRollsOntoItsBall()
        {
            float most = 0.0f;
            Walk(true, 4.0f, steps =>
            {
                for (int i = 0; i < 2; i++)
                {
                    if (!steps.Of(i).Stepping)
                    {
                        most = Mathf.Max(most, steps.Of(i).Pitch);
                    }
                }
            });
            Assert.Greater(most, 5.0f, "the heel never came up");
        }

        [Test]
        public void StandingOnTheStairsTheFeetStayPut()
        {
            var steps = new StairSteps();
            StairSteps.Body body = BodyAt(3.0f, true, 0.0f);
            for (int i = 0; i < 30; i++)
            {
                steps.Step(Dt, 1.0f, body, Ground);
            }
            int taken = steps.StepsTaken;
            Vector3 left = steps.Of(0).Ball, right = steps.Of(1).Ball;
            for (int i = 0; i < 120; i++)
            {
                steps.Step(Dt, 1.0f, body, Ground);
            }
            Assert.AreEqual(taken, steps.StepsTaken, "standing still, a foot stepped");
            Assert.AreEqual(left, steps.Of(0).Ball);
            Assert.AreEqual(right, steps.Of(1).Ball);
        }

        [Test]
        public void TheLegsFadeInAndBack()
        {
            var steps = new StairSteps();
            StairSteps.Body body = BodyAt(3.0f, true, 0.0f);
            steps.Step(Dt, 1.0f, body, Ground);
            Assert.Greater(steps.Weight, 0.0f);
            Assert.Less(steps.Weight, 1.0f, "the legs were taken at once");
            for (int i = 0; i < 30; i++)
            {
                steps.Step(Dt, 1.0f, body, Ground);
            }
            Assert.AreEqual(1.0f, steps.Weight);
            for (int i = 0; i < 30; i++)
            {
                steps.Step(Dt, 0.0f, body, Ground);
            }
            Assert.AreEqual(0.0f, steps.Weight);
            Assert.AreEqual(0.0f, steps.Drop);
        }

        [Test]
        public void TheHipsNeverDropFurtherThanTheLimit()
        {
            Walk(false, 7.5f, steps => Assert.LessOrEqual(steps.Drop, StairSteps.MostDrop + 1e-4f));
        }
    }
}
