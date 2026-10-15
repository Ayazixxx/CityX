using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// What makes flying FEEL fast, as pure rules. The jetpack's speeds, lean
    /// and controls are untouched by this; it only decides how much the CAMERA
    /// and the effects around it react to how fast the player is going.
    ///
    /// Researched from how games make speed readable (see docs/FLIGHT.md for
    /// the sources). The consistent findings:
    ///  - Widen the field of view as speed rises: the single cheapest, most
    ///    effective speed cue -- but the wider it is the less precise aiming
    ///    feels, so it is a moderate gain, not a fisheye.
    ///  - The camera hangs on a spring: it falls back a little when you boost
    ///    and lags slightly, instead of being welded to the character.
    ///  - A burst on the boost itself (Lego Batman 3's "super boost", Spider-
    ///    Man's swing release): a brief extra punch that then settles, so the
    ///    boost reads as an EVENT and not just a number going up.
    ///  - Streaks of air rushing past, scaled with speed, and wind that
    ///    quickens with it.
    ///  - Keep it subtle: heavy shake and blur make people ill and hide the
    ///    world they are flying through.
    ///
    /// The numbers are public static so they can be tuned live with the
    /// /flight console command instead of by rebuilding and guessing.
    /// </summary>
    public static class FlightFeel
    {
        /// <summary>Below this speed (m/s) flight feels like ordinary movement: no widening, no streaks.</summary>
        public static float StartSpeed = 6.0f;

        /// <summary>At this speed and above the effects are at full strength. Boosted climbs are 22 m/s.</summary>
        public static float FullSpeed = 22.0f;

        /// <summary>Degrees of extra field of view at full speed.</summary>
        public static float FovGainDegrees = 14.0f;

        /// <summary>Extra degrees the boost punches in for a moment, on top of the speed gain.</summary>
        public static float BoostKickFovDegrees = 6.0f;

        /// <summary>How long the boost punch takes to fade, in seconds.</summary>
        public static float BoostKickSeconds = 0.5f;

        /// <summary>Metres the camera falls back at full speed.</summary>
        public static float PullBackMetres = 1.4f;

        /// <summary>Metres the camera falls back further on the boost punch.</summary>
        public static float BoostKickPullBackMetres = 0.8f;

        /// <summary>Seconds the camera takes to settle toward where the speed says it should be: the spring.</summary>
        public static float SmoothSeconds = 0.3f;

        /// <summary>
        /// Streaks of rushing air, per second, at full speed. 0 turns them off. 40, down from 70 (owner, 2026-10-15: "make the lines ... a little
        /// more subtle"): other games keep speed lines to a few faint streaks at the screen's edges, the middle clear (docs/FLIGHT.md).
        /// </summary>
        public static float MaxStreaksPerSecond = 40.0f;

        /// <summary>The share of the speed range below which there are no streaks at all (0.25 before 2026-10-15).</summary>
        public const float StreakStartFraction = 0.4f;

        /// <summary>Speed as 0..1: nothing below StartSpeed, full at FullSpeed, eased between so it does not switch on.</summary>
        public static float SpeedFraction(float speed)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(StartSpeed, FullSpeed, speed));
            return t * t * (3.0f - 2.0f * t);
        }

        /// <summary>
        /// The boost punch, 0..1, <paramref name="secondsSinceBoost"/> after the boost
        /// began: full at once, easing out to nothing over BoostKickSeconds.
        /// </summary>
        public static float KickEnvelope(float secondsSinceBoost)
        {
            if (secondsSinceBoost < 0.0f || secondsSinceBoost >= BoostKickSeconds || BoostKickSeconds <= 0.0f)
            {
                return 0.0f;
            }
            float remaining = 1.0f - secondsSinceBoost / BoostKickSeconds;
            return remaining * remaining;
        }

        public static float FovBonus(float speedFraction, float kick)
        {
            return FovGainDegrees * speedFraction + BoostKickFovDegrees * kick * Mathf.Max(0.35f, speedFraction);
        }

        public static float DistanceBonus(float speedFraction, float kick)
        {
            return PullBackMetres * speedFraction + BoostKickPullBackMetres * kick * Mathf.Max(0.35f, speedFraction);
        }

        /// <summary>Wind pitch: a little lower and slower when cruising, rising toward full speed.</summary>
        public static float WindPitch(float speedFraction)
        {
            return Mathf.Lerp(0.85f, 1.2f, speedFraction);
        }

        /// <summary>Streaks per second: none below <see cref="StreakStartFraction"/> of the speed range (only really fast flight shows them), then rising.</summary>
        public static float StreakRate(float speedFraction)
        {
            float t = Mathf.Clamp01((speedFraction - StreakStartFraction) / (1.0f - StreakStartFraction));
            return MaxStreaksPerSecond * t * t;
        }
    }
}
