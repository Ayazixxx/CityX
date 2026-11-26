using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// MENUS ON A CONTROLLER (docs/CONTROLLER.md): uGUI already moves between buttons with the D-pad or the left stick and presses one with A,
    /// but only from a button that is chosen, and with a mouse nothing ever is. So while a controller is in use and the pointer is free (a menu
    /// is up), this chooses one whenever none is: the top-left button of the frontmost screen. One for the whole game, made at start-up.
    /// </summary>
    public sealed class PadMenuSelection : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            var keeper = new GameObject(nameof(PadMenuSelection)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(keeper);
            keeper.AddComponent<PadMenuSelection>();
        }

        private void Update()
        {
            EventSystem events = EventSystem.current;
            if (events == null || !GameControls.PadInUse || Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }
            GameObject chosen = events.currentSelectedGameObject;
            if (chosen != null && chosen.activeInHierarchy && chosen.GetComponent<Selectable>() is Selectable still && still.IsInteractable())
            {
                return;
            }
            Selectable first = FirstOnTop();
            if (first != null)
            {
                events.SetSelectedGameObject(first.gameObject);
            }
        }

        /// <summary>The top-left button of the frontmost screen that has any: the highest canvas, then the highest on it, then the leftmost.</summary>
        public static Selectable FirstOnTop()
        {
            Selectable best = null;
            int bestOrder = int.MinValue;
            Vector2 bestAt = Vector2.zero;
            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (selectable == null || !selectable.IsInteractable() || !selectable.isActiveAndEnabled || selectable is InputField || selectable is Scrollbar)
                {
                    continue;
                }
                Canvas canvas = selectable.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled)
                {
                    continue;
                }
                int order = canvas.rootCanvas.sortingOrder;
                Vector2 at = selectable.transform.position;
                bool better = best == null || order > bestOrder
                              || order == bestOrder && (at.y > bestAt.y + 1.0f || Mathf.Abs(at.y - bestAt.y) <= 1.0f && at.x < bestAt.x);
                if (better)
                {
                    best = selectable;
                    bestOrder = order;
                    bestAt = at;
                }
            }
            return best;
        }
    }
}
