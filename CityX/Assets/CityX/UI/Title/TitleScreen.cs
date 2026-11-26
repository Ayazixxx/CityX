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

        private Panel _showing = Panel.Menu;

        public Panel Showing => _showing;

        private void OnEnable()
        {
            CityX.World.WorldMemory.Forget();   // the title screen is where one world ends and the next begins
            // Straight to the menu (owner, 2026-11-11: "get rid of the press any key to start thing just make it go straight to the screen
            // with the buttons").
            Show(Panel.Menu);
            if (Application.isPlaying && TakeFirstStartRequest())
            {
                BeginCreator();   // CityX > Play Title As First Start: the first boot's empty belt, characters made or not
            }
            else if (Application.isPlaying && BeltOnFirstStart)
            {
                // Nobody made yet: no logo and no menu, the empty belt (docs/BELT_INTRO.md).
                CityX.Core.CharacterSources.Current.List(characters =>
                {
                    if (this != null && isActiveAndEnabled && _showing == Panel.Menu && characters != null && characters.Count == 0)
                    {
                        BeginCreator();
                    }
                });
            }
        }

        /// <summary>
        /// Whether a start with no character made yet opens on the conveyor belt instead of the menu. Off in tests of the title's own
        /// menu, which have no characters either.
        /// </summary>
        public static bool BeltOnFirstStart = true;

        /// <summary>
        /// Set (to 1) in PlayerPrefs by the editor's CityX > Play Title As First Start, and taken the next time the title comes up: it opens on
        /// the first boot's empty belt even with characters made (owner, 2026-11-11: "make it so i can still boot from the first start scene
        /// where you control the conveyer belt somehow (for test purposes)").
        /// </summary>
        public const string FirstStartRequest = "CityX.PlayTitleAsFirstStart";

        private static bool TakeFirstStartRequest()
        {
            if (PlayerPrefs.GetInt(FirstStartRequest, 0) != 1)
            {
                return false;
            }
            PlayerPrefs.DeleteKey(FirstStartRequest);
            return true;
        }

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
        /// Making a new character from the character list: the creator, its body rolled in by the belt itself behind the characters standing on
        /// it (BeltIntro.Bring), from <paramref name="start"/> metres along, the belt stopped having run <paramref name="travel"/>.
        /// </summary>
        public void BringNewCharacter(float start, float travel)
        {
            Show(Panel.Creator);
            if (CreatorPanel != null && CreatorPanel.TryGetComponent(out BeltIntro belt))
            {
                belt.Bring(start, travel);
            }
        }

        /// <summary>Shows one panel and hides the others. The only way to change what is on screen, so the invariant holds by construction.</summary>
        public void Show(Panel panel)
        {
            // The settings always come back on their own page, not on the graphics page they were left on.
            if (SettingsPanel != null && SettingsPanel.TryGetComponent(out TitleSettingsView settings))
            {
                settings.CloseGraphics();
            }
            _showing = panel;
            // The one coming up goes up before the rest come down: the character list and the creator share the creator's stage
            // (CreatorStage), which must not go down and up again between them, its belt and bodies starting over.
            GameObject shown = PanelObject(panel);
            if (shown != null) shown.SetActive(true);
            foreach (Panel other in AllPanels)
            {
                GameObject hidden = PanelObject(other);
                if (other != panel && hidden != null) hidden.SetActive(false);
            }
            // Shown this way the creator stands ready, whatever ride was under way; BeginCreator starts one on the empty belt.
            if (panel == Panel.Creator && CreatorPanel != null && CreatorPanel.TryGetComponent(out BeltIntro belt))
            {
                belt.Stand();
            }

            // The logo is only for the menu: every other panel is a box in the middle of the screen, which reaches up into the logo's
            // corner, and a panel under the logo cannot be read. The city and the rooftop stay behind the boxes; the character list and the
            // creator stand their own bodies on their own stages, so they go for those.
            SetLogoShown(panel == Panel.Menu);
            bool onTheBelt = panel == Panel.CharacterSelect || panel == Panel.Creator;
            ShowBackdrop(!onTheBelt);
            if (Vignette != null) Vignette.SetActive(!onTheBelt);   // the belt hangs in a daylight sky, which the dark corners turned grey

            // The rain's sound follows where you are: the list is a room out of it, the creator
            // is somewhere with no weather at all.
            if (Ambience != null) Ambience.SetMood(TitleAmbience.MoodFor(panel));
        }

        private static readonly Panel[] AllPanels = (Panel[])System.Enum.GetValues(typeof(Panel));

        private GameObject PanelObject(Panel panel)
        {
            switch (panel)
            {
                case Panel.Menu: return MenuPanel;
                case Panel.CharacterSelect: return CharacterSelectPanel;
                case Panel.Creator: return CreatorPanel;
                case Panel.Multiplayer: return MultiplayerPanel;
                default: return SettingsPanel;
            }
        }

        /// <summary>
        /// The city behind the title, and the character on its roof: drawn, or not at all. Not drawn, its camera is switched off as well as
        /// shown nothing: the belt's camera clears the whole screen to its sky and draws over it, so everything the city's camera still did
        /// each frame (its sky, the cel lines' pass, bloom, the smoothing of edges) was thrown away (owner, 2026-11-26: "see what you can do
        /// to lower ... framerate on the character select screen").
        /// </summary>
        private void ShowBackdrop(bool shown)
        {
            if (Hero != null) Hero.gameObject.SetActive(shown);
            if (Backdrop != null)
            {
                Backdrop.cullingMask = shown ? BackdropMask : 0;
                Backdrop.enabled = shown;
            }
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
            // Escape steps back one level; on the menu itself there is nowhere further back to go.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && _showing != Panel.Menu)
            {
                Back();
            }
            // F8, in the editor and development builds: the first boot's empty belt, to try it with characters already made.
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame && (Application.isEditor || Debug.isDebugBuild) && _showing != Panel.Creator)
            {
                BeginCreator();
            }
        }
    }
}
