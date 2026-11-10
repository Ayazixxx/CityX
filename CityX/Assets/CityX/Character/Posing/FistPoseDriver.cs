using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Plays the unarmed strikes (<see cref="FistStrikes"/>) on a body, owner or network copy alike: <see cref="PlayerAnimationDriver"/> calls
    /// <see cref="Begin"/> when a punch is thrown (here, or replayed from the network, which is why a copy punches the same way). Each blow is
    /// a full-body clip (FistStrikes.Motion), sampled on the blow's own clock and handed to <see cref="PlayerLocomotion"/>, whose LateUpdate
    /// puts it on the body after the Animator. Between blows, and for a moment after the last, the READY stance is held: a little crouched,
    /// the feet planted, the fists closed, the arms left to the walk cycle.
    ///
    /// A HIT FREEZES THE BLOW for a moment (hit-stop): the fist stays at full reach for <see cref="HitHoldSeconds"/> when it lands, the single
    /// cheapest thing that makes a punch feel like it connected.
    ///
    /// THE STRIDE IS AS LONG AS THE BODY REALLY WENT. A clip's feet are planted on ground the body is carried over (FistStrikes.Carried); a body
    /// stopped short by whoever it is hitting has gone less far, so the feet are drawn in by that share (PlayerLocomotion.SetFistStride), or
    /// the planted foot would slide forward under a body standing still.
    /// </summary>
    public sealed class FistPoseDriver : MonoBehaviour
    {
        /// <summary>How long the ready stance is held after the last blow, seconds (then the body goes back to standing as it always does).</summary>
        public const float ReadyLingerSeconds = 1.5f;

        /// <summary>How quickly the stance and a blow's clip come onto the body, seconds, and how quickly the stance lets go.</summary>
        public const float EngageSeconds = 0.09f;

        public const float DisengageSeconds = 0.35f;

        /// <summary>How quickly a finished blow's clip hands the body back to the walk cycle, seconds.</summary>
        public const float ReleaseSeconds = 0.2f;

        /// <summary>How long the next punch takes to take over from one still recovering, seconds.</summary>
        public const float CrossfadeSeconds = 0.1f;

        /// <summary>How long a landed blow holds at full reach, seconds.</summary>
        public const float HitHoldSeconds = 0.055f;

        /// <summary>The body must have been meant to go this far, metres, before how far it went says anything about the stride.</summary>
        private const float LeastCarryToMeasure = 0.03f;

        /// <summary>A body still carrying some pace into a blow goes further than the clip's stride: the feet reach no further than this share of it.</summary>
        private const float LongestStride = 1.25f;

        private PlayerLocomotion _locomotion;
        private MeleeAttacker _melee;
        private FistKind _kind;
        private bool _striking;
        private float _elapsed;
        private float _duration = 0.3f;
        private float _speed = 1.0f;
        private float _hold;
        private float _readyRemaining;
        private float _engage;
        private BakedMotion _motion;
        private BakedPose _pose;
        private BakedPose _crossfadeFrom;
        private float _crossfadeRemaining;
        private float _clipWeight;
        private bool _holdsBody;
        private Vector3 _beganAt;

        /// <summary>A blow is in progress.</summary>
        public bool IsStriking => _striking;

        public FistKind Kind => _kind;

        /// <summary>0 to 1 through the blow in progress.</summary>
        public float Progress => _duration > 0.0f ? Mathf.Clamp01(_elapsed / _duration) : 0.0f;

        /// <summary>0 to 1: how fully the body is in the stance (0 means standing as it always does).</summary>
        public float Engage => _engage;

        /// <summary>0 to 1: how much of the body a blow's clip holds.</summary>
        public float ClipWeight => _clipWeight;

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
            Release();
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
            if (_motion == null)
            {
                _motion = FistStrikes.Motion;
                if (_motion != null)
                {
                    _pose = _motion.NewPose();
                    _crossfadeFrom = _motion.NewPose();
                }
            }
            if (_clipWeight > 0.0f && _pose != null)
            {
                _crossfadeFrom.CopyBodyFrom(_pose);
                _crossfadeRemaining = CrossfadeSeconds;
            }
            _kind = kind;
            _striking = true;
            _elapsed = 0.0f;
            _speed = 1.0f;
            _duration = FistStrikes.Seconds(kind);
            _hold = 0.0f;
            _readyRemaining = ReadyLingerSeconds;
            _beganAt = transform.position;
            enabled = true;
        }

        /// <summary>The blow's clock rate (Melee Speed, 2026-10-14): set after <see cref="Begin"/>, which starts it at 1.</summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.0f, float.IsNaN(speed) ? 1.0f : speed);
        }

        /// <summary>Ends any blow and drops the stance at once (a block, a ragdoll, an equipped weapon).</summary>
        public void Cancel()
        {
            _striking = false;
            _readyRemaining = 0.0f;
            _crossfadeRemaining = 0.0f;
            Release();
        }

        /// <summary>Lets go of the body, if a blow's clip has it: the baked pose is one slot, and a reload or a sword move may have it next.</summary>
        private void Release()
        {
            _clipWeight = 0.0f;
            if (_holdsBody && _locomotion != null)
            {
                _locomotion.SetBakedPose(null, 0.0f);
                _locomotion.SetFistStride(1.0f);
            }
            _holdsBody = false;
        }

        private PlayerInputReader _reader;
        private PlayerClimbing _climbing;
        private PlayerVaulting _vaulting;
        private Ragdoll _ragdoll;
        private GrappleSwing _grapple;

        /// <summary>
        /// Something else owns the body: a weapon or gun or tool in hand, a climb or vault, a ragdoll, a grapple, a sit. The fists never fight those poses
        /// (a punch is thrown with bare hands), and the stance is dropped at once.
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

            bool wantsStance = _striking || _readyRemaining > 0.0f;
            _engage = Mathf.MoveTowards(_engage, wantsStance ? 1.0f : 0.0f, deltaTime / (wantsStance ? EngageSeconds : DisengageSeconds));
            _clipWeight = Mathf.MoveTowards(_clipWeight, _striking ? 1.0f : 0.0f, deltaTime / (_striking ? EngageSeconds : ReleaseSeconds));

            if (_locomotion == null)
            {
                return;
            }
            ShowClip(deltaTime);
            if (_engage <= 0.001f && !wantsStance)
            {
                _locomotion.ClearFistPose();
                enabled = false;
                return;
            }

            // The stance under the clip and after it: where the feet stand once the clip lets go.
            FistFrame stance = FistStrikes.Ready();
            float w = Mathf.SmoothStep(0.0f, 1.0f, _engage);
            stance.HipDrop *= w;
            stance.Footwork *= w;
            _locomotion.SetFistPose(stance);
        }

        /// <summary>This frame of the blow's clip on the body, or the body let go of once the clip has faded out.</summary>
        private void ShowClip(float deltaTime)
        {
            if (_clipWeight <= 0.0f || _pose == null)
            {
                if (_holdsBody)
                {
                    Release();
                }
                return;
            }
            // A finished blow holds its last frame (standing ready) while it hands the body back.
            _motion.Sample(FistStrikes.NameOf(_kind), _elapsed, false, _pose);
            if (_crossfadeRemaining > 0.0f)
            {
                _crossfadeRemaining -= deltaTime;
                _pose.MixBodyFrom(_crossfadeFrom, Mathf.SmoothStep(0.0f, 1.0f, 1.0f - _crossfadeRemaining / CrossfadeSeconds));
            }
            _holdsBody = true;
            _locomotion.SetBakedPose(_pose, Mathf.SmoothStep(0.0f, 1.0f, _clipWeight));
            _locomotion.SetFistStride(StrideShare());
        }

        /// <summary>How far the body has really been carried since the blow began, as a share of how far the clip's feet are planted for.</summary>
        private float StrideShare()
        {
            float meant = FistStrikes.Carried(_kind, _elapsed);
            if (meant < LeastCarryToMeasure)
            {
                return 1.0f;
            }
            Vector3 gone = transform.position - _beganAt;
            gone.y = 0.0f;
            // A giant's stride is a giant's: the clip's metres are the body's own.
            return Mathf.Clamp(Vector3.Dot(gone, transform.forward) / (meant * transform.lossyScale.y), 0.0f, LongestStride);
        }
    }
}
