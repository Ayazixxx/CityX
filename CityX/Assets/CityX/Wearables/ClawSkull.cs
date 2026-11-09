using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The Claw Skull (owner, 2026-11-08: "make it an item ingame "Claw Skull" and wearable as a helmet"): the Crimson Claw's own head, cut from
    /// its claws, with the back pulled in to the wearer's head. Its model is baked by ClawSkullBuilder with the head's centre at the origin,
    /// placed the way the owner fitted it in Blender, so it sits on the Head bone's pivot with no offset.
    /// </summary>
    public sealed class ClawSkull : HeadWearable
    {
        /// <summary>The child holding the mesh.</summary>
        public const string ModelName = "Model";

        protected override string ItemId => ItemRegistry.ClawSkull;

        protected override Vector3 HeadPosition => Vector3.zero;

        /// <summary>Hair would come out through its spiked fringe; bald under it, as under the Knight Helmet.</summary>
        protected override bool HidesHair => true;
    }
}
