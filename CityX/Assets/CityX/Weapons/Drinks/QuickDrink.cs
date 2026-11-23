using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A DRINK FROM THE HOTBAR (owner, 2026-11-23: "make it so drinks can be equipped in the hotbar ant it appears and does the drinking animation
    /// immediately"; docs/PHIZZ_CAN.md): its key puts away what is in the hand, takes one out of the bag and drinks it at once; once it is drunk the hand
    /// has back what it held (what is left of the drink, the empty bottle, goes in the bag). On the player's body; other players see the drink in the hand
    /// and the drinking as they see any (the equipment, Drinker).
    /// </summary>
    public sealed class QuickDrink : MonoBehaviour
    {
        private PlayerInputReader _input;

        private void OnEnable()
        {
            _input = GetComponent<PlayerInputReader>();
            if (_input != null)
            {
                _input.HotbarItemPressed += Pressed;
            }
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.HotbarItemPressed -= Pressed;
            }
        }

        private void Pressed(string itemId)
        {
            Use(itemId);
        }

        /// <summary>The hand has back what was put away for a drink that could not be taken out.</summary>
        private void GiveBack(Inventory inventory, string before)
        {
            if (!string.IsNullOrEmpty(before) && inventory.RemoveFromStorage(before, 1, out ItemInstanceData held))
            {
                inventory.EquipItemId(before, gameObject, held);
            }
        }

        /// <summary>Drinks one <paramref name="itemId"/> now. False if it cannot (a drink going, none in the bag or the hand, the bag full).</summary>
        public bool Use(string itemId)
        {
            var inventory = GetComponent<Inventory>();
            var drinker = GetComponent<Drinker>();
            if (inventory == null || drinker == null || drinker.Drinking || !Hotbar.IsBarItem(itemId))
            {
                return false;
            }
            string slot = inventory.ActiveWeaponSlot;
            if (inventory.EquippedItemId(slot) == itemId)
            {
                HeldDrink inHand = inventory.EquippedInstance(slot) != null ? inventory.EquippedInstance(slot).GetComponent<HeldDrink>() : null;
                return inHand != null && inHand.Drink();
            }
            if (inventory.CountInStorage(itemId) <= 0)
            {
                return false;
            }
            // What is in the hand goes in the bag first: a weapon is drawn into the hand only when the hand is empty (it goes to the first free weapon slot).
            string before = inventory.EquippedItemId(slot);
            if (!string.IsNullOrEmpty(before) && !inventory.UnequipToSlot(slot, gameObject))
            {
                return false;
            }
            if (!inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData taken))
            {
                GiveBack(inventory, before);
                return false;
            }
            if (!inventory.EquipItemId(itemId, gameObject, taken))
            {
                inventory.AddItem(itemId, 1, taken);
                GiveBack(inventory, before);
                return false;
            }
            HeldDrink drink = inventory.EquippedInstance(slot) != null ? inventory.EquippedInstance(slot).GetComponent<HeldDrink>() : null;
            if (drink == null)
            {
                return false;
            }
            drink.Quick = true;
            drink.HandBack = before;
            return drink.Drink();
        }
    }
}
