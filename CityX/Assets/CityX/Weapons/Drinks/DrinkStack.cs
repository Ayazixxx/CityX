using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// DRINKS STACK WITHOUT END (owner, 2026-11-23: "they stack infinitely"): one is in the hand, the rest in a bag slot. A hand holds one item, so a stack in
    /// the hand is held the way grenades are (GrenadeItem): when the one in the hand is gone (drunk, thrown), the next is taken out of the bag into the hand.
    /// What is left of a drink (the PHIZZ bottle drunk empty, a weapon again: owner) stays in the hand if it was the last, and goes in the bag if there are
    /// more to drink. A drink taken from the hotbar (QuickDrink) gives the hand back what it held.
    /// </summary>
    public static class DrinkStack
    {
        /// <summary>
        /// <paramref name="drink"/> (in <paramref name="holder"/>'s hand) is gone; <paramref name="leftover"/> is what is left of it (an item id), or null.
        /// The next of the same drink comes out of the bag into the hand if there is one.
        /// </summary>
        public static void Drunk(HeldDrink drink, GameObject holder, string leftover)
        {
            var inventory = holder != null ? holder.GetComponent<Inventory>() : null;
            string itemId = drink.DrinkItemId;
            drink.OnUnequip(holder);
            if (inventory == null)
            {
                return;
            }
            string slot = inventory.ActiveWeaponSlot;
            inventory.ClearEquipSlotWithoutReturning(slot);
            if (drink.Quick)
            {
                // From the hotbar: what is left goes in the bag and the hand has back what it held.
                if (!string.IsNullOrEmpty(leftover) && !inventory.AddItem(leftover, 1))
                {
                    DroppedLeftover(holder, leftover);
                }
                if (!string.IsNullOrEmpty(drink.HandBack) && inventory.RemoveFromStorage(drink.HandBack, 1, out Items.ItemInstanceData held))
                {
                    inventory.EquipItemId(drink.HandBack, holder, held);
                }
                return;
            }
            if (inventory.RemoveFromStorage(itemId, 1, out Items.ItemInstanceData next))
            {
                inventory.EquipItemId(itemId, holder, next);
                if (!string.IsNullOrEmpty(leftover) && !inventory.AddItem(leftover, 1))
                {
                    DroppedLeftover(holder, leftover);
                }
            }
            else if (!string.IsNullOrEmpty(leftover))
            {
                inventory.EquipItemId(leftover, holder);
            }
        }

        /// <summary>No room in the bag for what is left: it is set down at the drinker's feet.</summary>
        private static void DroppedLeftover(GameObject holder, string leftover)
        {
            var inventory = holder.GetComponent<Inventory>();
            GameObject prefab = inventory != null ? inventory.PrefabForItem(leftover) : null;
            if (prefab != null)
            {
                Object.Instantiate(prefab, holder.transform.position + holder.transform.forward * 0.6f + Vector3.up * 0.3f, Quaternion.identity);
            }
        }
    }
}
