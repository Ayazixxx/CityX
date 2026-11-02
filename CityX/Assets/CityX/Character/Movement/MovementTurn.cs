using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// GTA-STYLE TURNING (docs/MOVEMENT.md; owner, 2026-11-01: "dynamic movement that feels good", "lets do gta style first"): out of an aim or a
    /// lock-on the body turns toward where the keys point and goes where it faces. How fast it turns falls with speed, so a walk turns on the spot
    /// and a sprint curves round; a turn far off where the body is going brakes it first, so a reversal plants and turns instead of skating round.
    /// </summary>
    public static class MovementTurn
    {
        /// <summary>From a standstill, degrees per second: an about-face in a fifth of a second.</summary>
        public const float StandingDegreesPerSecond = 900.0f;

        /// <summary>At a walk.</summary>
        public const float WalkDegreesPerSecond = 600.0f;

        /// <summary>At a sprint: a right angle takes 0.3 s, an arc about 1.4 m across at 7.5 m/s.</summary>
        public const float SprintDegreesPerSecond = 300.0f;

        /// <summary>A turn this far off the way the body faces starts to brake it, degrees.</summary>
        public const float BrakeFromDegrees = 60.0f;

        /// <summary>A turn this far off brakes it fully, to <see cref="BrakedSpeedShare"/>.</summary>
        public const float FullBrakeDegrees = 160.0f;

        public const float BrakedSpeedShare = 0.35f;

        /// <summary>How fast a body going <paramref name="speed"/> turns, degrees per second.</summary>
        public static float DegreesPerSecond(float speed, float walkSpeed, float sprintSpeed)
        {
            if (speed <= walkSpeed)
            {
                return Mathf.Lerp(StandingDegreesPerSecond, WalkDegreesPerSecond, walkSpeed > 0.0f ? speed / walkSpeed : 1.0f);
            }
            return Mathf.Lerp(WalkDegreesPerSecond, SprintDegreesPerSecond, Mathf.InverseLerp(walkSpeed, sprintSpeed, speed));
        }

        /// <summary>The share of its speed a body keeps while still <paramref name="degreesOff"/> from where the keys point.</summary>
        public static float SpeedShare(float degreesOff)
        {
            return Mathf.Lerp(1.0f, BrakedSpeedShare, Mathf.InverseLerp(BrakeFromDegrees, FullBrakeDegrees, degreesOff));
        }
    }
}
