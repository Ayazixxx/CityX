using CityX.Items;
using NUnit.Framework;

namespace CityX.Tests
{
    public sealed class ItemRegistryTests
    {
        [Test]
        public void GlassBottleFillsTheRightHandSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.GlassBottle, out ItemDefinition definition));
            Assert.AreEqual("hand_r", definition.EquipSlot);
            Assert.AreEqual(1, definition.StackSize);
        }

        [Test]
        public void SkelliJeansFillsTheLegsSlotAndHoodedCloakFillsTheTopSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.SkelliJeans, out ItemDefinition jeans));
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.HoodedCloak, out ItemDefinition cloak));
            Assert.AreEqual("legs", jeans.EquipSlot);
            Assert.AreEqual("top", cloak.EquipSlot);
        }

        [Test]
        public void TopHatAndFarmerHatShareTheHelmetSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.TopHat, out ItemDefinition topHat));
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.FarmerHat, out ItemDefinition farmerHat));
            Assert.AreEqual("helmet", topHat.EquipSlot);
            Assert.AreEqual("helmet", farmerHat.EquipSlot);
        }

        [Test]
        public void AnUnknownIdIsReportedRatherThanThrown()
        {
            Assert.IsFalse(ItemRegistry.Has("does_not_exist"));
            Assert.IsFalse(ItemRegistry.TryGet("does_not_exist", out _));
        }
    }
}
