using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// An item that can be shown on ANOTHER player's copy of a body. The real item's <see cref="IEquippableItem.OnEquip"/> wires it into the
    /// wearer's input, camera, HUD, combat and ammo, none of which exists (or must be touched) for a copy; a cosmetic mount does only what can be SEEN:
    /// parent the model to the right bone, set its grip, and give the free hand its marker. The caller then switches the item's behaviours off.
    /// </summary>
    public interface ICosmeticMountable
    {
        /// <summary>Put the model on <paramref name="body"/> (a copy of another player) and nothing else.</summary>
        void MountCosmetic(GameObject body);

        /// <summary>Undo what <see cref="MountCosmetic"/> hung on the body (hand IK targets); the caller destroys the model.</summary>
        void UnmountCosmetic(GameObject body);

        /// <summary>The wearer is doing the thing this item does (a jetpack thrusting): show it (the flame). Items with nothing to show ignore it.</summary>
        void SetCosmeticActive(bool active);
    }
}
