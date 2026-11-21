using System;
using CityX.Items;

namespace CityX.Character
{
    /// <summary>
    /// A SHIRT UNDER A COAT (owner, 2026-11-21: "make it so i can wear a shirt underneath the parka. make it so they both go into the top slot in
    /// inventory and can stack"). The top slot has a second place, under the coat, that takes a shirt while a coat is worn
    /// (<see cref="EquipSlots.TorsoUnder"/>), as a weapon slot has one for a second pistol (PistolPairs). Pure: the inventory asks it with what is
    /// in each place.
    /// </summary>
    public static class TopLayers
    {
        public const string Top = EquipSlots.Torso;
        public const string Under = EquipSlots.TorsoUnder;

        /// <summary>
        /// Where a top being put on goes: a coat in the top place (in place of a coat there); a shirt under the coat worn (in place of the shirt
        /// there), or in the top place with no coat on. <paramref name="shirtGoesUnder"/>: a coat going on over the shirt worn, which moves to the
        /// place under it first.
        /// </summary>
        public static string SlotFor(string itemId, Func<string, string> equipped, out bool shirtGoesUnder)
        {
            string top = equipped(Top);
            if (ItemRegistry.IsCoat(itemId))
            {
                shirtGoesUnder = !string.IsNullOrEmpty(top) && !ItemRegistry.IsCoat(top) && string.IsNullOrEmpty(equipped(Under));
                return Top;
            }
            shirtGoesUnder = false;
            return ItemRegistry.IsCoat(top) ? Under : Top;
        }

        /// <summary>What putting <paramref name="itemId"/> on takes off (empty: nothing): the top of its own kind.</summary>
        public static string Replaces(string itemId, Func<string, string> equipped)
        {
            string slot = SlotFor(itemId, equipped, out bool shirtGoesUnder);
            return shirtGoesUnder ? string.Empty : equipped(slot);
        }
    }
}
