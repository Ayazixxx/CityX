using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays a gun's reload (<see cref="GunReload"/>) on a body, owner or network copy alike: the gun in hand registers its reload motion
    /// (<see cref="SetMotion"/>) and its moving parts (PlayerLocomotion.RegisterBakedProp); <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when the reload starts here or is replayed from the network; this samples the motion every frame and hands it to
    /// <see cref="PlayerLocomotion"/> as an UPPER-BODY-ONLY baked pose (the legs go on walking, the feet stay planted), eased in and out, and
    /// raises the motion's events (<see cref="Marker"/>: the magazine dropped, grabbed, seated; the action racked) as their frames pass, which is
    /// how the gun knows when the ammo is in.
    ///
    /// A reload yields to anything that owns the whole body (a ragdoll, a climb, a grapple, a seat): it is cancelled, and the pose eases out
    /// from wherever it was rather than snapping.
    /// </summary>
    public sealed class GunReloadDriver : MonoBehaviour
    {
        /// <summary>How long the reload pose takes over from the aim or carry pose, and gives the body back after, seconds.</summary>
        public const float BlendInSeconds = 0.1f;

        public const float BlendOutSeconds = 0.16f;

        private PlayerLocomotion _locomotion;
        private BakedMotion _motion;
        private BakedPose _pose;
        private string _segment;
        private bool _empty;
        private bool _playing;
        private float _elapsed;
        private float _duration;
        private float _weight;
        private float _speed = 1.0f;
        private readonly List<KeyValuePair<string, float>> _events = new List<KeyValuePair<string, float>>();
        private int _nextEvent;
        private readonly HashSet<string> _passed = new HashSet<string>();
        private bool _fadingOut;

        /// <summary>An event's frame was passed: "drop", "grab", "seat", "rack" (GunReload).</summary>
        public event System.Action<string> Marker;

        /// <summary>The reload ran to its end (not raised on a cancel).</summary>
        public event System.Action Finished;

        /// <summary>A reload began, and whether it is the empty one: on an owner's body and on other players' copies alike (the gun plays its own
        /// recording of it from here, FirearmProfile.ReloadSound).</summary>
        public event System.Action<bool> Started;

        /// <summary>A reload is in progress (its motion still playing).</summary>
        public bool IsPlaying => _playing;

        /// <summary>The reload in progress is the empty-gun one (with the rack).</summary>
        public bool IsEmptyReload => _empty;

        /// <summary>Seconds into the reload in progress.</summary>
        public float Elapsed => _elapsed;

        /// <summary>How long the reload in progress runs, seconds (0 with no motion).</summary>
        public float Duration => _duration;

        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        /// <summary>How much of the body the reload pose holds right now, 0 to 1 (still above 0 for a moment after the end).</summary>
        public float Weight => _weight;

        /// <summary>A motion is registered and has the segment for the reload asked for.</summary>
        public bool CanPlay(bool empty) => _motion != null && _motion.HasSegment(GunReload.SegmentFor(empty));

        /// <summary>The event with this name has gone by in the reload in progress.</summary>
        public bool PassedMarker(string name) => _passed.Contains(name);

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            if (_locomotion != null && (_playing || _weight > 0.0f))
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _playing = false;
            _weight = 0.0f;
            _fadingOut = false;
        }

        /// <summary>The gun in hand lends its reload motion (null to take it away: the gun put down).</summary>
        public void SetMotion(BakedMotion motion)
        {
            if (ReferenceEquals(motion, _motion))
            {
                return;
            }
            if (_playing)
            {
                Cancel();
            }
            _motion = motion;
            _pose = motion != null ? motion.NewPose() : null;
        }

        public BakedMotion Motion => _motion;

        /// <summary>
        /// Starts the reload (the empty-gun one with <paramref name="empty"/>). False, and nothing happens, when no motion for it is registered:
        /// the gun then falls back to its own timer.
        /// </summary>
        public bool Begin(bool empty)
        {
            if (!CanPlay(empty))
            {
                return false;
            }
            _segment = GunReload.SegmentFor(empty);
            _empty = empty;
            _playing = true;
            _fadingOut = false;
            _elapsed = 0.0f;
            _speed = 1.0f;
            _duration = _motion.SegmentSeconds(_segment);
            _events.Clear();
            _events.AddRange(_motion.EventsIn(_segment));
            _nextEvent = 0;
            _passed.Clear();
            enabled = true;
            Started?.Invoke(empty);
            return true;
        }

        /// <summary>Stops the reload where it is (a shot fired once the ammo was in, the gun put away, the body taken over); the pose eases out.</summary>
        public void Cancel()
        {
            if (_playing)
            {
                _playing = false;
                _fadingOut = _weight > 0.0f;
            }
        }

        /// <summary>The reload's clock rate (1 normally).</summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.0f, float.IsNaN(speed) ? 1.0f : speed);
        }

        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private Ragdoll _ragdoll;
        private GrappleSwing _grapple;
        private bool _looked;

        /// <summary>Something else owns the whole body: a climb or vault, a ragdoll, a grapple, a seat.</summary>
        private bool BodyIsBusy()
        {
            if (!_looked)
            {
                _looked = true;
                _climbing = GetComponent<PlayerClimbing>();
                _vaulting = GetComponent<PlayerVaulting>();
                _ragdoll = GetComponent<Ragdoll>();
                _grapple = GetComponent<GrappleSwing>();
            }
            return (_climbing != null && (_climbing.IsClimbing || _climbing.IsToppingOut))
                || (_vaulting != null && _vaulting.IsVaulting)
                || (_ragdoll != null && _ragdoll.IsLimp)
                || (_grapple != null && _grapple.IsActive)
                || (_locomotion != null && (_locomotion.IsSitting || _locomotion.InCarSeat || _locomotion.SwordSpinPoseBlend > 0.01f));
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's pose. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            if (_playing && BodyIsBusy())
            {
                Cancel();
            }
            if (_playing)
            {
                _elapsed += deltaTime * _speed;
                while (_nextEvent < _events.Count && _events[_nextEvent].Value <= _elapsed)
                {
                    string name = _events[_nextEvent].Key;
                    _nextEvent++;
                    _passed.Add(name);
                    Marker?.Invoke(name);
                }
                if (_elapsed >= _duration)
                {
                    _elapsed = _duration;
                    _playing = false;
                    _fadingOut = true;
                    Finished?.Invoke();
                }
            }

            if (_playing)
            {
                // Eased in at the start and out toward the end, so the arms come from and go back to the aim or carry pose rather than snapping.
                float fadeIn = Mathf.Clamp01(_elapsed / BlendInSeconds);
                float fadeOut = Mathf.Clamp01((_duration - _elapsed) / BlendOutSeconds);
                _weight = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Min(fadeIn, fadeOut));
            }
            else if (_fadingOut)
            {
                _weight = Mathf.MoveTowards(_weight, 0.0f, deltaTime / BlendOutSeconds);
                if (_weight <= 0.0f)
                {
                    _fadingOut = false;
                }
            }
            else
            {
                _weight = 0.0f;
            }

            if (_locomotion == null)
            {
                return;
            }
            if (_weight <= 0.0f || _pose == null || _motion == null || string.IsNullOrEmpty(_segment))
            {
                _locomotion.SetBakedPose(null, 0.0f);
                if (!_playing && !_fadingOut)
                {
                    enabled = false;
                }
                return;
            }
            // Held at where it stopped while fading out; a cancelled reload lets go from its last frame.
            _motion.Sample(_segment, _elapsed, false, _pose);
            _pose.UpperBodyOnly = true;
            _pose.Turn = Quaternion.identity;
            _locomotion.SetBakedPose(_pose, _weight);
        }
    }
}
