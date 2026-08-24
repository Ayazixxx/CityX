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
    /// NO VOICE CHAT SECTION, SAME REASONING AS PauseMenuView's OWN: there is
    /// no voice chat system anywhere in this port to point a device picker
    /// at.
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
        public Toggle FullscreenToggle;

        public Text ResolutionLabel;
        public Button ResolutionPrevButton;
        public Button ResolutionNextButton;

        private int _resolutionIndex = DisplaySettings.DefaultIndex;

        private void Awake()
        {
            if (MasterSlider != null) MasterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (FullscreenToggle != null) FullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);

            LoadSettings();
        }

        /// <summary>Called by a ButtonAction.Kind.SettingsResolutionNext button.</summary>
        public void Next() => StepResolution(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsResolutionPrevious button.</summary>
        public void Previous() => StepResolution(-1);

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

            if (MasterSlider != null) MasterSlider.value = data.MasterVolume;
            else AudioListener.volume = data.MasterVolume;

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
            GameSettings.Save(new GameSettingsData
            {
                MasterVolume = MasterSlider != null ? MasterSlider.value : AudioListener.volume,
                Fullscreen = FullscreenToggle != null && FullscreenToggle.isOn,
                ResolutionIndex = _resolutionIndex,
            });
        }
    }
}
