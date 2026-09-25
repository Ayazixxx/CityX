using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// A torso-only coat, cut from a third-party Meshy AI biped and stripped
    /// down to just the body's own trunk -- head, arms and legs removed on
    /// request, so the coat is a "top" alongside HoodedCloak, and (like
    /// SkelliJeans) the body underneath keeps showing wherever the coat
    /// itself does not reach.
    /// </summary>
    public sealed class MidnightPatchworkCoat : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.MidnightPatchworkCoat;
    }
}
