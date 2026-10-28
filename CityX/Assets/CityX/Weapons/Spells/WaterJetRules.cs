using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WATER STAFF'S NUMBERS (owner, 2026-10-28: "make an item just called "water staff" for now. Have it function like the hose at the top
    /// of firetrucks in gta 5 do. it just shoots water and pushes and ragdolls whoever is the target of it. our will also do a bit of damage not
    /// much though just like 3 damage consecutively as you are spraying"; docs/STAFFS.md). A jet of water held on a target shoves them, throws
    /// them limp, keeps pushing them along the ground while they are down, and ticks 3 damage every <see cref="TickSeconds"/> (10 a second, a
    /// third of the ice staff's). The reach, the push and the mana are mine.
    /// </summary>
    public static class WaterJetRules
    {
        /// <summary>How far the jet reaches, metres, and how wide the stream is round its line.</summary>
        public const float Range = 16.0f;

        public const float StreamRadius = 0.5f;

        /// <summary>A body is in the stream only this close to the jet's line in angle, degrees: a hose, not a cone.</summary>
        public const float ConeDegrees = 4.0f;

        /// <summary>"3 damage consecutively": each tick, and how often, while a body stays in the stream.</summary>
        public const float DamagePerTick = 3.0f;

        public const float TickSeconds = 0.3f;

        /// <summary>Mana the jet drinks a second (about sixteen seconds from a full bar).</summary>
        public const float ManaPerSecond = 6.0f;

        /// <summary>How fast a body standing in the jet is thrown limp, along the jet and a little up, metres a second.</summary>
        public const float KnockdownSpeed = 8.0f;

        public const float KnockdownLift = 3.0f;

        /// <summary>How hard the jet keeps pushing a limp body along, metres a second each second.</summary>
        public const float PushAcceleration = 25.0f;

        /// <summary>A body that cannot be thrown (just back on its feet, unhurtable for a moment) is still shoved this fast.</summary>
        public const float ShoveSpeed = 6.0f;

        /// <summary>How often other players are told where the jet points while it is held, seconds.</summary>
        public const float AimSendSeconds = 0.1f;

        /// <summary>Whether <paramref name="point"/> is in a jet from <paramref name="from"/> along <paramref name="direction"/>: within reach, and within the stream's width and angle of its line.</summary>
        public static bool InStream(Vector3 from, Vector3 direction, Vector3 point)
        {
            Vector3 to = point - from;
            float along = Vector3.Dot(to, direction.normalized);
            if (along <= 0.0f || along > Range)
            {
                return false;
            }
            float off = (to - direction.normalized * along).magnitude;
            return off <= StreamRadius || Vector3.Angle(direction, to) <= ConeDegrees;
        }

        /// <summary>The throw a body in the jet gets: along the jet, lifted a little (so it leaves the ground and goes limp cleanly).</summary>
        public static Vector3 KnockdownVelocity(Vector3 direction)
        {
            Vector3 flat = new Vector3(direction.x, 0.0f, direction.z);
            Vector3 along = flat.sqrMagnitude > 1e-6f ? flat.normalized : direction.normalized;
            return along * KnockdownSpeed + Vector3.up * KnockdownLift;
        }

        /// <summary>How many damage ticks fall in a stay of <paramref name="seconds"/> in the stream (the first the moment it begins).</summary>
        public static int TicksIn(float seconds) => seconds < 0.0f ? 0 : 1 + Mathf.FloorToInt(seconds / TickSeconds);
    }
}
