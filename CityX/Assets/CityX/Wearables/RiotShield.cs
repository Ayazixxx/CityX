using CityX.Character;
using CityX.Items;

namespace CityX.Wearables
{
    /// <summary>
    /// THE RIOT SHIELD (owner, 2026-11-18: "make a riot shield make it so you can equip 1 handed weapons with it. so you can equip shield and pistol
    /// or shield and 1 handed melee"; docs/SHIELD.md): a tall dark slab with a window (RiotShieldBuilder), under ShieldTraits.Riot's rules.
    /// </summary>
    public sealed class RiotShield : HeldShield
    {
        protected override string ItemId => ItemRegistry.RiotShield;

        protected override ShieldTraits Traits => ShieldTraits.Riot;
    }
}
