using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The bottle in flight, spawned fresh on every throw -- ported from
    /// weapons/thrown_bottle.gd. A separate physics-driven object from the
    /// equipped BottleWeapon, which is destroyed the moment this is spawned
    /// (see BottleWeapon.Throw): a thrown bottle is consumed, not carried.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownBottle : MonoBehaviour
    {
        public const float ThrowDamage = 18.0f;

        public GameObject ShatterPrefab;
        public AudioClip ImpactCharacterSound;
        public AudioClip ImpactTerrainSound;

        private Rigidbody _rigidbody;
        private int _throwerStrength;
        private bool _consumed;

        public static ThrownBottle Spawn(GameObject prefab, Vector3 origin, Vector3 velocity,
            int throwerStrength, Collider[] ignoreColliders)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, origin, Quaternion.identity);
            var thrown = instance.GetComponent<ThrownBottle>();
            if (thrown == null)
            {
                return null;
            }

            thrown._throwerStrength = throwerStrength;
            // GetComponent RATHER THAN THE CACHED FIELD: Awake() has not
            // necessarily run yet on a clone made from an inactive prefab
            // template by the time Spawn touches it here (Unity defers
            // Awake on a GameObject that starts inactive) -- same fix as
            // BottleWeapon.OnEquip, for the same reason.
            if (thrown._rigidbody == null)
            {
                thrown._rigidbody = instance.GetComponent<Rigidbody>();
            }
            thrown._rigidbody.linearVelocity = velocity;

            if (ignoreColliders != null)
            {
                Collider ownCollider = instance.GetComponent<Collider>();
                if (ownCollider != null)
                {
                    foreach (Collider ignore in ignoreColliders)
                    {
                        if (ignore != null)
                        {
                            Physics.IgnoreCollision(ownCollider, ignore);
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

        /// <summary>The impact speed, in m/s, a bottle needs to break a pane rather than bounce off it.</summary>
        public const float MinGlassBreakSpeed = 4.0f;

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

            // A thrown bottle goes through a window: a heavy blow, straight into the pane, so it is taken
            // out at once (a thrown brick in The Last of Us Part II is the model). A lob that only bumps
            // the glass does nothing.
            if (collision.relativeVelocity.magnitude >= MinGlassBreakSpeed)
            {
                var breakable = collision.collider.GetComponentInParent<CityX.World.IBreakableSurface>();
                breakable?.TakeBlow(point, -normal, CityX.World.BlowStrength.Heavy);
            }

            GlassShatter.Spawn(ShatterPrefab, point, normal);
            PlayImpactSound(hitCharacter ? ImpactCharacterSound : ImpactTerrainSound, point);

            if (hitCharacter)
            {
                float damage = CombatTuning.DamageFor(ThrowDamage, _throwerStrength);
                target.ApplyHit(damage, transform.position, CombatTuning.HitstunSeconds);
            }

            Destroy(gameObject);
        }

        private static void PlayImpactSound(AudioClip clip, Vector3 point)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, point);
            }
        }
    }
}
