using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Tells the game's E key (InteractKey) when a text box has the keyboard (the chat, the console, a name being typed), so an E typed there
    /// is a letter and picks nothing up (bug hunt, 2026-11-06).
    /// </summary>
    public static class TypingFocus
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            InteractKey.IsTyping = AnyTextBoxFocused;
        }

        /// <summary>Whether the selected UI object is a text box taking typing now.</summary>
        public static bool AnyTextBoxFocused()
        {
            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }
            var field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }
    }
}
