using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Builds the arrow buttons (<see cref="ArrowGraphic"/>, <see cref="ArrowButtonLook"/>): a straight one with its word along the shaft, and
    /// a curled one with its letters following the curl, each in fat white italics edged black (owner, 2026-11-10, from two quick Photoshop
    /// mock-ups: a green PROCEED arrow and a red BACK curling under it). The first of a new style the owner means for every button.
    /// </summary>
    public static class ArrowButtons
    {
        public static readonly Color GreenTop = new Color(0.52f, 0.85f, 0.22f);
        public static readonly Color GreenBottom = new Color(0.26f, 0.58f, 0.05f);
        public static readonly Color RedTop = new Color(1.0f, 0.22f, 0.20f);
        public static readonly Color RedBottom = new Color(0.80f, 0.05f, 0.06f);

        /// <summary>The word's height as a share of the shaft's (straight) or the band's (curled).</summary>
        private const float WordShareOfShaft = 0.66f;

        private const float LetterShareOfBand = 0.72f;

        /// <summary>The black edge round the words, canvas units.</summary>
        private const float WordEdge = 1.2f;

        /// <summary>Where along the curl the first and last letters sit, degrees: inside its ends, clear of the head.</summary>
        private const float FirstLetterDegrees = 112.0f;

        private const float LastLetterDegrees = 46.0f;

        /// <summary>A straight arrow pointing right, <paramref name="word"/> along its shaft.</summary>
        public static Button Straight(Transform parent, string name, string word, Vector2 size, Color top, Color bottom)
        {
            ArrowGraphic arrow = Make(parent, name, size, false, top, bottom, out Button button);
            float back = ArrowShape.StraightLength - ArrowShape.StraightHeadLength;
            float height = 2.0f * ArrowShape.StraightShaftHalfHeight * arrow.Scale;
            Text text = Word(arrow.transform, "Label", word, Mathf.RoundToInt(height * WordShareOfShaft));
            var rect = (RectTransform)text.transform;
            // Over the shaft and a little into the head, which is where the eye reads the middle of an arrow.
            Vector2 from = arrow.ToLocal(new Vector2(0.0f, 0.0f)), to = arrow.ToLocal(new Vector2(back + 0.9f, 0.0f));
            rect.anchoredPosition = 0.5f * (from + to);
            rect.sizeDelta = new Vector2(to.x - from.x, height);
            return button;
        }

        /// <summary>A curled arrow turning back to the left, the letters of <paramref name="word"/> round its curl.</summary>
        public static Button Curled(Transform parent, string name, string word, Vector2 size, Color top, Color bottom)
        {
            ArrowGraphic arrow = Make(parent, name, size, true, top, bottom, out Button button);
            float band = (ArrowShape.CurveOuter - ArrowShape.CurveInner) * arrow.Scale;
            int letterSize = Mathf.RoundToInt(band * LetterShareOfBand);
            for (int i = 0; i < word.Length; i++)
            {
                float degrees = Mathf.Lerp(FirstLetterDegrees, LastLetterDegrees, (i + 0.5f) / word.Length);
                Text letter = Word(arrow.transform, "Letter" + i, word[i].ToString(), letterSize);
                var rect = (RectTransform)letter.transform;
                rect.anchoredPosition = arrow.ToLocal(ArrowShape.Polar(ArrowShape.CurveMiddle, degrees * Mathf.Deg2Rad));
                rect.sizeDelta = new Vector2(band * 1.4f, band * 1.2f);
                rect.localRotation = Quaternion.Euler(0.0f, 0.0f, degrees - 90.0f);   // standing out from the curl's centre
            }
            return button;
        }

        private static ArrowGraphic Make(Transform parent, string name, Vector2 size, bool curled, Color top, Color bottom, out Button button)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var arrow = go.AddComponent<ArrowGraphic>();
            arrow.Curled = curled;
            arrow.Top = top;
            arrow.Bottom = bottom;
            button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = arrow;
            go.AddComponent<ArrowButtonLook>().Arrow = arrow;
            return arrow;
        }

        /// <summary>White, fat, leaning, edged black all round.</summary>
        private static Text Word(Transform parent, string name, string content, int size)
        {
            Text text = UiKit.MakeText(parent, name, content, size, Color.white);
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<Outline>().effectDistance = new Vector2(WordEdge, WordEdge);
            text.gameObject.AddComponent<Outline>().effectDistance = new Vector2(WordEdge * 1.3f, 0.0f);
            text.gameObject.AddComponent<Outline>().effectDistance = new Vector2(0.0f, WordEdge * 1.3f);
            foreach (Outline edge in text.GetComponents<Outline>())
            {
                edge.effectColor = Color.black;
            }
            return text;
        }
    }
}
