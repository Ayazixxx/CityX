using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A giant hostile beast -- 5x the player's own scale. Ported from
    /// npc/sloth.gd.
    ///
    /// BUILT ON WOLF'S OWN SHAPE, same as the Godot original states of
    /// itself: the nearest-player scan, the straight-line (steered) chase, a
    /// duck-typed IDamageable, no pathfinding. The differences are bigger and
    /// slower numbers, a down-then-up overhead slam instead of a forward
    /// lunge, and a bipedal/hunched silhouette instead of a quadruped one.
    ///
    /// NO XP REWARD. Godot's own sloth.gd has none either -- it never calls
    /// add_xp the way wolf.gd does on its own attacker. Inventing one here
    /// would be adding a mechanic the source doesn't have.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Sloth : MonoBehaviour, IDamageable
    {
        public const float MaxHealth = 500.0f;
        public const float DetectionRange = 22.0f;

        /// <summary>Below the player's own sprint speed -- outrunnable, matching Wolf's own "not inescapable" balance.</summary>
        public const float ChaseSpeed = 4.5f;

        public const float AttackRange = 3.2f;
        public const float AttackDamage = 24.0f;
        public const float AttackCooldownSeconds = 1.7f;
        public const float Gravity = 9.8f;

        public const float SlamDropDistance = 0.55f;
        public const float SlamSeconds = 0.32f;
        public const float ToppleSeconds = 0.7f;
        public const float ToppleDropDistance = 0.6f;
        public const float ToppleDegrees = 80.0f;

        public static readonly Color BodyColour = new Color(0.42f, 0.41f, 0.43f);
        public static readonly Color DarkColour = new Color(0.24f, 0.23f, 0.25f);
        public static readonly Color UndersideColour = new Color(0.33f, 0.32f, 0.34f);
        public static readonly Color EyeColour = new Color(0.95f, 0.1f, 0.05f);

        [Tooltip("The visible body. Built by SlothBuilder; slams and topples on its own.")]
        public Transform Model;

        private readonly NpcSteering _steering = new NpcSteering();

        private CharacterController _controller;
        private Transform _target;
        private float _attackCooldown;
        private float _verticalVelocity;
        private float _slamRemaining;
        private float _topple;

        public float Health { get; private set; } = MaxHealth;

        public bool IsDead => Health <= 0.0f;

        public bool IsAlive => !IsDead;

        /// <summary>Raised on death, so anything listening can react -- no reward is implied (see class comment).</summary>
        public event System.Action Felled;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _steering.ConfigureFrom(_controller);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (IsDead)
            {
                Topple(deltaTime);
                return;
            }

            _attackCooldown = Mathf.Max(0.0f, _attackCooldown - deltaTime);
            Slam(deltaTime);

            if (_target == null || !IsValidTarget(_target))
            {
                _target = FindNearestPlayer();
            }

            Vector3 travel = Vector3.zero;
            if (_target != null)
            {
                Vector3 toTarget = _target.position - transform.position;
                toTarget.y = 0.0f;
                float distance = toTarget.magnitude;

                if (distance > DetectionRange)
                {
                    _target = null;
                    _steering.Reset();
                }
                else if (distance > AttackRange)
                {
                    Vector3 direction = _steering.Steer(
                        transform, toTarget / distance, deltaTime);
                    Face(direction);
                    travel = direction * ChaseSpeed;
                }
                else
                {
                    Face(toTarget);
                    if (_attackCooldown <= 0.0f)
                    {
                        SlamAttack();
                    }
                }
            }

            if (_controller.isGrounded && _verticalVelocity <= 0.0f)
            {
                _verticalVelocity = -2.0f;
            }
            else
            {
                _verticalVelocity -= Gravity * deltaTime;
            }
            travel.y = _verticalVelocity;
            _controller.Move(travel * deltaTime);
        }

        private static bool IsValidTarget(Transform target)
        {
            if (target == null)
            {
                return false;
            }
            var vitals = target.GetComponentInParent<PlayerVitals>();
            return vitals != null && vitals.IsAlive;
        }

        private Transform FindNearestPlayer()
        {
            PlayerVitals[] bodies = Object.FindObjectsByType<PlayerVitals>(
                FindObjectsSortMode.None);

            Transform nearest = null;
            float nearestDistance = DetectionRange;
            foreach (PlayerVitals body in bodies)
            {
                if (body.IsDead)
                {
                    continue;
                }
                float distance = Vector3.Distance(transform.position, body.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = body.transform;
                }
            }
            return nearest;
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void SlamAttack()
        {
            _attackCooldown = AttackCooldownSeconds;
            _slamRemaining = SlamSeconds;

            if (_target == null)
            {
                return;
            }
            var damageable = _target.GetComponentInParent<IDamageable>();
            damageable?.ApplyHit(AttackDamage, transform.position, 0.0f);
        }

        /// <summary>
        /// The model dips down and recovers -- a two-armed overhead slam
        /// rather than Wolf's forward lunge, matching Godot's own two-segment
        /// tween (down over the first 35% of the swing, back up over the
        /// rest).
        /// </summary>
        private void Slam(float deltaTime)
        {
            if (Model == null)
            {
                return;
            }
            _slamRemaining = Mathf.Max(0.0f, _slamRemaining - deltaTime);

            float through = 1.0f - _slamRemaining / SlamSeconds;
            float drop = through < 0.35f
                ? Mathf.SmoothStep(0.0f, 1.0f, through / 0.35f)
                : Mathf.SmoothStep(1.0f, 0.0f, (through - 0.35f) / 0.65f);
            Model.localPosition = new Vector3(0.0f, -drop * SlamDropDistance, 0.0f);
        }

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (IsDead || amount <= 0.0f)
            {
                return HitResponse.Ignored;
            }

            Health = Mathf.Max(0.0f, Health - amount);
            if (Health <= 0.0f)
            {
                Die();
            }
            return HitResponse.Landed;
        }

        private void Die()
        {
            _target = null;
            _topple = 0.0f;
            Felled?.Invoke();
        }

        /// <summary>No ragdoll -- tips over like Wolf does, just a bigger fall to match the bigger body.</summary>
        private void Topple(float deltaTime)
        {
            if (Model == null || _topple >= 1.0f)
            {
                return;
            }
            _topple = Mathf.Min(1.0f, _topple + deltaTime / ToppleSeconds);
            float eased = Mathf.SmoothStep(0.0f, 1.0f, _topple);
            Model.localRotation = Quaternion.Euler(0.0f, 0.0f, ToppleDegrees * eased);
            Model.localPosition = new Vector3(0.0f, -ToppleDropDistance * eased, 0.0f);
        }
    }
}
