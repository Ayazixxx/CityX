using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW MUCH OF A BLOW ARMOUR CAN STOP (docs/ARMOUR.md; owner, 2026-11-06: "make sure it isnt too op for pvp", and "the fights should feel
    /// fun and last long enough that you can utilize your hotbar or different weapons and strategies you have earned"). The Armor stat and the
    /// worn gear add up into one rating, stopping rating / (rating + 100) of a blow as the stat always did, but never more than a cap: half a
    /// player's blow, two thirds of an enemy's, so a fight between two well-armoured players still ends. Magic resistance answers spells the
    /// same way under the same caps. The attacker's penetration takes its share off the rating first, as it always did, so it still counts
    /// against all but the most heavily armoured. My numbers.
    /// </summary>
    public static class ArmourRules
    {
        /// <summary>The most of a player's blow any armour stops.</summary>
        public const float PlayerBlowCap = 0.50f;

        /// <summary>The most of anything else's blow (enemies, the world) any armour stops.</summary>
        public const float OtherBlowCap = 0.65f;

        /// <summary>
        /// The sledgehammer's weight goes through armour: this share of the target's armour is ignored, on top of the wielder's own Armor
        /// Penetration (owner, 2026-11-06, asking how a hammer differs from a sword; my suggestion, which the owner said go to).
        /// </summary>
        public const float HammerPierce = 0.35f;

        /// <summary>The share of armour a weapon in hand ignores by its own weight: a hammer's <see cref="HammerPierce"/>, nothing for anything else.</summary>
        public static float PierceOf(object weapon) => weapon is ISwordBlade blade && blade.IsHammer ? HammerPierce : 0.0f;

        /// <summary>The cap on what armour stops of a blow struck by a player (<paramref name="byPlayer"/>) or by anything else.</summary>
        public static float CapFor(bool byPlayer) => byPlayer ? PlayerBlowCap : OtherBlowCap;

        /// <summary>The share of a blow stopped by <paramref name="rating"/> armour after <paramref name="penetration"/>, capped by who struck it.</summary>
        public static float Reduction(float rating, float penetration, bool byPlayer)
        {
            return Mathf.Min(StatCatalog.RatingReduction(rating * (1.0f - Mathf.Clamp01(penetration))), CapFor(byPlayer));
        }

        /// <summary>Two sources of penetration together (a stat and a weapon): each ignores its share of what the other left.</summary>
        public static float Combine(float a, float b) => 1.0f - (1.0f - Mathf.Clamp01(a)) * (1.0f - Mathf.Clamp01(b));
    }
}
