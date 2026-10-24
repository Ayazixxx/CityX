using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// How a damage number looks and moves, Fortnite's way (owner, 2026-10-24: "try to recreate it how fortnite does it"): it pops out of the
    /// spot that was hit a little bigger than it settles, rises and drifts aside, and fades within a second; every hit its own number.
    ///
    /// ITS COLOUR IS THE SHARE OF THE TARGET'S HEALTH BAR THE ONE BLOW TOOK (owner: "make it turn more red based on the amount of the enemies
    /// healthbar you are taking away with that 1 attack"): white for a scratch, redder the bigger the bite, full red from half the bar. Against
    /// someone far weaker every blow is red; against someone far stronger the numbers stay white, and only a big blow shows red. The size
    /// grows with it, and a heavy blow is bigger still.
    /// </summary>
    public static class DamageNumberLook
    {
        public const float LifeSeconds = 0.7f;

        /// <summary>The number holds fully opaque this long, then fades out by the end of its life.</summary>
        public const float FadeFromSeconds = 0.45f;

        /// <summary>It appears this many times its size and settles to it over <see cref="PopSeconds"/>.</summary>
        public const float PopScale = 1.6f;

        public const float PopSeconds = 0.12f;

        /// <summary>Design pixels (the HUD's 1152 x 648): how far it rises over its life, how far it drifts aside, how far apart hits start.</summary>
        public const float RisePixels = 46.0f;

        public const float DriftPixels = 18.0f;

        public const float SpreadPixels = 26.0f;

        /// <summary>Under this share of the bar a blow is plain white; from <see cref="FullRedShare"/> it is full red.</summary>
        public const float WhiteBelowShare = 0.03f;

        public const float FullRedShare = 0.5f;

        public static readonly Color Red = new Color(1.0f, 0.13f, 0.08f);

        /// <summary>Font sizes in design pixels: a white scratch and a full-red bite; a heavy blow is this many times bigger.</summary>
        public const int SmallSize = 24;

        public const int BigSize = 34;

        public const float HeavyScale = 1.25f;

        /// <summary>
        /// Hits on one target this close together add into one number (a flamethrower burns every frame, a shotgun's pellets land at once), for
        /// up to <see cref="MergeForSeconds"/>; an automatic's bullets, a tenth of a second apart, each get their own, as Fortnite's do.
        /// </summary>
        public const float MergeWithinSeconds = 0.08f;

        public const float MergeForSeconds = 0.5f;

        /// <summary>Farther than this a hit shows no number; from <see cref="ShrinkFromMetres"/> out to it, numbers shrink to <see cref="FarScale"/>.</summary>
        public const float MaxMetres = 60.0f;

        public const float ShrinkFromMetres = 8.0f;

        public const float FarScale = 0.65f;

        /// <summary>0 (white) to 1 (full red): how red a blow taking <paramref name="shareOfBar"/> of the target's bar is.</summary>
        public static float Redness(float shareOfBar)
        {
            return Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(WhiteBelowShare, FullRedShare, shareOfBar));
        }

        public static Color ColourFor(float shareOfBar)
        {
            return Color.Lerp(Color.white, Red, Redness(shareOfBar));
        }

        public static float SizeFor(float shareOfBar, bool heavy)
        {
            return Mathf.Lerp(SmallSize, BigSize, Redness(shareOfBar)) * (heavy ? HeavyScale : 1.0f);
        }

        /// <summary>The pop: <see cref="PopScale"/> as it appears, easing to 1.</summary>
        public static float ScaleAt(float age)
        {
            float u = Mathf.Clamp01(age / PopSeconds);
            float ease = 1.0f - (1.0f - u) * (1.0f - u);
            return Mathf.Lerp(PopScale, 1.0f, ease);
        }

        /// <summary>Where it has moved to from where it appeared, design pixels: up fast and slowing, and aside the way it set off (<paramref name="side"/>, -1 to 1).</summary>
        public static Vector2 OffsetAt(float age, float side)
        {
            float u = Mathf.Clamp01(age / LifeSeconds);
            float ease = 1.0f - (1.0f - u) * (1.0f - u);
            return new Vector2(side * DriftPixels * ease, RisePixels * ease);
        }

        public static float AlphaAt(float age)
        {
            return 1.0f - Mathf.InverseLerp(FadeFromSeconds, LifeSeconds, age);
        }

        /// <summary>How big a number at <paramref name="metres"/> from the camera is drawn (0 past <see cref="MaxMetres"/>).</summary>
        public static float DistanceScale(float metres)
        {
            if (metres > MaxMetres)
            {
                return 0.0f;
            }
            return Mathf.Lerp(1.0f, FarScale, Mathf.InverseLerp(ShrinkFromMetres, MaxMetres, metres));
        }

        /// <summary>The number as shown: whole points, never 0 for a blow that did something.</summary>
        public static string TextFor(float amount)
        {
            return Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
        }
    }
}
