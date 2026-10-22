using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>
    /// Every item in the game, by id (ported from items/item_registry.gd). Pure data: prefabs are wired elsewhere (Inventory.ItemPrefabs),
    /// so this stays testable without Unity objects and is the one place each item id is spelled.
    /// </summary>
    public static class ItemRegistry
    {
        public const string GlassBottle = "glass_bottle";
        public const string SkelliJeans = "skelli_jeans";
        public const string HoodedCloak = "hooded_cloak";
        public const string MidnightCoat = "midnight_coat";
        public const string TShirt = "t_shirt";
        public const string BloodRavenTShirt = "blood_raven_t_shirt";
        public const string RedCamoTShirt = "red_camo_t_shirt";
        public const string BlackTShirt = "black_t_shirt";
        public const string GrapplingHook = "grappling_hook";
        public const string Pistol = "pistol";
        public const string Jetpack = "jetpack";
        public const string Parachute = "parachute";
        public const string SpikeSword = "spike_sword";
        public const string CombatSword = "combat_sword";
        public const string MetalCrowbar = "metal_crowbar";
        public const string BaseballBat = "baseball_bat";
        public const string Flamethrower = "flamethrower";
        public const string AK47 = "ak47";
        public const string SprayCan = "spray_can";
        public const string TopHat = "top_hat";
        public const string KnightHelmet = "knight_helmet";

        private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>
        {
            [GlassBottle] = new ItemDefinition(GlassBottle, "Glass Bottle", "weapon", EquipSlots.Primary, 1),
            [SkelliJeans] = new ItemDefinition(SkelliJeans, "Skelli Jeans", ItemDefinition.WearableCategory, EquipSlots.Legs, 1),
            [HoodedCloak] = new ItemDefinition(HoodedCloak, "Hooded Cloak", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [MidnightCoat] = new ItemDefinition(MidnightCoat, "Midnight Coat", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [TShirt] = new ItemDefinition(TShirt, "T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt with the Blood Raven logo printed on it (Wearables/Prints/BloodRavenPrint).
            [BloodRavenTShirt] = new ItemDefinition(BloodRavenTShirt, "Blood Raven T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The T-Shirt covered all over in red camo (Wearables/Prints/RedCamoPrint).
            [RedCamoTShirt] = new ItemDefinition(RedCamoTShirt, "Red Camo T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            // The plain T-Shirt in black cotton.
            [BlackTShirt] = new ItemDefinition(BlackTShirt, "Black T-Shirt", ItemDefinition.WearableCategory, EquipSlots.Torso, 1),
            [GrapplingHook] = new ItemDefinition(GrapplingHook, "Grappling Hook", "weapon", EquipSlots.Primary, 1),
            [Pistol] = new ItemDefinition(Pistol, "Pistol", "weapon", EquipSlots.Primary, 1),
            // The jetpack and the parachute share the back slot: worn with a coat, never with each other (which would Space work?).
            [Jetpack] = new ItemDefinition(Jetpack, "Jetpack", "tool", EquipSlots.Back, 1),
            [Parachute] = new ItemDefinition(Parachute, "Parachute", "tool", EquipSlots.Back, 1),
            // Two-handed: fought with the greatsword's moves.
            [SpikeSword] = new ItemDefinition(SpikeSword, "Spike Sword", "weapon", EquipSlots.Primary, 1),
            // One-handed, with its own attacks and guard; the crowbar is fought with its moves, the bat with the greatsword's.
            [CombatSword] = new ItemDefinition(CombatSword, "Combat Sword", "weapon", EquipSlots.Primary, 1),
            [MetalCrowbar] = new ItemDefinition(MetalCrowbar, "Metal Crowbar", "weapon", EquipSlots.Primary, 1),
            [BaseballBat] = new ItemDefinition(BaseballBat, "Baseball Bat", "weapon", EquipSlots.Primary, 1),
            [Flamethrower] = new ItemDefinition(Flamethrower, "Flamethrower", "weapon", EquipSlots.Primary, 1),
            // Automatic; its handling is Weapons/FirearmProfile.AK47.
            [AK47] = new ItemDefinition(AK47, "AK-47", "weapon", EquipSlots.Primary, 1),
            // Right-click a wall with it to paint graffiti (GraffitiMenuView).
            [SprayCan] = new ItemDefinition(SprayCan, "Spray Can", "tool", EquipSlots.Primary, 1),
            // One headwear slot for now: Godot's separate hat and mask slots are not ported.
            [TopHat] = new ItemDefinition(TopHat, "Top Hat", ItemDefinition.WearableCategory, EquipSlots.Helmet, 1),
            [KnightHelmet] = new ItemDefinition(KnightHelmet, "Knight Helmet", ItemDefinition.WearableCategory, EquipSlots.Helmet, 1),
        };

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            return Items.TryGetValue(itemId, out definition);
        }

        public static bool Has(string itemId) => Items.ContainsKey(itemId);

        public static IReadOnlyCollection<string> Ids => Items.Keys;
    }
}
