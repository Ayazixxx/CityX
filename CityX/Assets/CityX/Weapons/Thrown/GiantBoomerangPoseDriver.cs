using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// PLAYS THE GIANT BOOMERANG'S MOVES ON A BODY (docs/BOOMERANG.md), owner and other players' copies alike (BoomerangThrower tells it what to play):
    /// right-click held draws it off the back into the aim and holds the aim; let go without throwing, the draw is played backward and it goes back on
    /// the back; a click throws it, from the aim or straight off the back. Coming home it is caught (<see cref="Catch"/>): the hand goes out for it, the arm
    /// gives with it and comes round into the aim, which is held if right-click is (even if it was pressed while the boomerang was still out), or else the
    /// boomerang is put back on the back. Says when the hand has it (<see cref="InHand"/>: the item shows it there and not on the back) and when it lets go
    /// (<see cref="Released"/>). Each giant boomerang has its own clips (<see cref="Clips"/>). Standing, the moves are the whole body's (the bone one's
    /// lunge, the Iron Spiked one's sunk, side-on stance); as the body moves off the legs are given back to the walk, and taken again when it stands.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class GiantBoomerangPoseDriver : MonoBehaviour
    {
        /// <summary>How long a move takes to come onto the body from what it was doing, and to give it back after, seconds.</summary>
        public const float EaseInSeconds = 0.08f;

        public const float EaseOutSeconds = 0.15f;

        /// <summary>A body going faster than this, m/s, is moving: its moves are from the waist up and it walks on.</summary>
        public const float StandingSpeed = 0.6f;

        /// <summary>Putting it back on the back (the draw played backward) goes this much quicker than the draw (owner, 2026-11-12, of the catch and the return to
        /// the back: "id like it a bit faster").</summary>
        public const float PutAwayPace = 1.4f;

        public enum Move
        {
            None,
            Drawing,
            Aiming,
            PuttingAway,
            ThrowingAimed,
            ThrowingQuick,
            Catching,
        }

        private PlayerLocomotion _locomotion;
        private BakedMotion _motion;
        private BakedPose _pose;
        private Move _move;
        private float _elapsed;
        private float _weight;
        private bool _released;
        private bool _holdsBody;
        private bool _aimHeld;
        private bool _out;
        private GiantBoomerangClips _clips = GiantBoomerangMoves.Bone;
        private float _legs;

        /// <summary>A body moving off gives its legs to the walk, and one coming to a stand takes them back, over this long, seconds.</summary>
        public const float LegsSeconds = 0.2f;

        public Move Current => _move;

        /// <summary>Which giant boomerang's moves are played (the item carried says: GiantBoomerangItem).</summary>
        public GiantBoomerangClips Clips
        {
            get => _clips;
            set
            {
                if (value == null || ReferenceEquals(_clips, value))
                {
                    return;
                }
                LetGoOfTheBody();
                _clips = value;
                _motion = null;
                _pose = null;
                _weight = 0.0f;
            }
        }

        /// <summary>Right-click's draw or the aim it holds.</summary>
        public bool IsAiming => _move == Move.Drawing || _move == Move.Aiming;

        public bool IsThrowing => _move == Move.ThrowingAimed || _move == Move.ThrowingQuick;

        public bool IsCatching => _move == Move.Catching;

        /// <summary>Right-click is held, whatever is being played: the catch goes on into the aim if it is.</summary>
        public bool AimHeld => _aimHeld;

        /// <summary>
        /// The boomerang is in the air (BoomerangThrower says so): there is nothing to draw, and a catch begun before it is home has not got it yet. Set false as
        /// it is caught, the hand has it.
        /// </summary>
        public bool Out
        {
            get => _out;
            set
            {
                if (_out == value)
                {
                    return;
                }
                bool hadIt = InHand;
                _out = value;
                if (hadIt != InHand)
                {
                    HandChanged?.Invoke();
                }
            }
        }

        /// <summary>The hand has it: from the grab on the back until it lets go, or puts it back; caught, from the moment it is home.</summary>
        public bool InHand
        {
            get
            {
                switch (_move)
                {
                    case Move.Drawing: return _elapsed >= _clips.DrawGrabSeconds;
                    case Move.Aiming: return true;
                    case Move.PuttingAway: return _elapsed > _clips.DrawGrabSeconds;   // the draw backward: _elapsed counts down
                    case Move.ThrowingAimed: return !_released;
                    case Move.ThrowingQuick: return _elapsed >= _clips.QuickGrabSeconds && !_released;
                    case Move.Catching: return !_out;
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

        private bool Standing => _locomotion == null || _locomotion.CurrentSpeed < StandingSpeed;

        private float LastFrameOf(string clip) => (_motion.SegmentFrames(clip) - 1) / (float)_motion.FramesPerSecond;

        private float DrawSeconds => LastFrameOf(_clips.Draw);

        private bool Ready()
        {
            if (_motion == null)
            {
                _motion = _clips.Motion;
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
            _aimHeld = on;
            if (!Ready() || IsThrowing || IsCatching || _out)
            {
                return;   // remembered: a catch goes on into the aim, or puts it away, as right-click then is
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

        /// <summary>A click: from the aim if it is drawn far enough to be in hand, else straight off the back. False if a throw or a catch is going, or it is out.</summary>
        public bool Throw()
        {
            if (!Ready() || IsThrowing || IsCatching || _out)
            {
                return false;
            }
            bool fromTheAim = _move == Move.Aiming || (_move == Move.Drawing && _elapsed >= _clips.DrawGrabSeconds);
            Begin(fromTheAim ? Move.ThrowingAimed : Move.ThrowingQuick, 0.0f);
            return true;
        }

        /// <summary>It is coming home: the hand goes out for it. Begun before it is home (it is caught as <see cref="Out"/> goes false), or as it gets there if not.</summary>
        public void Catch()
        {
            if (!Ready() || IsCatching)
            {
                return;
            }
            Begin(Move.Catching, 0.0f);
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
                _pose.UpperBodyOnly = false;
                if (_weight <= 0.0f)
                {
                    _legs = Standing ? 1.0f : 0.0f;
                }
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
                    _elapsed = Stepped(_elapsed, deltaTime, _clips.DrawGrabSeconds);
                    if (_elapsed >= DrawSeconds)
                    {
                        _move = Move.Aiming;
                    }
                    clip = _clips.Draw;
                    at = Mathf.Min(_elapsed, DrawSeconds);
                    break;
                case Move.Aiming:
                    clip = _clips.Draw;
                    at = DrawSeconds;
                    break;
                case Move.PuttingAway:
                    _elapsed = Stepped(_elapsed, -deltaTime * PutAwayPace, _clips.DrawGrabSeconds);
                    clip = _clips.Draw;
                    at = Mathf.Max(0.0f, _elapsed);
                    if (_elapsed <= 0.0f)
                    {
                        _move = Move.None;
                    }
                    break;
                case Move.Catching:
                {
                    _elapsed += deltaTime;
                    clip = _clips.Catch;
                    float last = LastFrameOf(clip);
                    // Not home yet when the hand is out for it: the hand waits there for it.
                    at = _out ? Mathf.Min(_elapsed, GiantBoomerangMoves.CatchSeconds) : Mathf.Min(_elapsed, last);
                    if (_out)
                    {
                        _elapsed = Mathf.Min(_elapsed, GiantBoomerangMoves.CatchSeconds);
                    }
                    else if (_elapsed >= last)
                    {
                        // Round into the aim: held if right-click is, else put back on the back (the draw backward, from the aim it ended on).
                        if (_aimHeld)
                        {
                            _move = Move.Aiming;
                        }
                        else
                        {
                            _move = Move.PuttingAway;
                            _elapsed = DrawSeconds;
                        }
                    }
                    break;
                }
                default:
                {
                    _elapsed = _move == Move.ThrowingQuick ? Stepped(_elapsed, deltaTime, _clips.QuickGrabSeconds) : _elapsed + deltaTime;
                    clip = _move == Move.ThrowingAimed ? _clips.ThrowAimed : _clips.ThrowQuick;
                    float release = _move == Move.ThrowingAimed ? _clips.AimedReleaseSeconds : _clips.QuickReleaseSeconds;
                    float last = LastFrameOf(clip);
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
            }
            _motion.Sample(clip, at, false, _pose);
            _legs = Mathf.MoveTowards(_legs, Standing ? 1.0f : 0.0f, deltaTime / LegsSeconds);
            _pose.LegsWeight = Mathf.SmoothStep(0.0f, 1.0f, _legs);
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

        /// <summary>
        /// A move's time a frame on, stopped at its <paramref name="grab"/> on the frame it passes it: the hand takes the boomerang off the back, or lays it
        /// back on it, in the grab's own pose however long the frame (the Iron Spiked one's grab is where it hangs: a frame past it, it jumped).
        /// </summary>
        private static float Stepped(float elapsed, float by, float grab)
        {
            float next = elapsed + by;
            return (elapsed < grab && next > grab) || (elapsed > grab && next < grab) ? grab : next;
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
