using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// OCTASLASH's timeline, as frame counts at 60 fps like SwordSpinMove, so it can be checked against Sephiroth's (Smash Ultimate up special,
    /// held: SmashWiki "Sephiroth (SSBU)/Up special"). There the eight cuts land on frames 36, 38, 40, 42, 44, 46, 48 and 50: seven quick light
    /// ones, then a final one three times as strong (2.3% x7, then 7%). Smash's percentages do not carry over (owner: "the percentage thing is
    /// just how smash bros does its damage"); the SHAPE does: seven light cuts two frames apart and a heavy last one.
    ///
    /// Asked for (2026-10-11): a hotbar skill with the sword in hand; pressing it starts a 3 s wind-up, then the dash goes where the player is
    /// looking; a click goes early.
    /// </summary>
    public static class OctaslashMove
    {
        public const float FrameSeconds = 1.0f / 60.0f;

        /// <summary>The wind-up: the dash goes by itself when it runs out, or earlier on a click.</summary>
        public const float ChargeSeconds = 3.0f;

        /// <summary>Frame the dash starts on, in Smash's own count: two frames before the first cut.</summary>
        public const int DashStartFrame = 34;

        /// <summary>Smash's own frames for the eight cuts.</summary>
        public static readonly int[] CutFrames = { 36, 38, 40, 42, 44, 46, 48, 50 };

        /// <summary>
        /// How far the dash carries, metres. Not on SmashWiki; Octaslash goes "slightly farther than Blade Dash", about four of Sephiroth's
        /// heights. Here about five of the 2.6 m body's.
        /// </summary>
        public const float DashMetres = 14.0f;

        /// <summary>The seven light cuts and the final one, as multiples of the sword's own damage (a light swing is 1).</summary>
        public const float LightCutScale = 0.35f;

        public const float FinalCutScale = 1.05f;

        /// <summary>How far round the body each cut reaches: the long blade swept all round it.</summary>
        public const float CutReachMetres = 3.2f;

        /// <summary>The last cut throws what it hits on along the dash, and up.</summary>
        public const float FinalLaunchAlong = 11.0f;

        public const float FinalLaunchUp = 6.0f;

        /// <summary>Held still after the last cut ("heavy endlag" in Smash: 106 frames in all), before the body is free.</summary>
        public const float RecoverySeconds = 0.5f;

        /// <summary>From the end of one before another can start.</summary>
        public const float CooldownSeconds = 2.0f;

        public static int CutCount => CutFrames.Length;

        /// <summary>Seconds from the start of the dash to cut <paramref name="index"/>.</summary>
        public static float CutTime(int index)
        {
            return (CutFrames[index] - DashStartFrame) * FrameSeconds;
        }

        /// <summary>The dash lasts until the last cut.</summary>
        public static float DashSeconds => CutTime(CutCount - 1);

        public static float DashSpeed => DashMetres / DashSeconds;

        public static bool IsFinal(int index) => index == CutCount - 1;

        public static float CutScale(int index) => IsFinal(index) ? FinalCutScale : LightCutScale;

        /// <summary>How many cuts have come due <paramref name="seconds"/> into the dash.</summary>
        public static int CutsDueBy(float seconds)
        {
            int due = 0;
            while (due < CutCount && CutTime(due) <= seconds + 1e-5f)
            {
                due++;
            }
            return due;
        }

        /// <summary>
        /// Which way the body leans while it dashes: its top tipped toward the level part of the dash, so a dash across the ground is a long
        /// lunge, and one straight up or straight down stays upright (feet first down, not head first).
        /// </summary>
        public static Vector3 LeanUp(Vector3 dashDirection)
        {
            Vector3 d = dashDirection.sqrMagnitude > 1e-6f ? dashDirection.normalized : Vector3.zero;
            return (Vector3.up + new Vector3(d.x, 0.0f, d.z) * 1.2f).normalized;
        }
    }
}
