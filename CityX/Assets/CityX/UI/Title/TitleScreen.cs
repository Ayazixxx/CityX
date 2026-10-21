using CityX.View;
using UnityEngine;
using UnityEngine.InputSystem;
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
            CharacterSelect,
            Creator,
            Multiplayer,
            Settings,
        }

        [Header("Wired by TitleScreenBuilder")]
        public GameObject MenuPanel;

        public GameObject CharacterSelectPanel;
        public GameObject CreatorPanel;
        public GameObject MultiplayerPanel;
        public GameObject SettingsPanel;
        [Tooltip("The skyline the logo hangs over. The logo is a quad in that scene (behind the rooftop tank), so it flickers and hides through here, not as a UI image.")]
        public TitleSkyline Skyline;

        [Tooltip("The character select's own component, so Back can step within it.")]
        public CharacterSelect Select;

        [Tooltip("Hidden while the character list is up -- it covers the whole screen.")]
        public GameObject LogoGroup;

        [Tooltip("title_screen.gd's own StarsPlayer -- plays through Menu/CharacterSelect/Creator, set to loop and play on awake, and never touched by Show().")]
        public AudioSource MusicPlayer;

        [Tooltip("The rain's sound and streaks: full outdoors on the menu, muffled and quiet on the character list, gone on the creator.")]
        public TitleAmbience Ambience;

        [Tooltip("Seconds for one full pulse of the logo. The Godot build's own period.")]
        public float LogoFlickerPeriod = 6.5f;

        [Tooltip("title_screen.gd's own PromptLabel -- 'PRESS ANY KEY TO START', shown before the menu is revealed and hidden the moment it is.")]
        public Text PromptLabel;

        [Tooltip("Seconds for one full pulse of the prompt's opacity. The Godot build's own PROMPT_PULSE_PERIOD.")]
        public float PromptPulsePeriod = 2.2f;

        private Panel _showing = Panel.Menu;
        private float _elapsed;
        private float _promptElapsed;

        // Godot's own _menu_revealed: any key or click reveals the menu
        // exactly once. Without this guard a held key (auto-repeat) or
        // several keys in the same frame would re-trigger the reveal
        // repeatedly -- harmless once revealed, but worth keeping the exact
        // same guard shape the original had.
        private bool _menuRevealed;

        public Panel Showing => _showing;

        private void OnEnable()
        {
            ShowIdlePrompt();
        }

        /// <summary>
        /// title_screen.gd's own idle state: nothing but the logo, the
        /// flicker, the rain and the pulsing prompt -- EVERY panel hidden,
        /// including Menu, until the player actually presses something. The
        /// old Unity title screen skipped this entirely and showed Menu
        /// immediately; Requested: "recreate the godot title screen
        /// instead of the one you have now."
        ///
        /// PUBLIC, NOT JUST OnEnable's OWN BODY -- TitleScreenCapture calls
        /// Show(Panel) directly to render a screenshot, and OnEnable does not
        /// run when a scene is opened outside Play Mode (see that file's own
        /// comment on this), so there would otherwise be no way to render
        /// this state at all.
        /// </summary>
        public void ShowIdlePrompt()
        {
            _menuRevealed = false;
            _promptElapsed = 0.0f;
            if (MenuPanel != null) MenuPanel.SetActive(false);
            if (CharacterSelectPanel != null) CharacterSelectPanel.SetActive(false);
            if (CreatorPanel != null) CreatorPanel.SetActive(false);
            if (MultiplayerPanel != null) MultiplayerPanel.SetActive(false);
            if (SettingsPanel != null) SettingsPanel.SetActive(false);
            if (PromptLabel != null) PromptLabel.gameObject.SetActive(true);
            SetLogoShown(true);
            if (Ambience != null) Ambience.SetMood(TitleAmbience.MoodFor(Panel.Menu));
        }

        /// <summary>
        /// Shows one panel and hides the others. The only way to change what is
        /// on screen, so the invariant holds by construction.
        ///
        /// ALWAYS IMPLIES THE MENU HAS BEEN REVEALED. Bug, caught by
        /// a title screenshot: calling Show() directly (TitleScreenCapture
        /// does exactly this, and so does Back()) left the idle "PRESS ANY
        /// KEY TO START" prompt showing behind every panel, since only
        /// RevealMenu() used to know to hide it. Any real panel and the idle
        /// prompt are mutually exclusive, so this is the one place that
        /// invariant actually needs to live.
        /// </summary>
        public void Show(Panel panel)
        {
            _showing = panel;
            _menuRevealed = true;
            if (PromptLabel != null) PromptLabel.gameObject.SetActive(false);
            if (MenuPanel != null) MenuPanel.SetActive(panel == Panel.Menu);
            if (CharacterSelectPanel != null)
            {
                CharacterSelectPanel.SetActive(panel == Panel.CharacterSelect);
            }
            if (CreatorPanel != null) CreatorPanel.SetActive(panel == Panel.Creator);
            if (MultiplayerPanel != null) MultiplayerPanel.SetActive(panel == Panel.Multiplayer);
            if (SettingsPanel != null) SettingsPanel.SetActive(panel == Panel.Settings);

            // The character list, the creator, and settings all carry real
            // controls dead centre -- exactly where the logo sits -- and the
            // logo over them makes everything behind it unreadable
            // (confirmed live: SETTINGS's own heading and resolution value
            // both landed right under the X). The Godot build hid its logo
            // the same way for its own equivalent screens. Multiplayer stays
            // a sparse stub with nothing under the logo yet, so it is left
            // alone rather than hidden pre-emptively.
            SetLogoShown(panel != Panel.CharacterSelect && panel != Panel.Creator
                && panel != Panel.Settings);

            // The rain's sound follows where you are: the list is a room out of it, the creator
            // is somewhere with no weather at all.
            if (Ambience != null) Ambience.SetMood(TitleAmbience.MoodFor(panel));
        }

        /// <summary>
        /// Shows or hides the logo: the group the UI tests and the panels see, and
        /// the quad in the skyline scene that is the logo itself.
        /// </summary>
        private void SetLogoShown(bool shown)
        {
            if (LogoGroup != null) LogoGroup.SetActive(shown);
            if (Skyline != null) Skyline.SetLogoVisible(shown);
        }

        /// <summary>
        /// Escape goes back one step, and from the menu it does nothing --
        /// quitting the game on an accidental keypress from the front screen is
        /// a bad surprise.
        ///
        /// ONE STEP, NOT ALL THE WAY OUT. The character select has its own
        /// sub-modes -- typing a name, confirming a delete -- and escaping from
        /// a delete confirmation should cancel the delete, not throw away the
        /// whole screen. So it gets asked first and only refuses once it is
        /// back at its own list.
        /// </summary>
        public void Back()
        {
            if (_showing == Panel.CharacterSelect && Select != null
                && Select.Showing != CharacterSelect.Mode.List)
            {
                Select.Back();
                return;
            }
            Show(Panel.Menu);
        }

        private void Update()
        {
            if (Skyline != null)
            {
                // title_screen.gd's own _flicker_opacity, ported exactly (a
                // failing-neon-sign look: mostly full opacity, with a couple
                // of quick dips/flares) -- Requested: "recreate the godot
                // title screen instead of the one you have now." The logo is
                // a quad in the skyline scene now, so this drives its opacity
                // rather than a UI image's.
                _elapsed += Time.deltaTime;
                float flickerPercent = Mathf.Repeat(_elapsed, LogoFlickerPeriod) / LogoFlickerPeriod * 100.0f;
                Skyline.SetLogoFlicker(FlickerOpacity(flickerPercent));
            }

            // Escape is handled on its own and never also counts as "any
            // key" for the reveal below -- title_screen.gd's own
            // _unhandled_input checks Escape first and always returns
            // afterward, whether or not the menu was revealed yet.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_menuRevealed)
                {
                    HandleEscape();
                }
                return;
            }

            if (!_menuRevealed)
            {
                TickPrompt();
                if (AnyKeyOrClickThisFrame())
                {
                    RevealMenu();
                }
            }
        }

        /// <summary>
        /// title_screen.gd's own _on_escape_pressed: steps back one level at
        /// a time. From Multiplayer/Settings/CharacterSelect/Creator that is
        /// Back()'s own job; from the root Menu itself it goes all the way
        /// back to the original idle "press any key" state (the reverse of
        /// RevealMenu), which Back() alone has no way to express since it
        /// only ever lands ON Menu, never past it.
        /// </summary>
        private void HandleEscape()
        {
            if (_showing == Panel.Menu)
            {
                ShowIdlePrompt();
                return;
            }
            Back();
        }

        /// <summary>
        /// title_screen.gd's own _flicker_opacity, verbatim: full opacity
        /// for the first 92% of the period, then two quick dip-and-recover
        /// pairs (a bigger one, then a smaller one) before settling back to
        /// full for the rest of the cycle.
        /// </summary>
        private static float FlickerOpacity(float percent)
        {
            if (percent < 92.0f) return 1.0f;
            if (percent < 93.0f) return Mathf.Lerp(1.0f, 0.8f, percent - 92.0f);
            if (percent < 94.0f) return Mathf.Lerp(0.8f, 1.0f, percent - 93.0f);
            if (percent < 95.0f) return Mathf.Lerp(1.0f, 0.55f, percent - 94.0f);
            if (percent < 96.0f) return Mathf.Lerp(0.55f, 1.0f, percent - 95.0f);
            return 1.0f;
        }

        /// <summary>
        /// title_screen.gd's own prompt pulse: a smooth sine breathing
        /// between 0.15 and 0.9 alpha, same period and phase offset (starts
        /// mid-fade-in rather than at a trough or peak).
        /// </summary>
        private void TickPrompt()
        {
            if (PromptLabel == null)
            {
                return;
            }

            _promptElapsed += Time.deltaTime;
            float phase = Mathf.Repeat(_promptElapsed, PromptPulsePeriod) / PromptPulsePeriod;
            float wave = (Mathf.Sin(phase * Mathf.PI * 2.0f - Mathf.PI / 2.0f) + 1.0f) / 2.0f;
            Color colour = PromptLabel.color;
            colour.a = Mathf.Lerp(0.15f, 0.9f, wave);
            PromptLabel.color = colour;
        }

        /// <summary>
        /// Any keyboard key or mouse button pressed THIS frame -- Keyboard.
        /// current/Mouse.current are null outside a real Input System device
        /// (see this project's own established pattern, e.g. BottleWeapon.cs),
        /// which the null checks here guard against the same way.
        /// </summary>
        private static bool AnyKeyOrClickThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
            {
                return true;
            }

            return false;
        }

        /// <summary>title_screen.gd's own _reveal_menu -- Show() itself now handles hiding the prompt.</summary>
        private void RevealMenu()
        {
            Show(Panel.Menu);
        }
    }
}
