using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Escape opens and closes this. Ported from ui/pause_menu.gd +
    /// ui/settings_panel.gd, merged into one component the way DevConsole's
    /// own parsing and its view are split -- here there wasn't enough pure
    /// logic on the settings side to justify a second file beyond
    /// <see cref="DisplaySettings"/> and <see cref="GameSettings"/>, which
    /// already carry the parts a test can reach.
    ///
    /// THE RESOLUTION PICKER IS PREV/NEXT BUTTONS ROUND A LABEL, NOT A
    /// DROPDOWN. Godot's OptionButton is a single ready-made control;
    /// Unity's Dropdown needs a template/viewport/item hierarchy built by
    /// hand to work at all. A stepper is the same choice with a fraction of
    /// the scaffolding, and every other control on this panel is already
    /// hand-built rather than pulled from DefaultControls.
    ///
    /// NO VOICE CHAT SECTION. Godot's settings_panel.gd also picks an input/
    /// output device and a push-to-talk mode -- there is no voice chat
    /// system anywhere in this port to point those at, so porting the
    /// picker would be UI for a feature that doesn't exist. Master volume,
    /// fullscreen and resolution are the parts with something real behind
    /// them.
    ///
    /// ALL LISTENERS ARE WIRED HERE, IN AWAKE -- NOT BY THE SCENE BUILDER.
    /// <see cref="ButtonAction"/>'s own doc comment explains why: an
    /// onClick.AddListener added by an editor script does not survive the
    /// scene being saved, because a C# lambda is not something Unity can
    /// serialize. Awake runs at real play time, every time, so a listener
    /// added here is never stale. The builder's only job is to build the
    /// hierarchy and hand this component the references.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until Escape is pressed.")]
        public GameObject Dim;

        public GameObject PausePanel;
        public GameObject SettingsPanel;

        public Button ResumeButton;
        public Button SettingsButton;
        public Button QuitButton;
        public Button BackButton;

        public Slider MasterSlider;
        public Toggle FullscreenToggle;

        public Text ResolutionLabel;
        public Button ResolutionPrevButton;
        public Button ResolutionNextButton;

        [Tooltip("Escape is ignored while this is open -- typing a command should not also pause the game.")]
        public DevConsoleView Console;

        [Tooltip("The scene Quit returns to.")]
        public string TitleScene = "Title";

        private bool _wasCursorCaptured;
        private int _resolutionIndex = DisplaySettings.DefaultIndex;

        public bool IsOpen => Dim != null && Dim.activeSelf;

        private void Awake()
        {
            if (ResumeButton != null) ResumeButton.onClick.AddListener(Close);
            if (SettingsButton != null) SettingsButton.onClick.AddListener(ShowSettings);
            if (BackButton != null) BackButton.onClick.AddListener(ShowPause);
            if (QuitButton != null) QuitButton.onClick.AddListener(QuitToTitle);

            if (MasterSlider != null) MasterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (FullscreenToggle != null) FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            if (ResolutionPrevButton != null) ResolutionPrevButton.onClick.AddListener(() => StepResolution(-1));
            if (ResolutionNextButton != null) ResolutionNextButton.onClick.AddListener(() => StepResolution(1));

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
            _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0.0f;

            if (Dim != null) Dim.SetActive(true);
            ShowPause();
        }

        public void Close()
        {
            if (Dim != null) Dim.SetActive(false);
            Time.timeScale = 1.0f;
            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ShowSettings()
        {
            if (PausePanel != null) PausePanel.SetActive(false);
            if (SettingsPanel != null) SettingsPanel.SetActive(true);
        }

        private void ShowPause()
        {
            if (SettingsPanel != null) SettingsPanel.SetActive(false);
            if (PausePanel != null) PausePanel.SetActive(true);
        }

        private void QuitToTitle()
        {
            // Un-pause first -- loading a scene under a frozen timescale
            // would leave the title screen frozen too.
            Time.timeScale = 1.0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(TitleScene);
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            SaveSettings();
        }

        private void OnFullscreenToggled(bool fullscreen)
        {
            Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            SetResolutionControlsInteractable(!fullscreen);
            if (!fullscreen)
            {
                ApplyResolution(_resolutionIndex);
            }
            SaveSettings();
        }

        private void StepResolution(int direction)
        {
            // Wraps rather than clamping at the ends -- Next past the last
            // entry should reach the first one rather than doing nothing,
            // the same reasoning the radio's own playlist wrap uses.
            int count = DisplaySettings.Options.Length;
            _resolutionIndex = ((_resolutionIndex + direction) % count + count) % count;
            ApplyResolution(_resolutionIndex);
            SaveSettings();
        }

        private void ApplyResolution(int index)
        {
            _resolutionIndex = DisplaySettings.Clamp(index);
            DisplaySettings.Resolution resolution = DisplaySettings.Options[_resolutionIndex];
            Screen.SetResolution(resolution.Width, resolution.Height, Screen.fullScreenMode);
            if (ResolutionLabel != null)
            {
                ResolutionLabel.text = resolution.ToString();
            }
        }

        private void SetResolutionControlsInteractable(bool interactable)
        {
            if (ResolutionPrevButton != null) ResolutionPrevButton.interactable = interactable;
            if (ResolutionNextButton != null) ResolutionNextButton.interactable = interactable;
        }

        private void LoadSettings()
        {
            GameSettingsData data = GameSettings.Load();
            _resolutionIndex = DisplaySettings.Clamp(data.ResolutionIndex);

            if (ResolutionLabel != null)
            {
                ResolutionLabel.text = DisplaySettings.Options[_resolutionIndex].ToString();
            }
            if (!data.Fullscreen)
            {
                ApplyResolution(_resolutionIndex);
            }
            SetResolutionControlsInteractable(!data.Fullscreen);

            if (MasterSlider != null) MasterSlider.value = data.MasterVolume;
            else AudioListener.volume = data.MasterVolume;

            // Set last: its own listener re-applies fullscreen mode and, if
            // not fullscreen, re-applies the resolution above using the
            // index already loaded -- redundant with the block above but
            // harmless, and it means fullscreen alone is the single source
            // of truth for Screen.fullScreenMode.
            if (FullscreenToggle != null) FullscreenToggle.isOn = data.Fullscreen;
            else Screen.fullScreenMode = data.Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        }

        private void SaveSettings()
        {
            GameSettings.Save(new GameSettingsData
            {
                MasterVolume = MasterSlider != null ? MasterSlider.value : AudioListener.volume,
                Fullscreen = FullscreenToggle != null && FullscreenToggle.isOn,
                ResolutionIndex = _resolutionIndex,
            });
        }
    }
}
