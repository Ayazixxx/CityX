using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// GLACIER SPIKES, the Cryomancer's spell (owner, 2026-11-12: "make the ice crystals erupting from the ground line spell a hotbar spell and
    /// when you press it you look which direction you want to cast it in and then click to cast. similar to the octoslash"; docs/GLACIER_SPIKES.md).
    /// Its key ARMS it: the body turns to where the camera looks and an arrow on the ground shows the line (<see cref="GlacierSpikesArrow"/>).
    /// A left click casts: a front races down the line from just ahead of the feet and crystals of ice tear out of the ground behind it
    /// (<see cref="GlacierSpikesField"/>); whoever it passes in the band is struck once and chilled. The key again, or a right click, puts it
    /// away. It stays armed only while its reader keeps aiming it (<see cref="Aim"/>, every frame): a menu, a fall, a stagger, a climb end it.
    ///
    /// Decided on the caster's machine (the strikes reach another player through their copy, as every blow does). Other players are told the
    /// cast (<see cref="IReplicatedAbility"/>: the line's start, its way, its length and the seed) and grow the same field on their copy of the
    /// caster, hurting nobody.
    /// </summary>
    public sealed class GlacierSpikesAbility : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "glacier-spikes";

        /// <summary>The one event other players are sent (<see cref="AbilityEvent.Kind"/>): the cast, Origin the line's start on the ground,
        /// Direction its way, A its length, B its seed.</summary>
        public const int Cast = 0;

        /// <summary>The seeds a cast picks from: whole numbers that fit the event's checked range.</summary>
        public const int SeedCount = 1000;

        /// <summary>The arrow is laid again when the line's start has moved this far, metres, or its way has turned this much, degrees.</summary>
        private const float RelayMetres = 0.1f;
        private const float RelayDegrees = 1.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        /// <summary>The soft-particle light material the mist and glitter are made from (PlayerAbilityWiring), so they fade into the street.</summary>
        public Material LightMaterial;

        private static readonly Collider[] Nearby = new Collider[64];

        private readonly GlacierSpikesPath _aim = new GlacierSpikesPath();
        private readonly GlacierSpikesPath _struckAlong = new GlacierSpikesPath();
        private readonly HashSet<IDamageable> _struck = new HashSet<IDamageable>();
        private PlayerVitals _vitals;
        private GlacierSpikesArrow _arrow;
        private Vector3 _look = Vector3.forward;
        private int _aimedFrame = -1;
        private float _cooldown;
        private float _pose;
        private float _poseEase = GlacierSpikesRules.PoseEaseSeconds;
        private Vector3 _laidFrom;
        private Vector3 _laidLook;
        private bool _striking;
        private float _strikeAge;
        private GlacierSpikesCast _strikeCast;
        private float _spellPower;
        private float _penetration;

        /// <summary>Armed: aiming, waiting for the click.</summary>
        public bool IsArmed { get; private set; }

        public float CooldownLeft => Mathf.Max(0.0f, _cooldown);

        /// <summary>The raised-hand cast pose's strength (PlayerLocomotion.SetCastPose): held at the cast, then easing out.</summary>
        public float CastFraction => _pose <= 0.0f ? 0.0f : Mathf.Clamp01(_pose / _poseEase);

        /// <summary>The line as it is aimed now (empty when not armed).</summary>
        public GlacierSpikesPath Aimed => _aim;

        public GlacierSpikesArrow Arrow => _arrow;

        /// <summary>The last field this body grew (cast here, or shown for another player), until it is gone.</summary>
        public GlacierSpikesField LastField { get; private set; }

        /// <summary>The bodies the last cast struck.</summary>
        public int StruckCount => _struck.Count;

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
        }

        private void OnDestroy()
        {
            if (_arrow != null)
            {
                Destroy(_arrow.gameObject);
            }
        }

        private bool Dead => _vitals != null && _vitals.IsDead;

        /// <summary>Arms it: false while it cools down, without the mana for it, or dead.</summary>
        public bool TryArm()
        {
            if (IsArmed || _cooldown > 0.0f || Dead || (_vitals != null && !_vitals.HasMana(ManaCost)))
            {
                return false;
            }
            IsArmed = true;
            _aimedFrame = Time.frameCount;
            _laidFrom = new Vector3(float.MaxValue, 0.0f, 0.0f);
            return true;
        }

        /// <summary>Puts it away without casting (the key again, a right click, a stagger).</summary>
        public void Disarm()
        {
            IsArmed = false;
            if (_arrow != null)
            {
                _arrow.Hide();
            }
        }

        /// <summary>Where the camera looks while it is armed, handed in every frame by the reader; a frame without it puts it away.</summary>
        public void Aim(Vector3 look)
        {
            if (!IsArmed)
            {
                return;
            }
            look.y = 0.0f;
            if (look.sqrMagnitude > 1e-6f)
            {
                _look = look.normalized;
            }
            _aimedFrame = Time.frameCount;
        }

        /// <summary>The mana a cast pays (the wand's 10% off is PlayerVitals.TrySpendMana's).</summary>
        private static float ManaCost => GlacierSpikesRules.ManaCost;

        /// <summary>
        /// Casts it down the aimed line: false (and still armed) when there is no line to cast along (in the air, a wall at the nose) or not
        /// the mana for it.
        /// </summary>
        public bool TryCast()
        {
            if (!IsArmed || Dead)
            {
                return false;
            }
            if (!_aim.Walk(transform.position, _look, GlacierSpikesGround.World) || !_aim.IsCastable)
            {
                return false;
            }
            if (_vitals != null && !_vitals.TrySpendMana(ManaCost))
            {
                return false;
            }
            Disarm();
            _cooldown = GlacierSpikesRules.CooldownSeconds * PlayerVitals.CooldownFactorOf(_vitals);
            // Cast Speed quickens the hand, as it does every cast.
            float castSpeed = PlayerVitals.SpeedOf(_vitals, StatId.CastSpeed);
            _poseEase = GlacierSpikesRules.PoseEaseSeconds / castSpeed;
            _pose = GlacierSpikesRules.PoseHoldSeconds / castSpeed + _poseEase;
            int seed = Random.Range(0, SeedCount);
            LastField = GlacierSpikesField.Grow(new GlacierSpikesCast(_aim.Start, _aim.Direction, _aim.Length, seed), GlacierSpikesGround.World, LightMaterial);
            GlacierSpikesCast cast = LastField.Cast;   // the line as the field laid it: what every other machine is told
            BeginStrikes(cast);
            Fired?.Invoke(new AbilityEvent(Cast, cast.Start, cast.Direction, cast.Length, seed));
            return true;
        }

        /// <summary>A stagger (Stability) puts it away, as it cuts short every wind-up.</summary>
        public void Interrupt() => Disarm();

        /// <summary>Another player's cast, on this copy of them: the same field from the same line and seed, the raised hand. It strikes nobody.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind != Cast)
            {
                return;
            }
            Vector3 way = evt.Direction;
            way.y = 0.0f;
            if (way.sqrMagnitude < 0.25f || !(evt.A > 0.0f))
            {
                return;
            }
            int seed = Mathf.Clamp(Mathf.RoundToInt(evt.B), 0, SeedCount - 1);
            var cast = new GlacierSpikesCast(evt.Origin, way, Mathf.Min(evt.A, GlacierSpikesRules.Range), seed);
            LastField = GlacierSpikesField.Grow(cast, GlacierSpikesGround.World, LightMaterial);
            RemoteAbilityPose.Show(gameObject, GlacierSpikesRules.PoseHoldSeconds, GlacierSpikesRules.PoseEaseSeconds);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>The cooldown and the pose counting down, and the front's strikes; public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            _cooldown -= deltaTime;
            _pose -= deltaTime;
            if (IsArmed && Dead)
            {
                Disarm();
            }
            if (_striking)
            {
                _strikeAge += deltaTime;
                Strike(GlacierSpikesRules.FrontDistance(_strikeAge));
            }
        }

        private void LateUpdate()
        {
            if (!IsArmed)
            {
                return;
            }
            if (_aimedFrame != Time.frameCount)
            {
                Disarm();   // nobody aimed it this frame: the body is out of its player's hands
                return;
            }
            ShowArrow();
        }

        /// <summary>The arrow along the line as it would be cast now, laid again only when the line has moved.</summary>
        private void ShowArrow()
        {
            if (_arrow == null)
            {
                _arrow = GlacierSpikesArrow.Make();
            }
            Vector3 feet = transform.position;
            bool moved = (feet - _laidFrom).sqrMagnitude > RelayMetres * RelayMetres || Vector3.Angle(_look, _laidLook) > RelayDegrees;
            if (moved)
            {
                _laidFrom = feet;
                _laidLook = _look;
                if (!_aim.Walk(feet, _look, GlacierSpikesGround.World))
                {
                    _arrow.Hide();
                    return;
                }
            }
            _arrow.Show(_aim, GlacierSpikesGround.World, moved, Time.deltaTime);
        }

        private void BeginStrikes(in GlacierSpikesCast cast)
        {
            _struck.Clear();
            _strikeCast = cast;
            _strikeAge = 0.0f;
            _striking = true;
            _struckAlong.Follow(cast.Start, cast.Direction, cast.Length, GlacierSpikesGround.World);
            _spellPower = PlayerVitals.SpellPowerOf(_vitals);
            _penetration = _vitals != null ? _vitals.Stats.Effect(StatId.ArmorPenetration) : 0.0f;
        }

        /// <summary>Everyone the front has passed in the band (or round the end, once it is there) struck, each once.</summary>
        private void Strike(float front)
        {
            GlacierSpikesCast cast = _strikeCast;
            float reach = cast.Length * 0.5f + GlacierSpikesRules.ImpactRadius + GlacierSpikesRules.BodyMargin;
            Vector3 middle = cast.Start + cast.Direction * (cast.Length * 0.5f) + Vector3.up * 1.0f;
            var half = new Vector3(GlacierSpikesRules.ImpactRadius + GlacierSpikesRules.BodyMargin, GlacierSpikesRules.ReachAbove + 2.0f, reach);
            int count = Physics.OverlapBoxNonAlloc(middle, half, Nearby, Quaternion.LookRotation(cast.Direction), ~0, QueryTriggerInteraction.Ignore);
            float damage = CombatTuning.DamageFor(GlacierSpikesRules.Damage, _spellPower);
            for (int i = 0; i < count; i++)
            {
                var target = Nearby[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || _struck.Contains(target) || !(target is Component body) || body.transform.IsChildOf(transform))
                {
                    continue;
                }
                Vector3 feet = body.transform.position;
                if (!GlacierSpikesRules.Strikes(cast.Start, cast.Direction, cast.Length, front, feet, _struckAlong.HeightBeside(feet)))
                {
                    continue;
                }
                _struck.Add(target);
                Vector3 under = cast.Start + cast.Direction * Mathf.Clamp(Vector3.Dot(feet - cast.Start, cast.Direction), 0.0f, cast.Length);
                under.y = _struckAlong.HeightBeside(feet);
                using (DamageContext.Chilling(GlacierSpikesRules.ChillAmount))
                {
                    DamageContext.Strike(target, damage, under, GlacierSpikesRules.HitstunSeconds, DamageKind.Magic, _penetration,
                        feet + Vector3.up * 0.8f, gameObject);
                }
            }
            if (front >= cast.Length)
            {
                _striking = false;   // the end reached: what stands there now was struck or was not there
            }
        }
    }
}
