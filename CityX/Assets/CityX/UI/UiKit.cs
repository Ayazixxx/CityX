using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>Small pieces every panel built in code needs, whether an editor builder bakes it or the game builds it during play.</summary>
    public static class UiKit
    {
        /// <summary>
        /// Adds an EventSystem driven by the Input System when the scene has none. Without one no button on a panel ever
        /// receives a click; the panels that need it each ask, and the first one adds it.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>Makes the rect fill its parent exactly.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Puts <paramref name="text"/> in a text box with the caret after it, ready to type on (a line brought back from InputHistory).</summary>
        public static void SetTextCaretAtEnd(InputField field, string text)
        {
            if (field == null)
            {
                return;
            }
            field.text = text ?? string.Empty;
            int end = field.text.Length;
            field.caretPosition = end;
            field.selectionAnchorPosition = end;
            field.selectionFocusPosition = end;
        }

        /// <summary>A new empty rect under <paramref name="parent"/>.</summary>
        public static RectTransform NewChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Centred on its parent's middle, at <paramref name="at"/> design pixels, <paramref name="size"/> big.</summary>
        public static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        /// <summary>A flat-coloured image that clicks pass through.</summary>
        public static Image MakeImage(string name, RectTransform parent, Color colour)
        {
            Image image = NewChild(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A texture shown as it is, that clicks pass through.</summary>
        public static RawImage MakeRaw(string name, RectTransform parent, Texture texture, Color colour)
        {
            RawImage image = NewChild(name, parent).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A bold label in the built-in font that clicks pass through, never wrapped.</summary>
        public static Text MakeLabel(string name, RectTransform parent, string content, int size, TextAnchor alignment, Color colour)
        {
            Text text = NewChild(name, parent).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        /// <summary>A flat button with its word across it, placed on its parent's middle.</summary>
        public static Button MakeButton(RectTransform parent, string label, Vector2 at, Vector2 size, Color face, Color ink, UnityEngine.Events.UnityAction onClick)
        {
            Image image = MakeImage(label + "Button", parent, face);
            image.raycastTarget = true;
            Place(image.rectTransform, at, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            Text text = MakeLabel("Label", image.rectTransform, label, 15, TextAnchor.MiddleCenter, ink);
            Stretch(text.rectTransform);
            return button;
        }

        /// <summary>A label in the built-in font that clicks pass through, sized and placed by the caller.</summary>
        public static Text MakeText(Transform parent, string name, string content, int size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }
    }
}
