using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// The hook Inventory calls on whatever it just instantiated (or is
    /// about to remove). OnUnequip owns destroying every GameObject the
    /// item is responsible for -- for a single-piece weapon that's just
    /// itself, for multi-piece clothing (see ClothingItem) it's several
    /// scattered pieces living on different bones.
    /// </summary>
    public interface IEquippableItem
    {
        void OnEquip(GameObject player, ItemInstanceData instanceData);
        void OnUnequip(GameObject player);
        ItemInstanceData GetInstanceData();
    }
}
