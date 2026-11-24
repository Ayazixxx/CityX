using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>The rarity ladder every weapon style climbs (docs/GAME_DESIGN.md, "Items by rarity").</summary>
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        VeryRare,
        Legendary,
        Godly,
    }

    /// <summary>
    /// Which rung each item sits on: the owner's list (docs/GAME_DESIGN.md, 2026-10-24) for the items it names; the rest placed beside the
    /// listed item they are most like until the owner places them (docs/ITEM_INSPECT.md says which). An item the table does not know is common.
    /// </summary>
    public static class ItemRarity
    {
        private static readonly Dictionary<string, Rarity> Of_ = new Dictionary<string, Rarity>
        {
            // The owner's list.
            [ItemRegistry.MetalCrowbar] = Rarity.Common,
            [ItemRegistry.BaseballBat] = Rarity.Common,
            [ItemRegistry.WoodPlank] = Rarity.Common,   // the owner's: one of the lowest two-handed weapons
            [ItemRegistry.Sledgehammer] = Rarity.Common,   // not on GAME_DESIGN's list: my placement, beside the bat
            [ItemRegistry.Spear] = Rarity.Common,   // my placement too
            [ItemRegistry.BoStaff] = Rarity.Common,   // a metal pole: my placement
            [ItemRegistry.Chain] = Rarity.Rare,   // my placement: beside the Giant Boomerang, a weapon of its own kind
            [ItemRegistry.GlassBottle] = Rarity.Common,
            [ItemRegistry.TShirt] = Rarity.Common,
            [ItemRegistry.LongSleeveTShirt] = Rarity.Common,
            [ItemRegistry.Slingshot] = Rarity.Common,
            [ItemRegistry.MagicWand] = Rarity.Common,
            [ItemRegistry.ButterKnife] = Rarity.Common,
            [ItemRegistry.KitchenKnife] = Rarity.Common,
            [ItemRegistry.Machete] = Rarity.Common,   // my placement: a farm tool, beside the kitchen knife
            [ItemRegistry.CombatSword] = Rarity.Uncommon,
            [ItemRegistry.Pistol] = Rarity.Uncommon,
            [ItemRegistry.Bow] = Rarity.Uncommon,
            [ItemRegistry.MagicStaff] = Rarity.Uncommon,
            [ItemRegistry.KnightHelmet] = Rarity.Rare,
            [ItemRegistry.AK47] = Rarity.Rare,
            [ItemRegistry.Shotgun] = Rarity.Rare,
            [ItemRegistry.Crossbow] = Rarity.Rare,
            [ItemRegistry.GrapplingHook] = Rarity.Rare,
            [ItemRegistry.FlameStaff] = Rarity.Rare,
            [ItemRegistry.FrostStaff] = Rarity.Rare,
            [ItemRegistry.ArcaneStaff] = Rarity.Rare,
            [ItemRegistry.Flamethrower] = Rarity.Rare,
            [ItemRegistry.Jetpack] = Rarity.VeryRare,
            [ItemRegistry.RPG] = Rarity.VeryRare,
            // Ammunition (2026-11-21), everywhere guns are.
            [ItemRegistry.PistolAmmo] = Rarity.Common,
            [ItemRegistry.RifleAmmo] = Rarity.Common,
            [ItemRegistry.ShotgunShells] = Rarity.Common,
            [ItemRegistry.RpgRockets] = Rarity.Uncommon,
            // Not on the list yet: placed beside what they are most like.
            [ItemRegistry.BloodRavenTShirt] = Rarity.Common,
            [ItemRegistry.RedCamoTShirt] = Rarity.Common,
            [ItemRegistry.BlackTShirt] = Rarity.Common,
            [ItemRegistry.CityXShirt] = Rarity.Common,
            [ItemRegistry.RedStripedTShirt] = Rarity.Common,
            [ItemRegistry.BlackLongSleeveTShirt] = Rarity.Common,
            [ItemRegistry.BlackJeans] = Rarity.Common,
            [ItemRegistry.BlueJeans] = Rarity.Common,
            [ItemRegistry.BredJeans] = Rarity.Common,
            [ItemRegistry.MaroonJeans] = Rarity.Common,
            [ItemRegistry.Khakis] = Rarity.Common,
            [ItemRegistry.GreyJeans] = Rarity.Common,
            [ItemRegistry.WhiteSneaker] = Rarity.Common,
            [ItemRegistry.BWSneakers] = Rarity.Common,
            [ItemRegistry.BlackSneakers] = Rarity.Common,   // as the other two colours
            [ItemRegistry.WornBoots] = Rarity.Common,   // a boot a homeless person would wear (the owner's words): my placement
            [ItemRegistry.FarmerHat] = Rarity.Common,
            [ItemRegistry.ThreeDGlasses] = Rarity.Common,
            [ItemRegistry.RoundShield] = Rarity.Common,
            [ItemRegistry.RiotShield] = Rarity.Uncommon,
            [ItemRegistry.SprayCan] = Rarity.Common,
            [ItemRegistry.PhizzCan] = Rarity.Uncommon,
            [ItemRegistry.PhizzBottle] = Rarity.Uncommon,
            [ItemRegistry.Flashlight] = Rarity.Common,
            [ItemRegistry.JerryCan] = Rarity.Common,
            [ItemRegistry.HoodedCloak] = Rarity.Uncommon,
            [ItemRegistry.SpikeSword] = Rarity.Uncommon,
            [ItemRegistry.Parachute] = Rarity.Uncommon,
            [ItemRegistry.Grenade] = Rarity.Uncommon,
            [ItemRegistry.Semtex] = Rarity.Uncommon,
            [ItemRegistry.WoodenBoomerang] = Rarity.Uncommon,
            [ItemRegistry.GiantBoomerang] = Rarity.Rare,
            [ItemRegistry.MidnightCoat] = Rarity.Rare,
            [ItemRegistry.GreenFurHoodedParka] = Rarity.Rare,   // the owner's: "make it blue rarity"
            [ItemRegistry.MaroonFurHoodedParka] = Rarity.Rare,   // the owner's: "same blue rarity level"
            [ItemRegistry.BrownCoat] = Rarity.Uncommon,   // my placement: a plain cloth coat, under the parkas
            [ItemRegistry.FireballStaff] = Rarity.Rare,
            [ItemRegistry.IceShardStaff] = Rarity.Rare,
            [ItemRegistry.ArcaneBeamStaff] = Rarity.Rare,
            [ItemRegistry.HealingStaff] = Rarity.Rare,
            [ItemRegistry.WaterStaff] = Rarity.Rare,
            [ItemRegistry.AngelWings] = Rarity.VeryRare,
            [ItemRegistry.ClawSkull] = Rarity.VeryRare,   // my placement: it is meant as a lucky drop from the Crimson Claw
            [ItemRegistry.WraithStaff] = Rarity.VeryRare,   // one wild Wraith in 33 leaves one (my placement: not on the owner's list)
            [ItemRegistry.DarkAngelWings] = Rarity.VeryRare,
            [ItemRegistry.IronSpikedGiantBoomerang] = Rarity.VeryRare,   // the owner's: "make this a very rare item"
        };

        public static Rarity Of(string itemId) => itemId != null && Of_.TryGetValue(itemId, out Rarity rarity) ? rarity : Rarity.Common;

        /// <summary>The rung's name as the card prints it.</summary>
        public static string Word(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return "Uncommon";
                case Rarity.Rare: return "Rare";
                case Rarity.VeryRare: return "Very Rare";
                case Rarity.Legendary: return "Legendary";
                case Rarity.Godly: return "Godly";
                default: return "Common";
            }
        }
    }
}
