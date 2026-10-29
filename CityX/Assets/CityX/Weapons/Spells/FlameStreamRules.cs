using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FLAME STAFF'S NUMBERS (owner, 2026-10-28: "make a fire staff that functions like a flamethrower but costs mana"; docs/STAFFS.md). The
    /// flamethrower's stream (Flamethrower: 7 m, a 10 degree cone, 30 a second, free), as a staff's spell: a little longer and hotter for the mana
    /// it drinks, burning every frame it is held on someone, out in the rain half as fierce (Weather.FlameStrength) like the flamethrower. The
    /// numbers are mine.
    /// </summary>
    public static class FlameStreamRules
    {
        /// <summary>How far the stream reaches, metres (the flamethrower's 7: a staff throws its fire a little further).</summary>
        public const float Range = 9.0f;

        /// <summary>Half the angle of the cone that burns, degrees: narrower than the flame looks, as the flamethrower's.</summary>
        public const float ConeHalfAngleDegrees = 10.0f;

        /// <summary>Damage a second of being held on someone, before Spell Power (the flamethrower's 30 is a gun's; this pays mana).</summary>
        public const float DamagePerSecond = 36.0f;

        /// <summary>
        /// The burn lands in ticks this far apart, seconds (the flamethrower burns every frame; a tick keeps the damage numbers readable and the
        /// hit flash a flicker, not a strobe), each tick <see cref="DamagePerTick"/>.
        /// </summary>
        public const float TickSeconds = 0.15f;

        public const float DamagePerTick = DamagePerSecond * TickSeconds;

        /// <summary>The mana it drinks a second (mana comes back at PlayerVitals.ManaRegenPerSecond, so a full bar burns about 12 s).</summary>
        public const float ManaPerSecond = 8.0f;

        /// <summary>How often the stream's direction is sent to other players, seconds.</summary>
        public const float AimSendSeconds = 0.1f;

        /// <summary>Whether <paramref name="point"/> is in the stream from <paramref name="from"/> along <paramref name="direction"/> reaching <paramref name="reach"/>.</summary>
        public static bool InStream(Vector3 from, Vector3 direction, Vector3 point, float reach)
        {
            Vector3 to = point - from;
            if (to.sqrMagnitude < 1e-6f)
            {
                return true;
            }
            return to.magnitude <= reach && Vector3.Angle(direction, to) <= ConeHalfAngleDegrees;
        }

        /// <summary>The stream's reach in the weather: a quarter shorter in a downpour (the flamethrower's rule).</summary>
        public static float ReachFor(float flameStrength) => Range * (0.5f + 0.5f * Mathf.Clamp01(flameStrength));

        /// <summary>How many ticks land in <paramref name="seconds"/> of being held on someone (the first at once).</summary>
        public static int TicksIn(float seconds) => seconds < 0.0f ? 0 : 1 + Mathf.FloorToInt(seconds / TickSeconds);
    }
}
