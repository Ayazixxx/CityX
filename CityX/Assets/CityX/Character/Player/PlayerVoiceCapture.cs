using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Local microphone capture, gated by Push-to-Talk (V) or Open Mic (the
    /// settings' push_to_talk and input_device). What it records goes out in
    /// 20 ms frames (<see cref="FrameCaptured"/>), which NetworkedVoice sends
    /// to the players near enough to hear (docs/VOICE.md).
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
        /// <summary>The microphone is read this many samples at a time.</summary>
        private const int ReadBlock = 160;

        public bool IsCapturing { get; private set; }

        /// <summary>Every 20 ms of what the microphone heard while capturing, as the voice codec takes it (sent on by NetworkedVoice).</summary>
        public event System.Action<float[]> FrameCaptured
        {
            add => _framer.FrameReady += value;
            remove => _framer.FrameReady -= value;
        }

        private readonly VoiceFramer _framer = new VoiceFramer();
        private readonly float[] _block = new float[ReadBlock];
        private AudioClip _clip;
        private string _recording;
        private int _read;

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
            ReadMicrophone();
        }

        /// <summary>Hands the framer what the microphone recorded since the last read (its clip is a one-second loop it writes round and round).</summary>
        private void ReadMicrophone()
        {
            if (!IsCapturing || _clip == null)
            {
                return;
            }
            int position = Microphone.GetPosition(_recording);
            if (position < 0)
            {
                return;
            }
            int length = _clip.samples;
            int waiting = (position - _read + length) % length;
            while (waiting >= ReadBlock)
            {
                _clip.GetData(_block, _read);   // reads on round the loop's end
                _framer.Push(_block, ReadBlock);
                _read = (_read + ReadBlock) % length;
                waiting -= ReadBlock;
            }
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
            // The codec's rate when the microphone can record it, else the nearest it can (the framer resamples); 0 and 0 means any rate.
            Microphone.GetDeviceCaps(device, out int lowest, out int highest);
            int rate = lowest == 0 && highest == 0 ? VoiceCodec.SampleRate : Mathf.Clamp(VoiceCodec.SampleRate, lowest, highest);
            _clip = Microphone.Start(device, loop: true, lengthSec: 1, frequency: rate);
            if (_clip == null)
            {
                return;
            }
            _recording = device;
            _read = 0;
            _framer.InputRate = rate;
            _framer.Reset();
            IsCapturing = true;
        }

        private void StopCapture()
        {
            Microphone.End(_recording);
            _clip = null;
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
