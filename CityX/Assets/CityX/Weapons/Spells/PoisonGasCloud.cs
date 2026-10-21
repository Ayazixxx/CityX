using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A cloud of purple poison gas. Blown out fast and slowing to a drift, it swells from a puff to a bank 3 m across, hangs for a few seconds and
    /// thins away. ANYTHING THAT BREATHES IT -- every IDamageable inside it, checked five times a second -- is poisoned (PoisonEffect: 10 s, topped
    /// up while it stays in the gas). It never poisons whoever blew it (the caster's colliders are skipped).
    ///
    /// THE LOOK is the particle systems on the prefab (PoisonGasBuilder): many large, soft, slowly turning puffs in world space, so the cloud
    /// leaves a trail as it is blown and then hangs where it stopped. This class moves the cloud's centre and sizes the emitter to match;
    /// the puffs carry on by themselves.
    ///
    /// ONE STEP FUNCTION (<see cref="Tick"/>) that Update calls, so a test can run seconds in one go. A COSMETIC cloud (the copy another player's
    /// machine shows) looks and moves the same but poisons nothing: whether the real one hurt anyone is the caster's own machine's business.
    /// </summary>
    public sealed class PoisonGasCloud : MonoBehaviour
    {
        /// <summary>How long the cloud swells from its first puff to full size, in seconds.</summary>
        public const float ExpandSeconds = 1.2f;

        /// <summary>How long a cloud lasts in all, in seconds. The last <see cref="FadeSeconds"/> it thins away and is harmless.</summary>
        public const float LifeSeconds = 7.0f;

        public const float FadeSeconds = 1.5f;

        public const float StartRadius = 0.6f;

        public const float MaxRadius = 3.0f;

        /// <summary>The puffs stop being made after this long: the ones already out live on (about 4 s) and carry the look to the end.</summary>
        public const float EmitSeconds = 2.6f;

        /// <summary>How fast a blown cloud slows: its speed is multiplied by exp(-this x time).</summary>
        public const float SlowRate = 2.2f;

        /// <summary>How often the cloud checks who is in it, in seconds.</summary>
        public const float CheckSeconds = 0.2f;

        [Tooltip("The little rising bubbles shown on each poisoned victim.")]
        public GameObject VictimFxPrefab;

        /// <summary>True for the copy another player's machine shows: it poisons nothing.</summary>
        public bool Cosmetic { get; private set; }

        public float Age { get; private set; }

        /// <summary>How big the cloud is right now, in metres (its radius).</summary>
        public float Radius { get; private set; } = StartRadius;

        /// <summary>True while the cloud is thick enough to poison (not in its last moments).</summary>
        public bool IsHarmful => Age < LifeSeconds - FadeSeconds;

        /// <summary>How many separate bodies this cloud has poisoned (counting a body again each time it re-enters after a check it missed).</summary>
        public int Poisonings { get; private set; }

        private Vector3 _velocity;
        private Collider[] _ignore;
        private int _strength;
        private float _sinceCheck = 99.0f;
        private ParticleSystem[] _particles;
        private Light _light;
        private float _lightBase;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly Collider[] Overlaps = new Collider[48];

        public static PoisonGasCloud Spawn(GameObject prefab, Vector3 origin, Vector3 velocity, int casterStrength, Collider[] ignoreColliders,
            bool cosmetic = false)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Instantiate(prefab, origin, Quaternion.identity);
            var cloud = instance.GetComponent<PoisonGasCloud>();
            if (cloud == null)
            {
                return null;
            }
            cloud._velocity = velocity;
            cloud._strength = casterStrength;
            cloud._ignore = ignoreColliders;
            cloud.Cosmetic = cosmetic;
            return cloud;
        }

        private void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>();
            _light = GetComponentInChildren<Light>();
            _lightBase = _light != null ? _light.intensity : 0.0f;
        }

        private void Start()
        {
            foreach (ParticleSystem particles in _particles)
            {
                particles.Play();
            }
        }

        private void Update()
        {
            Tick(Mathf.Min(Time.deltaTime, 0.05f));
        }

        /// <summary>Advances the cloud by <paramref name="deltaTime"/> seconds: moves and swells it, poisons what is in it, thins it away at the end.</summary>
        public void Tick(float deltaTime)
        {
            Age += deltaTime;
            if (Age >= LifeSeconds + 0.5f)
            {
                Destroy(gameObject);
                return;
            }

            Move(deltaTime);

            float swell = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(Age / ExpandSeconds));
            float thin = Mathf.Clamp01((LifeSeconds - Age) / FadeSeconds);
            Radius = Mathf.Lerp(StartRadius, MaxRadius, swell) * Mathf.Lerp(0.8f, 1.0f, thin);
            ShapeParticles(thin);

            if (!Cosmetic && IsHarmful)
            {
                _sinceCheck += deltaTime;
                if (_sinceCheck >= CheckSeconds)
                {
                    _sinceCheck = 0.0f;
                    Poison();
                }
            }
        }

        private void Move(float deltaTime)
        {
            _velocity *= Mathf.Exp(-SlowRate * deltaTime);
            float distance = _velocity.magnitude * deltaTime;
            if (distance < 1e-4f)
            {
                return;
            }
            Vector3 direction = _velocity / (distance / deltaTime);
            // Stops against anything solid (a wall, the ground, a car), but not against people: gas goes round and through them.
            int count = Physics.SphereCastNonAlloc(transform.position, 0.4f, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i].collider;
                if (hit == null || IsIgnored(hit) || hit.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                _velocity = Vector3.zero;
                return;
            }
            transform.position += direction * distance;
        }

        /// <summary>Sizes the puffs' emitter to the cloud, stops making puffs once it is old enough, and dims the glow as it thins.</summary>
        private void ShapeParticles(float thin)
        {
            bool emitting = Age < EmitSeconds;
            foreach (ParticleSystem particles in _particles)
            {
                if (particles == null)
                {
                    continue;
                }
                ParticleSystem.ShapeModule shape = particles.shape;
                shape.radius = Radius * 0.75f;
                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = emitting;
            }
            if (_light != null)
            {
                _light.intensity = _lightBase * thin * Mathf.Clamp01(Age / 0.3f);
            }
        }

        private bool IsIgnored(Collider other)
        {
            return other == null || other.isTrigger || (_ignore != null && System.Array.IndexOf(_ignore, other) >= 0);
        }

        private readonly HashSet<IDamageable> _seen = new HashSet<IDamageable>();

        private void Poison()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, Radius * 0.9f, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            _seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (IsIgnored(collider))
                {
                    continue;
                }
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || !_seen.Add(target))
                {
                    continue;
                }
                float dps = CombatTuning.DamageFor(PoisonEffect.DefaultDamagePerSecond, _strength);
                if (PoisonEffect.Apply(((Component)target).gameObject, dps, PoisonEffect.DefaultSeconds, VictimFxPrefab) != null)
                {
                    Poisonings++;
                }
            }
        }
    }
}
