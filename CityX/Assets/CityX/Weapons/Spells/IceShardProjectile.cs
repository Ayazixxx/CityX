using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// One ice shard. It flies straight and fast; when it hits it STICKS: into the world it stays 30 seconds, into a character 10, then it
    /// shrinks away. Stuck in a character it is carried with them (a child of whatever it hit), so it rides along when they walk and goes with
    /// them if they are removed; the same goes for a door or anything else that moves.
    ///
    /// ONE STEP FUNCTION (<see cref="Tick"/>) that Update calls, so a test can run any number of seconds in one go. The flight is swept (a sphere
    /// cast along each step, plus an overlap at the start in case it was fired from inside a wall), so nothing is skipped at 50 m/s.
    /// </summary>
    public sealed class IceShardProjectile : MonoBehaviour
    {
        public const float BaseDamage = 8.0f;

        /// <summary>How long a shard stays in the world (a wall, the ground, a door) before it goes.</summary>
        public const float WorldStickSeconds = 30.0f;

        /// <summary>How long a shard stays in a character before it goes.</summary>
        public const float CharacterStickSeconds = 10.0f;

        /// <summary>A shard that never hits anything goes after this long in the air.</summary>
        public const float FlightSeconds = 3.0f;

        /// <summary>The shard's thickness for the sweep.</summary>
        public const float Radius = 0.08f;

        /// <summary>How far the tip goes in past the surface it struck, in metres.</summary>
        public const float WorldEmbedMetres = 0.22f;

        public const float CharacterEmbedMetres = 0.30f;

        /// <summary>The last stretch of a stuck shard's life in which it melts away (shrinks to nothing).</summary>
        public const float MeltSeconds = 0.8f;

        public enum Phase
        {
            Flying,
            StuckInWorld,
            StuckInCharacter,
        }

        public Phase CurrentPhase { get; private set; }

        /// <summary>Seconds left before a stuck shard goes (meaningless while flying).</summary>
        public float StuckSecondsRemaining { get; private set; }

        /// <summary>What the shard hit, once it has (a collider's transform it is parented to), else null.</summary>
        public Transform StuckTo { get; private set; }

        private Vector3 _velocity;
        /// <summary>Points in the caster's Spell Power (2% a point), and how much of the target's armour and block it ignores.</summary>
        private int _casterStrength;

        /// <summary>Who cast it: named with its blows, so the caster sees their damage (DamageDealt).</summary>
        private GameObject _striker;
        private float _penetration;
        private Collider[] _ignore;
        private float _age;
        private Vector3 _stuckScale = Vector3.one;
        private TrailRenderer _trail;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>
        /// True for the copy another player's machine shows of a shard they fired: it flies and sticks (and ages) exactly the same, in a wall or
        /// in a character, but hurts nothing -- the caster's own machine decides who the real shard hurt.
        /// </summary>
        public bool Cosmetic { get; private set; }

        public static IceShardProjectile Spawn(GameObject prefab, Vector3 origin, Vector3 velocity, int casterStrength, Collider[] ignoreColliders,
            bool cosmetic = false, float penetration = 0.0f, GameObject striker = null)
        {
            if (prefab == null || velocity.sqrMagnitude < 1e-6f)
            {
                return null;
            }
            GameObject instance = Instantiate(prefab, origin, Quaternion.LookRotation(velocity.normalized));
            var shard = instance.GetComponent<IceShardProjectile>();
            if (shard == null)
            {
                return null;
            }
            shard._velocity = velocity;
            shard._casterStrength = casterStrength;
            shard._striker = striker;
            shard._penetration = Mathf.Clamp01(penetration);
            shard.Cosmetic = cosmetic;
            shard._ignore = ignoreColliders;
            shard._trail = instance.GetComponentInChildren<TrailRenderer>();
            return shard;
        }

        private void Awake()
        {
            if (_trail == null)
            {
                _trail = GetComponentInChildren<TrailRenderer>();
            }
        }

        private void Update()
        {
            // A long frame must not become a long step: the sweep copes with distance, but a stuck shard's melt should not skip either.
            Tick(Mathf.Min(Time.deltaTime, 0.05f));
        }

        /// <summary>Advances the shard by <paramref name="deltaTime"/> seconds: flies it (and sticks it if it meets something) or ages a stuck one.</summary>
        public void Tick(float deltaTime)
        {
            if (CurrentPhase == Phase.Flying)
            {
                Fly(deltaTime);
            }
            else
            {
                Age(deltaTime);
            }
        }

        private void Fly(float deltaTime)
        {
            _age += deltaTime;
            if (_age > FlightSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 from = transform.position;
            Vector3 direction = _velocity.normalized;
            float distance = _velocity.magnitude * deltaTime;

            // Fired from inside something (a wall right in front of the hand): it is already in it.
            if (TryOverlap(from, out Collider inside))
            {
                Stick(from, direction, inside);
                return;
            }

            if (TrySweep(from, direction, distance, out RaycastHit hit))
            {
                Stick(hit.point, direction, hit.collider);
                return;
            }
            transform.position = from + direction * distance;
        }

        private bool IsIgnored(Collider other)
        {
            return other == null || other.isTrigger || (_ignore != null && System.Array.IndexOf(_ignore, other) >= 0);
        }

        private bool TryOverlap(Vector3 position, out Collider inside)
        {
            int count = Physics.OverlapSphereNonAlloc(position, Radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            inside = null;
            for (int i = 0; i < count; i++)
            {
                if (!IsIgnored(Overlaps[i]))
                {
                    inside = Overlaps[i];
                    return true;
                }
            }
            return false;
        }

        private bool TrySweep(Vector3 from, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(from, Radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (IsIgnored(Hits[i].collider) || Hits[i].distance >= best)
                {
                    continue;
                }
                // A hit that starts inside a collider reports a zero point; use the shard's own position for it.
                best = Hits[i].distance;
                nearest = Hits[i];
                found = true;
            }
            return found;
        }

        private void Stick(Vector3 point, Vector3 direction, Collider what)
        {
            IDamageable target = what.GetComponentInParent<IDamageable>();
            bool character = target != null;
            if (character && target.IsAlive && !Cosmetic)
            {
                float damage = CombatTuning.DamageFor(BaseDamage, _casterStrength);
                DamageContext.Strike(target, damage, transform.position, CombatTuning.HitstunSeconds * 0.5f, DamageKind.Magic, _penetration, null, _striker);
            }

            // Tip in past the surface, pointing the way it came, with a little tilt so a volley does not look stamped out.
            float depth = character ? CharacterEmbedMetres : WorldEmbedMetres;
            Quaternion crooked = Quaternion.AngleAxis(Jitter(point.x + point.z * 3.1f) * 4.0f, Vector3.up)
                * Quaternion.AngleAxis(Jitter(point.y * 5.7f + point.x) * 4.0f, Vector3.right);
            transform.rotation = Quaternion.LookRotation(direction) * crooked;
            transform.position = point + transform.forward * depth;

            Transform parent = what.transform;
            transform.SetParent(parent, true);
            StuckTo = parent;
            _stuckScale = transform.localScale;

            CurrentPhase = character ? Phase.StuckInCharacter : Phase.StuckInWorld;
            StuckSecondsRemaining = character ? CharacterStickSeconds : WorldStickSeconds;

            if (_trail != null)
            {
                _trail.emitting = false;
            }
            AudioSource.PlayClipAtPoint(IceShardSounds.Impact, point, 0.8f);
        }

        /// <summary>A repeatable number in -1..1 from a position, so the same shot always sticks the same way.</summary>
        private static float Jitter(float seed)
        {
            float s = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
            return (s - Mathf.Floor(s)) * 2.0f - 1.0f;
        }

        private void Age(float deltaTime)
        {
            StuckSecondsRemaining -= deltaTime;
            if (StuckSecondsRemaining <= 0.0f)
            {
                Destroy(gameObject);
                return;
            }
            // Melts away over the last moments: shrinks to nothing.
            float melt = Mathf.Clamp01(StuckSecondsRemaining / MeltSeconds);
            transform.localScale = _stuckScale * Mathf.SmoothStep(0.0f, 1.0f, melt);
        }
    }
}
