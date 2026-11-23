namespace CityX.Items
{
    /// <summary>
    /// THE GLASS BOTTLES (owner, 2026-11-23: "try making a few different shaped glass bottles based on common glass bottle shapes that are
    /// throwable and use the one we have now for inspiration"; docs/GLASS_BOTTLE.md). Every one is the same weapon (Weapons/BottleWeapon): held
    /// by the neck, swung as a club until it breaks on something, a jagged half after that, thrown whole to shatter. A kind is its shape and
    /// its glass (Weapons/BottleShape, by the same id). An id is what a save stores: never change one once shipped.
    /// </summary>
    public static class GlassBottleKinds
    {
        public readonly struct Kind
        {
            public readonly string ItemId;
            public readonly string DisplayName;

            /// <summary>A short console name (/equip, /spawn), besides the id itself.</summary>
            public readonly string Alias;

            public Kind(string itemId, string displayName, string alias)
            {
                ItemId = itemId;
                DisplayName = displayName;
                Alias = alias;
            }
        }

        /// <summary>The first bottle, the owner's model (a spirit bottle): the id it always had.</summary>
        public const string Classic = "glass_bottle";
        public const string Beer = "beer_bottle";
        public const string Wine = "wine_bottle";
        public const string Soda = "soda_bottle";

        public static readonly Kind[] All =
        {
            new Kind(Classic, "Glass Bottle", "bottle"),
            new Kind(Beer, "Beer Bottle", "beer"),
            new Kind(Wine, "Wine Bottle", "wine"),
            new Kind(Soda, "Soda Bottle", "soda"),
        };

        /// <summary>Where a kind's prefab is built (BottleBuilder), for the lists of item prefabs that bodies draw from.</summary>
        public static string PrefabPath(string itemId) => $"Assets/CityX/Weapons/Bottles/{itemId}.prefab";

        /// <summary>Where a kind's bottle in the air is built: what another player's thrown bottle is shown as (BottleThrower).</summary>
        public static string ThrownPrefabPath(string itemId) => $"Assets/CityX/Weapons/Bottles/{itemId}_thrown.prefab";

        /// <summary>Which of <see cref="All"/> an item is, or -1 if it is no bottle.</summary>
        public static int IndexOf(string itemId)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].ItemId == itemId)
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool IsBottle(string itemId) => IndexOf(itemId) >= 0;

        /// <summary>The bottle a console name means: its alias or its id.</summary>
        public static string ItemIdForAlias(string alias)
        {
            foreach (Kind each in All)
            {
                if (each.Alias == alias || each.ItemId == alias || each.ItemId.Replace("_", string.Empty) == alias)
                {
                    return each.ItemId;
                }
            }
            return null;
        }
    }
}
