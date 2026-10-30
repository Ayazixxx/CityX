using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Frost Staff's spray and what it does to a body (docs/FROST.md), with no scene: Mei's Endothermic Blaster. Held, a short cone of frost;
    /// whoever is in it takes a little damage a tick at a time and is chilled: the chill builds while they stay in it and wears off when they
    /// get out, slowing them more the colder they are, and once it is full they freeze solid for a moment, then thaw (and cannot be frozen
    /// again straight away).
    /// </summary>
    public static class FrostRules
    {
        /// <summary>The spray's reach, metres (Mei's 10 m, a short weapon), and half its cone, degrees.</summary>
        public const float Range = 10.0f;

        public const float ConeHalfAngleDegrees = 12.0f;

        /// <summary>Damage a second of being held in it, before Spell Power: low (owner: "a low amount of consecutive damage").</summary>
        public const float DamagePerSecond = 12.0f;

        public const float TickSeconds = 0.25f;

        public const float DamagePerTick = DamagePerSecond * TickSeconds;

        /// <summary>The mana it drinks a second.</summary>
        public const float ManaPerSecond = 7.0f;

        /// <summary>How much colder a tick in the spray makes a body: a full chill (and the freeze) after 2 s held on it (Mei's ~1.5 to 2).</summary>
        public const float ChillPerTick = TickSeconds / 2.0f;

        /// <summary>How fast the chill wears off out of the spray, a second: a full chill is gone in two.</summary>
        public const float ThawPerSecond = 0.5f;

        /// <summary>How much a full chill slows a body (Mei's 50%, 40% since 2023).</summary>
        public const float MaxSlow = 0.5f;

        /// <summary>How long a body stays frozen solid, seconds, and how long after it thaws before it can freeze again.</summary>
        public const float FreezeSeconds = 1.5f;

        public const float FreezeImmunitySeconds = 1.5f;

        /// <summary>How often the spray's direction is sent to other players, seconds.</summary>
        public const float AimSendSeconds = 0.1f;

        /// <summary>Whether <paramref name="point"/> is in a spray from <paramref name="from"/> along <paramref name="direction"/> reaching <paramref name="reach"/>.</summary>
        public static bool InCone(Vector3 from, Vector3 direction, Vector3 point, float reach = Range)
        {
            Vector3 to = point - from;
            float distance = to.magnitude;
            if (distance > reach)
            {
                return false;
            }
            return distance < 0.5f || Vector3.Angle(direction, to) <= ConeHalfAngleDegrees + Mathf.Rad2Deg * Mathf.Atan2(0.4f, distance);
        }

        /// <summary>How fast a body with <paramref name="chill"/> (0 to 1) moves, as a share of its own pace.</summary>
        public static float SpeedShare(float chill)
        {
            return 1.0f - MaxSlow * Mathf.Clamp01(chill);
        }
    }
}
