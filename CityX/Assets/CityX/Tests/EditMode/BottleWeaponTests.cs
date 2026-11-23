using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class BottleWeaponTests
    {
        private GameObject _bottleObject;
        private BottleWeapon _bottle;

        [SetUp]
        public void SetUp()
        {
            _bottleObject = new GameObject("Bottle");
            _bottleObject.AddComponent<Rigidbody>();
            _bottle = _bottleObject.AddComponent<BottleWeapon>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bottleObject);
        }

        [Test]
        public void AFreshBottleStartsIntactAndThrowable()
        {
            Assert.AreEqual(BottleWeapon.DurabilityState.Intact, _bottle.State);
            Assert.AreEqual(BottleWeapon.IntactMeleeDamage, _bottle.GetMeleeDamage());
            Assert.IsTrue(_bottle.CanThrow());
        }

        [Test]
        public void OneLandedHitOnAnIntactBottleBreaksIt()
        {
            _bottle.RegisterLandedMeleeHit();

            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State);
            Assert.AreEqual(BottleWeapon.BrokenMeleeDamage, _bottle.GetMeleeDamage());
            Assert.IsTrue(_bottle.CanThrow(), "a broken bottle can still be thrown (owner: \"make it so you can still throw it in both its broken states\")");
            Assert.AreEqual(BottleWeapon.BrokenThrowDamage, _bottle.ThrowDamage);
        }

        [Test]
        public void TheNinthLandedHitWhileBrokenStaysBroken()
        {
            _bottle.RegisterLandedMeleeHit(); // Intact -> Broken
            for (int i = 0; i < BottleWeapon.BrokenHitsToFullyBroken - 1; i++)
            {
                _bottle.RegisterLandedMeleeHit();
            }

            Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State);
        }

        [Test]
        public void TheTenthLandedHitWhileBrokenFullyBreaksIt()
        {
            _bottle.RegisterLandedMeleeHit(); // Intact -> Broken
            for (int i = 0; i < BottleWeapon.BrokenHitsToFullyBroken; i++)
            {
                _bottle.RegisterLandedMeleeHit();
            }

            Assert.AreEqual(BottleWeapon.DurabilityState.FullyBroken, _bottle.State);
            Assert.AreEqual(BottleWeapon.FullyBrokenMeleeDamage, _bottle.GetMeleeDamage());
        }

        [Test]
        public void TheNeckStillHitsAndIsThrownUntilItsTenthBlow()
        {
            _bottle.RegisterLandedMeleeHit();
            for (int i = 0; i < BottleWeapon.BrokenHitsToFullyBroken + BottleWeapon.FullyBrokenHitsToShatter - 1; i++)
            {
                _bottle.RegisterLandedMeleeHit();
            }

            Assert.AreEqual(BottleWeapon.DurabilityState.FullyBroken, _bottle.State);
            Assert.Greater(_bottle.GetMeleeDamage(), 0.0f, "the neck does nothing");
            Assert.IsTrue(_bottle.CanThrow());
        }

        [Test]
        public void ABottleIsAWeakWeaponAndThrownHitsALittleHarderThanSwung()
        {
            // Owner, 2026-11-23: "this is low level stuff"; "make throwing it do a little more damage then swinging".
            foreach ((float swung, float thrown) in new[]
            {
                (BottleWeapon.IntactMeleeDamage, BottleWeapon.IntactThrowDamage), (BottleWeapon.BrokenMeleeDamage, BottleWeapon.BrokenThrowDamage),
                (BottleWeapon.FullyBrokenMeleeDamage, BottleWeapon.FullyBrokenThrowDamage),
            })
            {
                Assert.LessOrEqual(swung, CombatTuning.PunchDamage, "a bottle hits harder than a fist");
                Assert.Greater(thrown, swung, "thrown it does no more than swung");
                Assert.Less(thrown, swung * 1.5f, "thrown it does far more than swung");
            }
        }

        [Test]
        public void OnEquipRestoresDurabilityFromInstanceData()
        {
            var instanceData = new ItemInstanceData
            {
                HasDurability = true,
                Durability = (int)BottleWeapon.DurabilityState.Broken,
            };

            var player = new GameObject("Player");
            try
            {
                _bottle.OnEquip(player, instanceData);
                Assert.AreEqual(BottleWeapon.DurabilityState.Broken, _bottle.State);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void GetInstanceDataReflectsTheCurrentState()
        {
            _bottle.RegisterLandedMeleeHit();

            ItemInstanceData data = _bottle.GetInstanceData();

            Assert.IsTrue(data.HasDurability);
            Assert.AreEqual((int)BottleWeapon.DurabilityState.Broken, data.Durability);
        }
    }
}
