using CityX.Character;
using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// THE ROUND SHIELD (owner, 2026-10-27: "add a basic round, 1 handed shield il do a better model later just do something that functions for
    /// testing now"; docs/SHIELD.md): a plain wooden disc with an iron rim and boss (RoundShieldBuilder).
    /// </summary>
    public sealed class RoundShield : HeldShield
    {
        protected override string ItemId => ItemRegistry.RoundShield;

        protected override ShieldTraits Traits => ShieldTraits.Round;
    }
}
