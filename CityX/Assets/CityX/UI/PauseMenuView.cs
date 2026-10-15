using CityX.Character;
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
    /// THE OUTPUT DEVICE STEPPER IS SHOWN BUT PERMANENTLY INERT. Unlike
    /// Input Device (real, backed by Microphone.devices), Unity has no API
    /// to enumerate or select a specific audio OUTPUT device -- see
    /// VoiceDeviceOptions.OutputOptions's own doc comment. It stays on the
    /// panel for visual parity with ui/settings_panel.gd rather than being
    /// silently dropped.
    ///
    /// VOICE CHAT ITSELF DOES NOT EXIST YET. PlayerVoiceCapture proves the
    /// local microphone path works (Push-to-Talk/Open Mic actually starts
    /// and stops a real Microphone recording), but nothing sends that audio
    /// anywhere -- this port's real player has no networking at all yet, so
    /// there is nowhere to send it to. See PlayerVoiceCapture's own doc
    /// comment.
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
        public Slider MusicSlider;
        public Toggle FullscreenToggle;

        public Text ResolutionLabel;
        public Button ResolutionPrevButton;
        public Button ResolutionNextButton;

        [Tooltip("The frame-rate cap: 30 to 240 FPS, or UNLIMITED.")]
        public Text FpsLabel;
        public Button FpsPrevButton;
        public Button FpsNextButton;

        public Text InputDeviceLabel;
        public Button InputDevicePrevButton;
        public Button InputDeviceNextButton;

        [Tooltip("Shown for parity with the Godot original; always \"Default\" -- see this class's own doc comment.")]
        public Text OutputDeviceLabel;
        public Button OutputDevicePrevButton;
        public Button OutputDeviceNextButton;

        public Toggle PushToTalkToggle;

        [Tooltip("Told to re-read its settings when the Input Device stepper or Push-to-Talk toggle changes.")]
        public PlayerVoiceCapture VoiceCapture;

        [Tooltip("Escape is ignored while this is open -- typing a command should not also pause the game.")]
        public DevConsoleView Console;

        [Tooltip("Escape is ignored while this is open -- it cancels the chat message instead of also pausing the game.")]
        public PlayerChat Chat;

        [Tooltip("The scene Quit returns to.")]
        public string TitleScene = "Title";

        private bool _wasCursorCaptured;
        private int _resolutionIndex = DisplaySettings.DefaultIndex;
        private int _fpsIndex = DisplaySettings.FpsIndexFor(DisplaySettings.DefaultMaxFps);
        private int _inputDeviceIndex;
        private int _outputDeviceIndex;

        /// <summary>The frame-rate cap currently chosen, in fps (0 = no cap).</summary>
        public int MaxFps => DisplaySettings.FpsOptions[_fpsIndex];

        public bool IsOpen => Dim != null && Dim.activeSelf;

        private void Awake()
        {
            if (ResumeButton != null) ResumeButton.onClick.AddListener(Close);
            if (SettingsButton != null) SettingsButton.onClick.AddListener(ShowSettings);
            if (BackButton != null) BackButton.onClick.AddListener(ShowPause);
            if (QuitButton != null) QuitButton.onClick.AddListener(QuitToTitle);

            if (MasterSlider != null) MasterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (MusicSlider != null) MusicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (FullscreenToggle != null) FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            if (ResolutionPrevButton != null) ResolutionPrevButton.onClick.AddListener(() => StepResolution(-1));
            if (ResolutionNextButton != null) ResolutionNextButton.onClick.AddListener(() => StepResolution(1));
            if (FpsPrevButton != null) FpsPrevButton.onClick.AddListener(() => StepFps(-1));
            if (FpsNextButton != null) FpsNextButton.onClick.AddListener(() => StepFps(1));
            if (InputDevicePrevButton != null) InputDevicePrevButton.onClick.AddListener(() => StepInputDevice(-1));
            if (InputDeviceNextButton != null) InputDeviceNextButton.onClick.AddListener(() => StepInputDevice(1));
            if (OutputDevicePrevButton != null) OutputDevicePrevButton.onClick.AddListener(() => StepOutputDevice(-1));
            if (OutputDeviceNextButton != null) OutputDeviceNextButton.onClick.AddListener(() => StepOutputDevice(1));
            if (PushToTalkToggle != null) PushToTalkToggle.onValueChanged.AddListener(OnPushToTalkToggled);

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
            if (SkillMenuView.IsAnyOpen || SkillMenuView.ClosedOnFrame == Time.frameCount)
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

        private void StepFps(int direction)
        {
            // Wraps, like the resolution: past UNLIMITED comes 30 again.
            int count = DisplaySettings.FpsOptions.Length;
            _fpsIndex = ((_fpsIndex + direction) % count + count) % count;
            ApplyFps(_fpsIndex);
            SaveSettings();
        }

        private void ApplyFps(int index)
        {
            _fpsIndex = Mathf.Clamp(index, 0, DisplaySettings.FpsOptions.Length - 1);
            int fps = DisplaySettings.FpsOptions[_fpsIndex];
            DisplaySettings.ApplyMaxFps(fps);
            if (FpsLabel != null)
            {
                FpsLabel.text = DisplaySettings.FpsLabel(fps);
            }
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

        /// <summary>
        /// No live gameplay music track to push into (see this class's own
        /// doc comment) -- only saved, so it is what IntroCutscene reads the
        /// next time it starts its own music.
        /// </summary>
        private void OnMusicVolumeChanged(float value)
        {
            SaveSettings();
        }

        private void StepInputDevice(int direction)
        {
            string[] options = VoiceDeviceOptions.InputOptions();
            int count = options.Length;
            _inputDeviceIndex = ((_inputDeviceIndex + direction) % count + count) % count;
            if (InputDeviceLabel != null) InputDeviceLabel.text = options[_inputDeviceIndex];
            SaveSettings();
            if (VoiceCapture != null) VoiceCapture.ReloadSettings();
        }

        private void StepOutputDevice(int direction)
        {
            string[] options = VoiceDeviceOptions.OutputOptions();
            int count = options.Length;
            _outputDeviceIndex = ((_outputDeviceIndex + direction) % count + count) % count;
            if (OutputDeviceLabel != null) OutputDeviceLabel.text = options[_outputDeviceIndex];
            SaveSettings();
        }

        private void OnPushToTalkToggled(bool _)
        {
            SaveSettings();
            if (VoiceCapture != null) VoiceCapture.ReloadSettings();
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

            // The saved cap is put into force as soon as the game starts, not only once someone opens this screen.
            ApplyFps(DisplaySettings.FpsIndexFor(data.MaxFps));

            if (MasterSlider != null) MasterSlider.value = data.MasterVolume;
            else AudioListener.volume = data.MasterVolume;

            if (MusicSlider != null) MusicSlider.value = data.MusicVolume;

            string[] inputOptions = VoiceDeviceOptions.InputOptions();
            _inputDeviceIndex = VoiceDeviceOptions.IndexOf(inputOptions, data.InputDevice);
            if (InputDeviceLabel != null) InputDeviceLabel.text = inputOptions[_inputDeviceIndex];

            string[] outputOptions = VoiceDeviceOptions.OutputOptions();
            _outputDeviceIndex = VoiceDeviceOptions.IndexOf(outputOptions, data.OutputDevice);
            if (OutputDeviceLabel != null) OutputDeviceLabel.text = outputOptions[_outputDeviceIndex];

            if (PushToTalkToggle != null) PushToTalkToggle.isOn = data.PushToTalk;

            // Set last: its own listener re-applies fullscreen mode and, if
            // not fullscreen, re-applies the resolution above using the
            // index already loaded -- redundant with the block above but
            // harmless, and it means fullscreen alone is the single source
            // of truth for Screen.fullScreenMode.
            if (FullscreenToggle != null) FullscreenToggle.isOn = data.Fullscreen;
            else Screen.fullScreenMode = data.Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;

            if (VoiceCapture != null) VoiceCapture.ReloadSettings();
        }

        private void SaveSettings()
        {
            string[] inputOptions = VoiceDeviceOptions.InputOptions();
            string[] outputOptions = VoiceDeviceOptions.OutputOptions();
            GameSettings.Save(new GameSettingsData
            {
                MasterVolume = MasterSlider != null ? MasterSlider.value : AudioListener.volume,
                MusicVolume = MusicSlider != null ? MusicSlider.value : 1.0f,
                Fullscreen = FullscreenToggle != null && FullscreenToggle.isOn,
                ResolutionIndex = _resolutionIndex,
                MaxFps = DisplaySettings.FpsOptions[_fpsIndex],
                InputDevice = _inputDeviceIndex >= 0 && _inputDeviceIndex < inputOptions.Length
                    ? inputOptions[_inputDeviceIndex] : VoiceDeviceOptions.DefaultDevice,
                OutputDevice = _outputDeviceIndex >= 0 && _outputDeviceIndex < outputOptions.Length
                    ? outputOptions[_outputDeviceIndex] : VoiceDeviceOptions.DefaultDevice,
                PushToTalk = PushToTalkToggle != null ? PushToTalkToggle.isOn : true,
            });
        }
    }
}
