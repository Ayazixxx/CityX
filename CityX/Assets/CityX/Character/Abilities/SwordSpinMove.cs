using UnityEngine;

namespace CityX.Character
{
    public enum SpinKind
    {
        /// <summary>Feet on the ground: one strong, slow, chargeable spin that hurts most at its very start.</summary>
        Ground,

        /// <summary>Airborne: a fast multi-hit spin that carries the body upward and ends in a launching blow.</summary>
        Air,
    }

    /// <summary>One scheduled blow of the aerial spin.</summary>
    public readonly struct SpinPulse
    {
        /// <summary>Seconds after the spin starts.</summary>
        public readonly float Time;

        /// <summary>Multiplier on the sword's base damage.</summary>
        public readonly float DamageScale;

        /// <summary>The last blow: the one that sends its target upward.</summary>
        public readonly bool Final;

        public SpinPulse(float time, float damageScale, bool final)
        {
            Time = time;
            DamageScale = damageScale;
            Final = final;
        }
    }

    /// <summary>
    /// THE SWORD SPIN'S TIMELINE, taken from Link's Spin Attack (his Up Special) in Super Smash Bros. Ultimate -- every number below is
    /// a frame count at 60 fps from SmashWiki / UltimateFrameData, kept as frames so it can be checked against them. See
    /// docs/SWORD_SPIN.md for the sources. Pure functions of time, so none of it needs a scene.
    ///
    /// GROUND. One spin, slow and undulating (its speed swells and dips as it turns), that hits everything once. How hard depends on
    /// WHEN it connects: the first frames are the strongest and it fades to half over the spin (14% -> 12 -> 9 -> 7 in Smash). Hold
    /// the key to CHARGE it for up to 60 frames first, for up to 1.6x the damage. Committed for 74 frames, 91 for the whole animation.
    ///
    /// AIR. A fast spin that hits eight times (alternating front and back, 4% and 2%) across frames 8-38, with a final hit at frame
    /// 48 that launches upward, while the body RISES. 91 frames; landing before it ends costs 30 frames of landing lag; it can be
    /// used once per time in the air.
    /// </summary>
    public static class SwordSpinMove
    {
        public const float Fps = 60.0f;

        public static float Frames(float frames) => frames / Fps;

        // ---- shared ----

        /// <summary>How long the whole animation takes, ground or air (91 frames; the ground one is 94 when fully charged).</summary>
        public static readonly float AnimationSeconds = Frames(91.0f);

        /// <summary>Hitboxes come out at percent of the sword's base damage: Smash's 1% = one tenth of a light swing.</summary>
        public const float PercentToScale = 0.1f;

        // ---- ground ----

        /// <summary>Frames of holding the key that a ground spin can be charged for, at most.</summary>
        public static readonly float MaxChargeSeconds = Frames(60.0f);

        /// <summary>A fully charged ground spin does this much more damage (1 + 0.6 x charge).</summary>
        public const float MaxChargeBonus = 0.6f;

        /// <summary>The first frame, and the last, of a ground spin that hurts.</summary>
        public static readonly float GroundHitStart = Frames(7.0f);

        public static readonly float GroundHitEnd = Frames(39.0f);

        /// <summary>The ground spin hands the body back this long after it starts (frame 74: interruptible).</summary>
        public static readonly float GroundCommitSeconds = Frames(74.0f);

        /// <summary>The ground spin's body turns this long: its hit window (to frame 39) plus the slowing-down after it.</summary>
        public static readonly float GroundTurnSeconds = Frames(50.0f);

        /// <summary>A ground spin turns this many degrees (two revolutions).</summary>
        public const float GroundTotalDegrees = 720.0f;

        /// <summary>How strongly its speed swells and dips (0 = steady; 0.45 = from 55% to 145% of the average) and how many times.</summary>
        public const float GroundUndulation = 0.45f;

        public const int GroundUndulationCycles = 2;

