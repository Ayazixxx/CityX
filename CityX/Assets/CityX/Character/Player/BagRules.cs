using System;
using System.Collections.Generic;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SERVER'S CHECK ON A FAR PLAYER'S BAG (docs/SERVER_SAVING.md, step 3b). What a player on someone else's server reports carrying is
    /// kept only as far as it can be:
    /// <list type="bullet">
    /// <item>things the game has, a stack no bigger than the item stacks, each storage slot and each equipped slot used once, each thing in a
    /// slot it can be in (a weapon in a weapon slot, a pistol beside a pistol, clothes where they are worn), and a state that can be (rounds,
    /// cracks, litres no more than any item holds);</item>
    /// <item>of each item, no more than the bag the server kept held and what the server has handed the player since (a dropped item picked up,
    /// a fallen enemy's gear: ItemsHanded). A thing from nowhere is taken out, and a stack grown from nowhere cut back.</item>
    /// </list>
    /// Fewer things is always fine: things are thrown, broken, dropped and used up.
    /// </summary>
    public sealed class BagRules : IBagRules
    {
        /// <summary>Installs the rules on every server this copy of the game runs, before any scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            CharacterService.BagRules = new BagRules();
        }

        public List<BagEntry> Keep(List<BagEntry> stored, List<BagEntry> reported, Func<string, int> handed, List<string> takenOut)
        {
            var had = new Dictionary<string, int>();
            foreach (BagEntry entry in stored)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.ItemId))
                {
                    Add(had, entry.ItemId, Mathf.Max(1, entry.Count));
                }
            }
            var kept = new List<BagEntry>();
            var counted = new Dictionary<string, int>();
            var storageUsed = new HashSet<int>();
            var slotsUsed = new HashSet<string>();
            foreach (BagEntry entry in reported)
            {
                string problem = Problem(entry, reported, storageUsed, slotsUsed);
                if (problem != null)
                {
                    takenOut?.Add($"{entry?.ItemId} ({problem})");
                    continue;
                }
                int may = Count(had, entry.ItemId) + handed(entry.ItemId) - Count(counted, entry.ItemId);
                if (may <= 0)
                {
                    takenOut?.Add($"{entry.ItemId} (never got)");
                    continue;
                }
                BagEntry keep = entry;
                if (entry.Count > may)
                {
                    takenOut?.Add($"{entry.ItemId} x{entry.Count - may} (never got)");
                    keep = JsonUtility.FromJson<BagEntry>(JsonUtility.ToJson(entry));
                    keep.Count = may;
                }
                Add(counted, keep.ItemId, keep.Count);
                if (keep.InStorage)
                {
                    storageUsed.Add(keep.Index);
                }
                else
                {
                    slotsUsed.Add(keep.Slot);
                }
                kept.Add(keep);
            }
            return kept;
        }

        /// <summary>Why the entry cannot be carried as it is said to be (null: it can).</summary>
        private static string Problem(BagEntry entry, List<BagEntry> bag, HashSet<int> storageUsed, HashSet<string> slotsUsed)
        {
            if (entry == null || !ItemRegistry.TryGet(entry.ItemId, out ItemDefinition definition))
            {
                return "not a thing the game has";
            }
            if (entry.Count < 1 || entry.Count > Mathf.Max(1, definition.StackSize) || (!entry.InStorage && entry.Count != 1))
            {
                return $"{entry.Count} in one place";
            }
            if (entry.InStorage)
            {
                if (entry.Index < 0 || entry.Index >= Inventory.StorageSlotCount || storageUsed.Contains(entry.Index))
                {
                    return $"storage slot {entry.Index}";
                }
            }
            else if (slotsUsed.Contains(entry.Slot) || !Fits(entry, definition, bag))
            {
                return $"not in {entry.Slot}";
            }
            if (entry.HasAmmo && (entry.Magazine < 0 || entry.Reserve < 0 || entry.Magazine + entry.Reserve > DroppedItemsNetwork.MaxRounds))
            {
                return $"{entry.Magazine}/{entry.Reserve} rounds";
            }
            if ((entry.HasDurability && entry.Durability < 0) || (entry.HasContents && (entry.Contents < 0.0f || entry.Contents > DroppedItemsNetwork.MaxContents)))
            {
                return "its state";
            }
            return null;
        }

        /// <summary>The equipped slot can hold it: a weapon in a weapon slot, a pistol beside a pistol, anything else where it is worn.</summary>
        private static bool Fits(BagEntry entry, ItemDefinition definition, List<BagEntry> bag)
        {
            if (Inventory.IsWeaponSlot(entry.Slot))
            {
                return Inventory.IsWeaponSlot(definition.EquipSlot);
            }
            if (PistolPairs.IsPair(entry.Slot))
            {
                string main = PistolPairs.SlotOfPair(entry.Slot);
                BagEntry beside = bag.Find(e => e != null && e.Slot == main);
                return ItemRegistry.IsHandgun(entry.ItemId) && beside != null && ItemRegistry.IsHandgun(beside.ItemId);
            }
            return entry.Slot == definition.EquipSlot;
        }

        private static int Count(Dictionary<string, int> counts, string itemId) => counts.TryGetValue(itemId, out int n) ? n : 0;

        private static void Add(Dictionary<string, int> counts, string itemId, int count) => counts[itemId] = Count(counts, itemId) + count;
    }
}
