using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// An Arcane Orb in flight (docs/ARCANE_ORB.md): a ball of magenta aura, drawn in code, flying true like the fireball and growing with the
    /// orbs it carries. On someone it does its own small hit and puts its orbs on them (<see cref="CursedOrbs.Hit"/>, told back to the caster so
    /// other players see them too); on anything else it only bursts. Fades out unspent after <see cref="ArcaneOrbRules.BallLifetimeSeconds"/>.
    /// A cosmetic copy (another player's ball) flies and bursts the same, and does nothing else: their own machine places the orbs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcaneOrbProjectile : MonoBehaviour
    {
        /// <summary>The ball's collider, and its look, metres (one orb; a full shot is larger by <see cref="GrowthPerOrb"/> an orb).</summary>
        public const float Radius = 0.16f;

        public const float GrowthPerOrb = 0.2f;

        private int _orbs;
        private float _ballDamage;
        private float _orbDamage;
        private float _penetration;
        private GameObject _caster;
        private System.Action<CursedOrbs> _placed;
        private float _age;
        private bool _spent;

        public bool Cosmetic { get; private set; }

        public int Orbs => _orbs;

        /// <summary>
        /// Launches a ball from <paramref name="origin"/> at <paramref name="velocity"/> carrying <paramref name="orbs"/>. Its hit does
        /// <paramref name="ballDamage"/> and each orb <paramref name="orbDamage"/> (both already scaled by the caster); <paramref name="placed"/>
        /// hears of the curse it leaves.
        /// </summary>
        public static ArcaneOrbProjectile Spawn(Vector3 origin, Vector3 velocity, int orbs, float ballDamage, float orbDamage, float penetration,
            GameObject caster, Collider[] ignoreColliders, bool cosmetic = false, System.Action<CursedOrbs> placed = null)
        {
            Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.forward;
            var go = new GameObject("ArcaneOrb");
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(heading));
            var ball = go.AddComponent<ArcaneOrbProjectile>();
            ball._orbs = Mathf.Clamp(orbs, 1, ArcaneOrbRules.MaxOrbsPerShot);
            ball._ballDamage = ballDamage;
            ball._orbDamage = orbDamage;
            ball._penetration = Mathf.Clamp01(penetration);
            ball._caster = caster;
            ball._placed = placed;
            ball.Cosmetic = cosmetic;

            float size = 1.0f + GrowthPerOrb * (ball._orbs - 1);
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = Radius * size;
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

            ArcaneVisuals.Sprite(go.transform, ArcaneVisuals.Glow, 1.0f * size);
            ArcaneVisuals.Sprite(go.transform, ArcaneVisuals.Glow, 0.7f * size, "Aura");
            ArcaneVisuals.Sprite(go.transform, ArcaneVisuals.HotGlow, 0.32f * size, "Hot");
            ArcaneVisuals.Trail(go, 0.22f, 0.36f * size);
            ArcaneVisuals.Motes(go.transform, 40.0f, Radius * size, 1.0f, 0.12f * size, 0.5f);
            ArcaneVisuals.Light(go.transform, 4.0f, 8.0f);
            return ball;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= ArcaneOrbRules.BallLifetimeSeconds && !_spent)
            {
                _spent = true;
                ArcaneBurst.Spawn(transform.position, 0.6f, null);
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
                DamageContext.Strike(target, _ballDamage, transform.position, CombatTuning.HitstunSeconds * 0.5f, DamageKind.Magic, _penetration,
                    point, _caster);
                // On an Arcane Barrier the blow went to the wall (docs/ARCANE_BARRIER.md): whoever stands behind it is cursed by nothing.
                CursedOrbs curse = collision.collider.GetComponent<ArcaneBarrierWall>() != null
                    ? null
                    : CursedOrbs.Hit(target, _caster, _orbs, _orbDamage, _penetration, point);
                if (curse != null)
                {
                    _placed?.Invoke(curse);
                }
            }
            ArcaneBurst.Spawn(point, 0.5f + 0.15f * _orbs, ArcaneOrbSounds.Strike, 0.5f);
            Destroy(gameObject);
        }
    }
}
