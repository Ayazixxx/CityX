using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The title screen's panels and what is showing.
    ///
    /// THE PANEL STATE IS AN ENUM, NOT A SET OF BOOLS. The Godot original
    /// tracked its menu, settings and multiplayer panels as three independent
    /// visibilities, and every transition had to remember to turn the other two
    /// off. Getting that wrong shows up as two panels stacked on each other.
    /// One value that names what is showing cannot express that state at all.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public enum Panel
        {
            Menu,
            Multiplayer,
            Settings,
        }

        [Header("Wired by TitleScreenBuilder")]
        public GameObject MenuPanel;

        public GameObject MultiplayerPanel;
        public GameObject SettingsPanel;
        public Image Logo;

        [Tooltip("Seconds for one full pulse of the logo. The Godot build's own period.")]
        public float LogoFlickerPeriod = 6.5f;

        [Tooltip("How far the logo dims at the bottom of a pulse.")]
        public float LogoFlickerDepth = 0.18f;

        private Panel _showing = Panel.Menu;
        private float _elapsed;

        public Panel Showing => _showing;

        private void OnEnable()
        {
            Show(Panel.Menu);
        }

        /// <summary>
        /// Shows one panel and hides the others. The only way to change what is
        /// on screen, so the invariant holds by construction.
        /// </summary>
        public void Show(Panel panel)
        {
            _showing = panel;
            if (MenuPanel != null) MenuPanel.SetActive(panel == Panel.Menu);
            if (MultiplayerPanel != null) MultiplayerPanel.SetActive(panel == Panel.Multiplayer);
            if (SettingsPanel != null) SettingsPanel.SetActive(panel == Panel.Settings);
        }

        /// <summary>
        /// Escape goes back one step, and from the menu it does nothing --
        /// quitting the game on an accidental keypress from the front screen is
        /// a bad surprise.
        /// </summary>
        public void Back()
        {
            Show(Panel.Menu);
        }

        private void Update()
        {
            if (Logo == null)
            {
                return;
            }

            // A slow breath rather than a flicker: the logo sits over a moving
            // city and a fast flicker fights it for attention.
            _elapsed += Time.deltaTime;
            float phase = Mathf.Sin(_elapsed / LogoFlickerPeriod * Mathf.PI * 2.0f) * 0.5f + 0.5f;
            float alpha = 1.0f - LogoFlickerDepth * phase;

            Color colour = Logo.color;
            colour.a = alpha;
            Logo.color = colour;
        }
    }
}
