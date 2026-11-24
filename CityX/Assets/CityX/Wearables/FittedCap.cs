using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A fitted cap (docs/FITTED_CAP.md; owner, 2026-11-24: "make a basic fitted cap model we can make a bunch of colorways for ... make it fitted
    /// to our playermodel's round head"), one prefab a colourway (Items.FittedCapColourways). Its model is built by FittedCapBuilder about the
    /// head's centre, the Head bone's pivot, so it sits there with no offset. The hair under it is hidden, as under the Farmer Hat: every style
    /// but the shortest comes out through a crown that hugs the head.
    /// </summary>
    public sealed class FittedCap : HeadWearable
    {
        /// <summary>The child holding the mesh.</summary>
        public const string ModelName = "Model";

        [SerializeField] private string _itemId = Items.FittedCapColourways.BlackItemId;

        /// <summary>Makes this the colourway <paramref name="itemId"/> (FittedCapBuilder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;

        protected override string ItemId => _itemId;

        protected override Vector3 HeadPosition => Vector3.zero;

        protected override bool HidesHair => true;
    }
}
