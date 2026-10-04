using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class BloodrageAbilityTests
    {
        private GameObject _player;
        private BloodrageAbility _rage;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _rage = _player.AddComponent<BloodrageAbility>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void ItChangesNothingUntilItIsStarted()
        {
            Assert.IsFalse(_rage.IsActive);
            Assert.AreEqual(1.0f, _rage.SpeedFactor);
            Assert.AreEqual(1.0f, _rage.DamageFactor);
        }

        [Test]
        public void ItRaisesSpeedAndMeleeDamageBy25PercentForSevenAndAHalfSeconds()
        {
            Assert.IsTrue(_rage.TryActivate());
            Assert.AreEqual(1.25f, _rage.SpeedFactor, 1e-5f);
            Assert.AreEqual(1.25f, _rage.DamageFactor, 1e-5f);

            _rage.Tick(7.4f);
            Assert.IsTrue(_rage.IsActive, "still raging just before the 7.5 s are up");
            Assert.AreEqual(1.25f, _rage.SpeedFactor, 1e-5f);

            _rage.Tick(0.2f);
            Assert.IsFalse(_rage.IsActive, "over after 7.5 s");
            Assert.AreEqual(1.0f, _rage.SpeedFactor);
            Assert.AreEqual(1.0f, _rage.DamageFactor);
        }

        [Test]
        public void ItCannotBeStartedAgainWhileItRuns()
        {
            Assert.IsTrue(_rage.TryActivate());
            _rage.Tick(3.0f);
            Assert.IsFalse(_rage.TryActivate(), "no restarting the clock mid-rage");
            Assert.AreEqual(BloodrageAbility.DurationSeconds - 3.0f, _rage.Remaining, 1e-4f);
            _rage.Tick(10.0f);
            Assert.IsTrue(_rage.TryActivate(), "and it can be used again afterwards");
        }

        [Test]
        public void TheAuraBuildsUpAndOutlastsTheEffectBeforeFadingAway()
        {
            _rage.TryActivate();
            _rage.Tick(BloodrageAbility.AuraFadeInSeconds + 0.05f);
            Assert.AreEqual(1.0f, _rage.AuraIntensity, 1e-4f, "full aura");

            for (int i = 0; i < 76; i++)
            {
                _rage.Tick(0.1f);   // frames, not one huge step: the effect ends partway through
            }
            Assert.IsFalse(_rage.IsActive);
            Assert.Greater(_rage.AuraIntensity, 0.0f, "but the aura is still there, fading");
            Assert.Less(_rage.AuraIntensity, 1.0f);

            for (int i = 0; i < 20; i++)
            {
                _rage.Tick(0.1f);
            }
            Assert.AreEqual(0.0f, _rage.AuraIntensity, 1e-4f, "gone");
        }

        [Test]
        public void MeleeDamageReadsTheBonus()
        {
            var melee = _player.AddComponent<MeleeAttacker>();
            Assert.AreEqual(1.0f, melee.BloodrageFactor);
            _rage.TryActivate();
            Assert.AreEqual(1.25f, melee.BloodrageFactor, 1e-5f);
        }
    }
}
