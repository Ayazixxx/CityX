using System.Collections.Generic;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BAG, FOR A CHARACTER'S SAVE (docs/SERVER_SAVING.md, step 3): everything this inventory holds, as the plain entries the server keeps
    /// (<see cref="ToBag"/>), and a saved bag put back on a body (<see cref="FromBag"/>): the storage slots as they were, the worn things on,
    /// each weapon in its own slot with its rounds, the one that was in hand drawn with its pair.
    /// </summary>
    public sealed partial class Inventory
    {
        /// <summary>Every storage slot that holds something, then every equipped slot (by its key), each with its state.</summary>
        public List<BagEntry> ToBag()
        {
            var bag = new List<BagEntry>();
            for (int i = 0; i < Storage.Length; i++)
            {
                if (!Storage[i].IsEmpty)
                {
                    bag.Add(Entry(string.Empty, i, Storage[i].ItemId, Storage[i].Count, Storage[i].InstanceData));
                }
            }
            var slots = new List<string>(_equipment.Keys);
            slots.Sort(System.StringComparer.Ordinal);
            foreach (string slot in slots)
            {
                if (!string.IsNullOrEmpty(_equipment[slot]))
                {
                    bag.Add(Entry(slot, 0, _equipment[slot], 1, StateOf(slot)));
                }
            }
            return bag;
        }

        /// <summary>
        /// Empties this body and puts <paramref name="bag"/> on it. A thing the game does not know is left out; a weapon in a slot it cannot be in,
        /// or a storage slot taken twice, goes wherever storage has room.
        /// </summary>
        public void FromBag(IList<BagEntry> bag, string weaponInHand, GameObject player)
        {
            TakeEverythingOff(player);
            if (bag != null)
            {
                foreach (BagEntry entry in bag)
                {
                    if (entry != null && ItemRegistry.TryGet(entry.ItemId, out ItemDefinition definition))
                    {
                        Put(entry, definition, player);
                    }
                }
            }
            ActiveWeaponSlot = IsWeaponSlot(weaponInHand) ? weaponInHand : PrimarySlot;
            Draw(ActiveWeaponSlot, player);
            NotifyEquipmentChanged();
        }

        private void Put(BagEntry entry, ItemDefinition definition, GameObject player)
        {
            ItemInstanceData data = DataOf(entry);
            int count = Mathf.Max(1, entry.Count);
            if (entry.InStorage)
            {
                if (entry.Index >= 0 && entry.Index < Storage.Length && Storage[entry.Index].IsEmpty)
                {
                    Storage[entry.Index] = new StorageSlot { ItemId = entry.ItemId, Count = count, InstanceData = data };
                }
                else
                {
                    AddItem(entry.ItemId, count, data);
                }
                return;
            }
            bool weapon = IsWeaponSlot(definition.EquipSlot);
            bool fits = IsWeaponSlot(entry.Slot) ? weapon
                : PistolPairs.IsPair(entry.Slot) ? ItemRegistry.IsHandgun(entry.ItemId) && ItemRegistry.IsHandgun(EquippedItemId(PistolPairs.SlotOfPair(entry.Slot)))
                : entry.Slot == definition.EquipSlot || (entry.Slot == TopLayers.Under && definition.EquipSlot == TopLayers.Top);
            if (!fits)
            {
                AddItem(entry.ItemId, 1, data);
            }
            else if (weapon)
            {
                // Into its own slot, put away: the one in hand is drawn once everything is in (FromBag), its pair with it.
                _equipment[entry.Slot] = entry.ItemId;
                _equippedInstances[entry.Slot] = null;
                _holstered[entry.Slot] = data;
            }
            else
            {
                EquipItemId(entry.ItemId, player, data);
            }
        }

        /// <summary>Everything equipped put away for good (its model gone) and storage emptied: the body before a saved bag goes on it.</summary>
        private void TakeEverythingOff(GameObject player)
        {
            foreach (GameObject instance in new List<GameObject>(_equippedInstances.Values))
            {
                if (instance != null)
                {
                    instance.GetComponent<IEquippableItem>()?.OnUnequip(player);
                }
            }
            _equipment.Clear();
            _equippedInstances.Clear();
            _holstered.Clear();
            Storage = new StorageSlot[StorageSlotCount];
        }

        /// <summary>A slot's item's state: as it was put away, or read off the model in hand.</summary>
        private ItemInstanceData StateOf(string slot)
        {
            if (_holstered.TryGetValue(slot, out ItemInstanceData data))
            {
                return data;
            }
            GameObject instance = EquippedInstance(slot);
            var equippable = instance != null ? instance.GetComponent<IEquippableItem>() : null;
            return equippable != null ? equippable.GetInstanceData() : ItemInstanceData.None;
        }

        private static BagEntry Entry(string slot, int index, string itemId, int count, ItemInstanceData data) => new BagEntry
        {
            Slot = slot,
            Index = index,
            ItemId = itemId,
            Count = count,
            HasDurability = data.HasDurability,
            Durability = data.Durability,
            HasAmmo = data.HasAmmo,
            Magazine = data.Magazine,
            Reserve = data.Reserve,
            HasContents = data.HasContents,
            Contents = data.Contents,
            DesignId = data.DesignId ?? string.Empty,
        };

        /// <summary>The state a character's worn <paramref name="itemId"/> was saved with (a pressed shirt's design), for a body that only shows them.</summary>
        public static ItemInstanceData WornData(CharacterSave character, string itemId)
        {
            if (character?.Bag != null)
            {
                foreach (BagEntry entry in character.Bag)
                {
                    if (entry != null && !entry.InStorage && entry.ItemId == itemId)
                    {
                        return DataOf(entry);
                    }
                }
            }
            return ItemInstanceData.None;
        }

        /// <summary>A saved thing's state (its rounds, cracks, litres, printed design), as the item takes it; a design id that is not one is dropped.</summary>
        public static ItemInstanceData DataOf(BagEntry entry) => new ItemInstanceData
        {
            HasDurability = entry.HasDurability,
            Durability = entry.Durability,
            HasAmmo = entry.HasAmmo,
            Magazine = entry.Magazine,
            Reserve = entry.Reserve,
            HasContents = entry.HasContents,
            Contents = entry.Contents,
            DesignId = DesignStore.IsValidId(entry.DesignId) ? entry.DesignId : null,
        };
    }
}
