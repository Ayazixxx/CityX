using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The pause menu: Escape opens and closes it. Alone, the world waits while it is open; in a shared world only this player's keys are
    /// held (the clock is everybody's). Its settings page is <see cref="SettingsPage"/>, which the title screen shares.
    ///
    /// The buttons' listeners are added here, in Awake, not by the scene builder: a listener an editor script adds does not survive the
    /// scene being saved (<see cref="ButtonAction"/>).
    /// </summary>
    public sealed class PauseMenuView : SettingsPage
    {
        [Tooltip("The whole overlay, hidden until Escape is pressed.")]
        public GameObject Dim;

        public GameObject PausePanel;
        public GameObject SettingsPanel;

        public Button ResumeButton;
        public Button SettingsButton;
        public Button QuitButton;
        public Button BackButton;

        [Tooltip("Told to re-read its settings when the Input Device stepper or Push-to-Talk toggle changes.")]
        public PlayerVoiceCapture VoiceCapture;

        [Tooltip("Escape is ignored while this is open -- typing a command should not also pause the game.")]
        public DevConsoleView Console;

        [Tooltip("Escape is ignored while this is open -- it cancels the chat message instead of also pausing the game.")]
        public PlayerChat Chat;

        [Tooltip("The scene Quit returns to.")]
        public string TitleScene = "Title";

        private readonly FreedCursor _cursor = new FreedCursor();
        private PlayerInputReader _heldKeys;

        public bool IsOpen => Dim != null && Dim.activeSelf;

        private void Awake()
        {
            if (ResumeButton != null) ResumeButton.onClick.AddListener(Close);
            if (SettingsButton != null) SettingsButton.onClick.AddListener(ShowSettings);
            if (BackButton != null) BackButton.onClick.AddListener(ShowPause);
            if (QuitButton != null) QuitButton.onClick.AddListener(QuitToTitle);

            HookSettings();
            if (ResolutionPrevButton != null) ResolutionPrevButton.onClick.AddListener(() => StepResolution(-1));
            if (ResolutionNextButton != null) ResolutionNextButton.onClick.AddListener(() => StepResolution(1));
            if (FpsPrevButton != null) FpsPrevButton.onClick.AddListener(() => StepFps(-1));
            if (FpsNextButton != null) FpsNextButton.onClick.AddListener(() => StepFps(1));
            if (InputDevicePrevButton != null) InputDevicePrevButton.onClick.AddListener(() => StepInputDevice(-1));
            if (InputDeviceNextButton != null) InputDeviceNextButton.onClick.AddListener(() => StepInputDevice(1));
            if (OutputDevicePrevButton != null) OutputDevicePrevButton.onClick.AddListener(() => StepOutputDevice(-1));
            if (OutputDeviceNextButton != null) OutputDeviceNextButton.onClick.AddListener(() => StepOutputDevice(1));

            LoadSettings();
            Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Guarded on the console rather than the other way round: the
            // console closes on backtick, not Escape, so there is no
            // ordering conflict -- this simply refuses to open on top of it.
            if (Console != null && Console.IsOpen)
            {
                return;
            }

            // Chat DOES close on Escape too -- PlayerChat's own Update()
            // handles that half. Refusing to also open the pause menu here
            // is the fix for the exact race ThirdPersonCamera's leftover
            // Escape handler used to cause against this component: two
            // things reacting to the same keypress in the same frame.
            if (Chat != null && Chat.IsOpen)
            {
                return;
            }

            // The skill menu closes on Escape the same way, so the press
            // that closed it (whichever ran first this frame) is not a pause.
            if (SkillMenuView.IsAnyOpen || SkillMenuView.ClosedOnFrame == Time.frameCount || GraffitiMenuView.IsAnyOpen || GraffitiMenuView.ClosedOnFrame == Time.frameCount)
            {
                return;
            }

            // Any other menu (the stats, the loadout, the book, the C menu, the inventory, the map, an item's card...) holds the keys while it is
            // open and closes on Escape too: the press that closed it, whichever of the two read it first this frame, is not a pause.
            // (Open in a shared world, this menu holds them itself, and the Escape is its own.)
            if (!IsOpen && (PlayerInputReader.AnyHeld || PlayerInputReader.KeysGivenBackOnFrame == Time.frameCount))
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            _cursor.Free();
            // Alone, the world waits. A shared world's clock is everybody's: stopped here, this player's body froze on the others' screens
            // and theirs on this one, wherever each then stood. There the menu only takes the keys: the body stands, and can be hit.
            if (CityX.Net.NetworkBootstrap.PlaysAlone)
            {
                Time.timeScale = 0.0f;
            }
            else
            {
                _heldKeys = LocalPlayerBody.Find<PlayerInputReader>();
                if (_heldKeys != null) _heldKeys.Hold(this);
            }

            if (Dim != null) Dim.SetActive(true);
            ShowPause();
        }

        public void Close()
        {
            if (Dim != null) Dim.SetActive(false);
            Time.timeScale = 1.0f;
            if (_heldKeys != null) _heldKeys.Release(this);
            _heldKeys = null;
            _cursor.Restore();
        }

        private void ShowSettings()
        {
            if (PausePanel != null) PausePanel.SetActive(false);
            if (Graphics != null) Graphics.Close();
            if (SettingsPanel != null) SettingsPanel.SetActive(true);
        }

        private void ShowPause()
        {
            if (Graphics != null) Graphics.Close();
            if (SettingsPanel != null) SettingsPanel.SetActive(false);
            if (PausePanel != null) PausePanel.SetActive(true);
        }

        /// <summary>
        /// What the character has earned is written before the world is left, and before the session is: the autosave comes only every
        /// half minute (and stands still while this menu holds the clock), and once the session is gone there is no server to write to.
        /// </summary>
        public static void SaveTheCharacter()
        {
            if (CityX.Character.CharacterIdentity.Ours != null)
            {
                CityX.Character.CharacterIdentity.Ours.Write();
            }
        }

        private void QuitToTitle()
        {
            // Un-pause first -- loading a scene under a frozen timescale
            // would leave the title screen frozen too.
            Time.timeScale = 1.0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SaveTheCharacter();
            CityX.Net.NetworkBootstrap.LeaveSession();
            SceneManager.LoadScene(TitleScene);
        }

        protected override void VoiceSettingsChanged()
        {
            if (VoiceCapture != null) VoiceCapture.ReloadSettings();
        }
    }
}
