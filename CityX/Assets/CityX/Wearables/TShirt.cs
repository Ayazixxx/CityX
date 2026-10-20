using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// A plain short-sleeved tee, worn as a "top": Meshy's shirt cut off its mannequin and fitted to this body in Blender (TShirtBuilder), with a
    /// flat front and back so a design drawn on SourceArt/TShirt/tshirt_uv_template.png prints cleanly on it.
    /// </summary>
    public sealed class TShirt : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.TShirt;
    }
}
