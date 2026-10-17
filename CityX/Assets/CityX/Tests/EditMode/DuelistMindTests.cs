using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The swordsman's mind on its own (owner, 2026-10-17: "make a swordsman npc that wields the one handed sword so i can duel with him. make him
    /// utilize dashes as well as blocking and make him take advantage of the stagger if i attack their block").
    /// </summary>
    public sealed class DuelistMindTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static DuelistMind.Senses Facing(float distance)
        {
            return new DuelistMind.Senses { HasTarget = true, Distance = distance, Stamina = 1.0f, CanDash = true };
        }

        private static DuelistMind.Intent Run(DuelistMind mind, DuelistMind.Senses senses, float seconds, System.Func<DuelistMind.Intent, bool> until = null)
        {
            DuelistMind.Intent intent = default;
            for (float t = 0.0f; t < seconds; t += Dt)
            {
                intent = mind.Tick(senses, Dt);
                if (until != null && until(intent))
                {
                    return intent;
                }
            }
            return intent;
        }

        [Test]
        public void FarOffHeComesInAndRunsWhenItIsFar()
        {
            var mind = new DuelistMind(1);
            DuelistMind.Intent intent = mind.Tick(Facing(12.0f), Dt);
            Assert.AreEqual(DuelistState.Approach, mind.State);
            Assert.Greater(intent.Move.y, 0.9f, "he does not come toward you");
            Assert.IsTrue(intent.Sprint, "far off, he walks instead of running in");
            intent = mind.Tick(Facing(5.0f), Dt);
            Assert.IsFalse(intent.Sprint, "close, he still runs");
        }

        [Test]
        public void AtFightingDistanceHeCirclesAndChangesDirection()
        {
            var mind = new DuelistMind(2);
            float right = 0.0f, left = 0.0f;
            for (float t = 0.0f; t < 8.0f; t += Dt)
            {
                DuelistMind.Intent intent = mind.Tick(Facing(DuelistMind.EngageMetres), Dt);
                Assert.IsFalse(intent.Light || intent.Heavy, "out of reach he swings at the air");
                if (intent.Move.x > 0.5f) right += Dt;
                if (intent.Move.x < -0.5f) left += Dt;
            }
            Assert.AreEqual(DuelistState.Circle, mind.State);
            Assert.Greater(right, 1.0f, "he never circles one way");
            Assert.Greater(left, 1.0f, "he never circles the other way");
        }

        [Test]
        public void HeAnswersABlowAfterAReactionWithTheGuardMostlyAndSometimesADash()
        {
            int guards = 0, dashes = 0;
            const int Fights = 300;
            for (int seed = 1; seed <= Fights; seed++)
            {
                var mind = new DuelistMind(seed);
                DuelistMind.Senses senses = Facing(2.4f);
                Run(mind, senses, 1.0f);   // settled, circling
                senses.TargetAttacking = true;
                DuelistMind.Intent first = mind.Tick(senses, Dt);
                Assert.IsFalse(first.Guard || first.Dash, "he answered a blow the instant it started: no reaction time");
                DuelistMind.Intent answer = Run(mind, senses, 0.5f, i => i.Guard || i.Dash || i.Light || i.Heavy);
                if (answer.Guard) guards++;
                if (answer.Dash) dashes++;
            }
            Assert.That(guards / (float)Fights, Is.InRange(DuelistMind.GuardChance - 0.1f, DuelistMind.GuardChance + 0.1f), "he blocks too rarely or too often");
            Assert.That(dashes / (float)Fights, Is.InRange(DuelistMind.DashChance - 0.1f, DuelistMind.DashChance + 0.1f), "he dashes out of a blow too rarely or too often");
        }

        [Test]
        public void HePunishesAStaggeredTargetAtOnceAndKeepsCuttingWhileItIsOpen()
        {
            var mind = new DuelistMind(3);
            DuelistMind.Senses senses = Facing(2.2f);
            Run(mind, senses, 1.0f);
            senses.TargetStaggered = true;
            DuelistMind.Intent intent = mind.Tick(senses, Dt);
            Assert.AreEqual(DuelistState.Punish, mind.State, "a staggered target is not punished");
            Assert.IsTrue(intent.Light, "he does not cut at once into the opening");
            int cuts = 1;
            for (float t = 0.0f; t < 0.6f; t += Dt)
            {
                senses.Committed = t > 0.05f;
                if (mind.Tick(senses, Dt).Light) cuts++;
            }
            Assert.GreaterOrEqual(cuts, 2, "he stops at one cut while the target is still open");
        }

        [Test]
        public void AStaggeredTargetOutOfReachIsClosedOnWithADash()
        {
            var mind = new DuelistMind(4);
            DuelistMind.Senses senses = Facing(4.5f);
            senses.TargetStaggered = true;
            DuelistMind.Intent intent = mind.Tick(senses, Dt);
            Assert.Greater(intent.Move.y, 0.9f, "he does not go in");
            Assert.IsTrue(intent.Dash, "too far to walk in time, he does not dash in");
            Assert.Greater(intent.DashDirection.y, 0.9f, "the dash does not go toward the target");
        }

        /// <summary>The owner wants dashes used: after a chain he often gets back out with one.</summary>
        [Test]
        public void AfterAChainHeOftenDashesBackOut()
        {
            int dashes = 0;
            const int Fights = 200;
            for (int seed = 1; seed <= Fights; seed++)
            {
                var mind = new DuelistMind(seed);
                DuelistMind.Senses senses = Facing(2.0f);
                Run(mind, senses, 2.0f, i => i.Light || i.Heavy);
                if (mind.State != DuelistState.Attack) continue;
                senses.Committed = true;
                Run(mind, senses, 0.6f);
                senses.Committed = false;
                DuelistMind.Intent after = Run(mind, senses, 0.3f, i => i.Dash);
                if (after.Dash)
                {
                    dashes++;
                    Assert.Less(after.DashDirection.y, 0.1f, "he dashes in, not out, after his chain");
                }
            }
            Assert.That(dashes / (float)Fights, Is.InRange(DuelistMind.DashOutChance - 0.12f, DuelistMind.DashOutChance + 0.12f),
                "he dashes back out after a chain too rarely or too often");
        }

        [Test]
        public void ComingInFromFarHeOftenClosesWithADash()
        {
            int dashed = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var mind = new DuelistMind(seed);
                DuelistMind.Intent intent = Run(mind, Facing(7.0f), 5.0f, i => i.Dash);
                if (intent.Dash)
                {
                    dashed++;
                    Assert.Greater(intent.DashDirection.y, 0.9f, "closing in, he dashes away");
                }
            }
            Assert.Greater(dashed, 70, "coming in from far he hardly ever closes with a dash");
        }

        [Test]
        public void HeAttacksAnOpenTargetInReach()
        {
            var mind = new DuelistMind(5);
            DuelistMind.Intent intent = Run(mind, Facing(2.0f), 2.0f, i => i.Light || i.Heavy);
            Assert.IsTrue(intent.Light || intent.Heavy, "an open target in reach is never attacked");
            Assert.AreEqual(DuelistState.Attack, mind.State);
        }

        [Test]
        public void HeMostlyWaitsOutARaisedGuardAndOnlyDrivesAHeavyIntoIt()
        {
            int lights = 0, heavies = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var mind = new DuelistMind(seed);
                DuelistMind.Senses senses = Facing(2.0f);
                senses.TargetBlocking = true;
                DuelistMind.Intent intent = Run(mind, senses, 1.0f, i => i.Light || i.Heavy);
                if (intent.Light) lights++;
                if (intent.Heavy) heavies++;
            }
            Assert.AreEqual(0, lights, "he cuts into a raised guard (his blade would bounce)");
            Assert.Less(heavies, 50, "he swings into a raised guard far too often");
            Assert.Greater(heavies, 0, "he never tests a raised guard with a heavy");
        }

        [Test]
        public void ShortOfStaminaHeBacksOffUntilItIsBack()
        {
            var mind = new DuelistMind(6);
            DuelistMind.Senses senses = Facing(2.0f);
            senses.Stamina = 0.15f;
            DuelistMind.Intent intent = Run(mind, senses, 0.5f);
            Assert.AreEqual(DuelistState.Recover, mind.State);
            Assert.Less(intent.Move.y, 0.0f, "he does not back off to recover");
            senses.Stamina = 0.4f;
            Run(mind, senses, 0.5f);
            Assert.AreEqual(DuelistState.Recover, mind.State, "he goes back in before his stamina has recovered");
            senses.Stamina = 0.7f;
            Run(mind, senses, 0.2f);
            Assert.AreNotEqual(DuelistState.Recover, mind.State, "he stays out with his stamina back");
        }

        [Test]
        public void StunnedHePressesNothing()
        {
            var mind = new DuelistMind(7);
            DuelistMind.Senses senses = Facing(2.0f);
            senses.Stunned = true;
            senses.TargetStaggered = true;
            DuelistMind.Intent intent = Run(mind, senses, 0.5f, i => i.Light || i.Heavy || i.Guard || i.Dash || i.Move.sqrMagnitude > 0.0f);
            Assert.IsFalse(intent.Light || intent.Heavy || intent.Guard || intent.Dash, "stunned, he still acts");
            Assert.AreEqual(Vector2.zero, intent.Move);
        }

        [Test]
        public void HitHeSometimesDashesAway()
        {
            int dashes = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var mind = new DuelistMind(seed);
                DuelistMind.Senses senses = Facing(3.0f);
                Run(mind, senses, 0.5f);
                senses.TookHit = true;
                DuelistMind.Intent intent = mind.Tick(senses, Dt);
                if (intent.Dash)
                {
                    dashes++;
                    Assert.Less(intent.DashDirection.y, 0.1f, "he dashes toward what hit him");
                }
            }
            Assert.That(dashes / 200.0f, Is.InRange(DuelistMind.DashWhenHitChance - 0.12f, DuelistMind.DashWhenHitChance + 0.12f));
        }
    }
}
