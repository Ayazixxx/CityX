using UnityEngine;

namespace CityX.Character
{
    public enum SpinKind
    {
        /// <summary>Feet on the ground: a chargeable spin of four turns, a blow each, that draws its targets along.</summary>
        Ground,

        /// <summary>Airborne: a faster spin that carries the body upward, three blows drawing its targets along and a fourth that throws them limp.</summary>
        Air,
    }

    /// <summary>One scheduled blow of a spin.</summary>
    public readonly struct SpinPulse
    {
        /// <summary>Seconds after the spin starts.</summary>
        public readonly float Time;

        /// <summary>Multiplier on the sword's base damage.</summary>
        public readonly float DamageScale;

        /// <summary>The air spin's last blow: the one that knocks its target back and throws it limp. Every other blow draws it along.</summary>
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
    /// FOUR BLOWS, a damage number each (owner, 2026-11-05: "make it hit 4 times so 4 times the damage number pops up"), drawing the target
    /// along with the spin as Toon Link's and Young Link's do in Smash, rather than Link's one big hit.
    ///
    /// GROUND. Four fast turns in a low crouch, a blow as the blade comes round in front each turn (frames 7, 15, 26, 38), none of them a
    /// launch (owner: the fourth is a plain blow on the ground). Hold the key to CHARGE it for up to 60 frames first, for up to 1.6x the
    /// damage. Committed for 74 frames, 91 for the whole animation.
    ///
    /// AIR. A faster spin while the body RISES: three blows with the blade ahead (frames 8, 16, 31), then the fourth at frame 48 knocks the
    /// target back and throws it limp. 91 frames; landing before it ends costs 30 frames of landing lag; it can be used once per time in
    /// the air.
    ///
    /// THE LOOK is an animation made in Blender from Smash's own hitbox GIFs, frame by frame (CityX/SourceArt/SwordSpin): the body turns
    /// inside it, so the clip's frame f is the move's frame f and the blows land where the blade is.
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

        /// <summary>The ground spin hands the body back this long after it starts (frame 74: interruptible).</summary>
        public static readonly float GroundCommitSeconds = Frames(74.0f);

        /// <summary>
        /// The ground spin's four blows, one each time the blade comes round in front (its turns start on frames 7, 15, 26 and 38). Together
        /// they do what its one clean hit did before (14%), a quarter each: my split, before charge.
        /// </summary>
        public static readonly SpinPulse[] GroundPulses =
        {
            new SpinPulse(Frames(7.0f), 0.35f, false),
            new SpinPulse(Frames(15.0f), 0.35f, false),
            new SpinPulse(Frames(26.0f), 0.35f, false),
            new SpinPulse(Frames(38.0f), 0.35f, false),
        };

        /// <summary>Damage multiplier for having charged for <paramref name="chargeSeconds"/> (1 for none, 1.6 for the full second).</summary>
        public static float ChargeMultiplier(float chargeSeconds)
        {
            return 1.0f + MaxChargeBonus * Mathf.Clamp01(chargeSeconds / MaxChargeSeconds);
        }

        // ---- air ----

        /// <summary>
        /// The air spin's three blows with the blade ahead (frames 8, 16, 31) and the fourth at 48 that throws. Together they do what its eight
        /// blows and launch did before (36%): my split.
        /// </summary>
        public static readonly SpinPulse[] AirPulses =
        {
            new SpinPulse(Frames(8.0f), 0.8f, false),
            new SpinPulse(Frames(16.0f), 0.8f, false),
            new SpinPulse(Frames(31.0f), 0.8f, false),
            new SpinPulse(Frames(48.0f), 1.2f, true),
        };

        /// <summary>The aerial spin turns, rises and is under thinner gravity until here (frame 49, just after the last blow).</summary>
        public static readonly float AirActiveSeconds = Frames(49.0f);

        /// <summary>The body keeps its arm out for a little after the last blow, then lowers it.</summary>
        public static readonly float AirCommitSeconds = Frames(91.0f);

        /// <summary>Landing before the animation ends costs this long (30 frames).</summary>
        public static readonly float AirLandingLagSeconds = Frames(30.0f);

        // ---- both ----

        /// <summary>A spin's blows, in order.</summary>
        public static SpinPulse[] Pulses(SpinKind kind) => kind == SpinKind.Ground ? GroundPulses : AirPulses;

        /// <summary>How long the move keeps the body (until control comes back, the animation permitting).</summary>
        public static float CommitSeconds(SpinKind kind) => kind == SpinKind.Ground ? GroundCommitSeconds : AirCommitSeconds;

        // ---- the animation ----

        /// <summary>The Blender animation, in Resources: the ground spin, the held charge and the air spin.</summary>
        public const string MotionResource = "SwordSpin/SwordSpinMotion";

        public const string GroundSegment = "ground";

        /// <summary>The cocked pose, breathing, looped while a charge is held.</summary>
        public const string ChargeSegment = "ground_hold";

        public const string AirSegment = "air";

        public static string Segment(SpinKind kind) => kind == SpinKind.Ground ? GroundSegment : AirSegment;

        /// <summary>
        /// The ground spin's cocked pose, where a held charge waits (Smash: frames 3-4). A charged spin carries on from here when the key
        /// is let go, so its first blow lands three frames later.
        /// </summary>
        public static readonly float ChargeFrameSeconds = Frames(4.0f);

        /// <summary>Where a ground spin's timeline starts after <paramref name="chargeSeconds"/> of charging: the wind-up already done is not done again.</summary>
        public static float StartSeconds(SpinKind kind, float chargeSeconds) =>
            kind == SpinKind.Ground ? Mathf.Clamp(chargeSeconds, 0.0f, ChargeFrameSeconds) : 0.0f;

        /// <summary>
        /// Light trails from the blade while it sweeps: until the ground spin's last turn has slowed into the lunge (frame 46), and the air
        /// spin's launching cut is overhead (frame 51).
        /// </summary>
        public static float SlashEndSeconds(SpinKind kind) => Frames(kind == SpinKind.Ground ? 46.0f : 51.0f);

        /// <summary>The animation takes the body over in a few frames, and hands it back over the last few before the move lets go.</summary>
        public static readonly float PoseBlendInSeconds = Frames(3.0f);

        public static readonly float PoseBlendOutSeconds = Frames(8.0f);
    }
}
