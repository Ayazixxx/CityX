using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The creator's HAIR tab (docs/CHARACTER_CREATOR.md): the hairstyles as pictures, a plain white head wearing each (HairIcons),
    /// and the hair colour under them, for the title screen's creator and ReDraw's alike. The style the head wears is framed in gold
    /// (HairStyleTiles). The colour is hidden while the head is bald (HairColourControls). Laid out in canvas units from the top of
    /// the creator's column (CreatorKit.ColumnWidth wide), under its tabs.
    /// </summary>
    public static class HairPage
    {
        /// <summary>Hair colours a character might actually want, natural first and then dyed; the picker reaches every other colour.</summary>
        public static readonly Color[] Swatches =
        {
            new Color(0.05f, 0.05f, 0.06f),   // black
            new Color(0.16f, 0.10f, 0.06f),   // dark brown
            new Color(0.32f, 0.20f, 0.10f),   // brown
            new Color(0.45f, 0.17f, 0.10f),   // auburn
            new Color(0.75f, 0.35f, 0.12f),   // ginger
            new Color(0.75f, 0.55f, 0.15f),   // golden blond (the default)
            new Color(0.90f, 0.82f, 0.55f),   // pale blond
            new Color(0.55f, 0.55f, 0.58f),   // grey
            new Color(0.95f, 0.95f, 0.97f),   // white
            new Color(0.90f, 0.45f, 0.65f),   // pink
            new Color(0.20f, 0.40f, 0.85f),   // blue
            new Color(0.50f, 0.30f, 0.75f),   // purple
        };

        public const float StyleLabelY = -96.0f, GridTopY = -110.0f;

        private const float ColourLabelY = -346.0f, SwatchPitch = 26.0f, SwatchSize = 24.0f;

        private const int SwatchesPerRow = 4;

        /// <summary>The colour pickers' width: the column less a margin each side.</summary>
        private const float PickerWidth = 216.0f;

        /// <summary>The page's contents, under <paramref name="page"/>.</summary>
        public static HairColourControls Build(Transform page, CharacterCreator creator)
        {
            CreatorKit.MakeLabel(page, "HairStyleLabel", "STYLE", 13, CreatorKit.Paper, new Vector2(0.0f, StyleLabelY), 18.0f);
            BuildStyleGrid(page, creator);

            var card = new GameObject("HairColourCard", typeof(RectTransform));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(page, false);
            UiKit.Stretch(cardRect);
            CanvasGroup group = card.AddComponent<CanvasGroup>();
            Transform t = card.transform;

            CreatorKit.MakeLabel(t, "HairColourLabel", "COLOUR", 13, CreatorKit.Paper, new Vector2(0.0f, ColourLabelY), 18.0f);

            // The square on the left, the swatches in a block beside it. The picker component sits on the square: that is the thing that
            // takes the clicks.
            RawImage square = CreatorKit.MakeSquare(t, "HairSVSquare", new Vector2(-58.0f, -408.0f), 100.0f);
            HairColourControls controls = square.gameObject.AddComponent<HairColourControls>();
            controls.Creator = creator;
            controls.Card = group;

            for (int i = 0; i < Swatches.Length; i++)
            {
                var at = new Vector2(18.0f + (i % SwatchesPerRow) * SwatchPitch, -370.0f - (i / SwatchesPerRow) * SwatchPitch);
                MakeSwatchButton(t, i, at, Swatches[i], creator, controls);
            }

            controls.Hue = CreatorKit.MakeHueSlider(t, "HairHueSlider", new Vector2(0.0f, -474.0f), PickerWidth);
            controls.Preview = CreatorKit.MakeSwatch(t, "Preview", new Vector2(0.0f, -494.0f), new Vector2(PickerWidth, 10.0f), Color.white);

            CreatorKit.MakeLabel(page, "HairHintLabel", "Drag the body to turn it.\nScroll over it to zoom.", 10,
                CreatorKit.Hint, new Vector2(0.0f, -526.0f), 30.0f);
            return controls;
        }

        /// <summary>
        /// Every style as a tile in a window four rows tall that scrolls past that (CreatorPageKit). Each is named Hair{style}Button and selects the
        /// style of its index in CharacterHair.ValidStyles; the worn one is framed in gold (HairStyleTiles).
        /// </summary>
        private static void BuildStyleGrid(Transform page, CharacterCreator creator)
        {
            int count = CharacterHair.ValidStyles.Length;
            Transform tiles = CreatorPageKit.MakeTileWindow(page, "HairStyleGrid", GridTopY, count);
            var frames = new Image[count];
            var tints = new RawImage[count];
            for (int i = 0; i < count; i++)
            {
                string style = CharacterHair.ValidStyles[i];
                Image frame = CreatorPageKit.MakeTile(tiles, "Hair" + style + "Button", i);
                Button button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = frame;
                ColorBlock colours = button.colors;
                colours.highlightedColor = CreatorKit.Paper;
                colours.pressedColor = CreatorKit.Gold;
                button.colors = colours;
                var wiring = frame.gameObject.AddComponent<ButtonAction>();
                wiring.Action = ButtonAction.Kind.CreatorHair;
                wiring.Creator = creator;
                wiring.Value = i;
                // No picture baked for a style yet (CityX/Bake Hair Icons): its short name, so it can still be picked.
                CreatorPageKit.ShowPicture(frame.transform, HairIcons.For(style), CharacterHair.LabelFor(style));
                // The hair over the picture, in the hair colour (HairStyleTiles).
                tints[i] = CreatorPageKit.AddLayer(frame.transform, "HairTint", HairIcons.HairOf(style));
                frames[i] = frame;
            }
            HairStyleTiles selection = tiles.parent.gameObject.AddComponent<HairStyleTiles>();
            selection.Creator = creator;
            selection.Frames = frames;
            selection.Tints = tints;
        }

        private static void MakeSwatchButton(Transform parent, int index, Vector2 position, Color colour, CharacterCreator creator,
            HairColourControls controls)
        {
            Image image = CreatorPageKit.MakeRect<Image>(parent, $"HairSwatch{index}", position, new Vector2(SwatchSize, SwatchSize));
            image.color = colour;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var wiring = image.gameObject.AddComponent<ButtonAction>();
            wiring.Action = ButtonAction.Kind.CreatorHairColour;
            wiring.Creator = creator;
            wiring.Colour = colour;
            wiring.HairControls = controls;   // so the square, the strip and the preview follow a swatch press
        }
    }
}
