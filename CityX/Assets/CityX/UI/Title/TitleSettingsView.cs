using CityX.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The title screen's Settings panel. The settings themselves are <see cref="SettingsPage"/>'s, which the pause menu shares, so a
    /// change made from either screen sticks for both. This component is only what the title adds: its music, and the stepper methods
    /// its arrow buttons call.
    ///
    /// The arrows go through ButtonAction (Previous()/Next() and the rest below), not listeners added in Awake as the pause menu's are:
    /// this panel is saved into Title.unity, and TitleScreenWiringTests.EveryButtonDoesSomething holds every saved button to an action.
    /// There is no PlayerVoiceCapture here: before a world exists nobody is recording, so a voice setting changed here is only saved.
    /// Back is an ordinary ButtonAction.Kind.TitleBack button: navigation is TitleScreen's job.
    /// </summary>
    public sealed class TitleSettingsView : SettingsPage
    {
        [Tooltip("title_screen.gd's own StarsPlayer -- see TitleScreen.MusicPlayer's own doc comment.")]
        public AudioSource MusicPlayer;

        private void Awake()
        {
            HookSettings();

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

        /// <summary>Called by a ButtonAction.Kind.SettingsInputDeviceNext button.</summary>
        public void NextInputDevice() => StepInputDevice(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsInputDevicePrevious button.</summary>
        public void PreviousInputDevice() => StepInputDevice(-1);

        /// <summary>Called by a ButtonAction.Kind.SettingsOutputDeviceNext button.</summary>
        public void NextOutputDevice() => StepOutputDevice(1);

        /// <summary>Called by a ButtonAction.Kind.SettingsOutputDevicePrevious button.</summary>
        public void PreviousOutputDevice() => StepOutputDevice(-1);

        /// <summary>The title's own music: the slider plays on it, and a page with no slider reads and sets it directly.</summary>
        protected override float MusicVolume
        {
            get => MusicPlayer != null ? MusicPlayer.volume : 1.0f;
            set
            {
                if (MusicPlayer != null) MusicPlayer.volume = value;
            }
        }
    }
}
