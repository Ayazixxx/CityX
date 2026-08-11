using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The attack chain table.
    ///
    /// These read as trivial and are not. The chain is the thing that made the
    /// Godot build feel good to play, and the failure mode is a chain that
    /// silently loses its shape -- a window of zero mid-chain, two finishers, a
    /// stage that does no damage -- none of which throws.
    /// </summary>
    public class CombatTuningTests
    {
        [Test]
        public void TheUnarmedChainIsFiveStagesEndingInAFinisher()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            Assert.AreEqual(5, chain.Length, "the unarmed chain changed length");

            for (int i = 0; i < chain.Length - 1; i++)
            {
                Assert.IsFalse(chain[i].IsFinisher, $"stage {i} is flagged as a finisher mid-chain");
            }
            Assert.IsTrue(chain[chain.Length - 1].IsFinisher, "the chain does not end in a finisher");
        }

        /// <summary>
        /// Every non-final stage needs a window, or the chain cannot be
        /// continued and the combo is dead on the first hit.
        /// </summary>
        [Test]
        public void EveryStageBeforeTheFinisherCanBeFollowedUp()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            for (int i = 0; i < chain.Length - 1; i++)
            {
                Assert.Greater(chain[i].WindowSeconds, 0.0f,
                    $"stage {i} has no follow-up window, so the chain stops there");
            }
        }

        [Test]
        public void TheFinisherHitsHardestAndClosesTheChain()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            ComboStage finisher = chain[chain.Length - 1];

            for (int i = 0; i < chain.Length - 1; i++)
            {
                Assert.Greater(finisher.DamageScale, chain[i].DamageScale - 0.001f,
                    $"stage {i} hits harder than the finisher");
            }
            Assert.AreEqual(0.0f, finisher.WindowSeconds, 0.001f,
                "the finisher has a follow-up window, so the chain never ends");
        }

        /// <summary>
        /// The finisher ragdolls instead of stunning. Hitstun on top would be a
        /// second competing reaction to the same hit -- the original set the
        /// scale to zero for exactly this reason.
        /// </summary>
        [Test]
        public void TheFinisherAppliesNoHitstunBecauseItRagdolls()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            Assert.AreEqual(0.0f, CombatTuning.HitstunFor(chain[chain.Length - 1]), 0.001f);
        }

        [Test]
        public void EveryStageDealsRealDamage()
        {
            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                Assert.Greater(CombatTuning.DamageFor(stage, 0), 0.0f,
                    $"'{stage.Animation}' does no damage at all");
            }
        }

        [Test]
        public void StrengthScalesDamageByTwoPercentPerPoint()
        {
            ComboStage first = ComboChain.For(ComboChain.Unarmed)[0];
            float atZero = CombatTuning.DamageFor(first, 0);
            float atFifty = CombatTuning.DamageFor(first, 50);

            Assert.AreEqual(CombatTuning.PunchDamage, atZero, 0.001f,
                "a Strength of zero should deal exactly the base punch damage");
            Assert.AreEqual(atZero * 2.0f, atFifty, 0.01f,
                "50 points at 2% each should double damage");
        }

        [Test]
        public void BlockingLetsAQuarterThrough()
        {
            Assert.AreEqual(0.25f, CombatTuning.BlockDamageMultiplier, 0.001f,
                "blocking should absorb 75%, not negate the hit");
        }

        [Test]
        public void AnUnknownMovesetGivesAnEmptyChainRatherThanThrowing()
        {
            Assert.IsEmpty(ComboChain.For("sword"), "sword is deliberately not ported yet");
            Assert.IsEmpty(ComboChain.For(""));
            Assert.IsFalse(ComboChain.Has("nonsense"));
        }
    }
}
