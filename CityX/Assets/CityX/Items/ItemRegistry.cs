using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>
    /// Ported from items/item_registry.gd. Pure data -- no GameObject or
    /// prefab references live here, those are wired externally (see
    /// Inventory.ItemPrefabs), so this stays testable with zero Unity
    /// objects and stays the single place every item id is spelled once.
    /// </summary>
    public static class ItemRegistry
    {
        public const string GlassBottle = "glass_bottle";
        public const string SkelliJeans = "skelli_jeans";
        public const string HoodedCloak = "hooded_cloak";
        public const string MidnightPatchworkCoat = "midnight_patchwork_coat";
        public const string GrapplingHook = "grappling_hook";
        public const string Pistol = "pistol";
        public const string Jetpack = "jetpack";
        public const string Sword = "sword";

        private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>
        {
            [GlassBottle] = new ItemDefinition(GlassBottle, "Glass Bottle", "weapon", "hand_r", 1),
            [SkelliJeans] = new ItemDefinition(SkelliJeans, "Skelli Jeans", "wearable", "legs", 1),
            [HoodedCloak] = new ItemDefinition(HoodedCloak, "Hooded Cloak", "wearable", "top", 1),
            [MidnightPatchworkCoat] = new ItemDefinition(
                MidnightPatchworkCoat, "Midnight Patchwork Coat", "wearable", "top", 1),
            [GrapplingHook] = new ItemDefinition(GrapplingHook, "Grappling Hook", "weapon", "hand_r", 1),
            [Pistol] = new ItemDefinition(Pistol, "Pistol", "weapon", "hand_r", 1),
            // "back", not "top" -- LoadoutPanelView's own slot list already
            // anticipates it, unlike a Godot original (no jetpack exists
            // there to have defined one), so it can be worn at the same time
            // as a coat/cloak rather than competing with it for the slot.
            [Jetpack] = new ItemDefinition(Jetpack, "Jetpack", "tool", "back", 1),
            [Sword] = new ItemDefinition(Sword, "Sword", "weapon", "hand_r", 1),
        };

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            return Items.TryGetValue(itemId, out definition);
        }

        public static bool Has(string itemId) => Items.ContainsKey(itemId);

        public static IReadOnlyCollection<string> Ids => Items.Keys;
    }
}
