using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A hostile grey wolf. Ported from npc/wolf.gd.
    ///
    /// A QUADRUPED BUILT OUT OF PRIMITIVES, not the fused-body pipeline the
    /// humanoids use. That whole system -- the SDF surface, the skinning, the
    /// ragdoll -- is built around a biped rig and has no way to express a
    /// four-legged animal. This is its own much simpler body, and being simple
    /// is why it can exist at all.
    ///
    /// ALWAYS HOSTILE, unlike the sparring dummy, which waits to be hit. It
    /// scans for the nearest player every frame, chases in a straight line, and
    /// bites. No pathfinding -- the same simplification every NPC in the Godot
    /// build already accepts.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Wolf : MonoBehaviour, IDamageable
    {
        public const float MaxHealth = 60.0f;
        public const float DetectionRange = 14.0f;
        public const float ChaseSpeed = 5.5f;
        public const float BiteRange = 1.7f;
        public const float BiteDamage = 8.0f;
        public const float BiteCooldownSeconds = 0.9f;
        public const float Gravity = 9.8f;

        /// <summary>
        /// How far the MODEL snaps forward on a bite. Tweening the model rather
        /// than the body gives the bite a visible lunge without needing a
        /// skeleton or an animation system at all.
        /// </summary>
        public const float LungeDistance = 0.35f;

        public const float LungeSeconds = 0.12f;

        /// <summary>
        /// The first enemy with a real level. Half a fresh character's first
        /// level, so two wolves level you once -- an early-game pace for the
        /// weakest thing in the game. Not balance-tested; easy to retune.
        /// </summary>
        public const int EnemyLevel = 1;

        public const float ExperienceReward = 50.0f;

        public static readonly Color BodyColour = new Color(0.45f, 0.45f, 0.48f);
        public static readonly Color DarkColour = new Color(0.26f, 0.26f, 0.29f);
        public static readonly Color ToothColour = new Color(0.96f, 0.96f, 0.92f);
        public static readonly Color EyeColour = new Color(0.9f, 0.08f, 0.05f);

        [Tooltip("The visible body. Built by WolfBuilder; lunges and topples on its own.")]
        public Transform Model;

        /// <summary>
        /// Keeps it from running face-first into a building when you are on the
        /// other side of one.
        /// </summary>
        private readonly NpcSteering _steering = new NpcSteering();

        private CharacterController _controller;
        private Transform _target;
        private float _biteCooldown;
        private float _verticalVelocity;
        private float _lunge;
        private float _topple;

        public float Health { get; private set; } = MaxHealth;

        public bool IsDead => Health <= 0.0f;

        public bool IsAlive => !IsDead;

        /// <summary>Raised when it dies, so whatever killed it can be rewarded.</summary>
        public event System.Action<float> Felled;

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

            _biteCooldown = Mathf.Max(0.0f, _biteCooldown - deltaTime);
            Lunge(deltaTime);

            // RE-ACQUIRED EVERY FRAME rather than trusting a stale pointer. A
            // target can die, be destroyed, or walk out of range without ever
            // telling this, and a wolf still chasing a corpse across the city
            // is the symptom.
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
                else if (distance > BiteRange)
                {
                    // STEERED, not straight at you. And faced along the way it
                    // is actually travelling, so it looks where it is going
                    // while rounding a building rather than crabbing sideways
                    // with its head fixed on you.
                    Vector3 direction = _steering.Steer(
                        transform, toTarget / distance, deltaTime);
                    Face(direction);
                    travel = direction * ChaseSpeed;
                }
                else
                {
                    Face(toTarget);
                    if (_biteCooldown <= 0.0f)
                    {
                        Bite();
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

        /// <summary>
        /// The nearest living player inside detection range.
        ///
        /// FOUND BY COMPONENT rather than by a tag or a group. Godot uses a
        /// "players" group; Unity's nearest equivalent would be a tag, which is
        /// a string somebody has to remember to set on a prefab -- and which
        /// fails silently when they do not. Anything with vitals is a body
        /// worth biting.
        /// </summary>
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

        private void Bite()
        {
            _biteCooldown = BiteCooldownSeconds;
            _lunge = LungeSeconds;

            if (_target == null)
            {
                return;
            }
            var damageable = _target.GetComponentInParent<IDamageable>();
            damageable?.ApplyHit(BiteDamage, transform.position, 0.0f);
        }

        /// <summary>
        /// The forward-and-back snap. Out fast, back slow, which is what makes
        /// it read as a bite rather than as a nudge.
        /// </summary>
        private void Lunge(float deltaTime)
        {
            if (Model == null)
            {
                return;
            }
            _lunge = Mathf.Max(0.0f, _lunge - deltaTime);

            float through = 1.0f - _lunge / LungeSeconds;
            float reach = through < 0.4f
                ? Mathf.SmoothStep(0.0f, 1.0f, through / 0.4f)
                : Mathf.SmoothStep(1.0f, 0.0f, (through - 0.4f) / 0.6f);
            Model.localPosition = new Vector3(0.0f, 0.0f, reach * LungeDistance);
        }

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (IsDead || amount <= 0.0f)
            {
                return HitResponse.Ignored;
            }

            // Hitstun is accepted and ignored. A wolf has no hitstun lock and
            // no ragdoll to be finished into -- it has no skeleton at all -- so
            // taking the argument and doing nothing with it is what lets a
            // player's punch hit one with no special case at the other end.
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
            Felled?.Invoke(ExperienceReward);
        }

        /// <summary>
        /// NO RAGDOLL TO COLLAPSE -- there are no bodies or joints in here to
        /// go limp. It tips onto its side instead, which is a much simpler dead
        /// pose and still reads clearly from across a street.
        /// </summary>
        private void Topple(float deltaTime)
        {
            if (Model == null || _topple >= 1.0f)
            {
                return;
            }
            _topple = Mathf.Min(1.0f, _topple + deltaTime / 0.4f);
            float eased = Mathf.SmoothStep(0.0f, 1.0f, _topple);
            Model.localRotation = Quaternion.Euler(0.0f, 0.0f, 85.0f * eased);
            Model.localPosition = new Vector3(0.0f, -0.15f * eased, 0.0f);
        }
    }
}
