using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The Squirrel Mask (owner, 2026-11-25: "this mask for my superhero character "squirrel boy""): the furry mask of the owner's Meshy model,
    /// cut off its figure as the owner trimmed it, with a nose that stands out where Meshy's went in, dark brown with black eyes and nose
    /// (docs/SQUIRREL_MASK.md).
    /// Its model (SquirrelMaskBuilder, from SourceArt/SquirrelMask/squirrel_mask_build.py) is made round the head's centre, so it sits on the
    /// Head bone's pivot with no offset.
    /// </summary>
    public sealed class SquirrelMask : HeadWearable
    {
        /// <summary>The children: the fur, and the eyes' hollows with the nose.</summary>
        public const string FurName = "Fur";

        public const string BlackName = "Black";

        protected override string ItemId => ItemRegistry.SquirrelMask;

        protected override Vector3 HeadPosition => Vector3.zero;

        /// <summary>It covers the top and back of the head down to the nape: hair would come out through the fur.</summary>
        protected override bool HidesHair => true;
    }
}
