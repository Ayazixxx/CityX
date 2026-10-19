using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A wide-brimmed straw hat, worn on the head like the top hat (HeadWearable), but the mesh is cut out of a Meshy AI character export
    /// (FarmerHatBuilder): the source .glb was a WHOLE CHARACTER wearing the hat. See FarmerHatBuilder's class comment for how the hat was
    /// isolated and why the embedded texture isn't used.
    /// </summary>
    public sealed class FarmerHat : HeadWearable
    {
        /// <summary>Confirmed by a render of the cut, normalized mesh on the real Player rig: on top of the head sphere, centred, no clipping.</summary>
        private static readonly Vector3 HatOnHead = new Vector3(0.0f, 0.30f, 0.0f);

        protected override string ItemId => ItemRegistry.FarmerHat;

        protected override Vector3 HeadPosition => HatOnHead;
    }
}
