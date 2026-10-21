using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A short-sleeved tee, worn as a "top": Meshy's shirt cut off its mannequin and fitted to this body in Blender (TShirtBuilder), with a
    /// flat front and back so a design prints cleanly on it. Every printed shirt (the Blood Raven T-Shirt, ...) is this same shirt with its
    /// own texture and item.
    /// </summary>
    public sealed class TShirt : SkinnedGarment
    {
        [SerializeField] private string _itemId = ItemRegistry.TShirt;

        public override string ItemId => _itemId;

        /// <summary>Makes this shirt the given item (a printed shirt's builder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;
    }
}
