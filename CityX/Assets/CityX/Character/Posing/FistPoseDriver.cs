using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the unarmed strikes (<see cref="FistStrikes"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when a punch is thrown (here, or replayed from the network, which is why a copy punches the same way), this advances the blow
    /// and holds the READY stance (a little crouched, the feet planted, the arms left to the walk cycle) for a moment after the last one, and hands each frame to
    /// <see cref="PlayerLocomotion"/>, whose LateUpdate puts the fists where the frame says (after the Animator, like every other procedural pose).
    ///
    /// A HIT FREEZES THE BLOW for a moment (hit-stop): the fist stays at full reach for <see cref="HitHoldSeconds"/> when it lands, the single cheapest
    /// thing that makes a punch feel like it connected.
    /// </summary>
    public sealed class FistPoseDriver : MonoBehaviour
    {
        /// <summary>How long the ready stance is held after the last blow, seconds (then the body goes back to standing as it always does).</summary>
        public const float ReadyLingerSeconds = 1.5f;

        /// <summary>How quickly the arms come into the pose (and go back to the walk cycle's), seconds.</summary>
        public const float EngageSeconds = 0.09f;

        public const float DisengageSeconds = 0.35f;

        /// <summary>How long the next punch takes to take over from one still recovering, seconds.</summary>
        public const float CrossfadeSeconds = 0.1f;

        /// <summary>How long a landed blow holds at full reach, seconds.</summary>
        public const float HitHoldSeconds = 0.055f;

        private PlayerLocomotion _locomotion;
        private MeleeAttacker _melee;
        private FistKind _kind;
        private bool _striking;
        private float _elapsed;
        private float _duration = 0.3f;
        private float _hold;
        private float _readyRemaining;
        private float _engage;
        private FistFrame _shown;
        private FistFrame _crossfadeFrom;
        private float _crossfadeRemaining;

        /// <summary>A blow is in progress.</summary>
        public bool IsStriking => _striking;

        public FistKind Kind => _kind;

        /// <summary>0 to 1 through the blow in progress.</summary>
        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        /// <summary>0 to 1: how fully the arms are in the pose (0 means the walk cycle's own arms).</summary>
        public float Engage => _engage;

        private void Awake()
        {
            _locomotion = GetComponent<PlayerLocomotion>();
            _melee = GetComponent<MeleeAttacker>();
        }

        private void OnEnable()
        {
            if (_melee != null)
            {
                _melee.Landed += OnLanded;
            }
        }

        private void OnDisable()
        {
            if (_melee != null)
            {
                _melee.Landed -= OnLanded;
            }
            if (_locomotion != null)
            {
                _locomotion.ClearFistPose();
            }
        }

        private void OnLanded(IDamageable target, float damage)
        {
            if (_striking)
            {
                _hold = HitHoldSeconds;
            }
        }

        /// <summary>A blow starts (an earlier one still going is replaced: the chain's next punch).</summary>
        public void Begin(FistKind kind)
        {
            if (_striking)
            {
                _crossfadeFrom = _shown;
                _crossfadeRemaining = CrossfadeSeconds;
            }
            _kind = kind;
            _striking = true;
            _elapsed = 0.0f;
            _speed = 1.0f;
            _duration = FistStrikes.Seconds(kind);
            _hold = 0.0f;
            _readyRemaining = ReadyLingerSeconds;
            enabled = true;
        }

        /// <summary>The blow's clock rate (Melee Speed, 2026-10-14): set after <see cref="Begin"/>, which starts it at 1.</summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.0f, float.IsNaN(speed) ? 1.0f : speed);
        }

        private float _speed = 1.0f;

        /// <summary>Ends any blow and drops the stance at once (a block, a ragdoll, an equipped weapon).</summary>
        public void Cancel()
        {
            _striking = false;
            _readyRemaining = 0.0f;
            _crossfadeRemaining = 0.0f;
        }

        private PlayerInputReader _reader;
        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private Ragdoll _ragdoll;
        private GrappleSwing _grapple;

        /// <summary>
        /// Something else owns the arms: a weapon or gun or tool in hand, a climb or vault, a ragdoll, a grapple, a sit. The fists never fight those poses
        /// for the arms (a punch is thrown with bare hands), and the stance is dropped at once.
        /// </summary>
        private bool ArmsAreBusy()
        {
            if (_melee != null && _melee.EquippedWeapon != null)
            {
                return true;
            }
            if (_reader == null)
            {
                _reader = GetComponent<PlayerInputReader>();
                _climbing = GetComponent<PlayerClimbing>();
                _vaulting = GetComponent<PlayerVaulting>();
                _ragdoll = GetComponent<Ragdoll>();
                _grapple = GetComponent<GrappleSwing>();
            }
            if (_reader != null && (_reader.EquippedGun != null || _reader.EquippedFlamethrower != null || _reader.EquippedStaff != null))
            {
                return true;
            }
            return (_climbing != null && (_climbing.IsClimbing || _climbing.IsToppingOut))
                || (_vaulting != null && _vaulting.IsVaulting)
                || (_ragdoll != null && _ragdoll.IsLimp)
                || (_grapple != null && _grapple.IsActive)
                || (_locomotion != null && _locomotion.IsSitting);
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        /// <summary>Advances by <paramref name="deltaTime"/> and gives the locomotion this frame's pose. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            if ((_striking || _readyRemaining > 0.0f || _engage > 0.0f) && ArmsAreBusy())
            {
                Cancel();
                _engage = 0.0f;
            }
            if (_striking)
            {
                if (_hold > 0.0f && FistStrikes.ContactFraction(_kind) <= Progress)
                {
                    _hold -= deltaTime;   // frozen at the contact for a moment
                }
                else
                {
                    _elapsed += deltaTime * _speed;
                }
                if (_elapsed >= _duration)
                {
                    _striking = false;
                }
            }
            else if (_readyRemaining > 0.0f)
            {
                _readyRemaining -= deltaTime;
            }

            bool wantsPose = _striking || _readyRemaining > 0.0f;
            _engage = Mathf.MoveTowards(_engage, wantsPose ? 1.0f : 0.0f, deltaTime / (wantsPose ? EngageSeconds : DisengageSeconds));

            if (_locomotion == null)
            {
                return;
            }
            if (_engage <= 0.001f && !wantsPose)
            {
                _locomotion.ClearFistPose();
                enabled = false;
                return;
            }

            FistFrame frame = _striking ? FistStrikes.Evaluate(_kind, Progress) : FistStrikes.Ready();
            if (_crossfadeRemaining > 0.0f)
            {
                _crossfadeRemaining -= deltaTime;
                frame = FistStrikes.Blend(_crossfadeFrom, frame, Mathf.SmoothStep(0.0f, 1.0f, 1.0f - _crossfadeRemaining / CrossfadeSeconds));
            }
            _shown = frame;
            float w = Mathf.SmoothStep(0.0f, 1.0f, _engage);
            frame.RightWeight *= w;
            frame.LeftWeight *= w;
            frame.TwistDegrees *= w;
            frame.LeanDegrees *= w;
            frame.HipDrop *= w;
            frame.Footwork *= w;
            _locomotion.SetFistPose(frame);
        }
    }
}
