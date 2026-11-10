using System;
using UnityEngine;

namespace CityX.Core
{
    public enum GraphicsPreset
    {
        Low,
        Medium,
        High,

        /// <summary>The options match none of the presets: the player set them one by one.</summary>
        Custom,
    }

    public enum ShadowDetail
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// How good the picture is against how fast it draws (docs/GRAPHICS_SETTINGS.md): the effects that cost the most, each one the player can
    /// switch, and three presets that set them together. The resolution scale is the player's own and no preset touches it. Saved with the
    /// other settings (<see cref="GameSettingsData.Graphics"/>); a settings file from before this existed loads as <see cref="GraphicsPreset.High"/>,
    /// the game as it was made.
    /// </summary>
    [Serializable]
    public sealed class GraphicsOptions
    {
        public ShadowDetail Shadows = ShadowDetail.High;

        /// <summary>Anti-aliasing: samples a pixel (MSAA), 1 for none.</summary>
        public int AntiAliasing = 4;

        public bool AmbientOcclusion = true;

        public bool LightShafts = true;

        /// <summary>The wet streets mirror the city in the rain (docs/WET_STREETS.md).</summary>
        public bool Reflections = true;

        /// <summary>The share of the window's pixels drawn, in percent; the picture is stretched (and sharpened) to fill the window.</summary>
        public int ResolutionScale = 100;

        public static readonly int[] AntiAliasingChoices = { 1, 2, 4 };

        public static readonly int[] ResolutionScaleChoices = { 100, 85, 70, 50 };

        /// <summary>A preset's options (Custom is High), at full resolution.</summary>
        public static GraphicsOptions Of(GraphicsPreset preset)
        {
            var options = new GraphicsOptions();
            options.Use(preset);
            return options;
        }

        /// <summary>Sets every option a preset covers (all but the resolution scale); Custom changes nothing.</summary>
        public void Use(GraphicsPreset preset)
        {
            switch (preset)
            {
                case GraphicsPreset.Low:
                    Shadows = ShadowDetail.Low;
                    AntiAliasing = 1;
                    AmbientOcclusion = false;
                    LightShafts = false;
                    Reflections = false;
                    break;
                case GraphicsPreset.Medium:
                    Shadows = ShadowDetail.Medium;
                    AntiAliasing = 2;
                    AmbientOcclusion = true;
                    LightShafts = false;
                    Reflections = false;
                    break;
                case GraphicsPreset.High:
                    Shadows = ShadowDetail.High;
                    AntiAliasing = 4;
                    AmbientOcclusion = true;
                    LightShafts = true;
                    Reflections = true;
                    break;
            }
        }

        /// <summary>The preset these options are, or Custom when they match none.</summary>
        public GraphicsPreset Preset
        {
            get
            {
                for (var preset = GraphicsPreset.Low; preset < GraphicsPreset.Custom; preset++)
                {
                    GraphicsOptions made = Of(preset);
                    if (Shadows == made.Shadows && AntiAliasing == made.AntiAliasing && AmbientOcclusion == made.AmbientOcclusion &&
                        LightShafts == made.LightShafts && Reflections == made.Reflections)
                    {
                        return preset;
                    }
                }
                return GraphicsPreset.Custom;
            }
        }

        /// <summary>A copy with every value one the menu offers: an older game's file, or one edited by hand, may hold others.</summary>
        public GraphicsOptions Clean()
        {
            var clean = (GraphicsOptions)MemberwiseClone();
            clean.Shadows = (ShadowDetail)Mathf.Clamp((int)Shadows, (int)ShadowDetail.Low, (int)ShadowDetail.High);
            clean.AntiAliasing = Nearest(AntiAliasingChoices, AntiAliasing);
            clean.ResolutionScale = Nearest(ResolutionScaleChoices, ResolutionScale);
            return clean;
        }

        private static int Nearest(int[] choices, int value)
        {
            int best = choices[0];
            foreach (int choice in choices)
            {
                if (Mathf.Abs(choice - value) < Mathf.Abs(best - value))
                {
                    best = choice;
                }
            }
            return best;
        }
    }
}
