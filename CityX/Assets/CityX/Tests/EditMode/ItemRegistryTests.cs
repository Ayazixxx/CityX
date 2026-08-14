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
        public void TShirtAndHoodieBothFillTheTopSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.TShirt, out ItemDefinition shirt));
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.Hoodie, out ItemDefinition hoodie));
            Assert.AreEqual("top", shirt.EquipSlot);
            Assert.AreEqual("top", hoodie.EquipSlot);
        }

        [Test]
        public void AnUnknownIdIsReportedRatherThanThrown()
        {
            Assert.IsFalse(ItemRegistry.Has("does_not_exist"));
            Assert.IsFalse(ItemRegistry.TryGet("does_not_exist", out _));
        }
    }
}
