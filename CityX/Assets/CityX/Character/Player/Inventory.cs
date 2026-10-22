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

        // ---- primary / secondary weapons ----

        /// <summary>The primary weapon slot. (The key is the old "right hand" one: saves, the registry and the tests all know it by that name.)</summary>
        public const string PrimarySlot = EquipSlots.Primary;

        /// <summary>The secondary weapon slot (the old "left hand" key).</summary>
        public const string SecondarySlot = EquipSlots.Secondary;

        /// <summary>Which of the two weapons is in hand. Only that one exists as an object; the other is holstered (see <see cref="CycleWeapon"/>).</summary>
        public string ActiveWeaponSlot { get; private set; } = PrimarySlot;

        // What a holstered weapon remembers (its ammo, its durability), kept here while its object is gone.
        private readonly System.Collections.Generic.Dictionary<string, ItemInstanceData> _holstered =
            new System.Collections.Generic.Dictionary<string, ItemInstanceData>();

        public static bool IsWeaponSlot(string slot) => slot == PrimarySlot || slot == SecondarySlot;

        public bool IsHolstered(string slot) => _holstered.ContainsKey(slot);

        private string OtherWeaponSlot(string slot) => slot == PrimarySlot ? SecondarySlot : PrimarySlot;

        /// <summary>
        /// Puts the weapon in hand away and takes the other one out (primary to secondary and back). With the active slot empty (a thrown bottle,
        /// nothing equipped yet) it just draws whichever weapon there is. False when there is nothing to change to.
        /// A weapon is a real object only while it is in hand: holstering reads its state, runs its own OnUnequip (which puts down its hand
        /// pose, ammo UI, input hooks and so on) and remembers the state; drawing builds it again with that state, through the same OnEquip as any equip.
        /// </summary>
        public bool CycleWeapon(GameObject player)
        {
            string from = ActiveWeaponSlot;
            string to = OtherWeaponSlot(from);
            if (string.IsNullOrEmpty(EquippedItemId(to)))
            {
                return false;
            }
            Holster(from, player);
            if (!Draw(to, player))
            {
                Draw(from, player);
                return false;
            }
            NotifyEquipmentChanged(); return true;
        }

        private void Holster(string slot, GameObject player)
        {
            if (!_equippedInstances.TryGetValue(slot, out GameObject instance) || instance == null)
            {
                return;
            }
            var equippable = instance.GetComponent<IEquippableItem>();
            ItemInstanceData data = equippable != null ? equippable.GetInstanceData() : ItemInstanceData.None;
            equippable?.OnUnequip(player);
            _equippedInstances[slot] = null;
            _holstered[slot] = data;
        }

        private bool Draw(string slot, GameObject player)
        {
            string itemId = EquippedItemId(slot);
            if (string.IsNullOrEmpty(itemId) || !_holstered.TryGetValue(slot, out ItemInstanceData data))
            {
                return false;
            }
            if (!Spawn(itemId, player, data, out GameObject instance))
            {
                return false;
            }
            _equippedInstances[slot] = instance;
            _holstered.Remove(slot);
            ActiveWeaponSlot = slot;
            return true;
        }

        private bool Spawn(string itemId, GameObject player, ItemInstanceData data, out GameObject instance)
        {
            instance = null;
            GameObject prefab = PrefabFor(itemId);
            if (prefab == null)
            {
                return false;
            }
            GameObject made = UnityEngine.Object.Instantiate(prefab);
            made.transform.SetParent(player.transform, false);
            var equippable = made.GetComponent<IEquippableItem>();
            if (equippable == null)
            {
                UnityEngine.Object.Destroy(made);
                return false;
            }
            equippable.OnEquip(player, data);
            instance = made;
            return true;
        }

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
            bool weapon = IsWeaponSlot(slot);
            bool drawNow = true;
            if (weapon)
            {
                // A weapon goes to the primary slot, or the secondary if the primary is taken, or (both taken) replaces the one in hand.
                // It is drawn at once when nothing is in hand, or when it replaces the weapon in hand; otherwise it is holstered until the player scrolls to it.
                string inHand = EquippedInstance(ActiveWeaponSlot) != null ? ActiveWeaponSlot : null;
                if (string.IsNullOrEmpty(EquippedItemId(PrimarySlot))) slot = PrimarySlot;
                else if (string.IsNullOrEmpty(EquippedItemId(SecondarySlot))) slot = SecondarySlot;
                else slot = ActiveWeaponSlot;
                drawNow = inHand == null || inHand == slot;
            }

            if (PrefabFor(itemId) == null)
            {
                return false;
            }
            if (_equipment.TryGetValue(slot, out string currentItemId) && !string.IsNullOrEmpty(currentItemId))
            {
                if (!UnequipToSlot(slot, player))
                {
                    return false;
                }
            }

            if (!drawNow)
            {
                _equipment[slot] = itemId;
                _equippedInstances[slot] = null;
                _holstered[slot] = instanceData;
                NotifyEquipmentChanged(); return true;
            }

            if (!Spawn(itemId, player, instanceData, out GameObject instance))
            {
                return false;
            }
            _equipment[slot] = itemId;
            _equippedInstances[slot] = instance;
            _holstered.Remove(slot);
            if (weapon)
            {
                ActiveWeaponSlot = slot;
            }
            NotifyEquipmentChanged(); return true;
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
            if (_holstered.TryGetValue(slotKey, out ItemInstanceData holsteredData))
            {
                // Away in its holster: there is no object to tear down, only the remembered state to hand back.
                if (!AddItem(itemId, 1, holsteredData))
                {
                    return false;
                }
                _holstered.Remove(slotKey);
                _equipment[slotKey] = string.Empty;
                _equippedInstances[slotKey] = null;
                NotifyEquipmentChanged(); return true;
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
            NotifyEquipmentChanged(); return true;
        }

        /// <summary>
        /// Lets go of the weapon in hand (a body that dies drops it): gives its id, its state and where its model was in the world, puts the model
        /// away (its own OnUnequip) and empties the slot without returning it to storage. False with nothing in hand.
        /// </summary>
        public bool TakeWeaponInHand(GameObject player, out string itemId, out ItemInstanceData instanceData, out Pose pose)
        {
            string slot = ActiveWeaponSlot;
            itemId = EquippedItemId(slot);
            instanceData = ItemInstanceData.None;
            pose = default;
            GameObject instance = EquippedInstance(slot);
            if (string.IsNullOrEmpty(itemId) || instance == null)
            {
                return false;
            }
            var equippable = instance.GetComponent<IEquippableItem>();
            instanceData = equippable != null ? equippable.GetInstanceData() : ItemInstanceData.None;
            pose = new Pose(instance.transform.position, instance.transform.rotation);
            equippable?.OnUnequip(player);
            _equipment[slot] = string.Empty;
            _equippedInstances[slot] = null;
            _holstered.Remove(slot);
            NotifyEquipmentChanged();
            return true;
        }

        /// <summary>
        /// Takes a picked-up item (a tap of E), equipping it where it can (owner, 2026-10-21: "tapping E key equips it"):
        /// <list type="bullet">
        /// <item>a weapon goes straight into an empty hand, in the primary slot, else the secondary ("if you dont have any item in hand and you
        /// press E on weapon then it equips it automatically, starting with primary slot and if thats full then it goes in secondary"); with
        /// something already in hand it is stored;</item>
        /// <item>anything worn (clothes, back gear) is put on, what was worn there going into storage.</item>
        /// </list>
        /// What cannot be equipped is stored.
        /// </summary>
        public bool PickUp(string itemId, ItemInstanceData instanceData = default)
        {
            if (EquipsOnPickUp(itemId) && EquipItemId(itemId, gameObject, instanceData))
            {
                return true;
            }
            return AddItem(itemId, 1, instanceData);
        }

        /// <summary>Whether picking up <paramref name="itemId"/> now would equip it (<see cref="PickUp"/>'s rule).</summary>
        public bool EquipsOnPickUp(string itemId)
        {
            if (!ItemRegistry.TryGet(itemId, out ItemDefinition definition) || string.IsNullOrEmpty(definition.EquipSlot))
            {
                return false;
            }
            if (!IsWeaponSlot(definition.EquipSlot))
            {
                return true;
            }
            return EquippedInstance(ActiveWeaponSlot) == null &&
                   (string.IsNullOrEmpty(EquippedItemId(PrimarySlot)) || string.IsNullOrEmpty(EquippedItemId(SecondarySlot)));
        }

        /// <summary>Whether <see cref="PickUp"/> would take one <paramref name="itemId"/> now: equipped (with room for what it replaces), or stored.</summary>
        public bool CanPickUp(string itemId, ItemInstanceData instanceData = default)
        {
            if (EquipsOnPickUp(itemId) && PrefabFor(itemId) != null && ItemRegistry.TryGet(itemId, out ItemDefinition definition))
            {
                string worn = IsWeaponSlot(definition.EquipSlot) ? string.Empty : EquippedItemId(definition.EquipSlot);
                if (string.IsNullOrEmpty(worn) || HasRoomFor(worn))
                {
                    return true;
                }
            }
            return HasRoomFor(itemId, instanceData);
        }

        /// <summary>Whether one <paramref name="itemId"/> would fit in storage now: a stack of it with room, or an empty slot (AddItem's own rule).</summary>
        public bool HasRoomFor(string itemId, ItemInstanceData instanceData = default)
        {
            if (!ItemRegistry.TryGet(itemId, out ItemDefinition definition))
            {
                return false;
            }
            for (int i = 0; i < Storage.Length; i++)
            {
                if (Storage[i].IsEmpty)
                {
                    return true;
                }
                if (instanceData.IsEmpty && Storage[i].ItemId == itemId && Storage[i].InstanceData.IsEmpty && Storage[i].Count < definition.StackSize)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The prefab for an item from whichever inventory in the scene lists it (every body's lists the same): what a dropped item is shown with.</summary>
        public static GameObject FindPrefab(string itemId)
        {
            foreach (Inventory inventory in FindObjectsByType<Inventory>(FindObjectsSortMode.None))
            {
                GameObject prefab = inventory.PrefabFor(itemId);
                if (prefab != null)
                {
                    return prefab;
                }
            }
            return null;
        }

        /// <summary>
        /// Clears the slot with no return to storage -- the bottle-throw
        /// special case. The item is consumed, not unequipped.
        /// </summary>
        public void ClearEquipSlotWithoutReturning(string slotKey)
        {
            _holstered.Remove(slotKey);
            _equipment[slotKey] = string.Empty;
            _equippedInstances[slotKey] = null;
            NotifyEquipmentChanged();
        }

        /// <summary>The prefab for an item id (the same list equipping uses). Public so another player's COPY can build a model of what its owner holds.</summary>
        public GameObject PrefabForItem(string itemId) => PrefabFor(itemId);

        /// <summary>Raised whenever what is IN HAND or ON THE BACK changes (equip, unequip, holster, draw, a thrown bottle): another player's copy shows it.</summary>
        public event System.Action EquipmentChanged;

        private void NotifyEquipmentChanged()
        {
            EquipmentChanged?.Invoke();
        }

        /// <summary>The ids of every equipped item that exists as an object right now (a holstered weapon is not one).</summary>
        public System.Collections.Generic.List<string> VisibleItemIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (System.Collections.Generic.KeyValuePair<string, GameObject> pair in _equippedInstances)
            {
                if (pair.Value != null && _equipment.TryGetValue(pair.Key, out string id) && !string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        /// <summary>The ids of the clothes worn (ItemDefinition.IsWearable), by slot: what a character's save keeps on them.</summary>
        public System.Collections.Generic.List<string> WornClothingIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (System.Collections.Generic.KeyValuePair<string, string> pair in _equipment)
            {
                if (!string.IsNullOrEmpty(pair.Value) && ItemRegistry.TryGet(pair.Value, out ItemDefinition definition) && definition.IsWearable)
                {
                    ids.Add(pair.Value);
                }
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
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
