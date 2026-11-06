using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE STATS (owner, 2026-10-14: "I want to add all of these stats to the stats page ... and make them actually effect the character as the
    /// stat describes"): the list, what a point is worth, the save, and what each stat does to the body's own numbers -- health, stamina and mana
    /// and their regeneration, armour and magic resistance against the right blows, armour penetration, block chance, stability.
    /// </summary>
    public sealed class StatsTests
    {
        private GameObject _holder;

        [TearDown]
        public void TearDown()
        {
            if (_holder != null) Object.DestroyImmediate(_holder);
        }

        private PlayerVitals MakeVitals()
        {
            _holder = new GameObject("Vitals");
            PlayerVitals vitals = _holder.AddComponent<PlayerVitals>();
            vitals.Revive();
            return vitals;
        }

        [Test]
        public void EveryStatTheOwnerListedIsThereInTheirGroupsAndDodgeEfficiencyIsNot()
        {
            string[] names =
            {
                "Max Health", "Health Regen", "Max Stamina", "Stamina Regen", "Max Mana", "Mana Regen",
                "Melee Damage", "Melee Speed", "Firearm Damage", "Reload Speed", "Fire Rate", "Spell Power", "Cast Speed", "Cooldown Reduction",
                "Armor Penetration", "Accuracy", "Recoil Control", "Movement Speed", "Flight Speed", "Vehicle Handling",
                "Armor", "Magic Resistance", "Stability", "Block Chance",
            };
            CollectionAssert.AreEqual(names, StatCatalog.All.Select(s => s.Name).ToArray());
            Assert.IsFalse(StatCatalog.All.Any(s => s.Name.Contains("Dodge")), "the owner took Dodge Efficiency out");
            Assert.AreEqual(StatCatalog.Count, StatCatalog.All.Select(s => s.Key).Distinct().Count(), "two stats share a save key");
            Assert.AreEqual(StatGroup.Vitals, StatCatalog.Get(StatId.ManaRegen).Group);
            Assert.AreEqual(StatGroup.Offense, StatCatalog.Get(StatId.ArmorPenetration).Group);
            Assert.AreEqual(StatGroup.Precision, StatCatalog.Get(StatId.RecoilControl).Group);
            Assert.AreEqual(StatGroup.Mobility, StatCatalog.Get(StatId.VehicleHandling).Group);
            Assert.AreEqual(StatGroup.Defense, StatCatalog.Get(StatId.BlockChance).Group);
            Assert.IsTrue(StatCatalog.Get(StatId.FlightSpeed).InGame, "flight speed works since the angel wings (2026-10-28)");
            Assert.IsFalse(StatCatalog.Get(StatId.VehicleHandling).InGame, "handling waits for the new cars");
            Assert.AreEqual(23, StatCatalog.All.Count(s => s.InGame));
        }

        [Test]
        public void APointsWorthIsCappedWhereItMustBe()
        {
            StatDefinition speed = StatCatalog.Get(StatId.MovementSpeed);
            Assert.AreEqual(0.05f, speed.Effect(5), 1e-5f);
            Assert.AreEqual(speed.Cap, speed.Effect(1000), 1e-5f, "movement speed stops at its cap (the city has to stream in ahead of you)");
            Assert.AreEqual(0.5f, StatCatalog.Get(StatId.CooldownReduction).Effect(1000), 1e-5f, "cooldowns never shrink past half");
            Assert.AreEqual(50.0f, StatCatalog.Get(StatId.MaxHealth).Effect(5), 1e-4f);
            Assert.AreEqual("+50 health", StatCatalog.Get(StatId.MaxHealth).Describe(5));
            Assert.AreEqual("+6% melee speed", StatCatalog.Get(StatId.MeleeSpeed).Describe(4));
            Assert.AreEqual(0.5f, StatCatalog.RatingReduction(StatCatalog.RatingHalfPoint), 1e-5f, "100 armour stops half");
            Assert.Less(StatCatalog.RatingReduction(1e6f), 1.0f, "armour never stops everything");
        }

        [Test]
        public void TheOldThreeStatsAreMaxHealthMaxStaminaAndMeleeDamage()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.Vitality = 3;
            vitals.Endurance = 2;
            vitals.Strength = 4;
            Assert.AreEqual(3, vitals.Stats[StatId.MaxHealth]);
            Assert.AreEqual(2, vitals.Stats[StatId.MaxStamina]);
            Assert.AreEqual(4, vitals.Stats[StatId.MeleeDamage]);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth + 30.0f, vitals.MaxHealth, 1e-4f);
            Assert.AreEqual(PlayerVitals.BaseMaxStamina + 20.0f, vitals.MaxStamina, 1e-4f);
        }

        [Test]
        public void ASaveKeepsEveryStatAndAnOldSavesThreeComeAcross()
        {
            var sheet = new StatSheet();
            sheet[StatId.Armor] = 7;
            sheet[StatId.CastSpeed] = 2;
            var back = new StatSheet();
            back.Load(sheet.ToList());
            Assert.AreEqual(7, back[StatId.Armor]);
            Assert.AreEqual(2, back[StatId.CastSpeed]);
            Assert.AreEqual(9, back.Total);

            back.Load(new List<StatEntry> { new StatEntry("dodge-efficiency", 5), new StatEntry("armor", 1) });
            Assert.AreEqual(1, back.Total, "a stat the game no longer has is dropped");

            var old = JsonUtility.FromJson<CharacterSave>("{\"Id\":\"a\",\"Name\":\"Old\",\"Vitality\":3,\"Endurance\":1,\"Strength\":2,\"StatPoints\":4}");
            PlayerVitals vitals = MakeVitals();
            CharacterStats.Load(vitals, old);
            Assert.AreEqual(3, vitals.Stats[StatId.MaxHealth], "Vitality became Max Health");
            Assert.AreEqual(1, vitals.Stats[StatId.MaxStamina], "Endurance became Max Stamina");
            Assert.AreEqual(2, vitals.Stats[StatId.MeleeDamage], "Strength became Melee Damage");
        }

        [Test]
        public void SpendingRaisesWhatTheStatRaisesAtOnce()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.StatPoints = 7;
            Assert.AreEqual(5, vitals.SpendStatPoints(StatId.MaxHealth, 5));
            Assert.AreEqual(PlayerVitals.BaseMaxHealth + 50.0f, vitals.Health, 1e-3f, "the new health is there at once, full");
            Assert.AreEqual(2, vitals.SpendStatPoints(StatId.MaxMana, 5), "only the points there are");
            Assert.AreEqual(PlayerVitals.BaseMaxMana + 20.0f, vitals.Mana, 1e-3f);
            Assert.AreEqual(0, vitals.StatPoints);
            Assert.AreEqual(0, vitals.SpendStatPoints(StatId.Armor, 1));
        }

        [Test]
        public void HealthComesBackOnItsOwnAfterAWhileOutOfHarmAndHealthRegenSpeedsItUp()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.TakeDamage(50.0f);
            vitals.TickHealthRegen(1.0f);
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - 50.0f, vitals.Health, 1e-3f, "nothing comes back straight after a blow");
            vitals.TickHealthRegen(PlayerVitals.HealthRegenDelaySeconds);
            float before = vitals.Health;
            vitals.TickHealthRegen(2.0f);
            Assert.AreEqual(before + 2.0f * PlayerVitals.BaseHealthRegenPerSecond, vitals.Health, 1e-3f);

            vitals.Stats[StatId.HealthRegen] = 8;   // +2 a second
            before = vitals.Health;
            vitals.TickHealthRegen(2.0f);
            Assert.AreEqual(before + 2.0f * (PlayerVitals.BaseHealthRegenPerSecond + 2.0f), vitals.Health, 1e-3f);
        }

        [Test]
        public void StaminaAndManaRegenScaleTheirRecovery()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.TrySpendMana(50.0f);
            vitals.TickMana(1.0f);
            float plain = vitals.Mana;
            vitals.Stats[StatId.ManaRegen] = 10;   // +50%
            vitals.TickMana(1.0f);
            Assert.AreEqual(PlayerVitals.ManaRegenPerSecond * 1.5f, vitals.Mana - plain, 1e-3f);

            vitals.TrySpendStamina(100.0f);          // 30 left
            float a = vitals.Stamina;
            vitals.TickStamina(false, 1.0f);
            float plainStamina = vitals.Stamina - a;
            Assert.AreEqual(PlayerVitals.StaminaRegenPerSecond, plainStamina, 1e-2f);
            vitals.Stats[StatId.StaminaRegen] = 25;   // +100%
            vitals.TrySpendStamina(40.0f);
            float b = vitals.Stamina;
            vitals.TickStamina(false, 1.0f);
            Assert.AreEqual(plainStamina * 2.0f, vitals.Stamina - b, 1e-2f);
        }

        [Test]
        public void ArmourStopsBlowsAndBulletsAndMagicResistanceStopsSpells()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.Stats[StatId.Armor] = 20;            // 100 armour: half
            vitals.Stats[StatId.MagicResistance] = 30;  // 150: three fifths (under the cap, ArmourRules)

            Assert.AreEqual(10.0f, Hit(vitals, 20.0f, DamageKind.Physical, 0.0f), 1e-3f);
            Assert.AreEqual(10.0f, Hit(vitals, 20.0f, DamageKind.Bullet, 0.0f), 1e-3f);
            Assert.AreEqual(8.0f, Hit(vitals, 20.0f, DamageKind.Magic, 0.0f), 1e-3f);

            float before = vitals.Health;
            vitals.TakePoison(8.0f);
            Assert.AreEqual(3.2f, before - vitals.Health, 1e-3f, "poison is a spell's");

            // Armour penetration ignores a share of the armour: half of 100 leaves 50, which stops a third.
            Assert.AreEqual(20.0f * (1.0f - 50.0f / 150.0f), Hit(vitals, 20.0f, DamageKind.Physical, 0.5f), 1e-3f);

            before = vitals.Health;
            vitals.TakeDamage(10.0f);
            Assert.AreEqual(10.0f, before - vitals.Health, 1e-3f, "a fall is not a blow: armour does not stop it");
        }

        [Test]
        public void ABlockCanStopABlowOutrightAndPenetrationCutsTheChance()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.StartBlock();
            vitals.Roll = () => 0.09f;   // under everyone's 10%
            int deflected = 0;
            vitals.Deflected += (from, kind) => deflected++;
            Assert.AreEqual(0.0f, Hit(vitals, 20.0f, DamageKind.Bullet, 0.0f), 1e-3f, "a bullet deflected off the block");
            Assert.AreEqual(1, deflected);

            vitals.StartBlock();
            vitals.Roll = () => 0.2f;   // over 10%: an ordinary block, a quarter of the damage
            Assert.AreEqual(20.0f * CombatTuning.BlockDamageMultiplier, Hit(vitals, 20.0f, DamageKind.Physical, 0.0f), 1e-3f);

            vitals.Stats[StatId.BlockChance] = 10;   // 10% + 20%
            vitals.StartBlock();
            Assert.AreEqual(0.0f, Hit(vitals, 20.0f, DamageKind.Physical, 0.0f), 1e-3f, "30% now beats the roll of 20%");
            vitals.StartBlock();
            Assert.Greater(Hit(vitals, 20.0f, DamageKind.Physical, 0.5f), 0.0f, "half penetration halves the chance to 15%: the roll of 20% gets through");

            vitals.EndBlock();
            vitals.Roll = () => 0.0f;
            Assert.AreEqual(20.0f, Hit(vitals, 20.0f, DamageKind.Physical, 0.0f), 1e-3f, "no chance at all without a block");
        }

        [Test]
        public void StabilityShrugsOffStaggersAndDeepensPoise()
        {
            PlayerVitals vitals = MakeVitals();
            vitals.Roll = () => 0.5f;
            Hit(vitals, 5.0f, DamageKind.Physical, 0.0f, hitstun: 0.4f);
            Assert.IsTrue(vitals.IsStunned, "with no stability every blow staggers");
            vitals.TickHitstun(1.0f);

            vitals.Stats[StatId.Stability] = 20;   // 60%
            int staggered = 0;
            vitals.Staggered += () => staggered++;
            Hit(vitals, 5.0f, DamageKind.Physical, 0.0f, hitstun: 0.4f);
            Assert.IsFalse(vitals.IsStunned, "a roll of 50% under 60% stability: shrugged off");
            Assert.AreEqual(0, staggered);
            Assert.AreEqual(PlayerVitals.MaxPoise * 1.6f, vitals.PoiseCapacity, 1e-3f);

            vitals.Roll = () => 0.7f;
            Hit(vitals, 5.0f, DamageKind.Physical, 0.0f, hitstun: 0.4f);
            Assert.IsTrue(vitals.IsStunned);
            Assert.AreEqual(1, staggered, "a stagger is announced, so a cast can be cut short");
        }

        [Test]
        public void TheDamageStatsAreTheTwoPercentAPointEveryWeaponAlreadyUses()
        {
            // The projectiles, the gun and the blades scale by "points at 2% a point" (CombatTuning.DamageFor): the stats must agree.
            foreach (StatId id in new[] { StatId.MeleeDamage, StatId.SpellPower, StatId.FirearmDamage })
            {
                Assert.AreEqual(CombatTuning.StrengthDamagePerPoint, StatCatalog.Get(id).PerPoint, 1e-6f, id.ToString());
                Assert.AreEqual(CombatTuning.DamageFor(10.0f, 7), 10.0f * new StatSheet { [id] = 7 }.Increase(id), 1e-4f, id.ToString());
            }
        }

        [Test]
        public void ASpeedOrCooldownStatOnABodyWithoutVitalsChangesNothing()
        {
            Assert.AreEqual(1.0f, PlayerVitals.SpeedOf(null, StatId.MeleeSpeed));
            Assert.AreEqual(1.0f, PlayerVitals.CooldownFactorOf(null));
            PlayerVitals vitals = MakeVitals();
            vitals.Stats[StatId.CastSpeed] = 10;
            vitals.Stats[StatId.CooldownReduction] = 30;
            Assert.AreEqual(1.15f, PlayerVitals.SpeedOf(vitals, StatId.CastSpeed), 1e-5f);
            Assert.AreEqual(0.7f, PlayerVitals.CooldownFactorOf(vitals), 1e-5f);
        }

        [Test]
        public void AStandingFirstShotIsExactAndRunningJumpingAndSprayingOpenTheConeThatAccuracyNarrows()
        {
            const float sprint = 7.5f;
            Assert.AreEqual(0.0f, CityX.Weapons.PistolWeapon.SpreadDegrees(0.0f, sprint, true, 0, false, 0.0f), "standing, first shot: exact (CS:GO)");
            Assert.AreEqual(0.0f, CityX.Weapons.PistolWeapon.SpreadDegrees(0.8f, sprint, true, 0, false, 0.0f), "creeping counts as still");
            Assert.AreEqual(CityX.Weapons.PistolWeapon.RunningSpreadDegrees,
                CityX.Weapons.PistolWeapon.SpreadDegrees(sprint, sprint, true, 0, false, 0.0f), 1e-4f, "a full sprint");
            Assert.AreEqual(CityX.Weapons.PistolWeapon.AirborneSpreadDegrees,
                CityX.Weapons.PistolWeapon.SpreadDegrees(0.0f, sprint, false, 0, false, 0.0f), 1e-4f, "in the air");
            Assert.AreEqual(2.0f * CityX.Weapons.PistolWeapon.SpraySpreadPerShotDegrees,
                CityX.Weapons.PistolWeapon.SpreadDegrees(0.0f, sprint, true, 2, false, 0.0f), 1e-4f, "the third shot of a held trigger");
            float running = CityX.Weapons.PistolWeapon.SpreadDegrees(sprint, sprint, true, 0, false, 0.0f);
            Assert.AreEqual(running * CityX.Weapons.PistolWeapon.AimedSpreadFraction,
                CityX.Weapons.PistolWeapon.SpreadDegrees(sprint, sprint, true, 0, true, 0.0f), 1e-4f, "aiming halves it");
            Assert.AreEqual(running * 0.4f, CityX.Weapons.PistolWeapon.SpreadDegrees(sprint, sprint, true, 0, false, 0.6f), 1e-4f,
                "Accuracy 60% takes 60% off");
        }

        private static float Hit(PlayerVitals vitals, float amount, DamageKind kind, float penetration, float hitstun = 0.0f)
        {
            float before = vitals.Health;
            DamageContext.Strike(vitals, amount, vitals.transform.position + Vector3.forward, hitstun, kind, penetration);
            return before - vitals.Health;
        }
    }
}
