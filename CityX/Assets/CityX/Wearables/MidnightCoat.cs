using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// A long black shearling coat with white fur trim, worn as a "top": the owner fitted it to this body in Blender (MidnightCoatBuilder).
    /// </summary>
    public sealed class MidnightCoat : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.MidnightCoat;

        /// <summary>Its fur collar stands up round the lower head: the hair goes down inside it (owner, 2026-11-15: "the back spikes are clipping through").</summary>
        protected override bool TucksHairUnderCollar => true;
    }
}
