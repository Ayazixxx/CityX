using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// EVERY RAGDOLL THE SAME ON EVERY SCREEN (docs/MULTIPLAYER_STATE.md; owner, 2026-10-29: "make it so whatever ragdoll position you are in is
    /// the same on all multiplayer screens, whether it was ragdoll from the water jet or jetpack crash or grapple swinging. i want all the
    /// ragdolls to be synced up"). Before, each machine threw its own copy of a body about with its own physics (or not at all), so a body
    /// lying face-down on its owner's screen sat up, or stood, on everyone else's. Now only the owner's machine runs the ragdoll: while the
    /// body is limp -- falling, hanging from the rope, lying still, getting up -- its pose goes to the server (<see cref="SendSeconds"/> apart
    /// while it moves, <see cref="RestingSendSeconds"/> while it lies still, unreliably: a lost pose is replaced by the next), which checks it
    /// and passes it to every other player, whose copy plays it back (<see cref="RagdollMirror"/>). When the body is up, a last word, sent
    /// reliably, hands the copies back to their Animators. A copy that went limp on its own machine (an attacker's machine throws the copy it
    /// hit at once) and is never sent a pose -- the owner's machine did not throw it -- gets back up by itself after
    /// <see cref="LoneFallSeconds"/>, as the owner never fell.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DefaultExecutionOrder(10001)]
    public sealed class NetworkedRagdollSync : NetworkBehaviour
    {
        public const float SendSeconds = 1.0f / 20.0f;
        public const float RestingSendSeconds = 0.5f;

        /// <summary>
        /// Every body slower than this, m/s, is a body lying still. Low: a body still settling (a hand sliding off a hip) sent only twice a
        /// second showed on copies a few centimetres from where it had settled.
        /// </summary>
        public const float RestingSpeed = 0.05f;

        /// <summary>The most poses a second the server passes on for one body.</summary>
        public const int MaxPosesPerSecond = 30;

        public const float LoneFallSeconds = 1.0f;

        private Ragdoll _ragdoll;
        private RagdollMirror _mirror;
        private PlayerVitals _vitals;
        private readonly RagdollPose _pose = new RagdollPose();
        private readonly RagdollPose _received = new RagdollPose();
        private bool _wasLimp;
        private float _sinceSend;
        private float _lastSentTime;
        private float _loneFall;
        private float _windowStart;
        private int _windowCount;

        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private bool IsLiveCopy => NetworkObject != null && IsSpawned && !IsOwner;

        private void Awake()
        {
            _ragdoll = GetComponent<Ragdoll>();
            _mirror = GetComponent<RagdollMirror>();
            _vitals = GetComponent<PlayerVitals>();
        }

        private void LateUpdate()
        {
            if (_ragdoll == null || _mirror == null)
            {
                return;
            }
            if (IsLiveOwner)
            {
                TickOwner(Time.deltaTime);
            }
            else if (IsLiveCopy)
            {
                TickLoneFall(Time.deltaTime);
            }
        }

        private void TickOwner(float deltaTime)
        {
            bool limp = _ragdoll.IsLimp;
            if (limp)
            {
                _sinceSend += deltaTime;
                if (!_wasLimp || _sinceSend >= (IsLyingStill() ? RestingSendSeconds : SendSeconds))
                {
                    _sinceSend = 0.0f;
                    _mirror.Capture(_pose, Time.time);
                    _lastSentTime = Time.time;
                    SendPoseToServer(RagdollPoseCodec.Pack(_pose));
                }
            }
            else if (_wasLimp)
            {
                SendEndToServer(_lastSentTime);
            }
            _wasLimp = limp;
        }

        private bool IsLyingStill()
        {
            if (_ragdoll.IsStandingUp)
            {
                return false;   // getting up is moving, though the bodies are kinematic again
            }
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > RestingSpeed * RestingSpeed)
                {
                    return false;
                }
            }
            return true;
        }

        private void TickLoneFall(float deltaTime)
        {
            bool lone = _ragdoll.IsLimp && !_mirror.IsMirroring && (_vitals == null || !_vitals.IsDead);
            _loneFall = lone ? _loneFall + deltaTime : 0.0f;
            if (_loneFall >= LoneFallSeconds)
            {
                _loneFall = 0.0f;
                _ragdoll.Deactivate();
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendPoseToServer(byte[] pose, Channel channel = Channel.Unreliable)
        {
            if (!WithinRate() || !RagdollPoseCodec.TryUnpack(pose, _mirror != null ? _mirror.BoneCount : -1, transform.position, _received))
            {
                return;
            }
            BroadcastPose(pose);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastPose(byte[] pose, Channel channel = Channel.Unreliable)
        {
            // Checked again here: a client trusts the server's relay no further than the server trusted the sender.
            if (_mirror == null || !RagdollPoseCodec.TryUnpack(pose, _mirror.BoneCount, transform.position, _received))
            {
                return;
            }
            _loneFall = 0.0f;
            _mirror.Receive(_received);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendEndToServer(float lastTime)
        {
            if (!float.IsNaN(lastTime) && !float.IsInfinity(lastTime))
            {
                BroadcastEnd(lastTime);
            }
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastEnd(float lastTime)
        {
            if (_mirror != null && !float.IsNaN(lastTime) && !float.IsInfinity(lastTime))
            {
                _mirror.End(lastTime);
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
            return ++_windowCount <= MaxPosesPerSecond;
        }
    }
}
