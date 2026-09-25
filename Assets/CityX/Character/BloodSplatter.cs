using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A one-shot blood spurt at a landed hit's own impact point, ported from
    /// weapons/blood_splatter.gd and its scene.
    ///
    /// EVERY NUMBER HERE IS THE GODOT SCENE'S. 14 droplets, 0.4s lifetime, a
    /// 28-degree cone, 1.2-2.8 m/s, scale 0.6-1.4, and the same red that fades
    /// its alpha to nothing over the last 40% of life. They live in the builder
    /// that makes the prefab rather than being typed here, for the same reason
    /// the Godot version keeps them on the .tscn: this script's only real job
    /// is cleaning itself up.
    ///
    /// STANDALONE IN THE WORLD, not a child of either body. It has to finish
    /// playing in a fixed world position no matter what the attacker or the
    /// target does next -- and a corpse that despawns should not take the
    /// spray with it.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BloodSplatter : MonoBehaviour
    {
        private ParticleSystem _particles;

        /// <summary>
        /// Puts one burst at a hit, spraying away from the surface.
        ///
        /// A static entry point rather than an instance method on the attacker,
        /// so more than one thing that draws blood can call it without
        /// duplicating the spawn -- the same shape blood_splatter.gd uses, and
        /// for the same reason.
        /// </summary>
        public static BloodSplatter Spawn(GameObject prefab, Vector3 position, Vector3 normal)
        {
            if (prefab == null)
            {
                return null;
            }

            // A zero normal would give LookRotation an undefined answer and a
            // console error. Straight up is a poor spray direction but a
            // defined one.
            Vector3 away = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;

            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(away));
            return instance.GetComponent<BloodSplatter>();
        }

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            // Frees itself once the burst has finished, matching this project's
            // "a temporary effect cleans up after itself" convention. Without
            // it every punch leaks a GameObject for the rest of the session.
            if (_particles != null && !_particles.IsAlive(true))
            {
                Destroy(gameObject);
            }
        }
    }
}
