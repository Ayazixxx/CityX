using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Builds the hair colour card down the RIGHT of a creator screen -- for the title screen's
    /// creator (CharacterCreatorBuilder, at edit time) and the in-game ReDraw creator
    /// (RedrawCreatorRuntime, at runtime), so the two cannot drift apart.
    ///
    /// The right, because the left column is full: paint picker, tools, brush, and the hairstyle
    /// row down to the hint text, with a bar under it. A colour picker needs a square, a strip and
    /// a set of swatches, and there is nowhere in that column to put one.
    ///
    /// It is authored in canvas units on a 1152x648 reference, like everything else on these
    /// screens.
    /// </summary>
    public static class HairColourPanel
    {
        public const float Width = 200.0f;

        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);

        /// <summary>
        /// Hair colours a character might actually want, natural first and then dyed. The picker
        /// reaches every other colour; these are for the common ones in one click.
        /// </summary>
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

        /// <summary>Builds the card under <paramref name="parent"/> and returns its controls.</summary>
        public static HairColourControls Build(Transform parent, CharacterCreator creator)
        {
            var card = new GameObject("HairColourCard", typeof(RectTransform));
            var cardRect = (RectTransform)card.transform;
            cardRect.SetParent(parent, false);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(1.0f, 1.0f);
            cardRect.pivot = new Vector2(1.0f, 1.0f);
            cardRect.anchoredPosition = new Vector2(-14.0f, -14.0f);
            cardRect.sizeDelta = new Vector2(Width, 298.0f);

            card.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
            CanvasGroup group = card.AddComponent<CanvasGroup>();

            Transform t = card.transform;
            MakeLabel(t, "HairColourLabel", "HAIR COLOUR", 13, Paper, new Vector2(0.0f, -20.0f), 18.0f);

            // The picker component sits on the square: that is the thing that takes the clicks.
            RawImage square = MakeSquare(t, new Vector2(0.0f, -100.0f), 128.0f);
            HairColourControls controls = square.gameObject.AddComponent<HairColourControls>();
            controls.Creator = creator;
            controls.Card = group;

            controls.Hue = MakeHueSlider(t, new Vector2(0.0f, -178.0f), 150.0f);
            controls.Preview = MakeBar(t, "Preview", new Vector2(0.0f, -200.0f), new Vector2(150.0f, 16.0f), Color.white);

            const int perRow = 6;
            const float pitch = 28.0f;
            const float top = -232.0f;
            for (int i = 0; i < Swatches.Length; i++)
            {
                MakeSwatchButton(t, i, new Vector2(
                    (i % perRow) * pitch - pitch * (perRow - 1) * 0.5f,
                    top - (i / perRow) * pitch), Swatches[i], creator, controls);
            }

            return controls;
        }

        private static RawImage MakeSquare(Transform parent, Vector2 position, float size)
        {
            var go = new GameObject("HairSVSquare", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);

            var image = go.AddComponent<RawImage>();
            image.texture = CreatorControls.BuildSquare(0.0f);
            return image;
        }

        /// <summary>The hue slider: the rainbow drawn behind the handle, the slider's own fill left invisible.</summary>
        private static Slider MakeHueSlider(Transform parent, Vector2 position, float width)
        {
            var go = new GameObject("HairHueSlider", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 16.0f);

            var background = go.AddComponent<RawImage>();
            background.texture = CreatorControls.BuildHueStrip();

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillArea.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(6.0f, 20.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.value = 0.0f;
            return slider;
        }

        private static Image MakeBar(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static void MakeSwatchButton(Transform parent, int index, Vector2 position, Color colour,
            CharacterCreator creator, HairColourControls controls)
        {
            var go = new GameObject($"HairSwatch{index}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(26.0f, 26.0f);

            Image image = go.AddComponent<Image>();
            image.color = colour;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var wiring = go.AddComponent<ButtonAction>();
            wiring.Action = ButtonAction.Kind.CreatorHairColour;
            wiring.Creator = creator;
            wiring.Colour = colour;
            // So the square, the strip and the preview follow a swatch press.
            wiring.HairControls = controls;
        }

        private static void MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(Width - 16.0f, height);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = content;
        }
    }
}
