using UnityEngine;

namespace CityX.Items
{
    /// <summary>
    /// THE FINGERLESS GLOVES' COLOURS (owner, 2026-11-23: "generate some fingerless gloves. just black and brown for now but make it so we can
    /// make more colorways easily"; docs/FINGERLESS_GLOVES.md). Every pair is the same gloves, made on the wearer's own hands at the moment
    /// they are put on (Wearables/FingerlessGloves), in one flat colour. A NEW COLOURWAY IS ONE LINE HERE, then CityX/Rebuild Fingerless Gloves
    /// (or the scene rebuild) and the title screen rebuild: the item, its prefab, its material, the console's name for it and the lists every
    /// body draws items from all follow from this table. An id is what a save stores: never change one once shipped.
    /// </summary>
    public static class FingerlessGloveColourways
    {
        public readonly struct Colourway
        {
            /// <summary>The item's id: what the inventory and a save keep.</summary>
            public readonly string ItemId;

            /// <summary>What the item card and the console print.</summary>
            public readonly string DisplayName;

            /// <summary>A short console name (/equip, /spawn), besides the id itself.</summary>
            public readonly string Alias;

            /// <summary>The leather's colour, as it is before the game's cel shading lights it.</summary>
            public readonly Color Colour;

            public Colourway(string itemId, string displayName, string alias, Color colour)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Alias = alias;
                Colour = colour;
            }
        }

        public const string BlackItemId = "black_fingerless_gloves";
        public const string BrownItemId = "brown_fingerless_gloves";

        /// <summary>
        /// Every colourway. Black is a very dark grey rather than black: the cel shading's shaded side is darker still, and pure black loses
        /// the fingers' shape to the outline.
        /// </summary>
        public static readonly Colourway[] All =
        {
            new Colourway(BlackItemId, "Black Fingerless Gloves", "blackgloves", new Color(0.075f, 0.075f, 0.08f)),
            new Colourway(BrownItemId, "Brown Fingerless Gloves", "browngloves", new Color(0.36f, 0.21f, 0.11f)),
        };

        /// <summary>Where a colourway's prefab is built (FingerlessGlovesBuilder), for the lists of item prefabs that bodies draw from.</summary>
        public static string PrefabPath(string itemId) => $"Assets/CityX/Wearables/FingerlessGloves/{itemId}.prefab";

        /// <summary>The colourway a console name means: its alias or its id; "gloves" alone is the first (black).</summary>
        public static string ItemIdForAlias(string alias)
        {
            if (alias == "gloves" || alias == "fingerless_gloves" || alias == "fingerlessgloves")
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
