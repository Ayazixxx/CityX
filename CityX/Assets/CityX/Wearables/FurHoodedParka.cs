using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A long quilted parka with a fur-edged hood lying on the back, worn as a "top": Meshy's model remodelled onto this body in Blender
    /// (FurHoodedParkaBuilder, docs/FUR_HOODED_PARKA.md). Every colour is this same coat with its own texture and item (owner, 2026-11-20: "im
    /// planning on making different colorways of this same model"); the Green and the Maroon Fur Hooded Parka so far.
    /// </summary>
    public sealed class FurHoodedParka : SkinnedGarment
    {
        [SerializeField] private string _itemId = ItemRegistry.GreenFurHoodedParka;

        public override string ItemId => _itemId;

        /// <summary>Makes this coat the given item (each colour's builder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;

        /// <summary>Its fur ring stands up round the lower head, as the Midnight Coat's collar does: the hair goes down inside it.</summary>
        protected override bool TucksHairUnderCollar => true;

        /// <summary>
        /// The hair goes into the fur straight behind the head, 2.13 m up, not at the fur's tips beside the head, 2.165 m: tucked from there, the hair
        /// behind the head was drawn in above the fur and the scalp showed (owner, 2026-11-21: "there are small bits of the scalp visible on the back of
        /// the head ... you can have the hair clip into the hood slightly ... just dont have hair poking through the other side of the fur"). The fur is
        /// 13 to 16 cm thick there: the hair going into it stays inside it.
        /// </summary>
        protected override float CollarRimHalfWidth => BehindTheHead;

        /// <summary>How far either side of the middle the fur counts as straight behind the head.</summary>
        private const float BehindTheHead = 0.08f;
    }
}
