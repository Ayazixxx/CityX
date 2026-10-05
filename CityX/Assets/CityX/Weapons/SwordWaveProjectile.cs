using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A crescent of light in flight. It flies straight (no gravity), CUTS THROUGH whatever it hurts (each thing once) and is stopped by the world.
    ///
    /// TWO DIFFERENT SHAPES. The WHOLE crescent is the hitbox for enemies (a trigger that spans all of it, see SwordWaveBuilder). But only a small
    /// sphere at its CENTRE meets the world: asked for because a crescent that is stopped the instant any edge of it touches the ground dies
    /// almost at once when thrown low. The centre is swept along each step of the flight (so a thin wall cannot be skipped at 34 m/s) and checked
    /// where it stands (so it cannot be thrown from inside one).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SwordWaveProjectile : MonoBehaviour
    {
        public const float BaseDamage = 40.0f;
        public const float LifeSeconds = 1.6f;

        /// <summary>The radius of the part of the wave that is stopped by the world: its centre only.</summary>
        public const float WorldCheckRadius = 0.3f;

        private Rigidbody _rigidbody;
        private int _casterStrength;
        private float _age;
        private Collider[] _ignore;
        private Vector3 _lastCentre;
        private bool _hasLast;
        private readonly System.Collections.Generic.HashSet<IDamageable> _hit = new System.Collections.Generic.HashSet<IDamageable>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>True for the copy another player's machine shows of a wave they threw: it flies and stops at the world the same, but hurts nothing.</summary>
        public bool Cosmetic { get; private set; }

        public static SwordWaveProjectile Spawn(GameObject prefab, Vector3 origin, Vector3 velocity, int casterStrength, Collider[] ignoreColliders,
            bool cosmetic = false)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Instantiate(prefab, origin, Quaternion.LookRotation(velocity.normalized));
            var wave = instance.GetComponent<SwordWaveProjectile>();
            if (wave == null)
            {
                return null;
            }
            wave._casterStrength = casterStrength;
            wave.Cosmetic = cosmetic;
            wave._ignore = ignoreColliders;
            wave._rigidbody = instance.GetComponent<Rigidbody>();
            wave._rigidbody.linearVelocity = velocity;
            return wave;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age > LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }
            // Shrinks a little over its last third as it dies away.
            float fade = Mathf.Clamp01((LifeSeconds - _age) / (LifeSeconds * 0.33f));
            transform.localScale = Vector3.one * (0.85f + 0.15f * fade);
        }

        private void FixedUpdate()
        {
            Vector3 centre = _rigidbody != null ? _rigidbody.position : transform.position;
            if (!_hasLast)
            {
                _lastCentre = centre;
                _hasLast = true;
            }

            bool blocked = false;

            // Standing in something solid (thrown from inside a wall).
            int overlapping = Physics.OverlapSphereNonAlloc(centre, WorldCheckRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapping && !blocked; i++)
            {
                blocked = StopsTheWave(Overlaps[i]);
            }

            // And the way it came this step.
            Vector3 travel = centre - _lastCentre;
            float distance = travel.magnitude;
            if (!blocked && distance > 0.0001f)
            {
                int count = Physics.SphereCastNonAlloc(_lastCentre, WorldCheckRadius, travel / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && !blocked; i++)
                {
                    blocked = StopsTheWave(Hits[i].collider);
                }
            }
            _lastCentre = centre;

            if (blocked)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>The world: solid, not the caster's, and not something that can be hurt (those the wave passes through).</summary>
        private bool StopsTheWave(Collider other)
        {
            if (other == null || other.isTrigger)
            {
                return false;
            }
            if (_ignore != null && System.Array.IndexOf(_ignore, other) >= 0)
            {
                return false;
            }
            return other.GetComponentInParent<IDamageable>() == null;
        }

        /// <summary>The whole crescent hurts what it touches (each once). It never stops here: only the centre meets the world.</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other.isTrigger)
            {
                return;
            }
            if (_ignore != null && System.Array.IndexOf(_ignore, other) >= 0)
            {
                return;
            }
            if (Cosmetic)
            {
                return;
            }
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive && _hit.Add(target))
            {
                float damage = CombatTuning.DamageFor(BaseDamage, _casterStrength);
                target.ApplyHit(damage, transform.position, CombatTuning.HitstunSeconds);
            }
        }
    }
}
