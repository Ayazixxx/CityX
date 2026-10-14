using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the sword attacks (<see cref="SwordStrikes"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when an attack starts (here, or replayed from the network, which is why a copy swings the same way) and
    /// <see cref="BeginGuard"/> when a guard is raised; this advances the attack, holds the READY stance for a few seconds after the last one,
    /// and hands each frame to <see cref="PlayerLocomotion"/>, whose LateUpdate poses the arm, the blade and the torso (after the Animator,
    /// like every other procedural pose).
    ///
    /// The attack's clock is what the combat freezes for a hit-stop (<see cref="SetSpeed"/> 0): the pose holds exactly where the blade landed.
    /// </summary>
    public sealed class SwordPoseDriver : MonoBehaviour
    {
        /// <summary>How long the ready stance is held after the last attack or guard, seconds.</summary>
        public const float ReadyLingerSeconds = 3.0f;

        /// <summary>How quickly the pose takes hold (and lets go back to the carry), seconds.</summary>
        public const float EngageSeconds = 0.08f;

        public const float DisengageSeconds = 0.35f;

        /// <summary>An attack takes over from the pose the body is in (the last one's recovery, a guard) across this share of its own length.</summary>
        public const float AttackTakeoverShare = 0.3f;

        /// <summary>A guard comes up from, and the ready stance returns from, the pose the body is in over this long, seconds.</summary>
        public const float GuardTakeoverSeconds = 0.15f;

        public const float ReleaseTakeoverSeconds = 0.25f;

        private PlayerLocomotion _locomotion;
        private SwordStrikeKind _kind;
        private bool _striking;
        private bool _guarding;
        private bool _oneHanded;
        private float _elapsed;
        private float _duration = 0.5f;
        private float _speed = 1.0f;
        private float _readyRemaining;
        private float _engage;
        private SwordFrame _lastFrame;
        private bool _hasLastFrame;
        private SwordFrame _takeoverFrom;
        private float _takeoverSeconds;
        private float _takeoverRemaining;

        /// <summary>An attack is in progress.</summary>
        public bool IsStriking => _striking;

        /// <summary>A guard is being held.</summary>
        public bool IsGuarding => _guarding;

        public SwordStrikeKind Kind => _kind;

        /// <summary>Which hold the stance is for (the last attack's or guard's).</summary>
        public bool OneHanded => _oneHanded;

        /// <summary>0 to 1 through the attack in progress.</summary>
        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        /// <summary>0 to 1: how fully the arms and torso are in the pose (0 means the clip's own).</summary>
        public float Engage => _engage;

        /// <summary>The clock's rate: 1 normally, 0 held still by a hit-stop.</summary>
        public float Speed => _speed;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.ClearSwordPose();
            }
        }

        /// <summary>An attack starts (an earlier one still going is replaced: the chain's next cut).</summary>
        public void Begin(SwordStrikeKind kind)
        {
            StartTakeover(SwordStrikes.Seconds(kind) * AttackTakeoverShare);
            _kind = kind;
            _oneHanded = SwordStrikes.IsOneHanded(kind);
            _striking = true;
            _guarding = false;
            _elapsed = 0.0f;
            _duration = SwordStrikes.Seconds(kind);
            _speed = 1.0f;
            _readyRemaining = ReadyLingerSeconds;
            enabled = true;
        }

        /// <summary>A guard is raised and held until <see cref="Release"/> or <see cref="Cancel"/>.</summary>
        public void BeginGuard(bool oneHanded)
        {
            StartTakeover(GuardTakeoverSeconds);
            _oneHanded = oneHanded;
            _striking = false;
            _guarding = true;
            _speed = 1.0f;
            _readyRemaining = ReadyLingerSeconds;
            enabled = true;
        }

        /// <summary>How long the parry flourish takes, seconds: the guard snapped forward and back.</summary>
        public const float ParrySeconds = 0.22f;

        private float _parryRemaining;

        /// <summary>True while the parry flourish plays.</summary>
        public bool IsParrying => _parryRemaining > 0.0f;

        /// <summary>The guard caught a blow in its first instant: the sword snaps forward into it and back (on top of the held guard).</summary>
        public void Parry()
        {
            _parryRemaining = ParrySeconds;
            enabled = true;
        }

        /// <summary>Ends the attack or guard in progress; the ready stance lingers (a swing's end, a guard lowered).</summary>
        public void Release()
        {
            if (_striking || _guarding)
            {
                StartTakeover(ReleaseTakeoverSeconds);
            }
            _striking = false;
            _guarding = false;
            _speed = 1.0f;
        }

        /// <summary>Ends everything and drops the stance at once (a ragdoll, a climb, the sword put away).</summary>
        public void Cancel()
        {
            _striking = false;
            _guarding = false;
            _speed = 1.0f;
            _readyRemaining = 0.0f;
            _takeoverRemaining = 0.0f;
        }

        /// <summary>The next pose starts from the one the body is actually in and blends into its own keys, rather than snapping.</summary>
        private void StartTakeover(float seconds)
        {
            if (_hasLastFrame && _engage > 0.5f && seconds > 0.0f)
            {
                _takeoverFrom = _lastFrame;
                _takeoverSeconds = seconds;
                _takeoverRemaining = seconds;
            }
            else
            {
                _takeoverRemaining = 0.0f;
            }
        }

        /// <summary>Sets the attack clock's rate: 0 freezes the pose (a hit-stop), 1 runs it.</summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.0f, float.IsNaN(speed) ? 1.0f : speed);
        }

        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private Ragdoll _ragdoll;
        private GrappleSwing _grapple;
        private bool _looked;

        /// <summary>Something else owns the body: a climb or vault, a ragdoll, a grapple, a seat, the sword spin or a baked move. The pose yields to all of them.</summary>
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
                || (_locomotion != null && (_locomotion.IsSitting || _locomotion.InCarSeat
                    || _locomotion.SwordSpinPoseBlend > 0.01f || _locomotion.BakedPoseWeight > 0.01f));
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's pose. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            if ((_striking || _guarding || _readyRemaining > 0.0f || _engage > 0.0f) && BodyIsBusy())
            {
                Cancel();
                _engage = 0.0f;
            }
            if (_striking)
            {
                _elapsed += deltaTime * _speed;
                if (_elapsed >= _duration)
                {
                    _striking = false;
                    _readyRemaining = ReadyLingerSeconds;
                }
            }
            else if (!_guarding && _readyRemaining > 0.0f)
            {
                _readyRemaining -= deltaTime;
            }

            bool wantsPose = _striking || _guarding || _readyRemaining > 0.0f;
            _engage = Mathf.MoveTowards(_engage, wantsPose ? 1.0f : 0.0f, deltaTime / (wantsPose ? EngageSeconds : DisengageSeconds));

            if (_locomotion == null)
            {
                return;
            }
            if (_engage <= 0.001f && !wantsPose)
            {
                _locomotion.ClearSwordPose();
                _hasLastFrame = false;
                enabled = false;
                return;
            }

            SwordFrame frame = _striking ? SwordStrikes.Evaluate(_kind, Progress)
                : (_guarding ? SwordStrikes.Guard(_oneHanded) : SwordStrikes.Ready(_oneHanded));
            if (_takeoverRemaining > 0.0f)
            {
                _takeoverRemaining -= deltaTime * _speed;   // held still by a hit-stop like the attack itself
                float s = 1.0f - Mathf.Clamp01(_takeoverRemaining / Mathf.Max(_takeoverSeconds, 1e-4f));
                frame = SwordStrikes.Lerp(_takeoverFrom, frame, Mathf.SmoothStep(0.0f, 1.0f, s));
            }
            if (_parryRemaining > 0.0f)
            {
                // A pulse: out and back over ParrySeconds, the sword pushed forward and up into the blow, the torso turned into it.
                _parryRemaining -= deltaTime;
                float pulse = Mathf.Sin(Mathf.PI * (1.0f - Mathf.Clamp01(_parryRemaining / ParrySeconds)));
                frame.Hand += new Vector3(0.0f, 0.06f, 0.18f) * pulse;
                frame.TwistDegrees -= 12.0f * pulse;
                frame.LeanDegrees += 6.0f * pulse;
            }
            _lastFrame = frame;
            _hasLastFrame = true;
            frame.Weight = Mathf.SmoothStep(0.0f, 1.0f, _engage);
            _locomotion.SetSwordPose(frame);
        }
    }
}
