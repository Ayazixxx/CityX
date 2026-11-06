using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>How protective a worn piece is (docs/ARMOUR.md). Clothes are <see cref="None"/>.</summary>
    public enum ArmourTier
    {
        None,
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// GEAR ARMOUR (owner, 2026-11-06: "there should certainly be stronger armor than others", no slowing down for it, and "make sure it isnt
    /// too op for pvp"): every worn piece adds an armour rating to the wearer's Armor stat, by the slot it covers and its tier. A full heavy
    /// set adds <see cref="BestSetRating"/>, which alone stops about 40% of a blow (rating / (rating + 100), StatCatalog.RatingReduction).
    /// The numbers are my suggestion, which the owner said go to; the caps against players and enemies are in Character.ArmourRules.
    /// </summary>
    public static class ArmourRatings
    {
        /// <summary>What the heaviest piece in each slot adds: the torso most, then the legs and the head, a little on the hands and feet.</summary>
        public const float HelmetBest = 14.0f, TorsoBest = 27.0f, LegsBest = 16.0f, GlovesBest = 5.0f, ShoesBest = 5.0f;

        /// <summary>A full heavy set: about 40% of a blow stopped by the gear alone.</summary>
        public const float BestSetRating = HelmetBest + TorsoBest + LegsBest + GlovesBest + ShoesBest;

        /// <summary>The share of a slot's best that a light and a medium piece give.</summary>
        public const float LightShare = 0.35f, MediumShare = 0.65f;

        /// <summary>The pieces that are armour; everything else worn is clothing and protects nothing.</summary>
        private static readonly Dictionary<string, ArmourTier> Tiers = new Dictionary<string, ArmourTier>
        {
            [ItemRegistry.KnightHelmet] = ArmourTier.Heavy,
        };

        public static ArmourTier TierOf(string itemId)
        {
            return itemId != null && Tiers.TryGetValue(itemId, out ArmourTier tier) ? tier : ArmourTier.None;
        }

        /// <summary>The rating a slot's heaviest piece gives (0 for a slot no armour covers: a mask, the back, a shield, a hand).</summary>
        public static float SlotBest(string slot)
        {
            switch (slot)
            {
                case EquipSlots.Helmet: return HelmetBest;
                case EquipSlots.Torso: return TorsoBest;
                case EquipSlots.Legs: return LegsBest;
                case EquipSlots.Gloves: return GlovesBest;
                case EquipSlots.Shoes: return ShoesBest;
                default: return 0.0f;
            }
        }

        public static float TierShare(ArmourTier tier)
        {
            switch (tier)
            {
                case ArmourTier.Light: return LightShare;
                case ArmourTier.Medium: return MediumShare;
                case ArmourTier.Heavy: return 1.0f;
                default: return 0.0f;
            }
        }

        /// <summary>The armour rating <paramref name="itemId"/> adds when worn (0 for clothes, weapons and unknown ids).</summary>
        public static float RatingOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !ItemRegistry.TryGet(itemId, out ItemDefinition definition) || !definition.IsWearable)
            {
                return 0.0f;
            }
            return SlotBest(definition.EquipSlot) * TierShare(TierOf(itemId));
        }

        /// <summary>What the tier is called on an item's card.</summary>
        public static string NameOf(ArmourTier tier)
        {
            switch (tier)
            {
                case ArmourTier.Light: return "Light Armour";
                case ArmourTier.Medium: return "Medium Armour";
                case ArmourTier.Heavy: return "Heavy Armour";
                default: return "Clothing";
            }
        }
    }
}
