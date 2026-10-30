using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A giant coming down from its leap (docs/GIANT.md): everyone within its slam is hurt (most at the middle) and thrown down and away, the
    /// camera of a player near it shakes, and the street heaves (<see cref="DustBurst"/>: a ring of dust racing out over the ground, dust
    /// rolling out and up), with a deep boom.
    /// </summary>
    public sealed class GiantSlam : MonoBehaviour
    {
        /// <summary>How hard a player's camera shakes at the middle, metres, falling away to nothing at four times the slam's reach.</summary>
        public const float ShakeMetres = 0.6f;

        public const float ShakeSeconds = 0.8f;

        private static readonly List<IDamageable> Struck = new List<IDamageable>();

        /// <summary>
        /// A giant of <paramref name="scale"/> lands at <paramref name="feet"/>: the slam's blows (struck by <paramref name="giant"/>), the shake
        /// and the look. Returns how many it struck. The giant's own machine only: every other player's copy of it <see cref="Show"/>s it.
        /// </summary>
        public static int Land(Vector3 feet, float scale, GameObject giant)
        {
            float size = Mathf.Max(0.01f, scale / GiantRules.Scale);
            float radius = GiantRules.SlamRadius * size;
            Struck.Clear();
            foreach (Collider collider in Physics.OverlapSphere(feet, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || Struck.Contains(target) || !(target is Component body)
                    || (giant != null && (body.transform.IsChildOf(giant.transform) || EnemyPack.AreAllies(giant, target))))
                {
                    continue;   // never itself, nor a packmate (no friendly fire: not thrown either)
                }
                Struck.Add(target);
                Vector3 away = body.transform.position - feet;
                away.y = 0.0f;
                float share = GiantRules.SlamShare(away.magnitude / size);
                if (share <= 0.0f)
                {
                    continue;
                }
                Vector3 direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
                DamageContext.Strike(target, GiantRules.SlamDamage * share, feet, 0.4f, DamageKind.Physical, 0.0f,
                    body.transform.position + Vector3.up, giant);
                Vector3 throwVelocity = direction * GiantRules.SlamThrowSpeed * share + Vector3.up * GiantRules.SlamThrowUpSpeed * share;
                if (target.IsAlive && target is PlayerVitals vitals)
                {
                    vitals.TriggerLaunchKnockdown(throwVelocity);
                }
                else if (target.IsAlive && target is ILaunchable launchable)
                {
                    launchable.Launch(throwVelocity);
                }
            }
            Show(feet, scale);
            return Struck.Count;
        }

        /// <summary>The slam as everyone sees and feels it, with no blows: the shake, the dust and the boom.</summary>
        public static void Show(Vector3 feet, float scale)
        {
            float size = Mathf.Max(0.01f, scale / GiantRules.Scale);
            float radius = GiantRules.SlamRadius * size;
            ThirdPersonCamera.ShakeNear(feet, radius * 4.0f, ShakeMetres, ShakeSeconds);
            var go = new GameObject("GiantSlam");
            go.transform.position = feet;
            go.AddComponent<GiantSlam>().Build(radius, Mathf.Clamp(size, 0.5f, 1.5f));
        }

        private void Build(float radius, float amount)
        {
            DustBurst.Roll(transform.position, radius, amount, transform);
            if (Application.isPlaying)
            {
                var boom = gameObject.AddComponent<AudioSource>();
                boom.clip = ArcaneOrbSounds.Strike;
                boom.pitch = 0.35f;   // the arcane strike's thump, a long way down: a giant's weight
                boom.volume = 1.0f;
                boom.spatialBlend = 1.0f;
                boom.minDistance = radius;
                boom.maxDistance = radius * 12.0f;
                boom.Play();
            }
            Destroy(gameObject, DustBurst.LifeSeconds);
        }
    }
}
