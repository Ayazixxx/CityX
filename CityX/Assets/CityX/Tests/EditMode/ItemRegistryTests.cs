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
        public void TheHoodedCloakFillsTheTopSlotAndTheKnightHelmetTheHelmetSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.HoodedCloak, out ItemDefinition cloak));
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.KnightHelmet, out ItemDefinition knightHelmet));
            Assert.AreEqual("top", cloak.EquipSlot);
            Assert.AreEqual("helmet", knightHelmet.EquipSlot);
        }

        [Test]
        public void TheRemovedItemsAreGone()
        {
            // The skelli jeans and the top hat (owner, 2026-10-24) are to be replaced with better models.
            foreach (string id in new[] { "sword", "farmer_hat", "midnight_patchwork_coat", "black_puffer_jacket", "skelli_jeans", "top_hat" })
            {
                Assert.IsFalse(ItemRegistry.Has(id), $"{id} is still an item");
            }
        }

        [Test]
        public void AnUnknownIdIsReportedRatherThanThrown()
        {
            Assert.IsFalse(ItemRegistry.Has("does_not_exist"));
            Assert.IsFalse(ItemRegistry.TryGet("does_not_exist", out _));
        }
    }
}
