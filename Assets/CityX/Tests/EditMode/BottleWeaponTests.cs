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
            Assert.IsFalse(_bottle.CanThrow(), "a broken bottle can no longer be thrown");
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
        public void AFullyBrokenBottleIsInertAndFurtherHitsAreNoOps()
        {
            _bottle.RegisterLandedMeleeHit();
            for (int i = 0; i < BottleWeapon.BrokenHitsToFullyBroken; i++)
            {
                _bottle.RegisterLandedMeleeHit();
            }

            _bottle.RegisterLandedMeleeHit();

            Assert.AreEqual(BottleWeapon.DurabilityState.FullyBroken, _bottle.State);
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
