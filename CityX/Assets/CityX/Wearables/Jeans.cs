using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Denim jeans, worn on the legs: the owner's cut of a Meshy model, fitted to this body in Blender (JeansBuilder). Every colour (the Black,
    /// Blue, Bred and Maroon Jeans) is this same pair with its own texture and item.
    /// </summary>
    public sealed class Jeans : SkinnedGarment
    {
        [SerializeField] private string _itemId = ItemRegistry.BlackJeans;

        public override string ItemId => _itemId;

        /// <summary>Makes this pair the given item (each colour's builder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;
    }
}