        /// <summary>
        /// The ground spin's damage by when it connects, as a multiplier on the sword's damage (before charge): the clean hit (frames 7-8)
        /// 14%, mid (9-13) 12%, late (14-23) 9%, latest (24-39) 7%. Zero outside the hit window.
        /// </summary>
        public static float GroundDamageScale(float seconds)
        {
            if (seconds < GroundHitStart - 1e-4f || seconds > GroundHitEnd + 1e-4f)
            {
                return 0.0f;
            }
            float frame = seconds * Fps + 1e-3f;
            float percent = frame < 9.0f ? 14.0f : frame < 14.0f ? 12.0f : frame < 24.0f ? 9.0f : 7.0f;
            return percent * PercentToScale;
        }

        /// <summary>Damage multiplier for having charged for <paramref name="chargeSeconds"/> (1 for none, 1.6 for the full second).</summary>
        public static float ChargeMultiplier(float chargeSeconds)
        {
            return 1.0f + MaxChargeBonus * Mathf.Clamp01(chargeSeconds / MaxChargeSeconds);
        }

        // ---- air ----

        /// <summary>
        /// The eight blows (front, back, front, back ... at frames 8, 12, 16, 19, 22, 26, 31, 38: 4% and 2%) and the last one at 48.
        /// The final one is worth more here than Smash's 4%, because in Smash its job is the launch and in this game the launch has
        /// to come with a reason to land it.
        /// </summary>
        public static readonly SpinPulse[] AirPulses =
        {
            new SpinPulse(Frames(8.0f), 0.4f, false),
            new SpinPulse(Frames(12.0f), 0.2f, false),
            new SpinPulse(Frames(16.0f), 0.4f, false),
            new SpinPulse(Frames(19.0f), 0.2f, false),
            new SpinPulse(Frames(22.0f), 0.4f, false),
            new SpinPulse(Frames(26.0f), 0.2f, false),
            new SpinPulse(Frames(31.0f), 0.4f, false),
            new SpinPulse(Frames(38.0f), 0.2f, false),
            new SpinPulse(Frames(48.0f), 1.2f, true),
        };

        /// <summary>The aerial spin turns, rises and is under thinner gravity until here (frame 49, just after the last blow).</summary>
        public static readonly float AirActiveSeconds = Frames(49.0f);

        /// <summary>The body keeps its arm out for a little after the last blow, then lowers it.</summary>
        public static readonly float AirCommitSeconds = Frames(91.0f);

        /// <summary>Landing before the animation ends costs this long (30 frames).</summary>
        public static readonly float AirLandingLagSeconds = Frames(30.0f);

        /// <summary>An aerial spin turns this many degrees (three revolutions, fast).</summary>
        public const float AirTotalDegrees = 1080.0f;

        // ---- both ----

        /// <summary>How long the body turns for.</summary>
        public static float TurnSeconds(SpinKind kind) => kind == SpinKind.Ground ? GroundTurnSeconds : AirActiveSeconds;

        /// <summary>How long the move keeps the body (until control comes back, the animation permitting).</summary>
        public static float CommitSeconds(SpinKind kind) => kind == SpinKind.Ground ? GroundCommitSeconds : AirCommitSeconds;

        public static float TotalDegrees(SpinKind kind) => kind == SpinKind.Ground ? GroundTotalDegrees : AirTotalDegrees;

        /// <summary>
        /// How far round the body has turned <paramref name="seconds"/> into the spin, in degrees. Monotonic, and ends on exactly a
        /// whole number of revolutions so the body finishes facing where it started. Ground: speed swells and dips. Air: fast at the
        /// start, easing off to 70% of the average at the end.
        /// </summary>
        public static float AngleAt(SpinKind kind, float seconds)
        {
            float turn = TurnSeconds(kind);
            float u = Mathf.Clamp01(seconds / turn);
            float total = TotalDegrees(kind);
            if (kind == SpinKind.Ground)
            {
                float phase = 2.0f * Mathf.PI * GroundUndulationCycles;
                return total * (u + GroundUndulation * Mathf.Sin(phase * u) / phase);
            }
            // 0.7 u + 0.3 (1 - (1 - u)^2): speed 1.3 x average at the start, 0.7 x at the end.
            float eased = 0.7f * u + 0.3f * (1.0f - (1.0f - u) * (1.0f - u));
            return total * eased;
        }
    }
}
