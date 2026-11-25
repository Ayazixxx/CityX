using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// ONE PRESSING, AS TIME GOES (docs/HEAT_PRESS.md; owner, 2026-11-24: "when initiated, have it clamp down for 3 seconds and then steam puffs
    /// out of it and the shirt is done"): from the press at rest (lid open, drawer in), the drawer slides out for the shirt, back in, the lid comes
    /// down and holds for <see cref="ClampSeconds"/>, lifts with a puff of steam, and the drawer slides out with the shirt done, then home. A pure
    /// function of the seconds since it began, so every machine that knows when a pressing began shows the same moment of it.
    /// </summary>
    public static class HeatPressCycle
    {
        public enum Phase { Resting, LoadingOut, LoadingIn, Closing, Clamped, Opening, Unloading, Returning }

        public const float SlideSeconds = 0.7f;
        public const float CloseSeconds = 0.45f;

        /// <summary>The owner's 3 seconds.</summary>
        public const float ClampSeconds = 3.0f;

        public const float OpenSeconds = 0.6f;

        /// <summary>The shirt lying on the drawer out front, done, before it slides home.</summary>
        public const float ShowSeconds = 1.2f;

        private static readonly float[] Lengths = { SlideSeconds, SlideSeconds, CloseSeconds, ClampSeconds, OpenSeconds, SlideSeconds + ShowSeconds, SlideSeconds };

        public static float TotalSeconds
        {
            get
            {
                float total = 0.0f;
                foreach (float length in Lengths) total += length;
                return total;
            }
        }

        /// <summary>When the steam puffs out: the moment the lid starts up.</summary>
        public static float SteamAt => SlideSeconds * 2.0f + CloseSeconds + ClampSeconds;

        /// <summary>The shirt is done once the steam is out.</summary>
        public static float DoneAt => SteamAt;

        public readonly struct Pose
        {
            public readonly Phase Phase;

            /// <summary>Degrees the lid stands open (<see cref="HeatPressShape.OpenDegrees"/> at rest, 0 clamped).</summary>
            public readonly float LidDegrees;

            /// <summary>Metres the drawer stands out (0 home, <see cref="HeatPressShape.DrawerTravel"/> out).</summary>
            public readonly float DrawerOut;

            public Pose(Phase phase, float lidDegrees, float drawerOut)
            {
                Phase = phase;
                LidDegrees = lidDegrees;
                DrawerOut = drawerOut;
            }
        }

        /// <summary>The press <paramref name="seconds"/> into a pressing; before it and after it, at rest.</summary>
        public static Pose At(float seconds)
        {
            float open = HeatPressShape.OpenDegrees, travel = HeatPressShape.DrawerTravel;
            if (seconds < 0.0f || seconds >= TotalSeconds)
            {
                return new Pose(Phase.Resting, open, 0.0f);
            }
            float t = seconds;
            for (int i = 0; i < Lengths.Length; i++)
            {
                if (t < Lengths[i] || i == Lengths.Length - 1)
                {
                    var phase = (Phase)(i + 1);
                    switch (phase)
                    {
                        case Phase.LoadingOut: return new Pose(phase, open, travel * Ease(t / SlideSeconds));
                        case Phase.LoadingIn: return new Pose(phase, open, travel * (1.0f - Ease(t / SlideSeconds)));
                        case Phase.Closing: return new Pose(phase, open * (1.0f - Ease(t / CloseSeconds)), 0.0f);
                        case Phase.Clamped: return new Pose(phase, 0.0f, 0.0f);
                        case Phase.Opening: return new Pose(phase, open * Ease(t / OpenSeconds), 0.0f);
                        case Phase.Unloading: return new Pose(phase, open, travel * Ease(Mathf.Min(1.0f, t / SlideSeconds)));
                        default: return new Pose(phase, open, travel * (1.0f - Ease(t / SlideSeconds)));
                    }
                }
                t -= Lengths[i];
            }
            return new Pose(Phase.Resting, open, 0.0f);
        }

        /// <summary>Slow out, slow in.</summary>
        private static float Ease(float x) => Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(x));
    }
}
