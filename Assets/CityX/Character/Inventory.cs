using System;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Ported from player/inventory.gd. Fixed storage array plus an
    /// equipment map (slot key -> item id), the same shape the Godot
    /// original uses. No UI panel yet -- equip/unequip is driven by the dev
    /// console (/equip, /unequip) for this pass; the real panel is a later
    /// pass, same as it was in the Godot original's own history.
    /// </summary>
    public sealed class Inventory : MonoBehaviour
    {
        public const int StorageSlotCount = 20;

        [Serializable]
        public struct StorageSlot
        {
            public string ItemId;
            public int Count;
            public ItemInstanceData InstanceData;

            public bool IsEmpty => string.IsNullOrEmpty(ItemId);
        }

        [Serializable]
        public struct ItemPrefabEntry
        {
            public string ItemId;
            public GameObject Prefab;
        }

        public StorageSlot[] Storage = new StorageSlot[StorageSlotCount];
        public ItemPrefabEntry[] ItemPrefabs;

        private readonly System.Collections.Generic.Dictionary<string, string> _equipment =
            new System.Collections.Generic.Dictionary<string, string>();
        private readonly System.Collections.Generic.Dictionary<string, GameObject> _equippedInstances =
            new System.Collections.Generic.Dictionary<string, GameObject>();

        public string EquippedItemId(string slot) =>
            _equipment.TryGetValue(slot, out string itemId) ? itemId : string.Empty;

        public GameObject EquippedInstance(string slot) =>
            _equippedInstances.TryGetValue(slot, out GameObject instance) ? instance : null;

        public bool HasItem(string itemId)
        {
            foreach (StorageSlot slot in Storage)
            {
                if (!slot.IsEmpty && slot.ItemId == itemId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Stacks into an existing plain-item slot when possible, otherwise
        /// takes the first empty slot. Fails cleanly -- no partial state --
        /// when nothing fits.
        /// </summary>
        public bool AddItem(string itemId, int count, ItemInstanceData instanceData = default)
        {
            if (!ItemRegistry.TryGet(itemId, out ItemDefinition definition))
            {
                return false;
            }

            if (instanceData.IsEmpty)
            {
                for (int i = 0; i < Storage.Length; i++)
                {
                    if (!Storage[i].IsEmpty && Storage[i].ItemId == itemId &&
                        Storage[i].InstanceData.IsEmpty && Storage[i].Count < definition.StackSize)
                    {
                        Storage[i].Count += count;
                        return true;
                    }
                }
            }

            for (int i = 0; i < Storage.Length; i++)
            {
                if (Storage[i].IsEmpty)
                {
                    Storage[i] = new StorageSlot { ItemId = itemId, Count = count, InstanceData = instanceData };
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the first matching slot, decrements/clears it, and outputs
        /// its instance data so a caller can round-trip durability into
        /// EquipItemId. Fails cleanly when the item isn't present.
        /// </summary>
        public bool RemoveFromStorage(string itemId, int count, out ItemInstanceData instanceData)
        {
            for (int i = 0; i < Storage.Length; i++)
            {
                if (!Storage[i].IsEmpty && Storage[i].ItemId == itemId)
                {
                    instanceData = Storage[i].InstanceData;
                    Storage[i].Count -= count;
                    if (Storage[i].Count <= 0)
                    {
                        Storage[i] = default;
                    }
                    return true;
                }
            }
            instanceData = ItemInstanceData.None;
            return false;
        }

        /// <summary>
        /// Evicts whatever currently occupies the slot back to storage
        /// first -- aborting the whole call, mutating nothing, if that
        /// eviction would fail (storage full).
        /// </summary>
        public bool EquipItemId(string itemId, GameObject player, ItemInstanceData instanceData = default)
        {
            if (!ItemRegistry.TryGet(itemId, out ItemDefinition definition))
            {
                return false;
            }

            string slot = definition.EquipSlot;
            if (_equipment.TryGetValue(slot, out string currentItemId) && !string.IsNullOrEmpty(currentItemId))
            {
                if (!UnequipToSlot(slot, player))
                {
                    return false;
                }
            }

            GameObject prefab = PrefabFor(itemId);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.transform.SetParent(player.transform, false);
            var equippable = instance.GetComponent<IEquippableItem>();
            if (equippable == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            equippable.OnEquip(player, instanceData);

            _equipment[slot] = itemId;
            _equippedInstances[slot] = instance;
            return true;
        }

        /// <summary>
        /// Reads the equipped item's live instance data, returns it to
        /// storage (no-op/false if full -- the item stays equipped,
        /// unchanged), then tells it to detach/destroy itself.
        /// </summary>
        public bool UnequipToSlot(string slotKey, GameObject player)
        {
            if (!_equipment.TryGetValue(slotKey, out string itemId) || string.IsNullOrEmpty(itemId))
            {
                return false;
            }
            if (!_equippedInstances.TryGetValue(slotKey, out GameObject instance) || instance == null)
            {
                return false;
            }

            var equippable = instance.GetComponent<IEquippableItem>();
            ItemInstanceData liveData = equippable != null ? equippable.GetInstanceData() : ItemInstanceData.None;

            if (!AddItem(itemId, 1, liveData))
            {
                return false;
            }

            equippable?.OnUnequip(player);
            _equipment[slotKey] = string.Empty;
            _equippedInstances[slotKey] = null;
            return true;
        }

        /// <summary>
        /// Clears the slot with no return to storage -- the bottle-throw
        /// special case. The item is consumed, not unequipped.
        /// </summary>
        public void ClearEquipSlotWithoutReturning(string slotKey)
        {
            _equipment[slotKey] = string.Empty;
            _equippedInstances[slotKey] = null;
        }

        private GameObject PrefabFor(string itemId)
        {
            if (ItemPrefabs == null)
            {
                return null;
            }
            foreach (ItemPrefabEntry entry in ItemPrefabs)
            {
                if (entry.ItemId == itemId)
                {
                    return entry.Prefab;
                }
            }
            return null;
        }
    }
}
