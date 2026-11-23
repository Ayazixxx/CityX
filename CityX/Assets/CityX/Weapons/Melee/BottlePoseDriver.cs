using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// PLAYS THE BOTTLE'S THROW ON A BODY (docs/GLASS_BOTTLE.md), the owner's and other players' copies alike (BottleThrower tells it what to
    /// play): right-click held winds the arm back from the carry into the aim and holds it; let go without throwing, the same clip is played
    /// backward to the carry; a click throws from the aim (clicked before the arm is all the way back, it throws as soon as it is). Says when the hand lets go (<see cref="Released"/>).
    /// The moves are full-body clips made in Blender (CityX/SourceArt/Bottle/bottle_moves.py). Standing, they are the whole body's (the step
    /// into the throw); as the body moves off the legs are given back to the walk, and taken again when it stands, as the giant boomerang's are.
    ///
    /// With no clips in the game (not built yet) a throw lets go at once.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class BottlePoseDriver : MonoBehaviour
    {
        /// <summary>The clips: the wind-up from the carry (its last frame is the aim, held), and the throw from the aim.</summary>
        public const string Ready = RaisedCarry.Clip;
        public const string ThrowClip = "bottle_throw";

        /// <summary>
        /// When the throw lets the bottle go, seconds in: keep this in step with bottle_moves.py (the move's seconds times the share its
        /// RELEASE key sits at).
        /// </summary>
        public const float ReleaseSeconds = 0.55f * 0.36f;

        /// <summary>How long a move takes to come onto the body, and to give it back after, seconds.</summary>
        public const float EaseInSeconds = 0.07f;
        public const float EaseOutSeconds = 0.16f;

        /// <summary>A body going faster than this, m/s, is moving: its moves are from the waist up and it walks on.</summary>
        public const float StandingSpeed = 0.6f;

        /// <summary>A body moving off gives its legs to the walk, and one coming to a stand takes them back, over this long, seconds.</summary>
        public const float LegsSeconds = 0.2f;

        /// <summary>Lowering the arm again (the wind-up played backward) goes this much quicker than raising it.</summary>
        public const float LowerPace = 1.3f;

        public enum Move
        {
            None,
            Raising,
            Aiming,
            Lowering,
            Throwing,
        }

        private PlayerLocomotion _locomotion;
        private BakedMotion _motion;
        private BakedPose _pose;
        private bool _looked;
        private Move _move;
        private float _elapsed;
        private float _weight;
        private float _legs;
        private bool _released;
        private bool _throwWaits;
        private bool _holdsBody;

        public Move Current => _move;

        /// <summary>Right-click is held, whatever is being played.</summary>
        public bool AimHeld { get; private set; }

        /// <summary>The arm is going back, or is back and held.</summary>
        public bool IsAiming => _move == Move.Raising || _move == Move.Aiming;

        /// <summary>A throw is going, or waits for the arm to be back.</summary>
        public bool IsThrowing => _move == Move.Throwing || _throwWaits;

        /// <summary>Raised the moment a throw lets the bottle go.</summary>
        public event System.Action Released;

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

        private bool HasClips()
        {
            if (!_looked)
            {
                _looked = true;
                _motion = BakedMotion.Load(RaisedCarry.MotionPath);
                if (_motion != null && _motion.HasSegment(Ready) && _motion.HasSegment(ThrowClip))
                {
                    _pose = _motion.NewPose();
                }
                else
                {
                    _motion = null;
                }
            }
            return _motion != null;
        }

        /// <summary>Right-click held (true) or let go (false).</summary>
        public void Aim(bool on)
        {
            AimHeld = on;
            if (!HasClips() || IsThrowing)
            {
                return;
            }
            if (on && (_move == Move.None || _move == Move.Lowering))
            {
                Begin(Move.Raising, _move == Move.Lowering ? _elapsed : 0.0f);
            }
            else if (!on && IsAiming && !_throwWaits)
            {
                Begin(Move.Lowering, _move == Move.Aiming ? LastFrameOf(Ready) : _elapsed);
            }
        }

        /// <summary>A click: thrown from the aim; with the arm not yet back, as soon as it is. False if a throw is already going.</summary>
        public bool Throw()
        {
            if (IsThrowing)
            {
                return false;
            }
            if (!HasClips())
            {
                Released?.Invoke();
                return true;
            }
            if (_move == Move.Aiming)
            {
                Begin(Move.Throwing, 0.0f);
            }
            else
            {
                _throwWaits = true;
                if (_move != Move.Raising)
                {
                    Begin(Move.Raising, _move == Move.Lowering ? _elapsed : 0.0f);
                }
            }
            return true;
        }

        /// <summary>Lets go of everything at once (the bottle put away, broken or dropped).</summary>
        public void Stop()
        {
            _move = Move.None;
            _weight = 0.0f;
            _throwWaits = false;
            AimHeld = false;
            LetGoOfTheBody();
        }

        private void Begin(Move move, float at)
        {
            _move = move;
            _elapsed = at;
            _released = false;
            if (_weight <= 0.0f)
            {
                _legs = Standing ? 1.0f : 0.0f;
            }
            enabled = true;
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
            string clip;
            float at;
            switch (_move)
            {
                case Move.Raising:
                    _elapsed += deltaTime;
                    clip = Ready;
                    at = Mathf.Min(_elapsed, LastFrameOf(Ready));
                    if (_elapsed >= LastFrameOf(Ready))
                    {
                        _move = Move.Aiming;
                        if (_throwWaits)
                        {
                            _throwWaits = false;
                            _move = Move.Throwing;
                            _elapsed = 0.0f;
                            _released = false;
                        }
                    }
                    break;
                case Move.Aiming:
                    clip = Ready;
                    at = LastFrameOf(Ready);
                    break;
                case Move.Lowering:
                    _elapsed -= deltaTime * LowerPace;
                    clip = Ready;
                    at = Mathf.Max(0.0f, _elapsed);
                    if (_elapsed <= 0.0f)
                    {
                        _move = Move.None;
                    }
                    break;
                default:
                {
                    _elapsed += deltaTime;
                    clip = ThrowClip;
                    float last = LastFrameOf(clip);
                    at = Mathf.Min(_elapsed, last);
                    if (!_released && _elapsed >= ReleaseSeconds)
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
        }

        private void LetGoOfTheBody()
        {
            if (_holdsBody && _locomotion != null && _locomotion.HoldsBakedPose(_pose))
            {
                _locomotion.SetBakedPose(null, 0.0f);
            }
            _holdsBody = false;
        }
    }
}
