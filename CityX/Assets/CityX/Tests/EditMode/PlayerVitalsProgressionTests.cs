using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// AddXp/SpendStatPoint, ported from player.gd's own add_xp()/
    /// spend_stat_point() -- see PlayerVitals' own doc comments for the
    /// constants and the "raise current by the same delta as the max"
    /// reasoning behind SpendStatPoint.
    /// </summary>
    public sealed class PlayerVitalsProgressionTests
    {
        private GameObject _object;
        private PlayerVitals _vitals;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _vitals = _object.AddComponent<PlayerVitals>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void SettingTheLevelGivesThePointsTheLevelsInBetweenWouldHave()
        {
            // The same as earning it: AddXp up to level 20 on another body.
            var earned = new GameObject("Earned").AddComponent<PlayerVitals>();
            try
            {
                while (earned.Level < 20) earned.AddXp(earned.ExperienceToNextLevel);
                (int stat, int skill) = _vitals.SetLevel(20);
                Assert.AreEqual(20, _vitals.Level);
                Assert.AreEqual(earned.StatPoints, _vitals.StatPoints);
                Assert.AreEqual(earned.SkillPoints, _vitals.SkillPoints);
                Assert.AreEqual(19 * PlayerVitals.StatPointsPerLevel, stat);
                Assert.AreEqual(10, skill, "levels 2, 4 ... 20 each bring a skill point");
                Assert.AreEqual(0, _vitals.Experience);
            }
            finally
            {
                Object.DestroyImmediate(earned.gameObject);
            }
        }

        [Test]
        public void SettingALowerLevelTakesBackOnlyUnspentPoints()
        {
            _vitals.SetLevel(10);
            _vitals.StatPoints = 3;   // most of them spent
            (int stat, int skill) = _vitals.SetLevel(4);
            Assert.AreEqual(4, _vitals.Level);
            Assert.AreEqual(0, _vitals.StatPoints, "never below zero");
            Assert.AreEqual(-3, stat);
            Assert.AreEqual(2, _vitals.SkillPoints, "levels 6, 8 and 10 took their skill points back");
            Assert.AreEqual(-3, skill);
            _vitals.SetLevel(0);
            Assert.AreEqual(1, _vitals.Level, "level 1 is the lowest");
        }

        [Test]
        public void ANonNegativeAmountIsIgnored()
        {
            _vitals.AddXp(0);
            _vitals.AddXp(-50);

            Assert.AreEqual(0, _vitals.Experience);
            Assert.AreEqual(1, _vitals.Level);
        }

        [Test]
        public void XpBelowTheThresholdJustAccumulates()
        {
            _vitals.AddXp(40);

            Assert.AreEqual(40, _vitals.Experience);
            Assert.AreEqual(1, _vitals.Level);
            Assert.AreEqual(0, _vitals.StatPoints);
        }

        [Test]
        public void ExactlyEnoughXpLevelsUpAndGrantsStatPoints()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);

            Assert.AreEqual(0, _vitals.Experience);
            Assert.AreEqual(2, _vitals.Level);
            Assert.AreEqual(PlayerVitals.StatPointsPerLevel, _vitals.StatPoints);
            Assert.AreEqual(PlayerVitals.XpPerLevelBase * 2, _vitals.ExperienceToNextLevel,
                "level 2 costs more than level 1 did");
        }

        [Test]
        public void ALargeGrantCanTriggerSeveralLevelUpsAtOnce()
        {
            // Level 1->2 costs 100, level 2->3 costs 200: 100 + 200 + 30 leftover.
            _vitals.AddXp(PlayerVitals.XpPerLevelBase + PlayerVitals.XpPerLevelBase * 2 + 30);

            Assert.AreEqual(3, _vitals.Level);
            Assert.AreEqual(30, _vitals.Experience);
            Assert.AreEqual(PlayerVitals.StatPointsPerLevel * 2, _vitals.StatPoints);
        }

        [Test]
        public void SpendingWithNoPointsAvailableFailsAndChangesNothing()
        {
            Assert.IsFalse(_vitals.SpendStatPoint("vitality"));
            Assert.AreEqual(0, _vitals.Vitality);
        }

        [Test]
        public void SpendingOnAnUnrecognisedStatFailsAndDoesNotConsumeThePoint()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            int pointsBefore = _vitals.StatPoints;

            Assert.IsFalse(_vitals.SpendStatPoint("mana"), "mana was not ported to this project's 3 stats");

            Assert.AreEqual(pointsBefore, _vitals.StatPoints);
        }

        [Test]
        public void SpendingOnVitalityRaisesMaxHealthAndCurrentHealthByTheSameAmount()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            float maxBefore = _vitals.MaxHealth;
            float healthBefore = _vitals.Health;

            Assert.IsTrue(_vitals.SpendStatPoint("vitality"));

            Assert.AreEqual(1, _vitals.Vitality);
            Assert.AreEqual(maxBefore + PlayerVitals.HealthPerStatPoint, _vitals.MaxHealth);
            Assert.AreEqual(healthBefore + PlayerVitals.HealthPerStatPoint, _vitals.Health,
                "spending a point should feel like an immediate boost, not something you rest to benefit from");
        }

        [Test]
        public void SpendingOnEnduranceRaisesMaxStaminaAndCurrentStaminaByTheSameAmount()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            float maxBefore = _vitals.MaxStamina;
            float staminaBefore = _vitals.Stamina;

            Assert.IsTrue(_vitals.SpendStatPoint("endurance"));

            Assert.AreEqual(1, _vitals.Endurance);
            Assert.AreEqual(maxBefore + PlayerVitals.StaminaPerStatPoint, _vitals.MaxStamina);
            Assert.AreEqual(staminaBefore + PlayerVitals.StaminaPerStatPoint, _vitals.Stamina);
        }

        [Test]
        public void SpendingNeverRaisesCurrentHealthPastTheDamageAlreadyTaken()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            _vitals.TakeDamage(50.0f);
            float healthAfterDamage = _vitals.Health;

            _vitals.SpendStatPoint("vitality");

            Assert.AreEqual(healthAfterDamage + PlayerVitals.HealthPerStatPoint, _vitals.Health,
                "the point should still only add its own fixed amount, not top the bar back up");
            Assert.Less(_vitals.Health, _vitals.MaxHealth, "damage taken before spending should still show");
        }

        [Test]
        public void SpendingOnStrengthConsumesThePointButTouchesNoHealthOrStamina()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            float maxHealthBefore = _vitals.MaxHealth;
            float maxStaminaBefore = _vitals.MaxStamina;

            Assert.IsTrue(_vitals.SpendStatPoint("strength"));

            Assert.AreEqual(1, _vitals.Strength);
            Assert.AreEqual(maxHealthBefore, _vitals.MaxHealth);
            Assert.AreEqual(maxStaminaBefore, _vitals.MaxStamina);
        }

        [Test]
        public void EachSpendDecrementsTheAvailablePool()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            int pointsAfterLevelUp = _vitals.StatPoints;

            _vitals.SpendStatPoint("vitality");

            Assert.AreEqual(pointsAfterLevelUp - 1, _vitals.StatPoints);
        }

        [Test]
        public void ManaStartsFullIsSpentOnlyWhenThereIsEnoughAndComesBack()
        {
            // The mana pool the HUD shows (2026-10-14). Spells do not cost anything yet; this is what they will draw on.
            Assert.AreEqual(_vitals.MaxMana, _vitals.Mana, 1e-4f, "a new body starts with full mana");
            Assert.IsTrue(_vitals.ManaUnlocked, "for now everyone has mana from the start");
            Assert.IsTrue(_vitals.TrySpendMana(60.0f));
            Assert.AreEqual(_vitals.MaxMana - 60.0f, _vitals.Mana, 1e-4f);
            Assert.IsFalse(_vitals.TrySpendMana(_vitals.Mana + 1.0f), "spent more mana than there was");
            Assert.AreEqual(_vitals.MaxMana - 60.0f, _vitals.Mana, 1e-4f, "a refused spell must not spend anything");
            Assert.IsFalse(_vitals.TrySpendMana(-5.0f), "a negative cost is not a way to gain mana");

            _vitals.TickMana(1.0f);
            Assert.AreEqual(_vitals.MaxMana - 60.0f + PlayerVitals.ManaRegenPerSecond, _vitals.Mana, 1e-3f, "mana did not come back over time");
            _vitals.TickMana(1000.0f);
            Assert.AreEqual(_vitals.MaxMana, _vitals.Mana, 1e-4f, "mana came back past its maximum");
        }

        [Test]
        public void ManaDoesNotComeBackWhileDeadAndIsFullAgainOnRespawn()
        {
            _vitals.TrySpendMana(80.0f);
            _vitals.TakeDamage(_vitals.MaxHealth * 2.0f, false);
            Assert.IsTrue(_vitals.IsDead, "setup: the body should be dead");
            float dead = _vitals.Mana;
            _vitals.TickMana(5.0f);
            Assert.AreEqual(dead, _vitals.Mana, 1e-4f, "a dead body's mana came back");
            _vitals.Revive();
            Assert.AreEqual(_vitals.MaxMana, _vitals.Mana, 1e-4f, "respawning did not refill mana");
        }
    }
}
