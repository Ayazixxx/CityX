using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A crescent of light in flight. It flies straight (no gravity), CUTS THROUGH whatever it hurts (each thing once) and is stopped by the
    /// world. A trigger on a rigid body, unlike the fireball's solid ball, because it passes through enemies.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SwordWaveProjectile : MonoBehaviour
    {
        public const float BaseDamage = 40.0f;
        public const float LifeSeconds = 1.6f;

        private Rigidbody _rigidbody;
        private int _casterStrength;
        private float _age;
        private Collider[] _ignore;
        private readonly System.Collections.Generic.HashSet<IDamageable> _hit = new System.Collections.Generic.HashSet<IDamageable>();

        public static SwordWaveProjectile Spawn(GameObject prefab, Vector3 origin, Vector3 velocity, int casterStrength, Collider[] ignoreColliders)
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
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                if (target.IsAlive && _hit.Add(target))
                {
                    float damage = CombatTuning.DamageFor(BaseDamage, _casterStrength);
                    target.ApplyHit(damage, transform.position, CombatTuning.HitstunSeconds);
                }
                return;   // through the body, and on
            }
            Destroy(gameObject);   // the world stops it
        }
    }
}
