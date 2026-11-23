using System.Collections.Generic;
using CityX.Items;

namespace CityX.Weapons
{
    /// <summary>
    /// WHAT EACH WEAPON'S HITS ARE (owner, 2026-11-21: "give every weapons we have so far its type viewable in the inspect menu"), as the first
    /// draft in docs/GAME_DESIGN.md has them ("Every weapon's types"): one type per attack, set by the move (the knife's clicks Slash, its heavy
    /// stab Pierce); a few hybrids split every hit in half. Ranged names are the owner's placeholders (Bullet, Bolt). Only shown on the item's
    /// card for now: no hit yet does anything by its type. Change a line here and in the design doc together.
    /// </summary>
    public static class HitTypes
    {
        public const string Label = "Hits";

        /// <summary>A glass bottle (owner, 2026-11-23: "it starts as a blunt", "a broken bottle that does pierce damage").</summary>
        private const string BottleHits = "Blunt (broken: Pierce; thrown: Impact)";

        private static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            // One-handed melee
            [ItemRegistry.CombatSword] = "Slash (Stab Dash: Pierce)",
            [ItemRegistry.MetalCrowbar] = "Blunt",
            [ItemRegistry.KitchenKnife] = "Slash (heavy stab: Pierce)",
            [ItemRegistry.ButterKnife] = "Blunt (heavy stab: Pierce)",
            [GlassBottleKinds.Classic] = BottleHits,
            [GlassBottleKinds.Beer] = BottleHits,
            [GlassBottleKinds.Wine] = BottleHits,
            [GlassBottleKinds.Soda] = BottleHits,
            [ItemRegistry.Chain] = "Blunt",
            // Two-handed melee
            [ItemRegistry.SpikeSword] = "Slash",
            [ItemRegistry.BaseballBat] = "Blunt",
            [ItemRegistry.Sledgehammer] = "Blunt",
            [ItemRegistry.Spear] = "Pierce (heavy sweep: Slash)",
            [ItemRegistry.BoStaff] = "Blunt",
            // Guns and strung weapons
            [ItemRegistry.Pistol] = "Bullet",
            [ItemRegistry.AK47] = "Bullet",
            [ItemRegistry.Shotgun] = "Bullet",
            [ItemRegistry.Bow] = "Bolt",
            [ItemRegistry.Crossbow] = "Bolt",
            [ItemRegistry.Slingshot] = "Impact",
            // Thrown
            [ItemRegistry.WoodenBoomerang] = "Impact",
            [ItemRegistry.GiantBoomerang] = "Impact",
            [ItemRegistry.IronSpikedGiantBoomerang] = "Impact and Pierce, half each",
            // Explosives
            [ItemRegistry.Grenade] = "Explosive",
            [ItemRegistry.Semtex] = "Explosive",
            [ItemRegistry.RPG] = "Explosive",
            // Magic: the element is the type
            [ItemRegistry.FlameStaff] = "Fire",
            [ItemRegistry.FireballStaff] = "Fire",
            [ItemRegistry.Flamethrower] = "Fire",
            [ItemRegistry.IceShardStaff] = "Ice",
            [ItemRegistry.FrostStaff] = "Ice",
            [ItemRegistry.WaterStaff] = "Water",
            [ItemRegistry.ArcaneStaff] = "Arcane",
            [ItemRegistry.ArcaneBeamStaff] = "Arcane",
            [ItemRegistry.MagicWand] = "Magic",
            [ItemRegistry.MagicStaff] = "Magic",
            [ItemRegistry.HealingStaff] = "None (it heals)",
            [ItemRegistry.WraithStaff] = "Its Wraith's claws: Slash",
        };

        /// <summary>What <paramref name="itemId"/>'s hits are, for its card; false for an item that does no damage (the grappling hook, clothes).</summary>
        public static bool TryGet(string itemId, out string line) => Lines.TryGetValue(itemId ?? string.Empty, out line);
    }
}
