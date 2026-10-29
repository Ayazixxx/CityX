using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The Staff of Healing's numbers and choices (docs/STAFFS.md), with no scene: Mercy's Caduceus Staff without its damage boost. Held, it
    /// ties a beam to the other player nearest the crosshair within reach, and the beam stays on them however the healer looks, until it is let
    /// go, they go past its reach, or they stay out of sight too long.
    /// </summary>
    public static class HealingBeamRules
    {
        /// <summary>How far an ally may be to tie the beam to, metres: Mercy's 15 m on a body 1.3 times life size.</summary>
        public const float LockRange = 20.0f;

        /// <summary>How far the beam stretches before it lets go, metres: a little past the lock, as Mercy's does.</summary>
        public const float BreakRange = 21.0f;

        /// <summary>How far off the crosshair an ally may stand to be picked, degrees.</summary>
        public const float LockConeDegrees = 15.0f;

        /// <summary>How long the beam holds on someone out of sight, seconds (Mercy's 1.3: peek round a corner without losing them).</summary>
        public const float SightGraceSeconds = 1.3f;

        /// <summary>
        /// Health a second, before Spell Power: about Mercy's share of a bar (55 a second on heroes of 200 to 250 at Overwatch 2's launch),
        /// trimmed for this game's lower damage.
        /// </summary>
        public const float HealPerSecond = 15.0f;

        public const float TickSeconds = 0.2f;

        public const float HealPerTick = HealPerSecond * TickSeconds;

        /// <summary>The mana it drinks a second while tied to someone (Mercy's beam is free; a staff here pays, a little).</summary>
        public const float ManaPerSecond = 5.0f;

        /// <summary>How often who the beam is on is sent to other players, seconds.</summary>
        public const float AimSendSeconds = 0.1f;

        /// <summary>
        /// How well a body at <paramref name="target"/> is aimed at by a camera at <paramref name="eye"/> looking along <paramref name="look"/>,
        /// as its angle off the crosshair in degrees (lower is better); <see cref="float.MaxValue"/> when it is past the cone or out of reach of
        /// the healer at <paramref name="healer"/>.
        /// </summary>
        public static float LockScore(Vector3 eye, Vector3 look, Vector3 healer, Vector3 target)
        {
            if (Vector3.Distance(healer, target) > LockRange)
            {
                return float.MaxValue;
            }
            float angle = Vector3.Angle(look, target - eye);
            return angle <= LockConeDegrees ? angle : float.MaxValue;
        }

        /// <summary>Whether the beam stays on someone <paramref name="distance"/> away who has been out of sight <paramref name="outOfSightSeconds"/>.</summary>
        public static bool Holds(float distance, float outOfSightSeconds)
        {
            return distance <= BreakRange && outOfSightSeconds <= SightGraceSeconds;
        }
    }
}
