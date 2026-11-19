using System;
using CityX.Items;

namespace CityX.Character
{
    /// <summary>
    /// WHERE A WEAPON GOES, with Dual Pistols (owner, 2026-11-19: "make it so you can equip two pistols in the primary or secondary slot once dual
    /// wield has been unlocked ... so technically you could carry 4 pistols"). Each weapon slot has a second, left-hand place that takes a
    /// pistol only beside a pistol (docs/DUAL_PISTOLS.md). Pure: the inventory asks it with what is in each slot.
    /// </summary>
    public static class PistolPairs
    {
        public const string PrimaryPair = EquipSlots.PrimaryOffHand;
        public const string SecondaryPair = EquipSlots.SecondaryOffHand;

        /// <summary>The left-hand place beside a weapon slot (null for any other slot).</summary>
        public static string PairOf(string weaponSlot) =>
            weaponSlot == EquipSlots.Primary ? PrimaryPair : weaponSlot == EquipSlots.Secondary ? SecondaryPair : null;

        /// <summary>The weapon slot a left-hand place belongs to (null for any other slot).</summary>
        public static string SlotOfPair(string pairSlot) =>
            pairSlot == PrimaryPair ? EquipSlots.Primary : pairSlot == SecondaryPair ? EquipSlots.Secondary : null;

        public static bool IsPair(string slot) => slot == PrimaryPair || slot == SecondaryPair;

        /// <summary>
        /// Where a weapon being equipped goes: an empty primary; else, a pistol, beside the primary's pistol; else an empty secondary; else, a
        /// pistol, beside the secondary's; else in place of the weapon in hand (the rule before pairs). <paramref name="pairs"/> is whether the
        /// body pairs pistols now (Dual Pistols learnt, no shield on the left arm).
        /// </summary>
        public static string SlotFor(string itemId, Func<string, string> equipped, bool pairs, string activeSlot)
        {
            bool handgun = pairs && ItemRegistry.IsHandgun(itemId);
            foreach (string slot in new[] { EquipSlots.Primary, EquipSlots.Secondary })
            {
                if (string.IsNullOrEmpty(equipped(slot)))
                {
                    return slot;
                }
                if (handgun && ItemRegistry.IsHandgun(equipped(slot)) && string.IsNullOrEmpty(equipped(PairOf(slot))))
                {
                    return PairOf(slot);
                }
            }
            return activeSlot;
        }

        /// <summary>Whether a weapon may stay beside <paramref name="pairedItemId"/> once <paramref name="itemId"/> takes the slot's main place.</summary>
        public static bool KeepsPair(string itemId, string pairedItemId) =>
            string.IsNullOrEmpty(pairedItemId) || ItemRegistry.IsHandgun(itemId);
    }
}
