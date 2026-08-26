using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The Player rail button's own 5-item fly-out -- Loadout, Stats,
    /// Skills, Items, ReDraw. Stays inside PlayerMenuView's own open
    /// session (see that class's doc comment on why this is the one
    /// destination that does NOT close the C-menu itself); every button
    /// here closes PlayerMenuView and opens its own independent overlay,
    /// exactly like Quests/Social/Map already do from the rail one level up.
    ///
    /// SKILLS IS A PLACEHOLDER ON PURPOSE. No skill tree exists in the
    /// Godot source either -- there's no skill_tree_data.gd, and the
    /// Skills tab there is an empty menu (the UI was built once, then the
    /// actual node content was deliberately left out). Building a real one
    /// in Unity is new design work, not a port, and needs the user's own
    /// direction -- their own wording when asking for this menu ("the
    /// skill tree we haven't made yet... that is all for the player button
    /// for now") explicitly scopes it out of this pass rather than leaving
    /// it silently unbuilt.
    /// </summary>
    public sealed class PlayerSubmenuView : MonoBehaviour
    {
        [Tooltip("The fly-out itself, hidden until Show() is called.")]
        public GameObject Root;

        public Button LoadoutButton;
        public Button StatsButton;
        public Button SkillsButton;
        public Button ItemsButton;
        public Button ReDrawButton;

        [Tooltip("Closed before opening whichever destination panel was clicked.")]
        public PlayerMenuView Menu;

        public LoadoutPanelView Loadout;
        public StatsPanelView Stats;
        public InventoryPanelView Items;
        public PlaceholderPanelView Placeholder;

        [Tooltip("Wired once ReDraw exists (see PlayerRedrawStarter) -- the button is a safe no-op until then.")]
        public PlayerRedrawStarter Redraw;

        public bool IsVisible => Root != null && Root.activeSelf;

        private void Awake()
        {
            if (LoadoutButton != null) LoadoutButton.onClick.AddListener(OnLoadoutClicked);
            if (StatsButton != null) StatsButton.onClick.AddListener(OnStatsClicked);
            if (SkillsButton != null) SkillsButton.onClick.AddListener(OnSkillsClicked);
            if (ItemsButton != null) ItemsButton.onClick.AddListener(OnItemsClicked);
            if (ReDrawButton != null) ReDrawButton.onClick.AddListener(OnRedrawClicked);
            Hide();
        }

        public void Show()
        {
            if (Root != null) Root.SetActive(true);
        }

        public void Hide()
        {
            if (Root != null) Root.SetActive(false);
        }

        private void OnLoadoutClicked()
        {
            Menu?.Close();
            Loadout?.Open();
        }

        private void OnStatsClicked()
        {
            Menu?.Close();
            Stats?.Open();
        }

        private void OnSkillsClicked()
        {
            Menu?.Close();
            Placeholder?.Show("SKILLS", "Coming soon.");
        }

        private void OnItemsClicked()
        {
            Menu?.Close();
            Items?.Open();
        }

        private void OnRedrawClicked()
        {
            if (Redraw == null)
            {
                return;
            }
            Menu?.Close();
            Redraw.Begin();
        }
    }
}
