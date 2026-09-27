using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The resolutions the settings panel offers, ported from
    /// ui/settings_panel.gd's own RESOLUTIONS.
    ///
    /// A SMALL CURATED LIST rather than one derived from the display's own
    /// supported modes -- Godot's own reasoning, kept as-is: it keeps the
    /// dropdown short and predictable across every monitor rather than
    /// trying to enumerate "every resolution this screen supports."
    ///
    /// PURE, so what can be wrong here -- an out-of-range index landing on
    /// nothing, a saved index from a build that had a different list --
    /// is something a test can reach without a window to resize.
    /// </summary>
    public static class DisplaySettings
    {
        public readonly struct Resolution
        {
            public readonly int Width;
            public readonly int Height;

            public Resolution(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public override string ToString() => $"{Width}x{Height}";
        }

        public static readonly Resolution[] Options =
        {
            new Resolution(1280, 720),
            new Resolution(1366, 768),
            new Resolution(1600, 900),
            new Resolution(1920, 1080),
            new Resolution(2560, 1440),
            new Resolution(3840, 2160),
        };

        /// <summary>1920x1080 -- Godot's own default.</summary>
        public const int DefaultIndex = 3;

        /// <summary>
        /// A saved or typed index clamped into range, so a list that shrank
        /// between builds, or a settings file from before this feature
        /// existed, lands on a real option instead of throwing.
        /// </summary>
        public static int Clamp(int index)
        {
            return Mathf.Clamp(index, 0, Options.Length - 1);
        }

        // ---- frame-rate cap ----

        /// <summary>The frame-rate caps on offer, lowest to highest, ending with 0 = no cap.</summary>
        public static readonly int[] FpsOptions = { 30, 60, 90, 120, 144, 165, 240, 0 };

        /// <summary>What a fresh install runs at: no cap, as it always did.</summary>
        public const int DefaultMaxFps = 0;

        /// <summary>How a cap reads on screen.</summary>
        public static string FpsLabel(int fps) => fps <= 0 ? "UNLIMITED" : $"{fps} FPS";

        /// <summary>
        /// The index of the offered cap closest to <paramref name="fps"/> (0 or less is "no cap", the last
        /// entry), so a saved value that is no longer a choice lands on a real one.
        /// </summary>
        public static int FpsIndexFor(int fps)
        {
            if (fps <= 0)
            {
                return FpsOptions.Length - 1;
            }
            int best = 0;
            int bestGap = int.MaxValue;
            for (int i = 0; i < FpsOptions.Length; i++)
            {
                if (FpsOptions[i] <= 0)
                {
                    continue;   // "no cap" is only ever picked by asking for it
                }
                int gap = Mathf.Abs(FpsOptions[i] - fps);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// Puts a cap into force. V-Sync is switched off, or the display's refresh rate would override the cap
        /// (Unity ignores targetFrameRate while V-Sync is on). 0 or less removes the cap.
        /// </summary>
        public static void ApplyMaxFps(int fps)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps <= 0 ? -1 : fps;
        }

        /// <summary>
        /// Puts the SAVED cap into force. Runs once as the game starts, before any scene loads: the settings
        /// screens only apply it when they wake up, and the title screen's starts switched off (nobody has
        /// opened it yet), so without this a saved 60 would not hold until the player visited Settings.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplySavedMaxFps()
        {
            ApplyMaxFps(FpsOptions[FpsIndexFor(CityX.Core.GameSettings.Load().MaxFps)]);
        }
    }
}
