using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A fireball in flight, spawned fresh on every cast -- same shape as
    /// ThrownBottle (a separate physics-driven object, not the caster
    /// itself), just for a spell instead of a thrown item. No Godot
    /// equivalent to port -- an explicit new-mechanic ask, same footing as
    /// Jetpack.cs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FireballProjectile : MonoBehaviour
    {
        public const float BaseDamage = 25.0f;

        public GameObject BurstPrefab;
        public AudioClip ImpactSound;

        private Rigidbody _rigidbody;
        private int _casterStrength;
        private bool _consumed;

        /// <summary>
        /// True for the copy another player's machine shows of a fireball they cast: it flies, bursts and sounds exactly as the real one
        /// does, but hurts nothing (the caster's own machine decides what the real fireball hits).
        /// </summary>
        public bool Cosmetic { get; private set; }

        public static FireballProjectile Spawn(GameObject prefab, Vector3 origin, Vector3 velocity,
            int casterStrength, Collider[] ignoreColliders, bool cosmetic = false)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, origin, Quaternion.LookRotation(velocity.normalized));
            var fireball = instance.GetComponent<FireballProjectile>();
            if (fireball == null)
            {
                return null;
            }

            fireball._casterStrength = casterStrength;
            fireball.Cosmetic = cosmetic;
            // GetComponent RATHER THAN THE CACHED FIELD -- see ThrownBottle.
            // Spawn's own comment: Awake has not necessarily run yet on a
            // clone made from an inactive prefab template.
            if (fireball._rigidbody == null)
            {
                fireball._rigidbody = instance.GetComponent<Rigidbody>();
            }
            fireball._rigidbody.linearVelocity = velocity;

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

            return fireball;
        }

        /// <summary>
        /// The fireball's own light, so it glows in the dark and lights the street and walls it flies past (owner, 2026-10-10: "make the
        /// fireball have a light effect so it glows in the dark"). Warm fire orange, no shadow (a moving point light's shadow is six renders a
        /// frame), flickering a little like a flame. On the prefab, so every player's copy of a fireball glows.
        /// </summary>
        public static readonly Color GlowColour = new Color(1.0f, 0.55f, 0.18f);
        public const float GlowIntensity = 6.0f;
        public const float GlowRange = 10.0f;

        /// <summary>How far the flicker takes the glow either side of its brightness, and how fast.</summary>
        public const float GlowFlicker = 0.25f;
        public const float GlowFlickerSpeed = 14.0f;

        private Light _glow;
        private float _flickerSeed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _glow = GetComponentInChildren<Light>();
            _flickerSeed = Random.value * 100.0f;
        }

        /// <summary>
        /// Keeps facing wherever it is CURRENTLY flying, not just where it
        /// was thrown from. Spawn() only sets transform.rotation once, from
        /// the launch velocity -- fine for a straight shot, since nothing
        /// else here ever changes _rigidbody.linearVelocity afterward, but
        /// a future reflect ability will: the trail is authored as a fixed
        /// local rotation pointing backward off THIS transform (see
        /// FireballProjectileBuilder.BuildTrail), not something that
        /// re-aims itself independently, so if a reflection changes the
        /// velocity without this the trail would keep streaming out of
        /// wherever the ball ORIGINALLY faced instead of "out of the back"
        /// relative to its new, reflected direction.
        /// </summary>
        private void Update()
        {
            if (_glow != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * GlowFlickerSpeed, _flickerSeed) * 2.0f - 1.0f;
                _glow.intensity = GlowIntensity * (1.0f + GlowFlicker * noise);
            }

            Vector3 velocity = _rigidbody.linearVelocity;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
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

            var target = Cosmetic ? null : collision.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                float damage = CombatTuning.DamageFor(BaseDamage, _casterStrength);
                target.ApplyHit(damage, transform.position, CombatTuning.HitstunSeconds);
            }

            FireBurst.Spawn(BurstPrefab, point, normal);
            if (ImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(ImpactSound, point);
            }

            Destroy(gameObject);
        }
    }
}
