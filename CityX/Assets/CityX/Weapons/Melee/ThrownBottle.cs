using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOTTLE IN THE AIR (docs/GLASS_BOTTLE.md): it leaves the hand as it lay in it, turning end over end, and whatever it first touches
    /// (a body, a wall, the ground, anything) it shatters on, whole: nothing is left to pick up (owner, 2026-11-23: "when thrown the whole
    /// thing shatters it doesnt leave a half you can pick up"). A body it hits is hurt; a window it hits is taken out. Another player's
    /// thrown bottle is flown here as a copy that shatters the same way and hurts nothing (<see cref="Harmless"/>): the thrower's machine
    /// dealt the blow.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownBottle : MonoBehaviour
    {

        /// <summary>The impact speed, in m/s, a bottle needs to break a pane rather than only itself.</summary>
        public const float MinGlassBreakSpeed = 4.0f;

        /// <summary>How fast it turns end over end, turns a second: a thrown bottle is seen to tumble.</summary>
        public const float TumblesPerSecond = 2.2f;

        [Tooltip("The bottle as it may be thrown, in BottleWeapon.DurabilityState's order: whole, broken, down to its neck (BottleBuilder).")]
        public GameObject[] StageVisuals = new GameObject[0];

        [Tooltip("The bottle's own pieces (BottleShatter), made where it breaks.")]
        public GameObject ShatterPrefab;
        public AudioClip ImpactCharacterSound;
        public AudioClip ImpactTerrainSound;

        private Rigidbody _rigidbody;
        private int _throwerStrength;

        /// <summary>Who threw it: named with its blows, so the thrower sees their damage (DamageDealt).</summary>
        private GameObject _striker;
        private float _penetration;
        private bool _consumed;
        private Vector3 _velocity;
        private float _damage;
        private float _reachesTo = float.PositiveInfinity;

        /// <summary>A copy of another player's throw: it breaks and hurts nothing.</summary>
        public bool Harmless { get; private set; }

        /// <summary>Raised as it shatters, with where.</summary>
        public event System.Action<Vector3> Shattered;

        /// <summary>
        /// A bottle leaves <paramref name="origin"/> turned as <paramref name="rotation"/> (as the hand held it), going at
        /// <paramref name="velocity"/> and tumbling forward over itself.
        /// </summary>
        public static ThrownBottle Spawn(GameObject prefab, Vector3 origin, Quaternion rotation, Vector3 velocity,
            int throwerStrength, Collider[] ignoreColliders, float penetration = 0.0f, GameObject striker = null, bool harmless = false,
            int stage = 0, float damage = BottleWeapon.IntactThrowDamage, float reachesTo = float.PositiveInfinity)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, origin, rotation);
            var thrown = instance.GetComponent<ThrownBottle>();
            if (thrown == null)
            {
                return null;
            }

            thrown._throwerStrength = throwerStrength;
            thrown._striker = striker;
            thrown._penetration = Mathf.Clamp01(penetration);
            thrown.Harmless = harmless;
            thrown._damage = damage;
            thrown._reachesTo = reachesTo;
            for (int i = 0; i < thrown.StageVisuals.Length; i++)
            {
                if (thrown.StageVisuals[i] != null)
                {
                    thrown.StageVisuals[i].SetActive(i == Mathf.Clamp(stage, 0, thrown.StageVisuals.Length - 1));
                }
            }
            // GetComponent RATHER THAN THE CACHED FIELD: Awake() has not necessarily run yet on a clone made from an inactive prefab.
            if (thrown._rigidbody == null)
            {
                thrown._rigidbody = instance.GetComponent<Rigidbody>();
            }
            thrown._rigidbody.linearVelocity = velocity;
            thrown._velocity = velocity;
            Vector3 over = Vector3.Cross(Vector3.up, velocity);
            if (over.sqrMagnitude > 1e-6f)
            {
                thrown._rigidbody.maxAngularVelocity = TumblesPerSecond * Mathf.PI * 2.0f * 1.5f;
                thrown._rigidbody.angularVelocity = over.normalized * (TumblesPerSecond * Mathf.PI * 2.0f);
            }

            if (ignoreColliders != null)
            {
                foreach (Collider own in instance.GetComponentsInChildren<Collider>())
                {
                    foreach (Collider ignore in ignoreColliders)
                    {
                        if (ignore != null)
                        {
                            Physics.IgnoreCollision(own, ignore);
                        }
                    }
                }
            }

            return thrown;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            // What it was doing just before it hit: by the time a collision is told of, the body has already bounced.
            if (!_consumed && _rigidbody != null)
            {
                _velocity = _rigidbody.linearVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_consumed)
            {
                return;
            }
            _consumed = true;

            Vector3 point = transform.position;
            Vector3 normal = Vector3.up;
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                point = contact.point;
                normal = contact.normal;
            }

            var target = collision.collider.GetComponentInParent<IDamageable>();
            bool hitCharacter = target != null && target.IsAlive;

            // A thrown bottle goes through a window: a heavy blow, straight into the pane, so it is taken out at once (a thrown brick in The
            // Last of Us Part II is the model). A lob that only bumps the glass breaks itself alone.
            if (!Harmless && collision.relativeVelocity.magnitude >= MinGlassBreakSpeed)
            {
                var breakable = collision.collider.GetComponentInParent<CityX.World.IBreakableSurface>();
                breakable?.TakeBlow(point, -normal, CityX.World.BlowStrength.Heavy);
            }

            BottleShatter.Spawn(ShatterPrefab, transform.position, transform.rotation, _velocity, normal, float.NegativeInfinity, _reachesTo);
            AudioClip sound = hitCharacter ? ImpactCharacterSound : ImpactTerrainSound;
            if (sound != null)
            {
                AudioSource.PlayClipAtPoint(sound, point);
            }

            if (hitCharacter && !Harmless)
            {
                float damage = CombatTuning.DamageFor(_damage, _throwerStrength);
                DamageContext.Strike(target, damage, transform.position, CombatTuning.HitstunSeconds, DamageKind.Physical, _penetration, null, _striker);
            }

            Shattered?.Invoke(point);
            Destroy(gameObject);
        }
    }
}
