using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// PLAYS THE GIANT BOOMERANG'S MOVES ON A BODY (docs/BOOMERANG.md), owner and other players' copies alike (BoomerangThrower tells it what to play):
    /// right-click held draws it off the back into the aim and holds the aim; let go without throwing, the draw is played backward and it goes back on
    /// the back; a click throws it, from the aim or straight off the back. Says when the hand has it (<see cref="InHand"/>: the item shows it there and not
    /// on the back) and when it lets go (<see cref="Released"/>). The draw, the aim and the aimed throw are on the upper body, so the legs walk on under
    /// them; a quick throw from standing is the whole body, with its step.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class GiantBoomerangPoseDriver : MonoBehaviour
    {
        /// <summary>How long a move takes to come onto the body from what it was doing, and to give it back after, seconds.</summary>
        public const float EaseInSeconds = 0.08f;

        public const float EaseOutSeconds = 0.15f;

        /// <summary>A body going faster than this when it throws, m/s, throws from the waist up and walks on.</summary>
        public const float StandingSpeed = 0.6f;

        public enum Move
        {
            None,
            Drawing,
            Aiming,
            PuttingAway,
            ThrowingAimed,
            ThrowingQuick,
        }

        private PlayerLocomotion _locomotion;
        private BakedMotion _motion;
        private BakedPose _pose;
        private Move _move;
        private float _elapsed;
        private float _weight;
        private bool _released;
        private bool _holdsBody;

        public Move Current => _move;

        /// <summary>Right-click's draw or the aim it holds.</summary>
        public bool IsAiming => _move == Move.Drawing || _move == Move.Aiming;

        public bool IsThrowing => _move == Move.ThrowingAimed || _move == Move.ThrowingQuick;

        /// <summary>The hand has it: from the grab on the back until it lets go, or puts it back.</summary>
        public bool InHand
        {
            get
            {
                switch (_move)
                {
                    case Move.Drawing: return _elapsed >= GiantBoomerangMoves.DrawGrabSeconds;
                    case Move.Aiming: return true;
                    case Move.PuttingAway: return _elapsed >= GiantBoomerangMoves.DrawGrabSeconds;   // the draw backward: _elapsed counts down
                    case Move.ThrowingAimed: return !_released;
                    case Move.ThrowingQuick: return _elapsed >= GiantBoomerangMoves.QuickGrabSeconds && !_released;
                    default: return false;
                }
            }
        }

        /// <summary>Raised the moment a throw lets it go.</summary>
        public event System.Action Released;

        /// <summary>Raised when whether the hand has it changes.</summary>
        public event System.Action HandChanged;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            LetGoOfTheBody();
        }

        private float DrawSeconds => (_motion.SegmentFrames(GiantBoomerangMoves.Draw) - 1) / (float)_motion.FramesPerSecond;

        private bool Ready()
        {
            if (_motion == null)
            {
                _motion = GiantBoomerangMoves.Motion;
                if (_motion == null)
                {
                    return false;
                }
                _pose = _motion.NewPose();
            }
            return true;
        }

        /// <summary>Right-click held (true) or let go (false).</summary>
        public void Aim(bool on)
        {
            if (!Ready() || IsThrowing)
            {
                return;
            }
            if (on && (_move == Move.None || _move == Move.PuttingAway))
            {
                // Put away part of the way, it is drawn again from there.
                Begin(Move.Drawing, _move == Move.PuttingAway ? _elapsed : 0.0f);
            }
            else if (!on && IsAiming)
            {
                Begin(Move.PuttingAway, _move == Move.Aiming ? DrawSeconds : _elapsed);
            }
        }

        /// <summary>A click: from the aim if it is drawn far enough to be in hand, else straight off the back. False if a throw is already going.</summary>
        public bool Throw()
        {
            if (!Ready() || IsThrowing)
            {
                return false;
            }
            bool fromTheAim = _move == Move.Aiming || (_move == Move.Drawing && _elapsed >= GiantBoomerangMoves.DrawGrabSeconds);
            Begin(fromTheAim ? Move.ThrowingAimed : Move.ThrowingQuick, 0.0f);
            // From standing, a quick throw is the whole body with its step; walking, or from the aim (whose legs were the walk's), the waist up.
            _pose.UpperBodyOnly = !(_move == Move.ThrowingQuick && (_locomotion == null || _locomotion.CurrentSpeed < StandingSpeed));
            return true;
        }

        /// <summary>Lets go of everything at once (the boomerang put away by a swap).</summary>
        public void Stop()
        {
            bool hadIt = InHand;
            _move = Move.None;
            _weight = 0.0f;
            LetGoOfTheBody();
            if (hadIt)
            {
                HandChanged?.Invoke();
            }
        }

        private void Begin(Move move, float at)
        {
            bool hadIt = InHand;
            _move = move;
            _elapsed = at;
            _released = false;
            if (_pose != null)
            {
                _pose.UpperBodyOnly = true;
            }
            enabled = true;
            if (hadIt != InHand)
            {
                HandChanged?.Invoke();
            }
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the body this frame's pose. Public so a test can step it.</summary>
        public void Step(float deltaTime)
        {
            if (_move == Move.None || _pose == null)
            {
                _weight = Mathf.MoveTowards(_weight, 0.0f, deltaTime / EaseOutSeconds);
                if (_weight <= 0.0f)
                {
                    LetGoOfTheBody();
                    enabled = false;
                }
                else if (_pose != null && _locomotion != null)
                {
                    _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
                }
                return;
            }
            bool hadIt = InHand;
            string clip;
            float at;
            switch (_move)
            {
                case Move.Drawing:
                    _elapsed += deltaTime;
                    if (_elapsed >= DrawSeconds)
                    {
                        _move = Move.Aiming;
                    }
                    clip = GiantBoomerangMoves.Draw;
                    at = Mathf.Min(_elapsed, DrawSeconds);
                    break;
                case Move.Aiming:
                    clip = GiantBoomerangMoves.Draw;
                    at = DrawSeconds;
                    break;
                case Move.PuttingAway:
                    _elapsed -= deltaTime;
                    clip = GiantBoomerangMoves.Draw;
                    at = Mathf.Max(0.0f, _elapsed);
                    if (_elapsed <= 0.0f)
                    {
                        _move = Move.None;
                    }
                    break;
                default:
                    _elapsed += deltaTime;
                    clip = _move == Move.ThrowingAimed ? GiantBoomerangMoves.ThrowAimed : GiantBoomerangMoves.ThrowQuick;
                    float release = _move == Move.ThrowingAimed ? GiantBoomerangMoves.AimedReleaseSeconds : GiantBoomerangMoves.QuickReleaseSeconds;
                    float last = (_motion.SegmentFrames(clip) - 1) / (float)_motion.FramesPerSecond;
                    at = Mathf.Min(_elapsed, last);
                    if (!_released && _elapsed >= release)
                    {
                        _released = true;
                        Released?.Invoke();
                    }
                    if (_elapsed >= last)
                    {
                        _move = Move.None;
                    }
                    break;
            }
            _motion.Sample(clip, at, false, _pose);
            _weight = _move == Move.None ? _weight : Mathf.MoveTowards(_weight, 1.0f, deltaTime / EaseInSeconds);
            if (_locomotion != null)
            {
                _holdsBody = true;
                _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _weight));
            }
            if (hadIt != InHand)
            {
                HandChanged?.Invoke();
            }
        }

        private void LetGoOfTheBody()
        {
            if (_holdsBody && _locomotion != null)
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _holdsBody = false;
        }
    }
}
