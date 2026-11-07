using CityX.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The title screen's own Settings panel -- Master volume, fullscreen,
    /// and a resolution stepper, backed by the exact same GameSettings/
    /// DisplaySettings the in-game pause menu already uses (see
    /// PauseMenuView), so a change made from either screen sticks for both
    /// and a player never has to open a match just to turn the volume down.
    ///
    /// A SEPARATE COMPONENT FROM PauseMenuView RATHER THAN A SHARED ONE.
    /// Reusing it here would drag along Dim/PausePanel/Time.timeScale/Escape
    /// handling/Console+Chat guards/"quit to title" -- none of which mean
    /// anything before a world even exists. What is genuinely shared
    /// (DisplaySettings' resolution list, GameSettings' load/save) already
    /// lives in its own file and both components call straight into it.
    ///
    /// THE OUTPUT DEVICE STEPPER IS SHOWN BUT PERMANENTLY INERT, SAME
    /// REASONING AS PauseMenuView's OWN: Unity has no API to enumerate or
    /// select a specific audio OUTPUT device (see
    /// VoiceDeviceOptions.OutputOptions's own doc comment), so it stays on
    /// the panel for visual parity rather than being silently dropped.
    ///
    /// NO PlayerVoiceCapture REFERENCE, UNLIKE PauseMenuView's OWN: the
    /// title screen never has a live player to point one at. Changing Input
    /// Device or Push-to-Talk here only ever writes the saved setting; it
    /// takes effect the next time a player actually exists to read it.
    ///
    /// BACK IS NOT HANDLED HERE. It's an ordinary ButtonAction.Kind.TitleBack
    /// button, wired the same way every other panel's Back button on this
    /// screen is -- navigation is TitleScreen's job, not this component's.
    ///
    /// THE RESOLUTION STEPPER GOES THROUGH ButtonAction TOO, UNLIKE
    /// PauseMenuView's OWN COPY OF THIS STEPPER. PauseMenuView's whole panel
    /// is built straight into a live scene at real play time and never
    /// saved, so wiring its buttons directly in Awake is harmless there. This
    /// panel is built by an EDITOR script and saved into Title.unity, which
    /// is exactly the shape TitleScreenWiringTests.EveryButtonDoesSomething
    /// exists to catch (see ButtonAction's own doc comment) -- so the two
    /// arrow buttons carry ButtonAction.Kind.SettingsResolutionPrevious/Next
    /// instead, calling the public Previous()/Next() below. The slider and
    /// toggle are NOT Button components, so that test does not apply to
    /// them, and they keep the direct Awake wiring PauseMenuView's own
    /// versions use.
    ///
    /// SLIDER/TOGGLE LISTENERS ARE WIRED HERE, IN AWAKE -- NOT BY THE SCENE
    /// BUILDER. See ButtonAction's own doc comment (and PauseMenuView's copy
    /// of it) for why: a listener an editor script adds with
    /// onClick.AddListener/onValueChanged.AddListener does not survive the
    /// scene being saved.
    /// </summary>
    public sealed class TitleSettingsView : MonoBehaviour
    {
        public Slider MasterSlider;
        public Slider MusicSlider;
        public Toggle FullscreenToggle;

        public Text ResolutionLabel;
        public Button ResolutionPrevButton;
        public Button ResolutionNextButton;

        [Tooltip("The frame-rate cap: 30 to 240 FPS, or UNLIMITED. Shown as a stepper, like the resolution.")]
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

        [Tooltip("Opens the graphics page over these settings.")]
        public Button GraphicsButton;

        public GraphicsSettingsPanel Graphics;

        [Tooltip("title_screen.gd's own StarsPlayer -- see TitleScreen.MusicPlayer's own doc comment.")]
        public AudioSource MusicPlayer;

        private int _resolutionIndex = DisplaySettings.DefaultIndex;
        private int _fpsIndex = DisplaySettings.FpsIndexFor(DisplaySettings.DefaultMaxFps);
        private int _inputDeviceIndex;
        private int _outputDeviceIndex;

        private void Awake()
        {
            if (MasterSlider != null) MasterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (MusicSlider != null) MusicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (FullscreenToggle != null) FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            if (PushToTalkToggle != null) PushToTalkToggle.onValueChanged.AddListener(OnPushToTalkToggled);
            if (GraphicsButton != null && Graphics != null) GraphicsButton.onClick.AddListener(Graphics.Open);

            LoadSettings();
        }

        /// <summary>Closes the graphics page if it is open (Escape steps back to these settings); whether it was.</summary>
        public bool CloseGraphics()
        {
            if (Graphics == null || !Graphics.IsOpen)
            {
                return false;
            }
            Graphics.Close();
            return true;
        }

        /// <summary>Called by a ButtonAction.Kind.SettingsResolutionNext button.</summary>
        public void Next() => StepResolution(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsResolutionPrevious button.</summary>
        public void Previous() => StepResolution(-1);

        /// <summary>Called by a ButtonAction.Kind.SettingsFpsNext button.</summary>
        public void NextFps() => StepFps(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsFpsPrevious button.</summary>
        public void PreviousFps() => StepFps(-1);

        /// <summary>The frame-rate cap currently chosen, in fps (0 = no cap).</summary>
        public int MaxFps => DisplaySettings.FpsOptions[_fpsIndex];

        /// <summary>Called by a ButtonAction.Kind.SettingsInputDeviceNext button.</summary>
        public void NextInputDevice() => StepInputDevice(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsInputDevicePrevious button.</summary>
        public void PreviousInputDevice() => StepInputDevice(-1);

        /// <summary>Called by a ButtonAction.Kind.SettingsOutputDeviceNext button.</summary>
        public void NextOutputDevice() => StepOutputDevice(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsOutputDevicePrevious button.</summary>
        public void PreviousOutputDevice() => StepOutputDevice(-1);

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            SaveSettings();
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (MusicPlayer != null) MusicPlayer.volume = value;
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

        private void StepInputDevice(int direction)
        {
            string[] options = VoiceDeviceOptions.InputOptions();
            int count = options.Length;
            _inputDeviceIndex = ((_inputDeviceIndex + direction) % count + count) % count;
            if (InputDeviceLabel != null) InputDeviceLabel.text = options[_inputDeviceIndex];
            SaveSettings();
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
            // Wraps rather than clamping at the ends -- same reasoning as
            // PauseMenuView's own copy of this: Next past the last entry
            // should reach the first one rather than doing nothing.
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

            // The saved cap is put into force as soon as the game starts, not only once someone opens this screen.
            ApplyFps(DisplaySettings.FpsIndexFor(data.MaxFps));

            if (MasterSlider != null) MasterSlider.value = data.MasterVolume;
            else AudioListener.volume = data.MasterVolume;

            if (MusicSlider != null) MusicSlider.value = data.MusicVolume;
            else if (MusicPlayer != null) MusicPlayer.volume = data.MusicVolume;

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
            // of truth for Screen.fullScreenMode. Same ordering PauseMenuView
            // itself relies on.
            if (FullscreenToggle != null) FullscreenToggle.isOn = data.Fullscreen;
            else Screen.fullScreenMode = data.Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
        }

        private void SaveSettings()
        {
            string[] inputOptions = VoiceDeviceOptions.InputOptions();
            string[] outputOptions = VoiceDeviceOptions.OutputOptions();
            GameSettings.Save(new GameSettingsData
            {
                MasterVolume = MasterSlider != null ? MasterSlider.value : AudioListener.volume,
                MusicVolume = MusicSlider != null ? MusicSlider.value : (MusicPlayer != null ? MusicPlayer.volume : 1.0f),
                Fullscreen = FullscreenToggle != null && FullscreenToggle.isOn,
                ResolutionIndex = _resolutionIndex,
                MaxFps = DisplaySettings.FpsOptions[_fpsIndex],
                InputDevice = _inputDeviceIndex >= 0 && _inputDeviceIndex < inputOptions.Length
                    ? inputOptions[_inputDeviceIndex] : VoiceDeviceOptions.DefaultDevice,
                OutputDevice = _outputDeviceIndex >= 0 && _outputDeviceIndex < outputOptions.Length
                    ? outputOptions[_outputDeviceIndex] : VoiceDeviceOptions.DefaultDevice,
                PushToTalk = PushToTalkToggle != null ? PushToTalkToggle.isOn : true,
                Graphics = GameSettings.Load().Graphics,   // the graphics page saves its own; kept as it is
            });
        }
    }
}
