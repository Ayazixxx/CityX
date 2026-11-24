using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// THE FITTED CAP'S COLOURWAYS (docs/FITTED_CAP.md; owner, 2026-11-24: "make a basic fitted cap model we can make a bunch of colorways for"):
    /// one item a colourway, its crown and brim in one colour, the brim's underside and the button on top each in their own. A new colourway is
    /// one line in <see cref="All"/>; the builder, the item list, the console and the bodies' item prefabs all read this list.
    /// </summary>
    public static class FittedCapColourways
    {
        public readonly struct Colourway
        {
            /// <summary>The item's id: what the inventory and a save keep.</summary>
            public readonly string ItemId;

            /// <summary>What the item card and the console print.</summary>
            public readonly string DisplayName;

            /// <summary>A short console name (/spawn, /equip), besides the id itself.</summary>
            public readonly string Alias;

            /// <summary>The crown and the brim's top, as they are before the game's cel shading lights them.</summary>
            public readonly Color Crown;

            /// <summary>The brim's underside.</summary>
            public readonly Color UnderBrim;

            /// <summary>The button on top of the crown.</summary>
            public readonly Color Button;

            public Colourway(string itemId, string displayName, string alias, Color crown, Color underBrim, Color button)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Alias = alias;
                Crown = crown;
                UnderBrim = underBrim;
                Button = button;
            }
        }

        public const string BlackItemId = "black_fitted_cap";

        /// <summary>
        /// Every colourway. Black is a very dark grey (the cel shading's shaded side is darker still, and pure black loses the cap's shape to the
        /// outline); the brim's underside grey, as a fitted cap's is.
        /// </summary>
        public static readonly Colourway[] All =
        {
            new Colourway(BlackItemId, "Black Fitted Cap", "blackcap", new Color(0.075f, 0.075f, 0.08f), new Color(0.32f, 0.32f, 0.32f),
                new Color(0.075f, 0.075f, 0.08f)),
        };

        /// <summary>Where a colourway's prefab is built (FittedCapBuilder), for the lists of item prefabs that bodies draw from.</summary>
        public static string PrefabPath(string itemId) => $"Assets/CityX/Wearables/FittedCaps/{itemId}.prefab";

        /// <summary>The colourway a console name means: its alias or its id; "cap" alone is the first.</summary>
        public static string ItemIdForAlias(string alias)
        {
            if (alias == "cap" || alias == "fittedcap" || alias == "fitted_cap")
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
