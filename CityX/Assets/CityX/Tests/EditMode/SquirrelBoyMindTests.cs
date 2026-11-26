using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Squirrel Boy's mind (owner, 2026-11-25: "he is non hostile until struck and then he fights unarmed with fists only for now. he is also able
    /// to crawl up walls and will run away and crawl up a wall when low health"): calm until struck; then after his foe, punching in combos with
    /// pauses; badly hurt, to the nearest wall and up it (or away, with no wall); at the top he stays, and runs again when followed up.
    /// </summary>
    public sealed class SquirrelBoyMindTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static SquirrelBoyMind.Senses Near(float distance, bool struck, float health = 1.0f) => new SquirrelBoyMind.Senses
        {
            Struck = struck, HasFoe = true, FoeDistance = distance, Health = health, HasWall = true, WallDistance = 10.0f,
        };

        [Test]
        public void CalmUntilStruckThenAfterHisFoe()
        {
            var mind = new SquirrelBoyMind();
            for (int i = 0; i < 120; i++)
            {
                SquirrelBoyMind.Intent calm = mind.Tick(Near(1.0f, false), Dt);
                Assert.IsFalse(calm.Punch, "he punches before anybody has hurt him");
                Assert.AreEqual(SquirrelBoyMind.Goal.Stay, calm.Goal, "he goes for somebody before anybody has hurt him");
            }
            Assert.AreEqual(SquirrelBoyState.Calm, mind.State);

            SquirrelBoyMind.Intent far = mind.Tick(Near(12.0f, true), Dt);
            Assert.AreEqual(SquirrelBoyState.Fight, mind.State, "struck, he does not fight");
            Assert.AreEqual(SquirrelBoyMind.Goal.Foe, far.Goal, "struck, he does not go for his foe");
            Assert.IsTrue(far.Run, "far off, he walks to his foe");
            Assert.IsFalse(mind.Tick(Near(3.0f, true), Dt).Run, "close, he still runs at his foe");
        }

        [Test]
        public void InReachHePunchesInCombosWithPauses()
        {
            var mind = new SquirrelBoyMind(7);
            int punches = 0, heavies = 0, longestGapFrames = 0, gap = 0;
            for (int i = 0; i < 60 * 20; i++)
            {
                SquirrelBoyMind.Intent intent = mind.Tick(Near(1.5f, true), Dt);
                Assert.AreEqual(SquirrelBoyMind.Goal.Stay, intent.Goal, "in reach he walks on into his foe");
                if (intent.Punch)
                {
                    punches++;
                    if (intent.Heavy) heavies++;
                    longestGapFrames = System.Math.Max(longestGapFrames, gap);
                    gap = 0;
                }
                else
                {
                    gap++;
                }
            }
            Assert.That(punches, Is.InRange(20, 60), "he does not punch at a fist fighter's pace");
            Assert.Greater(heavies, 0, "he never throws a heavy");
            Assert.Less(heavies, punches / 2, "most of his punches are heavy");
            Assert.GreaterOrEqual(longestGapFrames, (int)(SquirrelBoyMind.PauseAfterCombo * 60) - 1, "he never pauses between combos");

            Assert.IsFalse(mind.Tick(new SquirrelBoyMind.Senses { Struck = true, HasFoe = true, FoeDistance = 1.5f, Health = 1.0f, Punching = true }, Dt).Punch,
                "he throws a punch before the last one is done");
        }

        [Test]
        public void BadlyHurtHeRunsForAWallClimbsItAndStaysUpUntilFollowed()
        {
            var mind = new SquirrelBoyMind();
            mind.Tick(Near(2.0f, true), Dt);
            var hurt = new SquirrelBoyMind.Senses
            {
                Struck = true, HasFoe = true, FoeDistance = 2.0f, Health = SquirrelBoyMind.FleeBelowHealth - 0.05f, HasWall = true, WallDistance = 8.0f,
            };
            SquirrelBoyMind.Intent run = mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyState.Flee, mind.State, "badly hurt he fights on");
            Assert.AreEqual(SquirrelBoyMind.Goal.Wall, run.Goal, "badly hurt he does not run for the wall");
            Assert.IsTrue(run.Run);
            Assert.IsFalse(run.Punch, "running away he punches");
            Assert.IsFalse(run.StartClimb, "he tries to climb a wall that is still metres away");

            hurt.WallDistance = 0.6f;
            Assert.IsTrue(mind.Tick(hurt, Dt).StartClimb, "at the wall he does not go up it");

            hurt.Climbing = true;
            SquirrelBoyMind.Intent up = mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyState.Climb, mind.State);
            Assert.IsTrue(up.ClimbUp, "on the wall he does not climb up");

            hurt.Climbing = false;
            hurt.ClimbedOver = true;
            hurt.FoeDistance = 15.0f;
            mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyState.Perched, mind.State, "over the top he does not stay up there");
            for (int i = 0; i < 120; i++)
            {
                Assert.AreEqual(SquirrelBoyMind.Goal.Stay, mind.Tick(hurt, Dt).Goal, "up there, alone, he wanders off");
            }

            hurt.FoeDistance = 2.0f;
            mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyState.Flee, mind.State, "followed up there, he does not run again");
        }

        [Test]
        public void HurtWithNoWallNearHeRunsAwayAndFallingOffTheWallHeGoesBackToIt()
        {
            var mind = new SquirrelBoyMind();
            var hurt = new SquirrelBoyMind.Senses { Struck = true, HasFoe = true, FoeDistance = 2.0f, Health = 0.1f };
            SquirrelBoyMind.Intent away = mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyMind.Goal.AwayFromFoe, away.Goal, "with no wall near he does not run away");
            Assert.IsTrue(away.Run);

            hurt.HasWall = true;
            hurt.WallDistance = 0.5f;
            hurt.Climbing = true;
            mind.Tick(hurt, Dt);
            hurt.Climbing = false;   // fell off, not over the top
            mind.Tick(hurt, Dt);
            Assert.AreEqual(SquirrelBoyState.Flee, mind.State, "fallen off the wall he does not go back to it");
        }

        [Test]
        public void KnockedDownOrStunnedHeDoesNothing()
        {
            var mind = new SquirrelBoyMind();
            SquirrelBoyMind.Senses senses = Near(1.5f, true);
            senses.Helpless = true;
            for (int i = 0; i < 120; i++)
            {
                SquirrelBoyMind.Intent intent = mind.Tick(senses, Dt);
                Assert.IsFalse(intent.Punch, "stunned, he punches");
                Assert.AreEqual(SquirrelBoyMind.Goal.Stay, intent.Goal, "stunned, he moves");
            }
        }
    }
}
