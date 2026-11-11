using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The Wraith (owner, 2026-10-26): a low-level ghost that floats about the streets. Its head and body are the owner's model, on the player's own arms (2026-11-05), its tail
    /// wearing away into gas, all on the ghost surface (Shaders/CityXGhost), with two glowing red eyes and solid white teeth (WraithBuilder). It "dies very quickly":
    /// <see cref="MaxHealth"/> is one pistol round or two punches, and dead it fades away and rises. It sneaks up behind a player and bites their neck (Wraith.Bite.cs).
    /// Summoned by a Wraith Staff it fights on its summoner's side (Wraith.Summon.cs).
    ///
    /// No CharacterController and no Animator: it hovers (WraithDrift: drift, linger, bob, a ground-relative hover height), steered round
    /// walls by NpcSteering, and poses its own bones every frame (WraithPose) over the body's rest.
    ///
    /// IN A SESSION (NetworkedWraith, 2026-10-27) the machine that spawned it drifts it; on every other machine it is a COPY
    /// (<see cref="IsCopy"/>): moved by the owner, posed from how it moves, its hits sent to the owner (<see cref="Relay"/>) and its health
    /// taken from the owner's (<see cref="SetHealthFromNetwork"/>), so it flinches and fades on every screen alike.
    ///
    /// Tick is public and separate from Update, so a test drives it with its own time step.
    /// </summary>
    public sealed partial class Wraith : MonoBehaviour, IDamageable, IHealthBar, IXpReward, IBloodless, IHostile, ICameraPassesThrough
    {
        /// <summary>Owner: "dies very quickly". One pistol round (20), two punches (10 each).</summary>
        public const float MaxHealth = 18.0f;

        public const int XpForKill = 15;

        /// <summary>Dead, it fades away over this long, rising, and then is gone.</summary>
        public const float FadeSeconds = 1.1f;
        public const float RiseSpeed = 0.5f;

        /// <summary>Hit, its flinch and its flash settle over this long.</summary>
        public const float HurtSeconds = 0.9f;

        /// <summary>It turns its head to look at a player this near, metres.</summary>
        public const float LookRange = 10.0f;

        /// <summary>Where a player's eyes are, up from the ground they stand on, metres.</summary>
        public const float PlayerEyeHeight = 2.25f;

        /// <summary>How fast the body comes round to face its heading, degrees per second.</summary>
        public const float FacingDegreesPerSecond = 90.0f;

        public const float GroundProbeDistance = 300.0f;

        /// <summary>The ground is probed from this far up the root, under the collider's bottom.</summary>
        public const float GroundProbeFrom = 0.5f;

        /// <summary>Its point light: a pool of its own light on the street at night.</summary>
        public const float GlowIntensity = 3.0f;

        private const float PlayerSearchSeconds = 0.5f;

        [Header("Wired by WraithBuilder")]
        public Transform Hips;
        public Transform Spine;
        public Transform Head;
        public Transform ArmLeft;
        public Transform ArmRight;
        public Transform ForearmLeft;
        public Transform ForearmRight;
        public Transform HandLeft;
        public Transform HandRight;
        public Renderer[] Renderers = System.Array.Empty<Renderer>();
        public Collider MainCollision;
        public Light Glow;

        [Tooltip("The eyes, the mouth and the teeth on the skull (solid, so they shrink away as the body fades).")]
        public Transform Face;

        [Tooltip("The middle of its teeth: what goes on the prey's neck.")]
        public Transform Jaw;

        [Tooltip("The gas coming off its tail.")]
        public ParticleSystem Gas;

        [Tooltip("Seeds its drifting. 0: from where it is spawned, so several spawned together do not all go the same way.")]
        public int Seed;

        [Tooltip("What counts as ground and walls. World geometry by default.")]
        public LayerMask ObstacleMask = ~0;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");

        private readonly NpcSteering _steering = new NpcSteering();
        private WraithDrift _drift;
        private MaterialPropertyBlock _look;
        private Quaternion[] _restRotations = System.Array.Empty<Quaternion>();
        private Transform[] _posed = System.Array.Empty<Transform>();
        private Vector3 _hipsRest;

        private bool _bound;
        private float _health = MaxHealth;
        private bool _dead;
        private float _fade;
        private float _hurt;
        private float _time;
        private float _turnRate;
        private PlayerVitals _player;
        private float _playerSearch;

        /// <summary>Another machine's Wraith shown here: it does not drift by itself, and is told its health.</summary>
        public bool IsCopy { get; set; }

        /// <summary>Where a hit on a copy is sent instead (NetworkedWraith); null outside a session.</summary>
        public IRemoteDamageRelay Relay;

        private float _stalkSpeed;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private bool _followed;
        private float _copySpeed;
        private bool _removed;

        public bool IsAlive => !_dead;
        public float Health => _health;
        float IHealthBar.MaxHealth => MaxHealthNow;

        private static readonly List<Wraith> _all = new List<Wraith>();

        /// <summary>Every Wraith in the world here, wild or summoned (a summon looks for its prey among them).</summary>
        public static IReadOnlyList<Wraith> All => _all;
        int IXpReward.XpReward => XpForKill;

        /// <summary>How far it has faded after dying, 0 to 1; at 1 it is gone.</summary>
        public float FadeProgress => _fade;

        /// <summary>How hurt it still shows, 1 the moment it is hit, 0 settled.</summary>
        public float Hurt => _hurt;

        public float Speed => IsCopy ? _copySpeed : _hunt.Phase != WraithPhase.Drifting ? _stalkSpeed : _drift != null ? _drift.Speed : 0.0f;
        public bool IsLingering => _drift != null && _drift.IsLingering;

        private void Awake()
        {
            Bind();
        }

        /// <summary>The setup Awake does, public and idempotent: Awake never runs on a bare AddComponent in an EditMode test.</summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }
            _bound = true;
            int seed = Seed != 0 ? Seed : Mathf.RoundToInt(transform.position.x * 131.0f + transform.position.z * 17.0f) ^ GetInstanceID();
            _drift = new WraithDrift(seed, transform.position);
            // Sized off the collider WraithBuilder gives it: a body's width, its height, a short reach (it drifts slowly).
            _steering.Configure(width: 0.45f, topY: 2.3f, reach: 0.6f);
            _steering.ObstacleMask = ObstacleMask;
            _look = new MaterialPropertyBlock();
            _posed = new[] { Hips, Spine, Head, ArmLeft, ForearmLeft, HandLeft, ArmRight, ForearmRight, HandRight };
            _restRotations = new Quaternion[_posed.Length];
            for (int i = 0; i < _posed.Length; i++)
            {
                _restRotations[i] = _posed[i] != null ? _posed[i].localRotation : Quaternion.identity;
            }
            _hipsRest = Hips != null ? Hips.localPosition : Vector3.zero;
            BindBite();
            if (StartsSummoned)
            {
                _health = SummonedMaxHealth;
                _hunt.RestSeconds = SummonedRestSeconds;
                _hunt.Bold = true;
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
            if (_dead && _fade >= 1.0f && !_removed)
            {
                _removed = true;
                EnemySession.Remove(gameObject);   // in a session the owner has the server take it out everywhere; a copy waits for that
            }
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        public void Tick(float deltaTime)
        {
            _time += deltaTime;
            if (_dead)
            {
                _fade = Mathf.Min(1.0f, _fade + deltaTime / FadeSeconds);
                if (!IsCopy)
                {
                    transform.position += Vector3.up * (RiseSpeed * deltaTime);   // a copy rises with its owner's
                }
                if (Face != null)
                {
                    // The face is solid, so it cannot fade with the body: it shrinks away into the skull instead.
                    Face.localScale = Vector3.one * Mathf.Max(1.0f - _fade, 0.001f);
                }
                ShowLook();
                return;
            }
            _hurt = Mathf.Max(0.0f, _hurt - deltaTime / HurtSeconds);

            if (IsCopy)
            {
                FollowOwner(deltaTime);
                FindPlayer(deltaTime);
                _clipSeconds += deltaTime;
                ShowLook();
                return;
            }

            if (TickHunt(deltaTime))
            {
                FindPlayer(deltaTime);
                ShowLook();
                return;
            }
            if (IsSummoned)
            {
                TickSummoned(deltaTime);
                FindPlayer(deltaTime);
                ShowLook();
                return;
            }

            Vector3 velocity = _drift.Tick(transform.position, deltaTime);
            float speed = velocity.magnitude;
            if (speed > 1e-4f)
            {
                Vector3 direction = _steering.Steer(transform, velocity / speed, deltaTime);
                transform.position += direction * (speed * deltaTime);
            }
            TurnToward(_drift.Heading, deltaTime);
            Hover(deltaTime);
            FindPlayer(deltaTime);
            ShowLook();
        }

        /// <summary>
        /// A copy is moved by its owner: how fast it went and turned since the last frame is what its pose leans and sways by. The first frame
        /// of a copy only notes where it is.
        /// </summary>
        private void FollowOwner(float deltaTime)
        {
            float yaw = transform.eulerAngles.y;
            if (deltaTime > 0.0f && _followed)
            {
                Vector3 moved = transform.position - _lastPosition;
                moved.y = 0.0f;
                _copySpeed = moved.magnitude / deltaTime;
                _turnRate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaTime;
            }
            _lastPosition = transform.position;
            _lastYaw = yaw;
            _followed = true;
        }

        /// <summary>A copy told its owner's health: a drop is a flinch and a flash, none left is its death.</summary>
        public void SetHealthFromNetwork(float health)
        {
            if (_dead || float.IsNaN(health))
            {
                return;
            }
            health = Mathf.Clamp(health, 0.0f, MaxHealthNow);
            if (health < _health)
            {
                _hurt = 1.0f;
            }
            _health = health;
            if (_health <= 0.0f)
            {
                Die();
            }
        }

        /// <summary>Comes round to face <paramref name="heading"/>, no faster than FacingDegreesPerSecond; remembers how fast it turned.</summary>
        private void TurnToward(Vector3 heading, float deltaTime)
        {
            heading.y = 0.0f;
            if (heading.sqrMagnitude < 1e-6f || deltaTime <= 0.0f)
            {
                _turnRate = 0.0f;
                return;
            }
            float before = transform.eulerAngles.y;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(heading.normalized, Vector3.up),
                FacingDegreesPerSecond * deltaTime);
            _turnRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / deltaTime;
        }

        /// <summary>Eases the root onto its hover height over the real ground beneath it, bobbing.</summary>
        private void Hover(float deltaTime)
        {
            float groundY = transform.position.y - WraithDrift.HoverHeight;
            Vector3 from = transform.position + Vector3.up * GroundProbeFrom;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, GroundProbeDistance, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }
            float targetY = WraithDrift.HoverTargetY(groundY, _time);
            Vector3 position = transform.position;
            position.y += (targetY - position.y) * Mathf.Min(1.0f, WraithDrift.HoverEase * deltaTime);
            transform.position = position;
        }

        /// <summary>The player it looks at: this machine's own (the one with a working input reader; the Shoota wears the body too).</summary>
        private void FindPlayer(float deltaTime)
        {
            _playerSearch -= deltaTime;
            if (_playerSearch > 0.0f && _player != null)
            {
                return;
            }
            _playerSearch = PlayerSearchSeconds;
            _player = null;
            PlayerInputReader reader = Object.FindFirstObjectByType<PlayerInputReader>();
            if (reader != null && reader.isActiveAndEnabled)
            {
                _player = reader.GetComponent<PlayerVitals>();
            }
            if (_player == null)
            {
                _player = Object.FindFirstObjectByType<PlayerVitals>();
            }
        }

        /// <summary>The pose over the rest: each bone turned about the body's own axes, parents first (WraithPose).</summary>
        public void ApplyPose()
        {
            if (!_bound || Hips == null)
            {
                return;
            }
            bool looking = false;
            Vector3 lookTarget = Vector3.zero;
            if (_player != null && _player.IsAlive && Head != null)
            {
                Vector3 eye = _player.transform.position + Vector3.up * PlayerEyeHeight;
                if ((eye - Head.position).sqrMagnitude <= LookRange * LookRange)
                {
                    looking = true;
                    lookTarget = Quaternion.Inverse(transform.rotation) * (eye - Head.position);
                }
            }
            WraithPose.Result pose = WraithPose.Compute(new WraithPose.Input(_time, Speed, _turnRate, looking, lookTarget, _hurt));

            Hips.localPosition = _hipsRest + pose.HipShift;
            WraithPose.Turn[] turns =
            {
                pose.Hips, pose.Spine, pose.Head, pose.ArmLeft, pose.ForearmLeft, pose.HandLeft, pose.ArmRight, pose.ForearmRight, pose.HandRight,
            };
            for (int i = 0; i < _posed.Length; i++)
            {
                Transform bone = _posed[i];
                if (bone == null)
                {
                    continue;
                }
                bone.localRotation = _restRotations[i];
                bone.rotation = Quaternion.AngleAxis(turns[i].Pitch, transform.right) * Quaternion.AngleAxis(turns[i].Yaw, transform.up)
                    * Quaternion.AngleAxis(turns[i].Roll, transform.forward) * bone.rotation;
            }
            ApplyBiteOver(Time.deltaTime);
        }

        /// <summary>Its fade and its flash onto every renderer, and its glow with them.</summary>
        private void ShowLook()
        {
            if (_look == null)
            {
                return;
            }
            float shown = 1.0f - _fade;
            _look.SetFloat(FadeId, shown);
            _look.SetFloat(FlashId, _hurt * _hurt);
            foreach (Renderer renderer in Renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_look);
                }
            }
            if (Glow != null)
            {
                Glow.intensity = GlowIntensity * shown * (1.0f + _hurt);
            }
        }

        // --- IDamageable ---

        bool IDamageable.IsAlive => IsAlive;

        HitResponse IDamageable.ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
        {
            if (_dead || amount <= 0.0f)
            {
                return HitResponse.Ignored;
            }
            if (Relay != null && Relay.TryRelayHit(amount, fromPosition, hitstunSeconds, -1.0f))
            {
                _hurt = 1.0f;   // it flinches here at once; its health comes back from the owner
                return HitResponse.Landed;
            }
            _health = Mathf.Max(0.0f, _health - amount);
            _hurt = 1.0f;
            if (_health <= 0.0f)
            {
                Die();
            }
            return HitResponse.Landed;
        }

        private void Die()
        {
            if (_dead)
            {
                return;
            }
            _dead = true;
            _player = null;
            OnKilled();
            if (MainCollision != null)
            {
                MainCollision.enabled = false;
            }
            if (Gas != null)
            {
                Gas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
