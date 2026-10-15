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
    /// SKILLS OPENS THE SKILL MENU (SkillMenuView, 2026-10-14): every tree
    /// side by side on one big map, learning, and the bar. New design work
    /// rather than a port -- Godot's Skills tab was an empty menu -- built
    /// to the owner's plan (docs/SKILLS.md).
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
            // The body this machine controls (a player who joined a server
            // has their own), else the one the C menu was built for.
            PlayerSkills skills = PlayerSkills.Local;
            if (skills == null && Menu != null && Menu.Player != null)
            {
                skills = PlayerSkills.On(Menu.Player.gameObject);
            }
            PlayerInputReader input = Menu != null ? Menu.InputReader : null;
            ThirdPersonCamera camera = Menu != null ? Menu.OrbitCamera : null;
            Menu?.Close();
            if (skills == null)
            {
                Placeholder?.Show("SKILLS", "No body to learn skills on.");
                return;
            }
            SkillMenuView.Show(skills, input, camera);
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
