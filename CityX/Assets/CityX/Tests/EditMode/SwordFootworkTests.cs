using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sword fight's stepping feet on paper (owner, 2026-10-16: "make sure his feet dont lock up during combat animations while moving"): a
    /// planted foot never slides, a foot left behind steps, the feet step into the stance and keep up with a walking, lunging or turning body,
    /// one at a time, without crossing, and the stepper takes the feet from the clip and gives them back smoothly.
    /// </summary>
    public sealed class SwordFootworkTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float Leg = 0.765f;

        private static SwordFootwork.Body BodyAt(Vector3 ground, float yaw, Vector3 velocity, float lead = -1.0f, float depth = 0.35f)
        {
            Quaternion facing = Quaternion.Euler(0.0f, yaw, 0.0f);
            return new SwordFootwork.Body
            {
                Ground = ground,
                Yaw = yaw,
                Velocity = velocity,
                LegLength = Leg,
                Lead = lead,
                Depth = depth,
                // The clip stands with its feet together under the hips.
                ClipLeft = ground + facing * new Vector3(-0.15f, 0.135f, 0.0f),
                ClipRight = ground + facing * new Vector3(0.15f, 0.135f, 0.0f),
                ClipLeftYaw = yaw,
                ClipRightYaw = yaw,
            };
        }

        /// <summary>Runs the stepper with the body moving at <paramref name="velocity"/> (turning at <paramref name="turnRate"/>), checking every frame.</summary>
        private static void Run(SwordFootwork feet, ref Vector3 ground, ref float yaw, Vector3 velocity, float seconds, float turnRate = 0.0f,
            float lead = -1.0f, float depth = 0.35f, System.Action<int> everyFrame = null)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                ground += velocity * Dt;
                yaw += turnRate * Dt;
                feet.Step(Dt, 1.0f, BodyAt(ground, yaw, velocity, lead, depth));
                everyFrame?.Invoke(i);
            }
        }

        /// <summary>Settled in the left-foot-forward stance, then one frame after another with the stance and step rule given.</summary>
        private static SwordFootwork Settled(ref Vector3 ground, ref float yaw)
        {
            var feet = new SwordFootwork();
            Run(feet, ref ground, ref yaw, Vector3.zero, 1.0f);
            return feet;
        }

        [Test]
        public void AFootBarredFromSteppingStaysDownHoweverFarItsPlaceMoves()
        {
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            SwordFootwork feet = Settled(ref ground, ref yaw);
            int steps = feet.StepsTaken;
            Vector3 left = feet.Of(0).Position;
            Vector3 right = feet.Of(1).Position;
            SwordFootwork.Body body = BodyAt(ground, yaw, Vector3.zero, lead: 1.0f, depth: 0.8f);
            body.Steps = FootSteps.None;
            for (int i = 0; i < 30; i++)
            {
                feet.Step(Dt, 1.0f, body);
            }
            Assert.AreEqual(steps, feet.StepsTaken, "a foot set off with every step barred");
            Assert.AreEqual(left, feet.Of(0).Position);
            Assert.AreEqual(right, feet.Of(1).Position);
        }

        [Test]
        public void OnlyTheFootACutStridesOnSteps()
        {
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            SwordFootwork feet = Settled(ref ground, ref yaw);
            Vector3 left = feet.Of(0).Position;
            float rightWas = feet.Of(1).Position.z;
            SwordFootwork.Body body = BodyAt(ground, yaw, Vector3.zero, lead: 1.0f, depth: 0.8f);
            body.Steps = FootSteps.OnlyRight;
            for (int i = 0; i < 30; i++)
            {
                feet.Step(Dt, 1.0f, body);
            }
            Assert.AreEqual(left, feet.Of(0).Position, "the other foot stepped too: a shuffle");
            Assert.Greater(feet.Of(1).Position.z, rightWas + 0.2f, "the right foot did not stride through");
        }

        /// <summary>Walking and swinging must not lock the feet up (owner, 2026-10-16): a foot the leg can no longer reach goes, whatever the cut says.</summary>
        [Test]
        public void AFootTheLegCanNoLongerReachStepsAnyway()
        {
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            SwordFootwork feet = Settled(ref ground, ref yaw);
            int steps = feet.StepsTaken;
            Vector3 velocity = new Vector3(0.0f, 0.0f, 1.6f);
            const float reach = 0.45f;
            for (int i = 0; i < 60; i++)
            {
                ground += velocity * Dt;
                SwordFootwork.Body body = BodyAt(ground, yaw, velocity);
                body.Steps = FootSteps.None;
                body.Reach = reach;
                body.LeftHip = ground + new Vector3(-0.1f, 0.9f, 0.0f);
                body.RightHip = ground + new Vector3(0.1f, 0.9f, 0.0f);
                feet.Step(Dt, 1.0f, body);
            }
            Assert.Greater(feet.StepsTaken, steps + 1, "a walking body left its barred feet behind");
        }

        [Test]
        public void TheFeetTakeTheStanceFromWhereTheClipHadThem()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            feet.Step(Dt, 1.0f, BodyAt(ground, yaw, Vector3.zero));
            Assert.AreEqual(-0.15f, feet.Of(0).Position.x, 0.001f, "the left foot did not start where the clip had it");
            Assert.AreEqual(0.15f, feet.Of(1).Position.x, 0.001f, "the right foot did not start where the clip had it");

            Run(feet, ref ground, ref yaw, Vector3.zero, 1.0f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 place = SwordFootwork.Place(i, BodyAt(ground, yaw, Vector3.zero), out _);
                Assert.Less(Vector3.Distance(feet.Of(i).Position, place), SwordFootwork.StepTrigger * Leg, $"foot {i} never stepped into the stance");
                Assert.IsFalse(feet.Of(i).Stepping, $"foot {i} is still stepping a second later");
            }
            // Left foot forward, both apart.
            Assert.Greater(feet.Of(0).Position.z, feet.Of(1).Position.z + 0.05f, "the left foot is not the front foot");
            Assert.Greater(feet.Of(1).Position.x - feet.Of(0).Position.x, 0.35f, "the feet are not apart");
            Assert.Greater(feet.StepsTaken, 0);
        }

        [Test]
        public void APlantedFootNeverMoves()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Vector3 velocity = new Vector3(0.0f, 0.0f, 1.6f);   // a swing on the move: CommittedMoveSpeedFraction of a walk
            Vector3[] last = new Vector3[2];
            bool[] wasPlanted = new bool[2];
            float worst = 0.0f;
            Run(feet, ref ground, ref yaw, velocity, 3.0f, everyFrame: frame =>
            {
                for (int i = 0; i < 2; i++)
                {
                    SwordFootwork.Foot foot = feet.Of(i);
                    if (!foot.Stepping && wasPlanted[i])
                    {
                        worst = Mathf.Max(worst, Vector3.Distance(foot.Position, last[i]));
                    }
                    last[i] = foot.Position;
                    wasPlanted[i] = !foot.Stepping;
                }
            });
            Assert.Less(worst, 0.001f, $"a planted foot slid {worst * 100:0.0} cm in one frame");
        }

        [Test]
        public void WalkingTheFeetKeepStepping()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Vector3 velocity = new Vector3(0.0f, 0.0f, 1.6f);
            Run(feet, ref ground, ref yaw, velocity, 0.5f);
            int before = feet.StepsTaken;
            float farthest = 0.0f;
            int bothUp = 0;
            Run(feet, ref ground, ref yaw, velocity, 3.0f, everyFrame: frame =>
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector3 place = SwordFootwork.Place(i, BodyAt(ground, yaw, velocity), out _);
                    farthest = Mathf.Max(farthest, new Vector2(feet.Of(i).Position.x - place.x, feet.Of(i).Position.z - place.z).magnitude);
                }
                if (feet.Of(0).Stepping && feet.Of(1).Stepping) bothUp++;
            });
            float stepsPerSecond = (feet.StepsTaken - before) / 3.0f;
            // 1.6 m/s on 0.77 m legs: about three to five steps a second, never a frozen foot.
            Assert.Greater(stepsPerSecond, 2.5f, $"only {stepsPerSecond:0.0} steps a second walking at 1.6 m/s: the feet lock up");
            Assert.Less(stepsPerSecond, 7.0f, $"{stepsPerSecond:0.0} steps a second: a frantic shuffle");
            Assert.Less(farthest, (SwordFootwork.MaxStepTrigger + SwordFootwork.MaxLandAhead) * Leg, $"a foot fell {farthest:0.00} m from its place: out of the leg's reach");
            Assert.Less(bothUp / (3.0f / Dt), 0.15f, "both feet were in the air for much of the walk");
            // The feet alternate: the left and right both step.
            Assert.Greater(feet.StepsTaken - before, 6);
        }

        [Test]
        public void OneFootStepsAtATimeUntilTheFirstIsWellOnItsWay()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Vector3 velocity = new Vector3(0.0f, 0.0f, 2.0f);
            float[] progressWhenOtherStarted = { -1.0f, -1.0f };
            bool[] stepping = new bool[2];
            float[] since = new float[2];
            bool early = false;
            Run(feet, ref ground, ref yaw, velocity, 3.0f, everyFrame: frame =>
            {
                for (int i = 0; i < 2; i++)
                {
                    bool now = feet.Of(i).Stepping;
                    since[i] = now ? (stepping[i] ? since[i] + Dt : 0.0f) : 0.0f;
                    if (now && !stepping[i] && stepping[1 - i] && since[1 - i] < SwordFootwork.OverlapShare * SwordFootwork.StepSeconds - Dt * 1.5f)
                    {
                        early = true;
                    }
                    stepping[i] = now;
                }
            });
            Assert.IsFalse(early, "a foot set off while the other had only just left the ground");
        }

        [Test]
        public void TheFeetNeverCrossTurningOrSidestepping()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            float worst = float.MaxValue;
            System.Action<int> check = frame =>
            {
                Quaternion facing = Quaternion.Euler(0.0f, yaw, 0.0f);
                float left = (Quaternion.Inverse(facing) * (feet.Of(0).Position - ground)).x;
                float right = (Quaternion.Inverse(facing) * (feet.Of(1).Position - ground)).x;
                worst = Mathf.Min(worst, right - left);
            };
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.6f, everyFrame: check);
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.5f, turnRate: 360.0f, everyFrame: check);        // half round on the spot
            Run(feet, ref ground, ref yaw, Quaternion.Euler(0, yaw, 0) * new Vector3(1.6f, 0, 0), 1.5f, everyFrame: check);   // sidestepping right
            Run(feet, ref ground, ref yaw, Quaternion.Euler(0, yaw, 0) * new Vector3(-1.6f, 0, 0), 1.5f, everyFrame: check);  // and left
            Assert.Greater(worst, 0.0f, $"the feet crossed ({worst:0.00} m apart side to side)");
        }

        [Test]
        public void TurningOnTheSpotTheFeetStepRound()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.6f);
            int before = feet.StepsTaken;
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.15f, turnRate: 600.0f);   // the swing's turn into the camera's aim: 90 degrees
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.6f);
            Assert.GreaterOrEqual(feet.StepsTaken - before, 2, "the feet did not step round with the body");
            for (int i = 0; i < 2; i++)
            {
                SwordFootwork.Place(i, BodyAt(ground, yaw, Vector3.zero), out float placeYaw);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(feet.Of(i).Yaw, placeYaw)), SwordFootwork.StepTriggerDegrees, $"foot {i} was left pointing the old way");
            }
        }

        [Test]
        public void ALungeStepsTheFrontFootFirst()
        {
            // A cut opens the stance (the front foot strides) while the body surges forward: the front foot goes first.
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.8f, depth: 0.35f);
            bool[] stepped = new bool[2];
            int first = -1;
            Vector3 lunge = new Vector3(0.0f, 0.0f, 1.2f);
            Run(feet, ref ground, ref yaw, lunge, 0.25f, depth: 0.7f, everyFrame: frame =>
            {
                for (int i = 0; i < 2; i++)
                {
                    if (feet.Of(i).Stepping && !stepped[i])
                    {
                        stepped[i] = true;
                        if (first < 0) first = i;
                    }
                }
            });
            Assert.AreEqual(0, first, "the back foot stepped first: a lunge strides with the front (left) foot");
        }

        [Test]
        public void AFootFarFromItsPlaceIsPutBackRatherThanStrode()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            Run(feet, ref ground, ref yaw, Vector3.zero, 0.6f);
            ground += new Vector3(3.0f, 0.0f, 0.0f);   // carried off (a shove that did not count as a teleport)
            feet.Step(Dt, 1.0f, BodyAt(ground, yaw, Vector3.zero));
            for (int i = 0; i < 2; i++)
            {
                Vector3 place = SwordFootwork.Place(i, BodyAt(ground, yaw, Vector3.zero), out _);
                Assert.Less(Vector3.Distance(feet.Of(i).Position, place), 0.6f, $"foot {i} was left metres behind");
            }
        }

        [Test]
        public void TheWeightFadesInAndOutAndForgetsTheFeetOnceOut()
        {
            var feet = new SwordFootwork();
            SwordFootwork.Body body = BodyAt(Vector3.zero, 0.0f, Vector3.zero);
            feet.Step(Dt, 1.0f, body);
            Assert.Greater(feet.Weight, 0.0f);
            Assert.Less(feet.Weight, 0.5f, "the stepper took the feet in one frame");
            for (int i = 0; i < 30; i++) feet.Step(Dt, 1.0f, body);
            Assert.AreEqual(1.0f, feet.Weight, 0.001f);
            for (int i = 0; i < 30; i++) feet.Step(Dt, 0.0f, body);
            Assert.AreEqual(0.0f, feet.Weight, 0.001f);

            // Taken again somewhere else: it starts from the clip's feet there, not where it last left them.
            SwordFootwork.Body elsewhere = BodyAt(new Vector3(10.0f, 0.0f, 0.0f), 0.0f, Vector3.zero);
            feet.Step(Dt, 1.0f, elsewhere);
            Assert.AreEqual(elsewhere.ClipLeft.x, feet.Of(0).Position.x, 0.001f);
        }

        [Test]
        public void PlantedFeetStayWithinTheLegsReachOfTheirHips()
        {
            // A 0.77 m leg under hips crouched a little reaches about 0.3 m out along the ground: the stance, the landings and every planted
            // foot are kept inside that, or the leg IK would drag a foot it could not get to.
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            const float reach = 0.30f;
            float worst = 0.0f;
            Vector3 velocity = new Vector3(0.0f, 0.0f, 1.6f);
            for (int frame = 0; frame < 240; frame++)
            {
                ground += velocity * Dt;
                SwordFootwork.Body body = BodyAt(ground, yaw, velocity, -1.0f, 0.7f);   // a deep lunge stance: wider than the legs reach
                Quaternion facing = Quaternion.Euler(0.0f, yaw, 0.0f);
                body.LeftHip = ground + facing * new Vector3(-0.17f, 0.9f, 0.0f);
                body.RightHip = ground + facing * new Vector3(0.17f, 0.9f, 0.0f);
                body.Reach = reach;
                feet.Step(Dt, 1.0f, body);
                if (frame < 30)
                {
                    continue;   // stepping into the stance from the clip's feet
                }
                for (int i = 0; i < 2; i++)
                {
                    if (feet.Of(i).Stepping) continue;
                    Vector3 hip = i == 0 ? body.LeftHip : body.RightHip;
                    worst = Mathf.Max(worst, new Vector2(feet.Of(i).Position.x - hip.x, feet.Of(i).Position.z - hip.z).magnitude);
                }
            }
            Assert.Less(worst, reach * 1.05f, $"a planted foot was left {worst:0.00} m from under its hip, beyond the leg's {reach:0.00} m reach");
        }

        [Test]
        public void AStepLiftsTheFootInAnArc()
        {
            var feet = new SwordFootwork();
            Vector3 ground = Vector3.zero;
            float yaw = 0.0f;
            float highest = 0.0f;
            Run(feet, ref ground, ref yaw, new Vector3(0.0f, 0.0f, 1.6f), 1.5f, everyFrame: frame =>
            {
                for (int i = 0; i < 2; i++)
                {
                    SwordFootwork.Foot foot = feet.Of(i);
                    if (!foot.Stepping)
                    {
                        Assert.AreEqual(0.0f, foot.Lift, 0.0001f, "a planted foot is off the ground");
                    }
                    highest = Mathf.Max(highest, foot.Lift);
                }
            });
            Assert.Greater(highest, 0.05f, "the steps shuffle along the ground");
            Assert.Less(highest, 0.15f, "the steps are high-kneed");
        }
    }
}
