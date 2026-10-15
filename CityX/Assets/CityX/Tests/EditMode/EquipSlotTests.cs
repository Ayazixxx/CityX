using System.Linq;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The equipment slots (Items/EquipSlots): every item that can be worn or held has a slot to show in on both the inventory's equipped row and
    /// the loadout panel, and the inventory has the shoes, gloves and back slots the owner asked for (2026-10-15). Before, the inventory kept
    /// Godot's own keys (hat, hands, feet), which nothing is registered under: a hat or a jetpack worn never showed in it.
    /// </summary>
    public sealed class EquipSlotTests
    {
        [Test]
        public void EveryItemThatCanBeWornOrHeldHasASlotInBothPanels()
        {
            foreach (string id in ItemRegistry.Ids)
            {
                Assert.IsTrue(ItemRegistry.TryGet(id, out ItemDefinition item));
                if (string.IsNullOrEmpty(item.EquipSlot))
                {
                    continue;
                }
                CollectionAssert.Contains(InventoryPanelView.EquipSlotKeys, item.EquipSlot,
                    $"{item.DisplayName} goes in the {item.EquipSlot} slot, which the inventory does not show");
                CollectionAssert.Contains(LoadoutPanelView.SlotKeys, item.EquipSlot,
                    $"{item.DisplayName} goes in the {item.EquipSlot} slot, which the loadout does not show");
            }
        }

        [Test]
        public void TheInventoryHasShoesGlovesAndBackSlots()
        {
            string[] keys = InventoryPanelView.EquipSlotKeys;
            Assert.AreEqual(keys.Length, InventoryPanelView.SlotLabelText.Length, "a slot without a caption, or a caption without a slot");
            Assert.AreEqual("SHOES", InventoryPanelView.SlotLabelText[System.Array.IndexOf(keys, EquipSlots.Shoes)]);
            Assert.AreEqual("GLOVES", InventoryPanelView.SlotLabelText[System.Array.IndexOf(keys, EquipSlots.Gloves)]);
            Assert.AreEqual("BACK", InventoryPanelView.SlotLabelText[System.Array.IndexOf(keys, EquipSlots.Back)]);
            Assert.AreEqual(keys.Length, keys.Distinct().Count(), "a slot listed twice");
            Assert.AreEqual(LoadoutPanelView.SlotKeys.Length, LoadoutPanelView.SlotKeys.Distinct().Count(), "a loadout slot listed twice");
            foreach (string key in LoadoutPanelView.SlotKeys)
            {
                CollectionAssert.Contains(keys, key, $"the loadout has a {key} slot the inventory lacks");
            }
        }
    }
}
