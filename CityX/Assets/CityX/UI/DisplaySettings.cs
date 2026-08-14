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
    }
}
