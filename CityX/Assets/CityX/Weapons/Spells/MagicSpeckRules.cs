using CityX.Character;

namespace CityX.Weapons
{
    /// <summary>One kind of speck shot: the wand's or the staff's (its event kind, damage, pace and size).</summary>
    public readonly struct SpeckShot
    {
        public readonly int Kind;
        public readonly float Damage;
        public readonly float Seconds;
        public readonly float Size;

        public SpeckShot(int kind, float damage, float seconds, float size)
        {
            Kind = kind;
            Damage = damage;
            Seconds = seconds;
            Size = size;
        }

        public float DamagePerSecond => Damage / Seconds;
    }

    /// <summary>
    /// THE MAGIC WAND AND THE MAGIC STAFF IN NUMBERS (docs/MAGIC_WAND.md). Owner, 2026-10-30: the wand "just gives a slight boost to spells casted
    /// while holding it, maybe 5% more damage 10% less mana usage. clicking with it will do some basic projectile attack like it just shoots a
    /// little speck of dust at a low firerate like 1 per second"; the staff "does the same spec of dust attack but maybe it does slightly more
    /// damage at a slightly lower firerate". The damage, the speed and the rest are mine. Pure numbers, tested without a scene.
    /// </summary>
    public static class MagicSpeckRules
    {
        public const int WandKind = 0;
        public const int StaffKind = 1;

        /// <summary>A punch's worth, once a second: the free fallback when the mana is gone.</summary>
        public const float WandDamage = 10.0f;

        public const float WandShotSeconds = 1.0f;

        /// <summary>A little harder and a little slower: about a twentieth more damage a second than the wand's.</summary>
        public const float StaffDamage = 13.0f;

        public const float StaffShotSeconds = 1.25f;

        /// <summary>The staff's speck is this much bigger than the wand's.</summary>
        public const float StaffSpeckSize = 1.35f;

        public static readonly SpeckShot Wand = new SpeckShot(WandKind, WandDamage, WandShotSeconds, 1.0f);

        public static readonly SpeckShot Staff = new SpeckShot(StaffKind, StaffDamage, StaffShotSeconds, StaffSpeckSize);

        /// <summary>Metres a second, and how long a speck flies before it fizzles out (63 m).</summary>
        public const float Speed = 45.0f;

        public const float LifetimeSeconds = 1.4f;

        /// <summary>The wand's speck's collider, metres.</summary>
        public const float Radius = 0.06f;

        /// <summary>A click this long before the next shot is ready still shoots, the moment it is.</summary>
        public const float ClickBufferSeconds = 0.2f;

        /// <summary>The flinch on whoever a speck hits: a poke, not a stagger.</summary>
        public const float HitstunSeconds = 0.15f;

        /// <summary>A spell cast through the wand does this much more damage (or healing), and pays this share of its mana.</summary>
        public const float WandSpellDamageBonus = 0.05f;

        public const float WandManaCostFactor = 0.9f;

        /// <summary><see cref="WandSpellDamageBonus"/> as Spell Power points (2% of a spell's damage a point): two and a half.</summary>
        public static float WandSpellPowerBonus => WandSpellDamageBonus / CombatTuning.StrengthDamagePerPoint;

        /// <summary>The shot an event of <paramref name="kind"/> is (another player's word: anything unknown is the wand's).</summary>
        public static SpeckShot For(int kind) => kind == StaffKind ? Staff : Wand;
    }
}
