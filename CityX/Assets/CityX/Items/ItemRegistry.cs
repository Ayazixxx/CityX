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
        public const string TShirt = "t_shirt";
        public const string Hoodie = "hoodie";

        private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>
        {
            [GlassBottle] = new ItemDefinition(GlassBottle, "Glass Bottle", "weapon", "hand_r", 1),
            [TShirt] = new ItemDefinition(TShirt, "T-Shirt", "wearable", "top", 1),
            [Hoodie] = new ItemDefinition(Hoodie, "Hoodie", "wearable", "top", 1),
        };

        public static bool TryGet(string itemId, out ItemDefinition definition)
        {
            return Items.TryGetValue(itemId, out definition);
        }

        public static bool Has(string itemId) => Items.ContainsKey(itemId);

        public static IReadOnlyCollection<string> Ids => Items.Keys;
    }
}
