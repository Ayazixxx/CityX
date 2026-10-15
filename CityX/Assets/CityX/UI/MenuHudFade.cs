using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// THE HUD STEPS ASIDE FOR THE C MENU (owner, 2026-10-14: "make the hotbar and portrait in the top left fade away when you open the c menu"):
    /// while the C menu or any page it opens is up -- the Player fly-out's Loadout, Stats, Skills and Items, Quests and Social, the map -- the
    /// portrait frame in the top left and the hotbar in the bottom left fade out over <see cref="FadeSeconds"/>, and back in when it closes.
    ///
    /// Added by PlayerHud.Awake (as the hotbar is), so it needs no rebuilt scene: the portrait frame is found by its name (PlayerHudBuilder's
    /// "StatusFrame") and given a CanvasGroup here; the hotbar reads <see cref="Visibility"/> into its own (it already hides for the chat).
    /// Real time, so a menu that slows the game cannot slow the fade.
    /// </summary>
    public sealed class MenuHudFade : MonoBehaviour
    {
        /// <summary>How long the frame and the bar take to fade all the way out or back in, seconds.</summary>
        public const float FadeSeconds = 0.2f;

        /// <summary>PlayerHudBuilder's name for the portrait, the name plate and its bars.</summary>
        public const string StatusFrameName = "StatusFrame";

        /// <summary>1 with every menu shut, 0 with the C menu or one of its pages up, easing between.</summary>
        public float Visibility { get; private set; } = 1.0f;

        private CanvasGroup _frame;
        private PlayerMenuView _menu;
        private StatsPanelView _stats;
        private LoadoutPanelView _loadout;
        private InventoryPanelView _items;
        private PlaceholderPanelView _placeholder;
        private MapPanelView _map;
        private float _nextSearch;

        /// <summary>Whether the C menu or any page it opens is up.</summary>
        public bool AnyMenuOpen =>
            (_menu != null && _menu.IsOpen) || SkillMenuView.IsAnyOpen ||
            (_stats != null && _stats.IsOpen) || (_loadout != null && _loadout.IsOpen) || (_items != null && _items.IsOpen) ||
            (_placeholder != null && _placeholder.IsOpen) || (_map != null && _map.IsOpen);

        private void Update()
        {
            // The menus are looked for once a second until found (they live in the scene, the HUD can wake first).
            if (Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 1.0f;
                Find();
            }
            float target = AnyMenuOpen ? 0.0f : 1.0f;
            Visibility = Mathf.MoveTowards(Visibility, target, Time.unscaledDeltaTime / FadeSeconds);
            if (_frame != null)
            {
                _frame.alpha = Visibility;
            }
        }

        private void Find()
        {
            if (_frame == null)
            {
                Transform frame = FindDeep(transform, StatusFrameName);
                if (frame != null)
                {
                    _frame = frame.GetComponent<CanvasGroup>();
                    if (_frame == null)
                    {
                        _frame = frame.gameObject.AddComponent<CanvasGroup>();
                    }
                    _frame.blocksRaycasts = false;
                    _frame.interactable = false;
                }
            }
            if (_menu == null) _menu = Object.FindFirstObjectByType<PlayerMenuView>(FindObjectsInactive.Include);
            if (_stats == null) _stats = Object.FindFirstObjectByType<StatsPanelView>(FindObjectsInactive.Include);
            if (_loadout == null) _loadout = Object.FindFirstObjectByType<LoadoutPanelView>(FindObjectsInactive.Include);
            if (_items == null) _items = Object.FindFirstObjectByType<InventoryPanelView>(FindObjectsInactive.Include);
            if (_placeholder == null) _placeholder = Object.FindFirstObjectByType<PlaceholderPanelView>(FindObjectsInactive.Include);
            if (_map == null) _map = Object.FindFirstObjectByType<MapPanelView>(FindObjectsInactive.Include);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
