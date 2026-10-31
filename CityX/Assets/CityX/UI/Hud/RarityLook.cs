using CityX.Items;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>The colour each rarity's name is written in: one set over the world (the prompt), one on the white appraisal card.</summary>
    public static class RarityLook
    {
        public static Color OnDark(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.45f);
                case Rarity.Rare: return new Color(0.40f, 0.62f, 1.00f);
                case Rarity.VeryRare: return new Color(0.78f, 0.50f, 1.00f);
                case Rarity.Legendary: return new Color(1.00f, 0.65f, 0.25f);
                case Rarity.Godly: return new Color(1.00f, 0.30f, 0.30f);
                default: return new Color(0.92f, 0.92f, 0.92f);
            }
        }

        public static Color OnLight(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return new Color(0.16f, 0.52f, 0.20f);
                case Rarity.Rare: return new Color(0.14f, 0.36f, 0.78f);
                case Rarity.VeryRare: return new Color(0.48f, 0.22f, 0.72f);
                case Rarity.Legendary: return new Color(0.80f, 0.42f, 0.05f);
                case Rarity.Godly: return new Color(0.78f, 0.12f, 0.12f);
                default: return new Color(0.22f, 0.23f, 0.26f);
            }
        }
    }
}
