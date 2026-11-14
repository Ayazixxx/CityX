using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// THE PICTURE A MACHINE STARTS ON, before its player has chosen anything (docs/PERFORMANCE_WEAK_PC.md; owner, 2026-11-13: "My friend
    /// played the game on a weaker machine and it was experiencing performance issues which it shouldn't"). Every machine used to start on
    /// HIGH at its screen's full resolution, which is the owner's own laptop's setting. Now the first launch looks at the graphics card: one
    /// built into the processor starts on LOW with fewer pixels, a small card on MEDIUM, a card like the owner's on HIGH as before. Only a
    /// starting point: the settings' graphics page changes any of it, and once anything is saved this is never consulted again.
    ///
    /// The steps come from the benchmark (CityX/Builds/Bench): an RTX 3070 Laptop (8 GB) draws HIGH at 2560 x 1600 in 8.4 ms and with
    /// every switch off in 2.3 ms. A 4 GB card is about a third as fast, a 6 GB one about two thirds, and a built-in one a tenth or less.
    /// </summary>
    public static class HardwareDefaults
    {
        /// <summary>Cards with less graphics memory than this, in MB, are taken as built-in or very old: LOW.</summary>
        public const int SmallCardMegabytes = 3000;

        /// <summary>Under this, a 4 GB card: MEDIUM.</summary>
        public const int MiddleCardMegabytes = 5000;

        /// <summary>Under this, a 6 GB card: HIGH on a 1080p screen, MEDIUM on a bigger one. From here up, HIGH.</summary>
        public const int LargeCardMegabytes = 7000;

        /// <summary>A screen of more pixels than this is "bigger than 1080p" (1920 x 1200 still counts as 1080p).</summary>
        public const int Pixels1080p = 1920 * 1200;

        public const int Pixels1440p = 2560 * 1600;

        /// <summary>Names a graphics card built into the processor is known by (Intel's, and AMD's on its laptop and desktop chips).</summary>
        private static readonly string[] BuiltInNames =
        {
            "Intel(R) HD", "Intel(R) UHD", "Intel(R) Iris", "Intel HD", "Intel UHD", "Intel Iris", "Radeon(TM) Graphics", "Radeon Graphics",
            "Radeon(TM) Vega", "Radeon Vega", "Microsoft Basic",
        };

        /// <summary>True for a graphics card built into the processor: it reports the computer's own memory as its own, so its name is what tells.</summary>
        public static bool IsBuiltIn(string graphicsCardName)
        {
            if (string.IsNullOrEmpty(graphicsCardName))
            {
                return false;
            }
            foreach (string name in BuiltInNames)
            {
                if (graphicsCardName.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The options a machine starts on: its graphics card's name and memory (MB), and its screen's pixels. A card that says nothing of
        /// itself (0 MB: no graphics device, a test) is left on HIGH, the game as it is made.
        /// </summary>
        public static GraphicsOptions For(string graphicsCardName, int graphicsMemoryMegabytes, int screenWidth, int screenHeight)
        {
            long pixels = (long)Mathf.Max(0, screenWidth) * Mathf.Max(0, screenHeight);
            var options = new GraphicsOptions();
            if (graphicsMemoryMegabytes <= 0)
            {
                return options;
            }
            if (IsBuiltIn(graphicsCardName) || graphicsMemoryMegabytes < SmallCardMegabytes)
            {
                options.Use(GraphicsPreset.Low);
                options.ResolutionScale = pixels > Pixels1080p ? 50 : 70;
            }
            else if (graphicsMemoryMegabytes < MiddleCardMegabytes)
            {
                options.Use(GraphicsPreset.Medium);
                options.ResolutionScale = pixels > Pixels1440p ? 50 : pixels > Pixels1080p ? 70 : 100;
            }
            else if (graphicsMemoryMegabytes < LargeCardMegabytes)
            {
                options.Use(pixels > Pixels1080p ? GraphicsPreset.Medium : GraphicsPreset.High);
                options.ResolutionScale = pixels > Pixels1440p ? 70 : 100;
            }
            else
            {
                options.Use(GraphicsPreset.High);
            }
            return options.Clean();
        }
    }
}
