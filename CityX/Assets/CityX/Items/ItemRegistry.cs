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
        public const string BlackPufferJacket = "black_puffer_jacket";
        public const string GrapplingHook = "grappling_hook";
        public const string Pistol = "pistol";
        public const string Jetpack = "jetpack";
        public const string Parachute = "parachute";
        public const string Sword = "sword";
        public const string SpikeSword = "spike_sword";
        public const string CombatSword = "combat_sword";
        public const string Flamethrower = "flamethrower";
        public const string AK47 = "ak47";
        public const string TopHat = "top_hat";
        public const string FarmerHat = "farmer_hat";

        private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>
        {
            [GlassBottle] = new ItemDefinition(GlassBottle, "Glass Bottle", "weapon", "hand_r", 1),
            [SkelliJeans] = new ItemDefinition(SkelliJeans, "Skelli Jeans", "wearable", "legs", 1),
            [HoodedCloak] = new ItemDefinition(HoodedCloak, "Hooded Cloak", "wearable", "top", 1),
            [MidnightPatchworkCoat] = new ItemDefinition(
                MidnightPatchworkCoat, "Midnight Patchwork Coat", "wearable", "top", 1),
            [BlackPufferJacket] = new ItemDefinition(
                BlackPufferJacket, "Black Puffer Jacket", "wearable", "top", 1),
            [GrapplingHook] = new ItemDefinition(GrapplingHook, "Grappling Hook", "weapon", "hand_r", 1),
            [Pistol] = new ItemDefinition(Pistol, "Pistol", "weapon", "hand_r", 1),
            // "back", not "top" -- LoadoutPanelView's own slot list already
            // anticipates it, unlike a Godot original (no jetpack exists
            // there to have defined one), so it can be worn at the same time
            // as a coat/cloak rather than competing with it for the slot.
            [Jetpack] = new ItemDefinition(Jetpack, "Jetpack", "tool", "back", 1),
            // Same "back" slot as Jetpack, deliberately -- EXPLICIT ASK:
            // "add a parachute as an item parachute backpack." Wearing a
            // parachute and a jetpack at once would not mean anything
            // (which one does Space even talk to?), and reusing Jetpack's
            // own slot means Inventory's existing slot-eviction logic
            // already makes the two mutually exclusive for free, the same
            // way putting on a second coat already replaces the first --
            // no new code needed anywhere for that.
            [Parachute] = new ItemDefinition(Parachute, "Parachute", "tool", "back", 1),
            [Sword] = new ItemDefinition(Sword, "Sword", "weapon", "hand_r", 1),
            [SpikeSword] = new ItemDefinition(SpikeSword, "Spike Sword", "weapon", "hand_r", 1),
            // The owner's COMBATSWORDX.glb (2026-10-13): the first ONE-HANDED sword, with its own attacks and guard.
            [CombatSword] = new ItemDefinition(CombatSword, "Combat Sword", "weapon", "hand_r", 1),
            [Flamethrower] = new ItemDefinition(Flamethrower, "Flamethrower", "weapon", "hand_r", 1),
            // The AK-47 (2026-10-14): the first automatic rifle -- one firearm component, its own profile (Weapons/FirearmProfile.AK47).
            [AK47] = new ItemDefinition(AK47, "AK-47", "weapon", "hand_r", 1),
            // "helmet" -- LoadoutPanelView's own slot list already has one,
            // unused until now. Godot's hat and mask occupy separate slots
            // and can be worn together, but Unity only has this single
            // headwear slot today; only the hat is ported so far, so that
            // gap doesn't need resolving yet.
            [TopHat] = new ItemDefinition(TopHat, "Top Hat", "wearable", "helmet", 1),
            [FarmerHat] = new ItemDefinition(FarmerHat, "Farmer Hat", "wearable", "helmet", 1),
        };

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            return Items.TryGetValue(itemId, out definition);
        }

        public static bool Has(string itemId) => Items.ContainsKey(itemId);

        public static IReadOnlyCollection<string> Ids => Items.Keys;
    }
}
