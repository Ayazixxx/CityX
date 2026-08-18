using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A full-length hooded robe, and the first Unity wearable that is a
    /// single continuous skinned drape rather than a set of rigid pieces.
    /// Ported from wearables/hooded_cloak.gd. Covers the whole body in one
    /// mesh, so unlike SkelliJeans there is no rigid-piece seam to hide.
    ///
    /// HIDES THE BODY UNDERNEATH, the way the Godot original's
    /// set_body_stubs_only(true) does -- that class's own comment names
    /// exactly why this is not optional for a full-coverage garment:
    /// "rigid cloth over a mesh that moves on its own will always let a
    /// limb through somewhere, and no amount of fitting fixes that." Godot
    /// swaps in a stub mesh that still shows hands, feet and a chest
    /// collar; this just switches the body's own "Skin" renderer off
    /// outright while worn, a simpler trade for the same result (no bare
    /// skin visible) at the cost of hands/feet disappearing too.
    /// </summary>
    public sealed class HoodedCloak : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.HoodedCloak;

        private GameObject _bodySkin;

        protected override void OnEquipped(GameObject player)
        {
            Transform skin = FindDeep(player.transform, "Skin");
            if (skin == null)
            {
                return;
            }
            _bodySkin = skin.gameObject;
            _bodySkin.SetActive(false);
        }

        /// <summary>
        /// A plain Transform.Find("Skin") only checks DIRECT children, and
        /// FusedBodyBuilder.BuildBody nests it two deep (Player/Body/Skin,
        /// same for the civilian/OldGuy roots) -- a shallow Find silently
        /// returned null here and never hid anything, which is why a
        /// mid-stride pose still showed bare skin through the hood and
        /// wherever a limb kicked past the cloak's own coverage. Confirmed
        /// by rendering CaptureWearables' own stride shot before this fix.
        /// </summary>
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        protected override void OnUnequipped(GameObject player)
        {
            if (_bodySkin != null)
            {
                _bodySkin.SetActive(true);
            }
            _bodySkin = null;
        }
    }
}
