using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The Farmer Hat (owner, 2026-10-26): a wide straw hat. Its model is baked by FarmerHatBuilder with the head's centre at the origin, where
    /// Meshy's figure wore it scaled onto our head, so it sits on the Head bone's pivot with no offset.
    /// </summary>
    public sealed class FarmerHat : HeadWearable
    {
        /// <summary>The child holding the mesh.</summary>
        public const string ModelName = "Model";

        protected override string ItemId => ItemRegistry.FarmerHat;

        protected override Vector3 HeadPosition => Vector3.zero;

        /// <summary>
        /// Five hairstyles of eleven (the fuller ones: Danny, the anime hair, the swipe cut, HYDE, the dreads) come out through the crown and the
        /// band; bald under it, as under the Knight Helmet (owner's choice for that one, 2026-10-18).
        /// </summary>
        protected override bool HidesHair => true;
    }
}
