using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// PROXIMITY VOICE (docs/VOICE.md; GAME_DESIGN "Social"): what this body's player says into their microphone (<see cref="PlayerVoiceCapture"/>)
    /// goes, coded (<see cref="VoiceCodec"/>), to the server, which passes it only to the players whose bodies are near enough to hear it
    /// (<see cref="VoiceHearing.RangeMetres"/>, plus a margin for moving between frames); their copy of this body plays it from its head
    /// (<see cref="VoicePlayback"/>). Unreliable: a lost frame is 20 ms of silence, never a delay. A copy's microphone stays off.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedVoice : NetworkBehaviour
    {
        /// <summary>The most frames a second the server passes on for one speaker (speech is 50).</summary>
        public const int MaxFramesPerSecond = 60;

        /// <summary>How far past the hearing range the server still sends, metres: the listener may step into range before the next frame.</summary>
        public const float RelayMarginMetres = 5.0f;

        /// <summary>How many frames the server has passed on from this speaker, to anyone (for tests).</summary>
        public int FramesRelayed { get; private set; }

        private PlayerVoiceCapture _capture;
        private VoicePlayback _playback;
        private readonly byte[] _frame = new byte[VoiceCodec.FrameBytes];
        private VoiceCodec.State _coder;
        private ushort _sequence;
        private float _windowStart;
        private int _windowCount;
        private bool _listening;

        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private void Awake()
        {
            _capture = GetComponent<PlayerVoiceCapture>();
            _playback = GetComponent<VoicePlayback>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                if (_capture != null)
                {
                    _capture.FrameCaptured += Send;
                }
                var animator = GetComponentInChildren<Animator>();
                Transform head = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                VoicePlayback.SetListener(head != null ? head : transform, transform);
                _listening = true;
            }
            else if (_capture != null)
            {
                _capture.enabled = false;
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_capture != null)
            {
                _capture.FrameCaptured -= Send;
            }
            if (_listening)
            {
                VoicePlayback.ClearListener(transform);
                _listening = false;
            }
        }

        /// <summary>Sends one frame of this player's voice (<see cref="VoiceCodec.FrameSamples"/> samples) to the players near enough to hear it.</summary>
        public void Send(float[] samples)
        {
            if (!IsLiveOwner)
            {
                return;
            }
            VoiceCodec.Encode(samples, _sequence++, ref _coder, _frame);
            SendFrameToServer(_frame);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendFrameToServer(byte[] frame, Channel channel = Channel.Unreliable)
        {
            if (!WithinRate() || !VoiceCodec.IsWellFormed(frame))
            {
                return;
            }
            float reach = VoiceHearing.RangeMetres + RelayMarginMetres;
            Vector3 mouth = transform.position;
            foreach (NetworkConnection listener in Observers)
            {
                if (listener == Owner)
                {
                    continue;
                }
                NetworkObject body = listener.FirstObject;
                if (body == null || (body.transform.position - mouth).sqrMagnitude > reach * reach)
                {
                    continue;
                }
                FramesRelayed++;
                HearFrame(listener, frame);
            }
        }

        [TargetRpc]
        private void HearFrame(NetworkConnection listener, byte[] frame, Channel channel = Channel.Unreliable)
        {
            if (_playback != null)
            {
                _playback.Receive(frame);
            }
        }

        private bool WithinRate()
        {
            float now = Time.unscaledTime;
            if (now - _windowStart >= 1.0f)
            {
                _windowStart = now;
                _windowCount = 0;
            }
            return ++_windowCount <= MaxFramesPerSecond;
        }
    }
}
