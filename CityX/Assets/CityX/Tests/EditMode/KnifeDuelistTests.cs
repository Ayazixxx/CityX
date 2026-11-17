using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Crazy Guy's mind: the duelist fighting the knife's way (DuelistStyle.Knife; owner, 2026-11-16: "make him wield a kitchen knife and utilize its
    /// advantages"). Close and fast, hardly a guard, round to the back for the knife's backstab when he has a clear shot at it, round a slow blow
    /// instead of away from it.
    /// </summary>
    public sealed class KnifeDuelistTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static DuelistMind Knife(int seed) => new DuelistMind(seed, DuelistStyle.Knife);

        private static DuelistMind.Senses At(float distance, float roundDegrees)
        {
            return new DuelistMind.Senses { HasTarget = true, Distance = distance, Stamina = 1.0f, CanDash = true, RoundTargetDegrees = roundDegrees };
        }

        private const int CirclingSeeds = 20;
        private const float CirclingSeconds = 2.0f;

        /// <summary>The seconds <see cref="Circling"/> watches him for, over all its seeds.</summary>
        private const float Watched = CirclingSeeds * CirclingSeconds;

        /// <summary>Seconds he spends moving toward the target's back and toward its front, standing <paramref name="round"/> degrees round it.</summary>
        private static (float towardBack, float towardFront) Circling(float round)
        {
            float towardBack = 0.0f, towardFront = 0.0f;
            for (int seed = 1; seed <= CirclingSeeds; seed++)
            {
                DuelistMind mind = Knife(seed);
                for (float t = 0.0f; t < CirclingSeconds; t += Dt)
                {
                    // Just out of his reach, so he circles rather than cuts.
                    DuelistMind.Intent intent = mind.Tick(At(DuelistStyle.Knife.EngageMetres, round), Dt);
                    if (intent.Light || intent.Heavy || intent.Dash) continue;
                    // On its right (+), his left (-x) takes him behind it; on its left, his right.
                    float back = round > 0.0f ? -intent.Move.x : intent.Move.x;
                    if (back > 0.3f) towardBack += Dt;
                    if (back < -0.3f) towardFront += Dt;
                }
            }
            return (towardBack, towardFront);
        }

        [Test]
        public void WithAClearShotAtItsBackHeWorksRoundToIt()
        {
            foreach (float round in new[] { 130.0f, -130.0f })
            {
                (float towardBack, float towardFront) = Circling(round);
                Assert.Greater(towardBack, towardFront * 10.0f, $"past the target's {(round > 0 ? "right" : "left")} side he circles toward its front as much as its back");
            }
        }

        /// <summary>Owner, 2026-11-17: "the crazy guy always seems to go for backstabs make it so he only goes for that when he has a clear shot".</summary>
        [Test]
        public void InFrontOfTheTargetHeDoesNotWorkRoundForItsBack()
        {
            foreach (float round in new[] { 20.0f, 60.0f, 90.0f, -60.0f })
            {
                (float towardBack, float towardFront) = Circling(round);
                // Ready to cut, he steps straight in (neither way round); between cuts he circles both ways.
                Assert.LessOrEqual(towardBack, towardFront + 0.25f * Watched,
                    $"{round} degrees round the target, with no clear shot, he still works round for its back");
            }
        }

        [Test]
        public void InFrontOfTheTargetInReachHeCutsFaceOn()
        {
            foreach (float round in new[] { 0.0f, 45.0f, -90.0f })
            {
                for (int seed = 1; seed <= 20; seed++)
                {
                    DuelistMind mind = Knife(seed);
                    DuelistMind.Intent intent = mind.Tick(At(DuelistStyle.Knife.StrikeMetres - 0.2f, round), Dt);
                    Assert.IsTrue(intent.Light || intent.Heavy, $"seed {seed}, {round} degrees round: in reach and ready, he held his cut to go for the back");
                }
            }
        }

        [Test]
        public void BehindTheTargetInReachHeCutsAtOnce()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                DuelistMind mind = Knife(seed);
                DuelistMind.Senses senses = At(DuelistStyle.Knife.StrikeMetres - 0.2f, 175.0f);
                senses.TargetAttacking = true;   // even with the target busy swinging the other way
                Assert.IsTrue(mind.Tick(senses, Dt).Light, $"seed {seed}: behind it, in reach, he did not cut at once");
            }
        }

        [Test]
        public void TheSwordsmanDoesNotGoForTheBack()
        {
            var mind = new DuelistMind(1);
            DuelistMind.Senses senses = At(DuelistMind.StrikeMetres - 0.2f, 175.0f);
            senses.TargetAttacking = true;
            Assert.IsFalse(mind.Tick(senses, Dt).Light, "the swordsman swung into a target already swinging: that is the knife's way, not his");
        }

        [Test]
        public void HeCutsInFlurriesAndHardlyEverGuards()
        {
            int presses = 0, attacks = 0;
            float guarded = 0.0f, total = 0.0f;
            for (int seed = 1; seed <= 60; seed++)
            {
                DuelistMind mind = Knife(seed);
                bool wasAttack = false;
                float attackStarted = -10.0f;
                for (float t = 0.0f; t < 6.0f; t += Dt)
                {
                    DuelistMind.Senses senses = At(DuelistStyle.Knife.StrikeMetres - 0.2f, 0.0f);
                    // His swings hold him a second from the first press (the combat's chain), then let go.
                    senses.Committed = mind.State == DuelistState.Attack && t - attackStarted < 1.0f;
                    DuelistMind.Intent intent = mind.Tick(senses, Dt);
                    if (intent.Light) presses++;
                    if (mind.State == DuelistState.Attack && !wasAttack)
                    {
                        attacks++;
                        attackStarted = t;
                    }
                    wasAttack = mind.State == DuelistState.Attack;
                    if (intent.Guard) guarded += Dt;
                    total += Dt;
                }
            }
            Assert.Greater(presses / (float)Mathf.Max(1, attacks), 2.0f, "his attacks are single cuts, not a knife's flurries");
            Assert.Less(guarded / total, 0.1f, "he stands behind a guard a knife hardly has");
        }

        [Test]
        public void OutOfASlowBlowHeDashesRoundItNotAway()
        {
            int dashes = 0, round = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                DuelistMind mind = Knife(seed);
                DuelistMind.Senses senses = At(3.0f, 60.0f);   // in front of it, a little to its right
                senses.TargetAttacking = true;
                for (float t = 0.0f; t < 1.0f; t += Dt)
                {
                    senses.TargetWindingUp = t < 0.8f;   // a heavy
                    DuelistMind.Intent intent = mind.Tick(senses, Dt);
                    if (intent.Dash)
                    {
                        dashes++;
                        // On its right: round toward its back is his left (-x), and onward, past it (+y).
                        if (intent.DashDirection.x < -0.3f && intent.DashDirection.y > 0.0f) round++;
                        break;
                    }
                }
            }
            Assert.Greater(dashes, 60, "he hardly ever dashes out of a slow blow");
            Assert.Greater(round, dashes * 0.6f, "he dashes away from a slow blow instead of round it");
        }
    }
}
