using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>Which drawn weapon a BowWeapon is.</summary>
    public enum DrawnKind
    {
        Bow,
        Slingshot,
    }

    /// <summary>
    /// A DRAWN WEAPON'S NUMBERS AND HOLD: the bow's (ArcheryRules) or the slingshot's (SlingshotRules). Both are drawn by holding the click and
    /// loosed on letting go, the shot a hitscan over a flat range that grows with the draw, then a falling projectile (ArrowProjectile), as the
    /// crossbow is a gun with its own profile (FirearmProfile.Crossbow). What differs: how long the draw takes, the reach, the damage, a pause
    /// to fetch the next shot, and how the arms draw it (<see cref="Style"/>: the bow's quiver and long pull, the slingshot's pouch at the fork).
    /// </summary>
    public sealed class DrawnProfile
    {
        public readonly DrawnKind Kind;
        public readonly string ItemId;
        public readonly float FullDrawSeconds;
        public readonly float MinDraw;
        public readonly float TapFlatRange;
        public readonly float FullFlatRange;
        public readonly float TapDamage;
        public readonly float FullDamage;
        public readonly float TapFallSpeed;
        public readonly float FullFallSpeed;

        /// <summary>After a loose, how long before the next draw can start, seconds.</summary>
        public readonly float ReloadSeconds;

        /// <summary>What the HUD calls the shots.</summary>
        public readonly string AmmoName;

        public readonly PlayerLocomotion.DrawStyle Style;

        public static readonly DrawnProfile Bow = new DrawnProfile(DrawnKind.Bow, ItemRegistry.Bow, ArcheryRules.FullDrawSeconds, ArcheryRules.MinDraw,
            ArcheryRules.TapFlatRange, ArcheryRules.FullFlatRange, ArcheryRules.TapDamage, ArcheryRules.FullDamage, ArcheryRules.TapFallSpeed,
            ArcheryRules.FullFallSpeed, 0.0f, "ARROWS", PlayerLocomotion.DrawStyle.Bow);

        public static readonly DrawnProfile Slingshot = new DrawnProfile(DrawnKind.Slingshot, ItemRegistry.Slingshot, SlingshotRules.FullDrawSeconds,
            ArcheryRules.MinDraw, SlingshotRules.TapFlatRange, SlingshotRules.FullFlatRange, SlingshotRules.TapDamage, SlingshotRules.FullDamage,
            SlingshotRules.TapFallSpeed, SlingshotRules.FullFallSpeed, SlingshotRules.ReloadSeconds, "PEBBLES",
            new PlayerLocomotion.DrawStyle(0.0f, SlingshotRules.DrawLength, SlingshotRules.PouchAbove, slingshot: true));

        private DrawnProfile(DrawnKind kind, string itemId, float fullDrawSeconds, float minDraw, float tapFlatRange, float fullFlatRange,
            float tapDamage, float fullDamage, float tapFallSpeed, float fullFallSpeed, float reloadSeconds, string ammoName,
            PlayerLocomotion.DrawStyle style)
        {
            Kind = kind;
            ItemId = itemId;
            FullDrawSeconds = fullDrawSeconds;
            MinDraw = minDraw;
            TapFlatRange = tapFlatRange;
            FullFlatRange = fullFlatRange;
            TapDamage = tapDamage;
            FullDamage = fullDamage;
            TapFallSpeed = tapFallSpeed;
            FullFallSpeed = fullFallSpeed;
            ReloadSeconds = reloadSeconds;
            AmmoName = ammoName;
            Style = style;
        }

        /// <summary>The profile of <paramref name="kind"/> (another player's word: anything unknown is the bow's).</summary>
        public static DrawnProfile For(DrawnKind kind) => kind == DrawnKind.Slingshot ? Slingshot : Bow;

        /// <summary>The flat range, damage and the speed it starts falling at for a draw of <paramref name="draw"/>: the tap's at the least draw and under, the full at 1.</summary>
        public float FlatRangeFor(float draw) => Mathf.Lerp(TapFlatRange, FullFlatRange, Share(draw));

        public float DamageFor(float draw) => Mathf.Lerp(TapDamage, FullDamage, Share(draw));

        public float FallSpeedFor(float draw) => Mathf.Lerp(TapFallSpeed, FullFallSpeed, Share(draw));

        public float Clamp(float draw) => Mathf.Clamp(draw, MinDraw, 1.0f);

        /// <summary>How far from the least draw to the full one, 0..1.</summary>
        private float Share(float draw) => (Clamp(draw) - MinDraw) / (1.0f - MinDraw);
    }
}
