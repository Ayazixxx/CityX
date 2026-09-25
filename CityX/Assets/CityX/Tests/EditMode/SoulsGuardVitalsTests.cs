using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The vitals half of the soulslike sword: stamina you can spend down to
    /// the last drop, recovery that pauses after spending, and a held guard
    /// that pays for what it stops -- and breaks when it cannot.
    /// </summary>
    public sealed class SoulsGuardVitalsTests
    {
        private GameObject _object;
        private PlayerVitals _vitals;
        private IDamageable _target;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _vitals = _object.AddComponent<PlayerVitals>();
            _vitals.Revive();
            _target = _vitals;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void CombatStaminaCanBeSpentPastZeroButNotFromZero()
        {
            Assert.IsTrue(_vitals.TrySpendCombatStamina(_vitals.MaxStamina - 5.0f, 0.5f));
            Assert.IsTrue(_vitals.TrySpendCombatStamina(40.0f, 0.5f), "refused a swing with stamina left");
            Assert.AreEqual(0.0f, _vitals.Stamina, 0.001f);
            Assert.IsFalse(_vitals.TrySpendCombatStamina(1.0f, 0.5f), "swung from empty");
        }

        [Test]
        public void EmptyingTheBarExhaustsYou()
        {
            _vitals.TrySpendCombatStamina(_vitals.MaxStamina * 2.0f, 0.5f);

            Assert.IsTrue(_vitals.IsExhausted);
        }

        [Test]
        public void SpendingPausesRecoveryForTheGivenTime()
        {
            _vitals.TrySpendCombatStamina(50.0f, 0.6f);
            float after = _vitals.Stamina;

            _vitals.TickStamina(false, 0.5f);
            Assert.AreEqual(after, _vitals.Stamina, 0.001f, "recovered during the pause");

            _vitals.TickStamina(false, 0.2f);   // pause runs out
            _vitals.TickStamina(false, 1.0f);
            Assert.Greater(_vitals.Stamina, after, "never recovered after the pause");
        }

        [Test]
        public void TheRegenOverrideReplacesTheNormalRate()
        {
            _vitals.TrySpendCombatStamina(100.0f, 0.0f);
            float low = _vitals.Stamina;
            _vitals.StaminaRegenPerSecondOverride = 45.0f;

            _vitals.TickStamina(false, 1.0f);

            Assert.AreEqual(low + 45.0f, _vitals.Stamina, 0.01f);
        }

        [Test]
        public void PlainSprintingIsUnchangedByTheNewPause()
        {
            _vitals.TickStamina(true, 1.0f);
            float sprinted = _vitals.Stamina;

            Assert.Less(sprinted, _vitals.MaxStamina);
            Assert.AreEqual(0.0f, _vitals.StaminaRegenDelayRemaining, 0.0001f);
        }

        [Test]
        public void AHeldGuardPaysStaminaForWhatItStopsAndReportsGuarded()
        {
            _vitals.BlockDrainsStamina = true;
            _vitals.StartBlock();
            float before = _vitals.Stamina;

            HitResponse response = _target.ApplyHit(30.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Guarded, response);
            Assert.AreEqual(before - 30.0f, _vitals.Stamina, 0.01f);
            Assert.IsFalse(_vitals.IsStunned, "an affordable guarded hit staggered the defender");
        }

        [Test]
        public void AHeldGuardStillReducesDamage()
        {
            _vitals.BlockDrainsStamina = true;
            _vitals.StartBlock();
            float full = _vitals.MaxHealth;

            _target.ApplyHit(40.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(full - 10.0f, _vitals.Health, 0.01f);
        }

        [Test]
        public void AHitTheGuardCannotAffordBreaksItAndStaggersYou()
        {
            _vitals.BlockDrainsStamina = true;
            _vitals.GuardBreakStunSeconds = 1.0f;
            _vitals.TrySpendCombatStamina(_vitals.MaxStamina - 10.0f, 0.0f);   // 10 left
            _vitals.StartBlock();
            int breaks = 0;
            _vitals.GuardBroken += () => breaks++;

            HitResponse response = _target.ApplyHit(40.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Landed, response);
            Assert.AreEqual(1, breaks);
            Assert.IsTrue(_vitals.IsStunned, "a broken guard did not stagger");
            Assert.IsFalse(_vitals.IsBlocking, "the guard was still up after breaking");
            Assert.AreEqual(0.0f, _vitals.Stamina, 0.001f);
            Assert.IsTrue(_vitals.IsExhausted);
        }

        [Test]
        public void TheClassicTimedBlockStillReportsBlockedAndCostsNoStamina()
        {
            _vitals.StartBlock();   // BlockDrainsStamina left off
            float before = _vitals.Stamina;

            HitResponse response = _target.ApplyHit(30.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Blocked, response);
            Assert.AreEqual(before, _vitals.Stamina, 0.001f);
        }

        [Test]
        public void EndBlockDropsTheGuardAtOnce()
        {
            _vitals.StartBlock();
            Assert.IsTrue(_vitals.IsBlocking);

            _vitals.EndBlock();

            Assert.IsFalse(_vitals.IsBlocking);
        }
    }
}
