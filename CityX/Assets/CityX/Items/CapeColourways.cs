using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// THE CAPE'S COLOURWAYS (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily, make it
    /// tied around the front"): one item a colourway, the cape's outside (and the rolled hem over the shoulders) in one colour, its lining in
    /// another. A new colourway is one line in <see cref="All"/>; the builder, the item list, the console and the bodies' item prefabs all read
    /// this list.
    /// </summary>
    public static class CapeColourways
    {
        public readonly struct Colourway
        {
            /// <summary>The item's id: what the inventory and a save keep.</summary>
            public readonly string ItemId;

            /// <summary>What the item card and the console print.</summary>
            public readonly string DisplayName;

            /// <summary>A short console name (/spawn, /equip), besides the id itself.</summary>
            public readonly string Alias;

            /// <summary>The cape's outside, as it is before the game's cel shading lights it.</summary>
            public readonly Color Outside;

            /// <summary>The inside (the lining), seen when it swings out or blows up.</summary>
            public readonly Color Lining;

            public Colourway(string itemId, string displayName, string alias, Color outside, Color lining)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Alias = alias;
                Outside = outside;
                Lining = lining;
            }
        }

        public const string RedItemId = "red_cape";
        public const string BlackItemId = "black_cape";

        /// <summary>
        /// Every colourway. Red with a darker red lining; black (a very dark grey: pure black loses the folds to the outline) with a charcoal lining.
        /// </summary>
        public static readonly Colourway[] All =
        {
            new Colourway(RedItemId, "Red Cape", "redcape", new Color(0.66f, 0.07f, 0.08f), new Color(0.40f, 0.04f, 0.05f)),
            new Colourway(BlackItemId, "Black Cape", "blackcape", new Color(0.08f, 0.08f, 0.09f), new Color(0.22f, 0.22f, 0.24f)),
        };

        /// <summary>Where a colourway's prefab is built (CapeBuilder), for the lists of item prefabs that bodies draw from.</summary>
        public static string PrefabPath(string itemId) => $"Assets/CityX/Wearables/Capes/{itemId}.prefab";

        /// <summary>The colourway a console name means: its alias or its id; "cape" alone is the first.</summary>
        public static string ItemIdForAlias(string alias)
        {
            if (alias == "cape")
            {
                return All[0].ItemId;
            }
            foreach (Colourway each in All)
            {
                if (each.Alias == alias || each.ItemId == alias)
                {
                    return each.ItemId;
                }
            }
            return null;
        }
    }
}
