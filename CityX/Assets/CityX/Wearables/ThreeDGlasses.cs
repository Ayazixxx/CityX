using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The 3D Glasses (owner, 2026-11-19: "make a 3D Glasses model and equippable item. make it shaped like the black one but white ingame. make
    /// the blue and red part's transparent"): paper cinema glasses in white, the red lens before the left eye and the blue before the right, both
    /// seen through. Worn on the face (the mask slot), so with any hat. The model (ThreeDGlassesBuilder, made in Blender by
    /// SourceArt/ThreeDGlasses/glasses_model.py) is made round the head's centre, so it sits on the Head bone's pivot with no offset.
    /// </summary>
    public sealed class ThreeDGlasses : HeadWearable
    {
        /// <summary>The children: the white front and arms, and the two lenses.</summary>
        public const string FrameName = "Frame";

        public const string RedLensName = "LensRed";
        public const string BlueLensName = "LensBlue";

        protected override string ItemId => ItemRegistry.ThreeDGlasses;

        protected override Vector3 HeadPosition => Vector3.zero;
    }
}
