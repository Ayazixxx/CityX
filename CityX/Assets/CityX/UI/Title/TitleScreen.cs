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
        [Tooltip("The 3D logo in the top left corner, its neon flickering on its own; hidden through here where a panel covers its corner.")]
        public TitleLogo Logo;

        [Tooltip("The last played character on the rooftop; away while the character list or the creator, each with its own stage, is up.")]
        public TitleHero Hero;

        [Tooltip("The title's camera, which draws the city: it draws nothing while the character list or the creator, each with its own stage and camera, is up (their stage lights would light the city, and drawing it under them is work for nothing).")]
        public Camera Backdrop;

        [Tooltip("What the title's camera draws the rest of the time.")]
        public int BackdropMask = ~0;

        [Tooltip("The character select's own component, so Back can step within it.")]
        public CharacterSelect Select;

        [Tooltip("Hidden while the character list is up -- it covers the whole screen.")]
        public GameObject LogoGroup;

        [Tooltip("title_screen.gd's own StarsPlayer -- plays through Menu/CharacterSelect/Creator, set to loop and play on awake, and never touched by Show().")]
        public AudioSource MusicPlayer;

        [Tooltip("The rain's sound and streaks: full outdoors on the menu, muffled and quiet on the character list, gone on the creator.")]
        public TitleAmbience Ambience;

        [Tooltip("The dark corners over the night city. Off over the creator: its belt hangs in a daylight sky, which they turned grey.")]
        public GameObject Vignette;

        [Tooltip("title_screen.gd's own PromptLabel -- 'PRESS ANY KEY TO START', shown before the menu is revealed and hidden the moment it is.")]
        public Text PromptLabel;

        [Tooltip("Seconds for one full pulse of the prompt's opacity. The Godot build's own PROMPT_PULSE_PERIOD.")]
        public float PromptPulsePeriod = 2.2f;

        private Panel _showing = Panel.Menu;
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
            CityX.World.WorldMemory.Forget();   // the title screen is where one world ends and the next begins
            ShowIdlePrompt();
            if (Application.isPlaying && BeltOnFirstStart)
            {
                // Nobody made yet: no logo and no menu, the empty belt (docs/BELT_INTRO.md).
                CityX.Core.CharacterSources.Current.List(characters =>
                {
                    if (this != null && isActiveAndEnabled && !_menuRevealed && characters != null && characters.Count == 0)
                    {
                        BeginCreator();
                    }
                });
            }
        }

        /// <summary>
        /// Whether a start with no character made yet opens on the conveyor belt instead of the logo. Off in tests of the title's own
        /// first screen, which have no characters either.
        /// </summary>
        public static bool BeltOnFirstStart = true;

        /// <summary>Making a new character: the creator, from the empty conveyor belt (BeltIntro).</summary>
        public void BeginCreator()
        {
            Show(Panel.Creator);
            if (CreatorPanel != null && CreatorPanel.TryGetComponent(out BeltIntro belt))
            {
                belt.Begin();
            }
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
            ShowBackdrop(true);
            if (Vignette != null) Vignette.SetActive(true);
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
            // The settings always come back on their own page, not on the graphics page they were left on.
            if (SettingsPanel != null && SettingsPanel.TryGetComponent(out TitleSettingsView settings))
            {
                settings.CloseGraphics();
            }
            _showing = panel;
            _menuRevealed = true;
            if (PromptLabel != null) PromptLabel.gameObject.SetActive(false);
            if (MenuPanel != null) MenuPanel.SetActive(panel == Panel.Menu);
            if (CharacterSelectPanel != null)
            {
                CharacterSelectPanel.SetActive(panel == Panel.CharacterSelect);
            }
            if (CreatorPanel != null)
            {
                CreatorPanel.SetActive(panel == Panel.Creator);
                // Shown this way the creator stands ready, whatever ride was under way; BeginCreator starts one on the empty belt.
                if (panel == Panel.Creator && CreatorPanel.TryGetComponent(out BeltIntro belt))
                {
                    belt.Stand();
                }
            }
            if (MultiplayerPanel != null) MultiplayerPanel.SetActive(panel == Panel.Multiplayer);
            if (SettingsPanel != null) SettingsPanel.SetActive(panel == Panel.Settings);

            // The logo is only for the menu: every other panel is a box in the middle of the screen, which reaches up into the logo's
            // corner, and a panel under the logo cannot be read. The city and the rooftop stay behind the boxes; the character list and the
            // creator stand their own bodies on their own stages, so they go for those.
            SetLogoShown(panel == Panel.Menu);
            ShowBackdrop(panel != Panel.CharacterSelect && panel != Panel.Creator);
            if (Vignette != null) Vignette.SetActive(panel != Panel.Creator);

            // The rain's sound follows where you are: the list is a room out of it, the creator
            // is somewhere with no weather at all.
            if (Ambience != null) Ambience.SetMood(TitleAmbience.MoodFor(panel));
        }

        /// <summary>The city behind the title, and the character on its roof: drawn, or not at all.</summary>
        private void ShowBackdrop(bool shown)
        {
            if (Hero != null) Hero.gameObject.SetActive(shown);
            if (Backdrop != null) Backdrop.cullingMask = shown ? BackdropMask : 0;
        }

        /// <summary>Shows or hides the logo: the group the UI tests and the panels see, and the 3D logo itself.</summary>
        private void SetLogoShown(bool shown)
        {
            if (LogoGroup != null) LogoGroup.SetActive(shown);
            if (Logo != null) Logo.SetShown(shown);
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
            if (_showing == Panel.Settings && SettingsPanel != null && SettingsPanel.TryGetComponent(out TitleSettingsView settings) &&
                settings.CloseGraphics())
            {
                return;   // Escape on the graphics page steps back to the settings
            }
            Show(Panel.Menu);
        }

        private void Update()
        {
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
