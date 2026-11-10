using System;
using CityX.Core;

namespace CityX.UI
{
    /// <summary>
    /// The graphics page's rows (docs/GRAPHICS_SETTINGS.md): each a name, the choices it steps through with its arrows, and what it does and costs
    /// in a line for the player. Pure, so a test can step every row without a screen; <see cref="GraphicsSettingsPanel"/> shows them.
    /// </summary>
    public static class GraphicsMenu
    {
        public sealed class Row
        {
            public readonly string Name;
            public readonly string[] Choices;

            /// <summary>How many of <see cref="Choices"/> the arrows step through (the preset's CUSTOM is shown, never picked).</summary>
            public readonly int Steps;

            public readonly string Description;
            private readonly Func<GraphicsOptions, int> _get;
            private readonly Action<GraphicsOptions, int> _set;

            public Row(string name, string[] choices, string description, Func<GraphicsOptions, int> get, Action<GraphicsOptions, int> set,
                int steps = 0)
            {
                Name = name;
                Choices = choices;
                Steps = steps > 0 ? steps : choices.Length;
                Description = description;
                _get = get;
                _set = set;
            }

            public int Get(GraphicsOptions options) => _get(options);

            public void Set(GraphicsOptions options, int choice) => _set(options, choice);
        }

        public const int PresetRow = 0;

        public static readonly Row[] Rows =
        {
            new Row("QUALITY PRESET", new[] { "LOW", "MEDIUM", "HIGH", "CUSTOM" },
                "Sets every option below at once (not the resolution scale). HIGH is the game as it was made; LOW runs fastest.",
                options => (int)options.Preset, (options, choice) => options.Use((GraphicsPreset)choice), steps: 3),
            new Row("RESOLUTION SCALE", new[] { "100%", "85%", "70%", "50%" },
                "Draws fewer pixels and stretches them to fill the screen, sharpened. The biggest saving on a slow graphics card; " +
                "the picture gets softer.",
                options => Array.IndexOf(GraphicsOptions.ResolutionScaleChoices, options.ResolutionScale),
                (options, choice) => options.ResolutionScale = GraphicsOptions.ResolutionScaleChoices[choice]),
            new Row("SHADOWS", new[] { "LOW", "MEDIUM", "HIGH" },
                "How sharp the sun's shadows are, near and far. Cost: small to medium.",
                options => (int)options.Shadows, (options, choice) => options.Shadows = (ShadowDetail)choice),
            new Row("ANTI-ALIASING", new[] { "OFF", "2X", "4X" },
                "Smooths the jagged edges of buildings and lines. Cost: medium.",
                options => Array.IndexOf(GraphicsOptions.AntiAliasingChoices, options.AntiAliasing),
                (options, choice) => options.AntiAliasing = GraphicsOptions.AntiAliasingChoices[choice]),
            new Row("AMBIENT OCCLUSION", OffOn,
                "Darkens corners, creases and where things meet the ground. Cost: medium.",
                options => options.AmbientOcclusion ? 1 : 0, (options, choice) => options.AmbientOcclusion = choice == 1),
            new Row("LIGHT SHAFTS", OffOn,
                "Sunbeams through the haze between the towers, by day. Cost: medium.",
                options => options.LightShafts ? 1 : 0, (options, choice) => options.LightShafts = choice == 1),
            new Row("REFLECTIONS", OffOn,
                "The city mirrored in wet streets. Costs nothing until it rains, then a little.",
                options => options.Reflections ? 1 : 0, (options, choice) => options.Reflections = choice == 1),
        };

        private static string[] OffOn => new[] { "OFF", "ON" };

        /// <summary>The row's choice one step along (wrapping round, like the other settings' arrows).</summary>
        public static void Step(GraphicsOptions options, int row, int direction)
        {
            Row stepped = Rows[row];
            int at = stepped.Get(options);
            int count = stepped.Steps;
            stepped.Set(options, ((at + direction) % count + count) % count);
        }

        /// <summary>What the row shows for <paramref name="options"/>.</summary>
        public static string ValueOf(GraphicsOptions options, int row)
        {
            Row shown = Rows[row];
            int at = shown.Get(options);
            return at >= 0 && at < shown.Choices.Length ? shown.Choices[at] : "?";
        }
    }
}
