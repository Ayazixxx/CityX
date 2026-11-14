using CityX.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The settings a player can change, as the pause menu and the title screen both show them (PauseMenuView, TitleSettingsView): volumes,
    /// fullscreen and resolution, the frame-rate cap, the voice devices and push-to-talk, and the way into the graphics page. One page's
    /// logic for both, on the same saved file (<see cref="GameSettings"/>), so a change made on either sticks for both and the two cannot
    /// drift apart.
    ///
    /// The steppers are Prev/Next buttons round a label, not dropdowns (Unity's Dropdown needs a template hierarchy built by hand). The
    /// Output Device stepper is shown but does nothing: Unity cannot pick an audio output (VoiceDeviceOptions.OutputOptions).
    ///
    /// The slider and toggle listeners are added in Awake (<see cref="HookSettings"/>), not by the scene builder: a listener an editor
    /// script adds does not survive the scene being saved (ButtonAction).
    /// </summary>
    public abstract class SettingsPage : MonoBehaviour
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

        private int _resolutionIndex = DisplaySettings.DefaultIndex;
        private int _fpsIndex = DisplaySettings.FpsIndexFor(DisplaySettings.DefaultMaxFps);
        private int _inputDeviceIndex;
        private int _outputDeviceIndex;

        /// <summary>The frame-rate cap currently chosen, in fps (0 = no cap).</summary>
        public int MaxFps => DisplaySettings.FpsOptions[_fpsIndex];

        /// <summary>The music this page itself plays, where it has one (the title's); a page with no music slider reads and sets this instead.</summary>
        protected virtual float MusicVolume
        {
            get => 1.0f;
            set { }
        }

        /// <summary>The input device or push-to-talk has changed, or the settings were just loaded: whoever is recording re-reads them.</summary>
        protected virtual void VoiceSettingsChanged()
        {
        }

        protected void HookSettings()
        {
            if (MasterSlider != null) MasterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (MusicSlider != null) MusicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (FullscreenToggle != null) FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            if (PushToTalkToggle != null) PushToTalkToggle.onValueChanged.AddListener(OnPushToTalkToggled);
            if (GraphicsButton != null && Graphics != null) GraphicsButton.onClick.AddListener(Graphics.Open);
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            SaveSettings();
        }

        private void OnMusicVolumeChanged(float value)
        {
            MusicVolume = value;
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

        private void OnPushToTalkToggled(bool _)
        {
            SaveSettings();
            VoiceSettingsChanged();
        }

        /// <summary>One step along the frame-rate caps; like every stepper here it wraps round at the ends.</summary>
        protected void StepFps(int direction)
        {
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

        protected void StepResolution(int direction)
        {
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

        /// <summary>The resolution cannot be chosen while fullscreen (the screen's own is used).</summary>
        private void SetResolutionControlsInteractable(bool interactable)
        {
            if (ResolutionPrevButton != null) ResolutionPrevButton.interactable = interactable;
            if (ResolutionNextButton != null) ResolutionNextButton.interactable = interactable;
        }

        protected void StepInputDevice(int direction)
        {
            string[] options = VoiceDeviceOptions.InputOptions();
            int count = options.Length;
            _inputDeviceIndex = ((_inputDeviceIndex + direction) % count + count) % count;
            if (InputDeviceLabel != null) InputDeviceLabel.text = options[_inputDeviceIndex];
            SaveSettings();
            VoiceSettingsChanged();
        }

        protected void StepOutputDevice(int direction)
        {
            string[] options = VoiceDeviceOptions.OutputOptions();
            int count = options.Length;
            _outputDeviceIndex = ((_outputDeviceIndex + direction) % count + count) % count;
            if (OutputDeviceLabel != null) OutputDeviceLabel.text = options[_outputDeviceIndex];
            SaveSettings();
        }

        protected void LoadSettings()
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

            // The saved cap is put into force as soon as the page wakes, not only once someone steps it.
            ApplyFps(DisplaySettings.FpsIndexFor(data.MaxFps));

            if (MasterSlider != null) MasterSlider.value = data.MasterVolume;
            else AudioListener.volume = data.MasterVolume;

            if (MusicSlider != null) MusicSlider.value = data.MusicVolume;
            else MusicVolume = data.MusicVolume;

            string[] inputOptions = VoiceDeviceOptions.InputOptions();
            _inputDeviceIndex = VoiceDeviceOptions.IndexOf(inputOptions, data.InputDevice);
            if (InputDeviceLabel != null) InputDeviceLabel.text = inputOptions[_inputDeviceIndex];

            string[] outputOptions = VoiceDeviceOptions.OutputOptions();
            _outputDeviceIndex = VoiceDeviceOptions.IndexOf(outputOptions, data.OutputDevice);
            if (OutputDeviceLabel != null) OutputDeviceLabel.text = outputOptions[_outputDeviceIndex];

            if (PushToTalkToggle != null) PushToTalkToggle.isOn = data.PushToTalk;

            // Set last: its own listener applies the fullscreen mode and, windowed, the resolution loaded above, so the toggle alone
            // decides Screen.fullScreenMode.
            if (FullscreenToggle != null) FullscreenToggle.isOn = data.Fullscreen;
            else Screen.fullScreenMode = data.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

            VoiceSettingsChanged();
        }

        private void SaveSettings()
        {
            string[] inputOptions = VoiceDeviceOptions.InputOptions();
            string[] outputOptions = VoiceDeviceOptions.OutputOptions();
            GameSettings.Save(new GameSettingsData
            {
                MasterVolume = MasterSlider != null ? MasterSlider.value : AudioListener.volume,
                MusicVolume = MusicSlider != null ? MusicSlider.value : MusicVolume,
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
