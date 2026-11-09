using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The Claw Skull (owner, 2026-11-08: "make it an item ingame "Claw Skull" and wearable as a helmet"): the Crimson Claw's own head, cut from
    /// its claws, with the back pulled in to the wearer's head. Its model is baked by ClawSkullBuilder with the head's centre at the origin,
    /// placed the way the owner fitted it in Blender; the worn pose below is the owner's last adjustment, made on the head in Unity.
    /// </summary>
    public sealed class ClawSkull : HeadWearable
    {
        /// <summary>The child holding the mesh.</summary>
        public const string ModelName = "Model";

        /// <summary>The owner's fit on the Head bone, read off the Inspector while wearing it (2026-11-09).</summary>
        private static readonly Vector3 FittedPosition = new Vector3(0.0f, 0.00169f, 0.0f);

        private static readonly Vector3 FittedRotationEuler = new Vector3(13.26f, 0.0f, 0.0f);

        protected override string ItemId => ItemRegistry.ClawSkull;

        protected override Vector3 HeadPosition => FittedPosition;

        protected override Vector3 HeadRotationEuler => FittedRotationEuler;

        /// <summary>Hair would come out through its spiked fringe; bald under it, as under the Knight Helmet.</summary>
        protected override bool HidesHair => true;
    }
}
