using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// An item that can also be held in the LEFT hand, as the second of a pair (a pistol, with Dual Pistols learnt: docs/DUAL_PISTOLS.md).
    /// Inventory equips it with <see cref="OnEquipOffHand"/> instead of <see cref="IEquippableItem.OnEquip"/>; its own OnUnequip puts it away
    /// from whichever hand holds it. Another player's copy mounts it with <see cref="MountCosmeticOffHand"/>.
    /// </summary>
    public interface IOffHandItem
    {
        /// <summary>Into the wearer's left hand, the second of a pair.</summary>
        void OnEquipOffHand(GameObject player, ItemInstanceData instanceData);

        /// <summary>The model in the left hand of <paramref name="body"/> (a copy of another player), and nothing else.</summary>
        void MountCosmeticOffHand(GameObject body);
    }
}
