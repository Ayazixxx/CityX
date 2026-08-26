using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    public sealed class SwordWeaponTests
    {
        private GameObject _swordObject;
        private SwordWeapon _sword;

        [SetUp]
        public void SetUp()
        {
            _swordObject = new GameObject("Sword");
            _sword = _swordObject.AddComponent<SwordWeapon>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_swordObject);
        }

        [Test]
        public void ASwordSwingsThroughItsOwnComboChain()
        {
            Assert.AreEqual(ComboChain.Sword, _sword.MovesetId);
            Assert.IsNotEmpty(ComboChain.For(_sword.MovesetId),
                "a sword should have a real chain, not the flat single-swing path");
        }

        [Test]
        public void ASwordHasAHeavyVariant()
        {
            Assert.IsTrue(_sword.HeavyStage.HasValue, "a held swing should have somewhere to go");
            Assert.AreEqual(CombatTuning.SwordHeavyAttack.Animation, _sword.HeavyStage.Value.Animation);
        }

        [Test]
        public void ASwordCannotBeThrown()
        {
            Assert.IsFalse(_sword.CanThrow());
        }

        [Test]
        public void ASwordDealsItsOwnBaseDamage()
        {
            Assert.AreEqual(SwordWeapon.BaseMeleeDamage, _sword.GetMeleeDamage());
        }

        [Test]
        public void EquippingWiresItIntoMeleeAttacker()
        {
            var player = new GameObject("Player");
            try
            {
                MeleeAttacker melee = player.AddComponent<MeleeAttacker>();
                _sword.OnEquip(player, ItemInstanceData.None);

                Assert.AreSame(_sword, melee.EquippedWeapon);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void UnequippingClearsItFromMeleeAttackerAndDestroysTheSword()
        {
            var player = new GameObject("Player");
            try
            {
                MeleeAttacker melee = player.AddComponent<MeleeAttacker>();
                _sword.OnEquip(player, ItemInstanceData.None);

                // OnUnequip calls Object.Destroy on itself, matching every
                // other weapon's own convention -- correct at runtime, but
                // Unity logs an error (not thrown) when that specific call
                // happens outside Play mode, which is exactly where this
                // EditMode test runs. Expected and acknowledged rather than
                // avoided, so the real production OnUnequip still gets
                // exercised instead of a stand-in.
                LogAssert.Expect(LogType.Error,
                    "Destroy may not be called from edit mode! Use DestroyImmediate instead.\n" +
                    "Destroying an object in edit mode destroys it permanently.");
                _sword.OnUnequip(player);

                Assert.IsNull(melee.EquippedWeapon);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>
        /// Registering a landed hit is a legitimate no-op -- a sword does not
        /// degrade the way a bottle does. Pinned so a future durability
        /// feature has to change this test on purpose rather than by
        /// accident.
        /// </summary>
        [Test]
        public void LandingHitsDoesNotChangeTheSwordsOwnDamage()
        {
            float before = _sword.GetMeleeDamage();
            _sword.RegisterLandedMeleeHit();
            _sword.RegisterLandedMeleeHit();

            Assert.AreEqual(before, _sword.GetMeleeDamage());
        }
    }
}
