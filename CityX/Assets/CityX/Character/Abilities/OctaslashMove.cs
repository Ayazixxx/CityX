using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// OCTASLASH's timeline, as frame counts at 60 fps like SwordSpinMove, so it can be checked against Sephiroth's (Smash Ultimate up special,
    /// held: SmashWiki "Sephiroth (SSBU)/Up special"). There the eight cuts land on frames 36, 38, 40, 42, 44, 46, 48 and 50: seven quick light
    /// ones, then a final one three times as strong (2.3% x7, then 7%). Smash's percentages do not carry over (owner: "the percentage thing is
    /// just how smash bros does its damage"); the SHAPE does: seven light cuts two frames apart and a heavy last one.
    ///
    /// Asked for (2026-10-11): a hotbar skill with the sword in hand; pressing it starts a 3 s wind-up (2.5 s since 2026-10-12, owner: "shorten charge time ... to 2.5 seconds"), then the dash goes where the player is
    /// looking; a click goes early.
    /// </summary>
    public static class OctaslashMove
    {
        public const float FrameSeconds = 1.0f / 60.0f;

        /// <summary>The wind-up: the dash goes by itself when it runs out, or earlier on a click.</summary>
        public const float ChargeSeconds = 2.5f;

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

        // ---- THE ANIMATION (made in Blender: CityX/SourceArt/Octaslash, played by BakedMotion) ----

        /// <summary>The exported animation, in Resources.</summary>
        public const string MotionResource = "Octaslash/OctaslashMotion";

        /// <summary>Its segments: the coil (once), the hold (looped while the wind-up lasts), the dash, the follow-through.</summary>
        public const string CoilSegment = "coil_in";

        public const string HoldSegment = "hold";

        public const string DashSegment = "dash";

        public const string RecoverSegment = "recover";

        /// <summary>How long the animation takes to take the body from the sword's own stance, and to give it back after the follow-through.</summary>
        public const float PoseBlendInSeconds = 0.12f;

        public const float PoseBlendOutSeconds = 0.2f;

        /// <summary>
        /// The middle of the lunging body, in its own frame: where the animation's level dash has its hips. The body is turned about this point to
        /// the real dash's pitch.
        /// </summary>
        public static readonly Vector3 FlightPivot = new Vector3(0.0f, 1.1f, 0.0f);

        /// <summary>How fast the body turns to face where the wind-up is aimed, and to face the dash when it goes (a snap).</summary>
        public const float WindUpTurnDegreesPerSecond = 540.0f;

        public const float SnapTurnDegreesPerSecond = 7200.0f;

        /// <summary>
        /// How long into the follow-through the blade still trails light: the last cut carries on down through the animation's first three
        /// frames of it (octaslash_pose.py FINAL_CUT_FOLLOW_FRAMES), and one more.
        /// </summary>
        public const float TrailAfterDashSeconds = 4.0f * FrameSeconds;
    }
}
