using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// A full-length hooded robe, and the first Unity wearable that is a
    /// single continuous skinned drape rather than a set of rigid pieces.
    /// Ported from wearables/hooded_cloak.gd. Covers the whole body in one
    /// mesh, so unlike SkelliJeans there is no rigid-piece seam to hide, but
    /// also no attempt yet to hide the body underneath the way the Godot
    /// original's set_body_stubs_only() does -- a known simplification for
    /// this first pass, accepted the same way the old t-shirt/hoodie's own
    /// shoulder seam was.
    /// </summary>
    public sealed class HoodedCloak : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.HoodedCloak;
    }
}
