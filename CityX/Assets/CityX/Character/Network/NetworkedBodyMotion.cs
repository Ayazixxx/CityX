using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE A BODY IS, ON EVERY OTHER SCREEN (docs/MULTIPLAYER_MOVEMENT.md). The machine that plays a body (a player's own, or the enemy it
    /// runs) says where it stands <see cref="SendsPerSecond"/> times a second while it moves, each time with the moment on its own clock;
    /// the server checks that and passes it on; every other machine, the server too, plays it back a little in the past
    /// (<see cref="BodyMotionPlayback"/>). Sent unreliably and whole (place, turn and size every time): a lost packet is made good by the next,
    /// and a body standing still is told again <see cref="RestingSendsPerSecond"/> times a second, so no copy is left standing where a lost
    /// packet put it.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [DefaultExecutionOrder(-200)]   // a copy is put in its place before anything of the frame asks where it is
    public sealed class NetworkedBodyMotion : NetworkBehaviour
    {
        public const float SendsPerSecond = 60.0f;
        public const float RestingSendsPerSecond = 5.0f;

        /// <summary>
        /// The moving pace as a wait, a tenth short: a game running at exactly that many frames a second would otherwise miss every other turn
        /// by a hair.
        /// </summary>
        private const double MovingSendSeconds = 0.9 / SendsPerSecond;

        /// <summary>A body that has moved less than this since it was last told, metres, and turned less than <see cref="RestingDegrees"/>, stands still.</summary>
        public const float RestingMetres = 0.001f;
        public const float RestingDegrees = 0.05f;

        /// <summary>The most moments a second the server passes on for one body: the moving pace and the extra one said when it sets off, with room.</summary>
        public const int MaxMomentsPerSecond = 150;

        /// <summary>No body is smaller or larger than this (Shrink and Grow stay well inside it): a size outside it off the wire is not laid on a copy.</summary>
        public const float SmallestScale = 0.02f;
        public const float LargestScale = 100.0f;

        private readonly BodyMotionPlayback _playback = new BodyMotionPlayback();
        private readonly SenderRate _rate = new SenderRate(MaxMomentsPerSecond);
        private BodyMoment _sent;
        private bool _hasSent;
        private bool _resting;
        private double _lastFrame;

        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private bool IsLiveCopy => NetworkObject != null && IsSpawned && !IsOwner;

        public override void OnStartNetwork()
        {
            _playback.Clear();
            _hasSent = false;
            _resting = false;
        }

        private void Update()
        {
            if (!IsLiveCopy || !_playback.HasAny)
            {
                return;
            }
            BodyMoment shown = _playback.Advance(Time.deltaTime);
            transform.SetPositionAndRotation(shown.Position, shown.Rotation);
            transform.localScale = shown.Scale;
        }

        // After the frame's movement, so what is said is where the body ended this frame, at this frame's time.
        private void LateUpdate()
        {
            if (!IsLiveOwner)
            {
                return;
            }
            double now = Time.timeAsDouble;
            var here = new BodyMoment { Time = now, Position = transform.position, Rotation = transform.rotation, Scale = transform.localScale };
            bool moved = !_hasSent
                || (here.Position - _sent.Position).sqrMagnitude > RestingMetres * RestingMetres
                || Quaternion.Angle(here.Rotation, _sent.Rotation) > RestingDegrees
                || here.Scale != _sent.Scale;
            if (moved)
            {
                if (_resting && _lastFrame > _sent.Time)
                {
                    // Setting off after standing: it stood here until the last frame. Without this word a copy would start sliding from
                    // the last time it was told, up to a fifth of a second before the body moved.
                    BodyMoment stood = _sent;
                    stood.Time = _lastFrame;
                    Send(stood);
                    Send(here);
                }
                else if (!_hasSent || now - _sent.Time >= MovingSendSeconds)
                {
                    Send(here);
                }
                _resting = false;
            }
            else if (now - _sent.Time >= (_resting ? 1.0 / RestingSendsPerSecond : MovingSendSeconds))
            {
                // Standing still: the same place again (the very numbers last sent), once at the moving pace, then at the resting one.
                BodyMoment stood = _sent;
                stood.Time = now;
                Send(stood);
                _resting = true;
            }
            _lastFrame = now;
        }

        private void Send(in BodyMoment moment)
        {
            _sent = moment;
            _hasSent = true;
            var turn = new Vector4(moment.Rotation.x, moment.Rotation.y, moment.Rotation.z, moment.Rotation.w);
            if (IsServerInitialized)
            {
                BroadcastMoment(moment.Time, moment.Position, turn, moment.Scale);   // the host's own body: nobody to ask
            }
            else
            {
                SendMomentToServer(moment.Time, moment.Position, turn, moment.Scale);
            }
        }

        /// <summary>The turn travels as four plain numbers: a packed quaternion arrives in steps a fast spin shows.</summary>
        [ServerRpc(RequireOwnership = true)]
        private void SendMomentToServer(double time, Vector3 position, Vector4 turn, Vector3 scale, Channel channel = Channel.Unreliable)
        {
            if (!TryRead(time, position, turn, scale, out BodyMoment moment) || !_rate.Allow(0, Time.unscaledTime))
            {
                return;
            }
            // The server plays the body back too: what it checks (who is near whom, who can see whom) is measured from where bodies stand.
            _playback.Add(moment);
            BroadcastMoment(time, position, turn, scale);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastMoment(double time, Vector3 position, Vector4 turn, Vector3 scale, Channel channel = Channel.Unreliable)
        {
            // A host is server and client at once: its server side has taken this moment already.
            if (IsServerInitialized || !TryRead(time, position, turn, scale, out BodyMoment moment))
            {
                return;
            }
            _playback.Add(moment);
        }

        /// <summary>A moment off the wire, or false for numbers no body could have: they would throw a copy out of the world or tear it.</summary>
        public static bool TryRead(double time, Vector3 position, Vector4 turn, Vector3 scale, out BodyMoment moment)
        {
            var rotation = new Quaternion(turn.x, turn.y, turn.z, turn.w);
            moment = new BodyMoment { Time = time, Position = position, Rotation = rotation, Scale = scale };
            return !double.IsNaN(time) && !double.IsInfinity(time)
                && WireChecks.Finite(position) && WireChecks.IsRotation(rotation) && WireChecks.Finite(scale)
                && Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) >= SmallestScale && Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) <= LargestScale;
        }
    }
}
