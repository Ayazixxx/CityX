namespace CityX.World
{
    /// <summary>
    /// A street of shops, World Gacha's way (2026-10-15, phase 3): some shops have their roller shutter down (シャッター, the corrugated metal
    /// curtain every Japanese shopping street has), and over the shopfronts runs a band of signs, one per shop, in its own colours, some lit from
    /// behind at night. Pure: from the building's seed, so every machine shutters the same shops.
    /// </summary>
    public static class Shopfronts
    {
        /// <summary>The share of buildings with shopfronts that have any shutter down, and of their shop bays that are then shuttered.</summary>
        public const double ShutteredShare = 0.3;

        public const double BayShutteredShare = 0.6;

        /// <summary>How far in from the wall's face a shutter hangs, metres, and how tall and deep the box it rolls up into is.</summary>
        public const float ShutterRecess = 0.06f;

        public const float ShutterBoxHeight = 0.3f;

        public const float ShutterBoxDepth = 0.16f;

        /// <summary>A shop sign in the band: about one per this many metres of shopfront, with this gap between neighbours.</summary>
        public const float MetresPerSign = 4.0f;

        public const float SignGap = 0.14f;

        /// <summary>The share of shop signs lit from behind (they glow at night like the windows).</summary>
        public const double LitSignShare = 0.45;

        private const ulong Salt = 0x53485554UL;   // "SHUT"

        /// <summary>
        /// Whether the shop bay in window column <paramref name="column"/> of a building's street wall has its shutter down: no glass there (nothing
        /// to break, GlassDamage), a slatted metal curtain in a shallow recess, the box it rolls into over it.
        /// </summary>
        public static bool Shuttered(BuildingSite site, in WindowPlan plan, int column)
        {
            if (!plan.Storefront || site.Sides >= 3 || site.OnRoof)
            {
                return false;
            }
            ulong seed = unchecked((ulong)(uint)site.Seed);
            if (WorldHash.Unit01(0, 0, Salt, seed) >= ShutteredShare)
            {
                return false;
            }
            return WorldHash.Unit01(1 + column, 0, Salt, seed) < BayShutteredShare;
        }

        /// <summary>A die for one sign of the band (<paramref name="sign"/> counts along the whole shopfront), for its colour, whether it is lit, its light.</summary>
        public static double SignDie(BuildingSite site, int sign, int what)
        {
            return WorldHash.Unit01(sign, 1 + what, Salt, unchecked((ulong)(uint)site.Seed));
        }
    }
}
