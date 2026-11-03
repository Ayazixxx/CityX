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

        /// <summary>How quickly a turn eases in and settles from a standstill, seconds (nine tenths of a turn takes about twice this).</summary>
        public const float StandingEaseSeconds = 0.07f;

        /// <summary>At a walk: W to W+A (45 degrees) takes about 0.3 s.</summary>
        public const float WalkEaseSeconds = 0.14f;

        /// <summary>At a sprint.</summary>
        public const float SprintEaseSeconds = 0.2f;

        /// <summary>How fast a body going <paramref name="speed"/> turns at most, degrees per second.</summary>
        public static float DegreesPerSecond(float speed, float walkSpeed, float sprintSpeed)
        {
            return BySpeed(StandingDegreesPerSecond, WalkDegreesPerSecond, SprintDegreesPerSecond, speed, walkSpeed, sprintSpeed);
        }

        /// <summary>
        /// One frame of a player's turn: the yaw after <paramref name="deltaTime"/> on the way to <paramref name="wantedYaw"/>. It builds and settles
        /// (<paramref name="turnVelocity"/>, kept between frames) rather than going at one rate, which took a change of keys in a few frames: a snap.
        /// Not for a brain steering at a spot: it settles into a circle round it (PlayerLocomotion).
        /// </summary>
        public static float Step(float yaw, float wantedYaw, ref float turnVelocity, float speed, float walkSpeed, float sprintSpeed, float deltaTime)
        {
            float ease = BySpeed(StandingEaseSeconds, WalkEaseSeconds, SprintEaseSeconds, speed, walkSpeed, sprintSpeed);
            return Mathf.SmoothDampAngle(yaw, wantedYaw, ref turnVelocity, ease, DegreesPerSecond(speed, walkSpeed, sprintSpeed), deltaTime);
        }

        private static float BySpeed(float standing, float walking, float sprinting, float speed, float walkSpeed, float sprintSpeed)
        {
            if (speed <= walkSpeed)
            {
                return Mathf.Lerp(standing, walking, walkSpeed > 0.0f ? speed / walkSpeed : 1.0f);
            }
            return Mathf.Lerp(walking, sprinting, Mathf.InverseLerp(walkSpeed, sprintSpeed, speed));
        }

        /// <summary>The share of its speed a body keeps while still <paramref name="degreesOff"/> from where the keys point.</summary>
        public static float SpeedShare(float degreesOff)
        {
            return Mathf.Lerp(1.0f, BrakedSpeedShare, Mathf.InverseLerp(BrakeFromDegrees, FullBrakeDegrees, degreesOff));
        }
    }
}
