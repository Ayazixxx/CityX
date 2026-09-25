using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A one-shot glass-shard burst at a thrown bottle's impact point,
    /// ported from weapons/glass_shatter.gd -- the same standalone,
    /// self-cleaning shape as CityX.Character.BloodSplatter, so a shatter
    /// finishes playing in a fixed world position no matter what the
    /// thrown bottle does next (it's already destroyed itself by then).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class GlassShatter : MonoBehaviour
    {
        private ParticleSystem _particles;

        public static GlassShatter Spawn(GameObject prefab, Vector3 position, Vector3 normal)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector3 away = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(away));
            return instance.GetComponent<GlassShatter>();
        }

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            if (_particles != null && !_particles.IsAlive(true))
            {
                Destroy(gameObject);
            }
        }
    }
}
