using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A speck of glittering dust in flight, the Magic Wand's and the Magic Staff's shot (docs/MAGIC_WAND.md): a white spark in a soft white glow,
    /// a short streak behind it and a few motes shed as it goes, drawn in code, flying true. On someone it hurts them a little and makes them
    /// flinch (magic: Spell Power and magic resistance apply), with no blood (dust does not cut); on anything else it only bursts into sparkles.
    /// Fizzles out unspent after <see cref="MagicSpeckRules.LifetimeSeconds"/>. A cosmetic copy (another player's) flies and bursts the same and
    /// hurts nobody: their own machine does.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MagicSpeck : MonoBehaviour
    {
        /// <summary>The glow: white, a breath of blue in it so it reads as light, not paper.</summary>
        public static readonly Color Glow = new Color(0.88f, 0.92f, 1.0f);

        /// <summary>The spark's middle.</summary>
        public static readonly Color Hot = Color.white;

        public static readonly GlowPalette Palette = new GlowPalette("MagicSpeck", Glow, Hot);

        private float _damage;
        private float _penetration;
        private GameObject _caster;
        private float _size;
        private float _age;
        private bool _spent;

        public bool Cosmetic { get; private set; }

        /// <summary>What it does to whoever it hits (already scaled by the caster's Spell Power).</summary>
        public float Damage => _damage;

        /// <summary>How big it is against the wand's (the staff's is bigger).</summary>
        public float Size => _size;

        /// <summary>Shoots a speck <paramref name="size"/> times the wand's from <paramref name="origin"/> at <paramref name="velocity"/>.</summary>
        public static MagicSpeck Spawn(Vector3 origin, Vector3 velocity, float size, float damage, float penetration, GameObject caster,
            Collider[] ignoreColliders, bool cosmetic = false)
        {
            Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            var go = new GameObject("MagicSpeck");
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(heading));
            var speck = go.AddComponent<MagicSpeck>();
            speck._damage = damage;
            speck._penetration = Mathf.Clamp01(penetration);
            speck._caster = caster;
            speck._size = size;
            speck.Cosmetic = cosmetic;

            var collider = go.AddComponent<SphereCollider>();
            collider.radius = MagicSpeckRules.Radius * size;
            Rigidbody body = go.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            if (ignoreColliders != null)
            {
                foreach (Collider ignore in ignoreColliders)
                {
                    if (ignore != null)
                    {
                        Physics.IgnoreCollision(collider, ignore);
                    }
                }
            }

            ArcaneVisuals.Sprite(go.transform, Palette.Glow, 0.36f * size);
            ArcaneVisuals.Sprite(go.transform, Palette.Hot, 0.12f * size, "Hot");
            ArcaneVisuals.Trail(go, 0.12f, 0.08f * size, Palette);
            ArcaneVisuals.Motes(go.transform, 30.0f, 0.03f * size, 0.4f, 0.05f * size, 0.35f, Palette);
            ArcaneVisuals.Light(go.transform, 1.2f, 3.5f, Glow);
            return speck;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= MagicSpeckRules.LifetimeSeconds && !_spent)
            {
                _spent = true;
                ArcaneBurst.Spawn(transform.position, 0.25f * _size, null, 1.0f, Palette);
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_spent)
            {
                return;
            }
            _spent = true;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var target = Cosmetic ? null : collision.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                using (DamageContext.WithoutBlood())   // dust does not cut
                {
                    DamageContext.Strike(target, _damage, transform.position, MagicSpeckRules.HitstunSeconds, DamageKind.Magic, _penetration, point,
                        _caster);
                }
            }
            ArcaneBurst.Spawn(point, 0.45f * _size, MagicSpeckSounds.Hit, 0.5f, Palette);
            Destroy(gameObject);
        }
    }
}
