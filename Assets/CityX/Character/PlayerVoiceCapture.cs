using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Local microphone capture, gated by Push-to-Talk/Open Mic. Ported from
    /// the SETTING ui/settings_panel.gd exposes (push_to_talk, input_device),
    /// not from any actual transmission code -- this port's real player has
    /// no networking of any kind yet (confirmed: no NetworkObject, no
    /// NetworkBehaviour, CityX.Character does not reference FishNet), so
    /// there is nowhere to send captured audio to. This is the local half
    /// only: proving the mic path itself works, so a later networking pass
    /// has real captured audio to start from rather than nothing.
    ///
    /// THE DECISION IS SPLIT FROM THE DEVICE I/O, same reasoning
    /// PlayerAudio.StepFor is split from UpdateFootsteps: ShouldCapture is a
    /// pure function of two bools, testable without a microphone, an Input
    /// System device, or a scene. Tick applies it and is itself driven by an
    /// explicit talkKeyHeld bool rather than reading Keyboard.current
    /// directly for the same reason PistolWeaponPlayTests' own TryPickup
    /// calls give: "Keyboard.current (null outside a real Input System
    /// device -- nothing in this project's tests exercises it)".
    ///
    /// NEVER THROWS ON A MISSING DEVICE. Microphone.devices is empty on a
    /// machine with no microphone at all (this project's own headless test
    /// runner included) -- IsCapturing simply stays false, the same
    /// "nothing to play/nothing to catch" tolerance PlayerAudio's own
    /// PlayFallImpact/PlayDash already apply to a null AudioClip.
    /// </summary>
    public sealed class PlayerVoiceCapture : MonoBehaviour
    {
        public bool IsCapturing { get; private set; }

        private string _device = GameSettings.DefaultDevice;
        private bool _pushToTalk = true;

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// PUBLIC BECAUSE AWAKE IS NOT AVAILABLE OUTSIDE PLAY MODE -- same
        /// reasoning PlayerLocomotion.Bind's own doc comment gives. Reads
        /// the currently saved settings; call ReloadSettings instead if the
        /// intent is to pick up a change made while this is already running.
        /// </summary>
        public void Bind()
        {
            ReloadSettings();
        }

        /// <summary>
        /// Re-reads GameSettings, for a live PauseMenuView's Input Device
        /// stepper or Push-to-Talk toggle to call after saving -- the title
        /// screen never has a live player to point one of these at, so only
        /// PauseMenuView needs this hook.
        /// </summary>
        public void ReloadSettings()
        {
            GameSettingsData data = GameSettings.Load();
            _device = data.InputDevice;
            _pushToTalk = data.PushToTalk;
        }

        /// <summary>
        /// Whether the microphone should be capturing right now. True
        /// whenever Open Mic (pushToTalk false) is selected, since open mic
        /// means "always on" by definition; otherwise only while the talk
        /// key is actually held.
        /// </summary>
        public static bool ShouldCapture(bool pushToTalk, bool talkKeyHeld)
        {
            return !pushToTalk || talkKeyHeld;
        }

        private void Update()
        {
            Tick(Keyboard.current != null && Keyboard.current.vKey.isPressed);
        }

        /// <summary>
        /// Starts or stops the microphone to match ShouldCapture's answer.
        /// Public (not private) so PlayerVoiceCapturePlayTests can drive it
        /// directly with an explicit talkKeyHeld, bypassing Keyboard.current
        /// entirely -- see this class's own doc comment for why.
        /// </summary>
        public void Tick(bool talkKeyHeld)
        {
            bool wanted = ShouldCapture(_pushToTalk, talkKeyHeld);
            if (wanted && !IsCapturing)
            {
                StartCapture();
            }
            else if (!wanted && IsCapturing)
            {
                StopCapture();
            }
        }

        private void StartCapture()
        {
            if (Microphone.devices.Length == 0)
            {
                return;
            }
            // Unity's own Microphone API takes null for "the default device",
            // not the literal string "Default" this project's settings save.
            string device = _device == GameSettings.DefaultDevice ? null : _device;
            if (Microphone.Start(device, loop: true, lengthSec: 1, frequency: 16000) == null)
            {
                return;
            }
            IsCapturing = true;
        }

        private void StopCapture()
        {
            string device = _device == GameSettings.DefaultDevice ? null : _device;
            Microphone.End(device);
            IsCapturing = false;
        }

        private void OnDisable()
        {
            if (IsCapturing)
            {
                StopCapture();
            }
        }
    }
}
