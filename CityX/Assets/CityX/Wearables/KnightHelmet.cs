using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The owner's Knight Helmet (KnightxHelmet.glb, 2026-10-18): a closed steel helmet worn over the whole head. Its model is baked by
    /// KnightHelmetBuilder with the head's centre at the origin, placed the way the owner fitted it in Blender, so it sits on the Head bone's
    /// pivot (the head sphere's centre) with no offset.
    /// </summary>
    public sealed class KnightHelmet : HeadWearable
    {
        /// <summary>The child holding the mesh.</summary>
        public const string ModelName = "Model";

        protected override string ItemId => ItemRegistry.KnightHelmet;

        protected override Vector3 HeadPosition => Vector3.zero;

        /// <summary>Owner, 2026-10-18: hair pokes through it, so "make all hairstyles go bald until it is taken off".</summary>
        protected override bool HidesHair => true;
    }
}
