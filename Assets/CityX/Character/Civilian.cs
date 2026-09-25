using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A pedestrian. Walks the pavement, can be punched, falls over when
    /// killed.
    ///
    /// ONLY EVER PLACED DELIBERATELY. CivilianPopulation used to keep a pooled
    /// dozen streaming in and out around the player; it was deleted at the
    /// user's request -- the crowd was not wanted -- matching the Godot build,
    /// which retired its own civilian_population.gd to
    /// archive/civilian_population/ for the same reason. Nothing places one
    /// automatically now. The scale screenshots still stand a few in a street
    /// on purpose, and that is the remaining use.
    ///
    /// Reuses the player's own body: the same rig, animator and ragdoll,
    /// wearing a different material. That is deliberate rather than lazy --
    /// what the city needs most right now is other bodies at real scale
    /// standing next to the buildings, and a second body pipeline would take
    /// far longer and give a worse answer to that question.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Civilian : MonoBehaviour, IDamageable, IXpReward
    {
        public const float MaxHealth = 100.0f;

        /// <summary>Matches civilian.gd's own XP_REWARD, copied from wolf.gd's convention.</summary>
        public const int XpRewardAmount = 20;

        [Tooltip("Seeds the wander. Different values give different routes from the same spot.")]
        public int Seed;

        [Tooltip("How fast the body turns to face where it is going, in degrees per second.")]
        public float TurnDegreesPerSecond = 360.0f;

        public float Gravity = 9.8f;

        private CharacterController _controller;
        private Animator _animator;
        private Transform _hips;
        private Transform _spine;
        private Transform _leftFoot;
        private Transform _rightFoot;
        private Ragdoll _ragdoll;
        private CivilianBrain _brain;

        private float _verticalVelocity;
        private float _health = MaxHealth;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");

        public bool IsDead => _health <= 0.0f;

        /// <summary>
        /// Part of IDamageable: it is what stops an attacker wasting a swing on
        /// a corpse, and what makes a felled pedestrian stop being a target.
        /// </summary>
        public bool IsAlive => !IsDead;

        public float Health => _health;

        int IXpReward.XpReward => XpRewardAmount;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _hips = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            _spine = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            _leftFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            _rightFoot = _animator != null ? _animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            _ragdoll = GetComponent<Ragdoll>();
            _brain = new CivilianBrain(Seed);
            _brain.SetRainSense(CurrentWeatherSense.Instance);
            _steering.ConfigureFrom(_controller);
        }

        /// <summary>
        /// See PlayerLocomotion's own identical LateUpdate for why (hips
        /// and spine both -- freezing hips alone just moved the reported
        /// wiggle up to the torso -- plus the same general floor clamp, see
        /// PlayerLocomotion.ClampFeetAboveGround's own comment). Guarded on
        /// _animator.enabled rather than being auto-disabled the way
        /// PlayerLocomotion is: Ragdoll only ever looks up a PlayerLocomotion
        /// to disable (see its own _locomotion field), never a Civilian, so
        /// this component keeps running after Activate() and has to check
        /// for itself -- same condition Update() already uses below for the
        /// same reason.
        /// </summary>
        private void LateUpdate()
        {
            if (_animator == null || !_animator.enabled)
            {
                return;
            }
            if (_hips != null)
            {
                _hips.localRotation = Quaternion.identity;
            }
            if (_spine != null)
            {
                _spine.localRotation = Quaternion.identity;
            }
            if (_leftFoot != null && _rightFoot != null)
            {
                _animator.transform.localPosition = Vector3.zero;
                float lowestFootY = Mathf.Min(_leftFoot.position.y, _rightFoot.position.y);
                float penetration = transform.position.y - lowestFootY;
                if (penetration > 0.0f)
                {
                    _animator.transform.localPosition = new Vector3(0.0f, penetration, 0.0f);
                }
            }
        }

        /// <summary>
        /// Stops a pedestrian walking into the building they happen to be
        /// pointed at. The brain picks somewhere on the street network to go;
        /// getting there without a wall in the way is a separate problem.
        /// </summary>
        private readonly NpcSteering _steering = new NpcSteering();

        /// <summary>
        /// Re-seeds and revives, for a pedestrian being reused from a pool.
        ///
        /// Recycling rather than destroying matters: a crowd that streams in
        /// and out around a walking player would otherwise allocate and free a
        /// full character rig every few seconds, which is exactly the kind of
        /// per-frame churn that shows up as a stutter while running.
        /// </summary>
        public void Restart(int seed, Vector3 position)
        {
            Seed = seed;
            _brain = new CivilianBrain(seed);
            _brain.SetRainSense(CurrentWeatherSense.Instance);
            _health = MaxHealth;
            _verticalVelocity = 0.0f;

            if (_ragdoll != null)
            {
                _ragdoll.Deactivate();
            }

            // The controller has to be off to move it, or it fights the
            // teleport and drags the body back to where it was.
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = wasEnabled;
        }

        private void Update()
        {
            if (IsDead)
            {
                return;   // physics owns the body once the ragdoll is limp
            }

            float deltaTime = Time.deltaTime;
            // The brain picks somewhere on the street network to walk to;
            // getting there without a wall in the way is the steering's job.
            Vector3 direction = _steering.Steer(
                transform, _brain.Tick(transform.position, deltaTime), deltaTime);

            if (_controller.isGrounded && _verticalVelocity <= 0.0f)
            {
                // Slightly negative rather than zero, for the same reason the
                // player's is: exactly zero lets the controller drift off a
                // kerb and report not-grounded.
                _verticalVelocity = -2.0f;
            }
            else
            {
                _verticalVelocity -= Gravity * deltaTime;
            }

            float pace = CivilianBrain.WalkSpeed * _brain.SpeedMultiplier;
            Vector3 motion = direction * pace;
            motion.y = _verticalVelocity;
            _controller.Move(motion * deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, TurnDegreesPerSecond * deltaTime);
            }

            if (_animator != null && _animator.enabled)
            {
                _animator.SetFloat(SpeedParameter,
                    direction.sqrMagnitude > 0.0001f ? pace : 0.0f);
                _animator.SetBool(GroundedParameter, _controller.isGrounded);
            }
        }

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (IsDead)
            {
                return HitResponse.Ignored;
            }

            // A civilian never blocks. They are not a fighter, which is most of
            // the point of hitting one.
            _health = Mathf.Max(0.0f, _health - amount);
            if (!IsDead)
            {
                return HitResponse.Landed;
            }

            if (_ragdoll != null)
            {
                // Thrown away from whoever hit them, so a punch reads as having
                // landed rather than as the body deciding to lie down.
                Vector3 away = transform.position - fromPosition;
                away.y = 0.0f;
                _ragdoll.Activate(away.normalized * 2.0f);
            }

            return HitResponse.Landed;
        }
    }
}
