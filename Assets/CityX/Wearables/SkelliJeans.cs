using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// Belted black jeans, and the first Unity wearable to occupy the LEGS
    /// slot. Ported from wearables/skelli_jeans.gd. Unlike the cloak, the
    /// body underneath stays -- trousers only have to clear the legs, which
    /// tools/meshwork/build_skelli_jeans.py's own clearance pass already
    /// baked into the source mesh, so a top can still be worn at the same
    /// time.
    /// </summary>
    public sealed class SkelliJeans : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.SkelliJeans;
    }
}
