using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE GIANT (owner, 2026-10-29: "make an enemy that is a giant version of the playermodel like when you use the grow spell. take that size
    /// playermodel and make it a enemy"; docs/GIANT.md): the player's own body at the Grow spell's size, ten times over, 26 m tall. It strides
    /// at the nearest player (its size makes it quick: PlayerLocomotion reads the body's scale), and near enough it crouches -- the tell, slow
    /// enough to see and run from -- leaps, and wherever it comes down the ground heaves: everyone within <see cref="GiantRules.SlamRadius"/> is
    /// hurt (most at the middle) and thrown down, the street shakes, dust rolls out (<see cref="GiantSlam"/>). Then it stands a moment, and
    /// waits a while before the next leap (<see cref="GiantMind"/>). Its body is as any body's: it bleeds, ragdolls and comes apart.
    /// </summary>
    public sealed class GiantBrain : MonoBehaviour, IHostile
    {
        /// <summary>How long its body lies before it is taken away, seconds.</summary>
        public const float CorpseSeconds = 30.0f;

        public const float TargetSearchSeconds = 1.0f;

        /// <summary>How fast it turns to the player, degrees a second: slow, a giant's.</summary>
        public const float TurnDegreesPerSecond = 120.0f;

        public GiantMind Mind { get; } = new GiantMind();

        /// <summary>How many slams it has landed (for tests).</summary>
        public int Slams { get; private set; }

        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private Ragdoll _ragdoll;
        private Animator _animator;
        private PlayerAnimationDriver _driver;
        private PlayerVitals _target;
        private float _nextSearch;
        private float _deadFor;
        private bool _removed;
        private float _yaw;

        private static readonly int StrideRate = Animator.StringToHash(BodySizeAbility.StrideRateName);

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _ragdoll = GetComponent<Ragdoll>();
            _animator = GetComponentInChildren<Animator>();
            _yaw = transform.eulerAngles.y;
            float scale = transform.lossyScale.y;
            if (_vitals != null)
            {
                _vitals.FallScale = Mathf.Max(1.0f, scale);   // its own leaps land as a giant's, not as a fall ten times a man's
                _vitals.Stats[StatId.MaxHealth] = GiantRules.MaxHealthPoints;
                _vitals.XpRewardOnDeath = GiantRules.XpReward;
                _vitals.Revive();   // its whole health, now that it is a giant's
            }
            if (_animator != null)
            {
                _animator.SetFloat(StrideRate, 1.0f / Mathf.Sqrt(Mathf.Max(0.01f, scale)));   // a giant's slow stride, as Grow gives
            }
            _driver = GetComponent<PlayerAnimationDriver>();
            if (_driver != null)
            {
                _driver.RemoteSlammed += ShowRemoteSlam;   // on another player's copy of it: the ground heaves there too
            }
            // What a blow does to it: the freeze, the shove, the blood (as on the player's body, where PlayerInputReader adds it).
            if (GetComponent<HitImpactEffects>() == null)
            {
                gameObject.AddComponent<HitImpactEffects>();
            }
        }

        private void OnDestroy()
        {
            if (_driver != null)
            {
                _driver.RemoteSlammed -= ShowRemoteSlam;
            }
        }

        private void ShowRemoteSlam()
        {
            GiantSlam.Show(transform.position, transform.lossyScale.y);
        }

        private void OnEnable()
        {
            EnemyPack.Join(transform);
        }

        private void OnDisable()
        {
            EnemyPack.Leave(transform);
        }

        private void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            if (_vitals == null || _locomotion == null)
            {
                return;
            }
            _vitals.TickHitstun(deltaTime);
            if (_vitals.IsDead)
            {
                Mind.Tick(new GiantMind.Senses { Dead = true }, deltaTime);
                _deadFor += deltaTime;
                if (_deadFor >= CorpseSeconds && !_removed)
                {
                    _removed = true;
                    EnemySession.Remove(gameObject);
                }
                return;
            }
            if (_ragdoll != null && _ragdoll.IsLimp)
            {
                _ragdoll.Tick(deltaTime);
                if (_ragdoll.CanStandUp)
                {
                    _ragdoll.BeginStandingUp();
                }
                return;
            }
            FindTarget();
            float distance = _target != null ? Flat(_target.transform.position - transform.position).magnitude : float.MaxValue;
            GiantMind.Intent intent = Mind.Tick(new GiantMind.Senses
            {
                HasTarget = _target != null,
                Distance = distance,
                Grounded = _locomotion.IsGrounded,
            }, deltaTime);
            if (_target != null)
            {
                Vector3 to = Flat(_target.transform.position - transform.position);
                if (to.sqrMagnitude > 0.01f && Mind.Current != GiantMind.Phase.Crouch)
                {
                    _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, TurnDegreesPerSecond * deltaTime);
                }
            }
            if (intent.Slam)
            {
                Slams++;
                GiantSlam.Land(transform.position, transform.lossyScale.y, gameObject);
                _driver?.AnnounceState(PlayerAnimationDriver.GroundSlamName);   // every other player's copy of it shows the slam
            }
            _locomotion.Tick(new LocomotionInput
            {
                Move = new Vector2(0.0f, intent.Move),
                YawDegrees = _yaw,
                Crouch = intent.Crouch,
                Jump = intent.Jump,
            }, deltaTime);
        }

        private void FindTarget()
        {
            if (Time.time < _nextSearch && _target != null && _target.IsAlive)
            {
                return;
            }
            _nextSearch = Time.time + TargetSearchSeconds;
            _target = PlayerBodies.NearestAlive(transform.position, GiantRules.NoticeMetres);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0.0f;
            return v;
        }
    }
}
