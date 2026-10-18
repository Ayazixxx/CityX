using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE STAB DASH'S TIMELINE, taken from the Mii Swordfighter's grounded Power Thrust in Super Smash Bros. Ultimate (UltimateFrameData: the
    /// first hit on frame 12, hits through frame 30, interruptible on frame 59; 15% on frames 12-14, 13% on 15-22, 10% on 23-30), as frame
    /// counts at 60 fps like SwordSpinMove, so it can be checked against them. Pure functions of time: none of it needs a scene.
    ///
    /// Owner, 2026-10-18, with SmashWiki's hitbox GIF of it: "make a hotbar skill for the swordsman called stab dash where it pretty much goes
    /// the same distance as a dash but he does a stab like the gif attached". So THE SLIDE goes the dash's own distance (PlayerLocomotion: 24 m/s
    /// for 0.2 s), shaped the way the GIF's body moves: off at once, slowing all the way, still by frame 35.
    /// </summary>
    public static class StabDashMove
    {
        public const float Fps = 60.0f;

        public static float Frames(float frames) => frames / Fps;

        /// <summary>The first frame the stab hurts, and the last (Smash's 12 and 30).</summary>
        public static readonly float HitStart = Frames(12.0f);

        public static readonly float HitEnd = Frames(30.0f);

        /// <summary>The move holds the body until here (Smash's frame 59, where it can be interrupted), and its animation ends here.</summary>
        public static readonly float CommitSeconds = Frames(59.0f);

        /// <summary>
        /// The stab's damage by when it connects, as a multiplier on the sword's damage (Smash's percent, SwordSpinMove.PercentToScale): the clean
        /// thrust (frames 12-14) 15%, then 13% (15-22), then 10% (23-30). Zero outside the hit window.
        /// </summary>
        public static float DamageScale(float seconds)
        {
            if (seconds < HitStart - 1e-4f || seconds > HitEnd + 1e-4f)
            {
                return 0.0f;
            }
            float frame = seconds * Fps + 1e-3f;
            float percent = frame < 15.0f ? 15.0f : frame < 23.0f ? 13.0f : 10.0f;
            return percent * SwordSpinMove.PercentToScale;
        }

        /// <summary>A clean hit (frames 12-14) throws what can be thrown on along the stab, as Smash's strongest one launches.</summary>
        public static bool IsClean(float seconds) => DamageScale(seconds) > 0.0f && seconds * Fps + 1e-3f < 15.0f;

        // ---- the slide ----

        /// <summary>The frame the body moves off on: the release, a frame before the first hit.</summary>
        public const int SlideStartFrame = 11;

        /// <summary>
        /// How far the GIF's body went on each frame from frame 11 to 35, in its pixels: from frame 12 its back foot, the lunge being held, and
        /// before that the body as a whole (its head moved 78 pixels while the back foot pushed off). The GIF ends on frame 34; the last
        /// frame's step is the slide dying. Only the shape is used: the distance is the dash's.
        /// </summary>
        private static readonly float[] SlidePixels =
        {
            40, 90, 84, 74, 64, 60, 56, 50, 42, 34, 28, 28, 26, 24, 22, 20, 18, 16, 14, 12, 10, 8, 4, 2,
        };

        /// <summary>How far the stab carries the body: exactly a dash's (PlayerLocomotion.DashSpeed for DashSeconds, 4.8 m).</summary>
        public static float SlideMetres => PlayerLocomotion.DashSpeed * PlayerLocomotion.DashSeconds;

        public static float SlideStartSeconds => Frames(SlideStartFrame);

        /// <summary>The body is still from here on.</summary>
        public static float SlideEndSeconds => Frames(SlideStartFrame + SlidePixels.Length);

        /// <summary>How far along the slide the body is <paramref name="seconds"/> into the move, metres: 0 before it moves off, SlideMetres once it stops.</summary>
        public static float SlideDistance(float seconds)
        {
            float frame = seconds * Fps - SlideStartFrame;
            if (frame <= 0.0f)
            {
                return 0.0f;
            }
            float total = 0.0f;
            foreach (float step in SlidePixels)
            {
                total += step;
            }
            float done = 0.0f;
            for (int i = 0; i < SlidePixels.Length; i++)
            {
                if (frame < i + 1)
                {
                    done += SlidePixels[i] * (frame - i);
                    return SlideMetres * done / total;
                }
                done += SlidePixels[i];
            }
            return SlideMetres;
        }

        // ---- the blow ----

        /// <summary>How far ahead of the chest the thrust reaches: the arm straight out and the 2.1 m blade on its line.</summary>
        public const float ReachMetres = 3.0f;

        /// <summary>How far round from straight ahead a target can be and still be run through: a thrust, narrower than a swing's 60.</summary>
        public const float HalfArcDegrees = 35.0f;

        /// <summary>A clean hit's throw: on along the stab, and up.</summary>
        public const float CleanLaunchAlong = 9.0f;

        public const float CleanLaunchUp = 4.0f;

        /// <summary>From the end of one before another can start. Smash has none (the lag is the cost); a little stops one being chained into the next.</summary>
        public const float CooldownSeconds = 0.5f;

        // ---- the animation (made in Blender: CityX/SourceArt/StabDash, played by BakedMotion) ----

        /// <summary>The exported animation, in Resources, and its one segment (frame f is the move's frame f).</summary>
        public const string MotionResource = "StabDash/StabDashMotion";

        public const string Segment = "stab";

        /// <summary>How long the animation takes to take the body from the sword's own stance, and to give it back at the end.</summary>
        public static readonly float PoseBlendInSeconds = Frames(3.0f);

        public static readonly float PoseBlendOutSeconds = Frames(8.0f);

        /// <summary>How fast the body turns to face where the camera looks during the wind-up, and to face the stab when it goes (a snap).</summary>
        public const float WindUpTurnDegreesPerSecond = 720.0f;

        public const float SnapTurnDegreesPerSecond = 7200.0f;
    }
}
