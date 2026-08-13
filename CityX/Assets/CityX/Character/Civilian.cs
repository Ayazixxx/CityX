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
    public sealed class Civilian : MonoBehaviour, IDamageable
    {
        public const float MaxHealth = 100.0f;

        [Tooltip("Seeds the wander. Different values give different routes from the same spot.")]
        public int Seed;

        [Tooltip("How fast the body turns to face where it is going, in degrees per second.")]
        public float TurnDegreesPerSecond = 360.0f;

        public float Gravity = 9.8f;

        private CharacterController _controller;
        private Animator _animator;
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

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _ragdoll = GetComponent<Ragdoll>();
            _brain = new CivilianBrain(Seed);
        }

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
            Vector3 direction = _brain.Tick(transform.position, deltaTime);

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

            Vector3 motion = direction * CivilianBrain.WalkSpeed;
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
                    direction.sqrMagnitude > 0.0001f ? CivilianBrain.WalkSpeed : 0.0f);
                _animator.SetBool(GroundedParameter, _controller.isGrounded);
            }
        }

        void IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (IsDead)
            {
                return;
            }

            _health = Mathf.Max(0.0f, _health - amount);
            if (!IsDead)
            {
                return;
            }

            if (_ragdoll != null)
            {
                // Thrown away from whoever hit them, so a punch reads as having
                // landed rather than as the body deciding to lie down.
                Vector3 away = transform.position - fromPosition;
                away.y = 0.0f;
                _ragdoll.Activate(away.normalized * 2.0f);
            }
        }
    }
}
