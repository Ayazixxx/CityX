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
