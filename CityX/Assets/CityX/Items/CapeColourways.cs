using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// THE CAPE'S COLOURWAYS (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily, make it
    /// tied around the front"; 2026-11-25: "make it all the same color"): one item a colourway, all of the cape in its one colour. A new colourway
    /// is one line in <see cref="All"/>; the builder, the item list, the console and the bodies' item prefabs all read this list.
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

            /// <summary>The cape's colour, inside and out, as it is before the game's cel shading lights it.</summary>
            public readonly Color Colour;

            public Colourway(string itemId, string displayName, string alias, Color colour)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Alias = alias;
                Colour = colour;
            }
        }

        public const string RedItemId = "red_cape";
        public const string BlackItemId = "black_cape";
        public const string BrownItemId = "brown_cape";

        /// <summary>
        /// Every colourway. Black is a very dark grey (pure black loses the folds to the outline).
        /// </summary>
        public static readonly Colourway[] All =
        {
            new Colourway(RedItemId, "Red Cape", "redcape", new Color(0.66f, 0.07f, 0.08f)),
            new Colourway(BlackItemId, "Black Cape", "blackcape", new Color(0.08f, 0.08f, 0.09f)),
            new Colourway(BrownItemId, "Brown Cape", "browncape", new Color(0.30f, 0.18f, 0.10f)),   // owner, 2026-11-25: "make a brown one"
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
