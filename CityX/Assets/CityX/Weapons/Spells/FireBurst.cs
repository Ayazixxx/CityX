using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A one-shot flame burst at a fireball's impact point -- same
    /// standalone, self-cleaning shape as GlassShatter/BloodBurst, so it
    /// finishes playing in a fixed world position no matter what the
    /// fireball does next (it's already destroyed itself by then).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FireBurst : MonoBehaviour
    {
        /// <summary>
        /// How long the flash takes to fade out, in seconds -- short and
        /// CUBIC rather than linear, matching weapons/explosion.gd's own
        /// OmniLight3D reasoning: a cubic falloff reads as a detonation,
        /// a linear one reads as a light simply being dimmed.
        /// </summary>
        public const float FlashSeconds = 0.3f;

        private ParticleSystem _particles;
        private Light _flash;
        private float _flashStartIntensity;
        private float _flashElapsed;

        /// <summary>A burst at <paramref name="position"/>; a charged fireball's is <paramref name="scale"/> times as big, flash and all.</summary>
        public static FireBurst Spawn(GameObject prefab, Vector3 position, Vector3 normal, float scale = 1.0f)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector3 away = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(away));
            if (!Mathf.Approximately(scale, 1.0f))
            {
                instance.transform.localScale = Vector3.one * scale;
                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = particles.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                foreach (Light light in instance.GetComponentsInChildren<Light>(true))
                {
                    light.range *= scale;
                }
            }
            return instance.GetComponent<FireBurst>();
        }

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _flash = GetComponentInChildren<Light>();
            _flashStartIntensity = _flash != null ? _flash.intensity : 0.0f;
        }

        private void Update()
        {
            if (_flash != null && _flashElapsed < FlashSeconds)
            {
                _flashElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_flashElapsed / FlashSeconds);
                _flash.intensity = _flashStartIntensity * Mathf.Pow(1.0f - t, 3.0f);
            }

            // The flash decays fast on its own, but a Light left behind
            // after the particles finish would keep lighting the scene at
            // a dim, wrong-looking trickle for however long floating-point
            // noise takes to actually reach zero -- clamped off explicitly
            // once its own timer is done, rather than trusted to.
            if (_flash != null && _flashElapsed >= FlashSeconds && _flash.enabled)
            {
                _flash.enabled = false;
            }

            if (_particles != null && !_particles.IsAlive(true))
            {
                Destroy(gameObject);
            }
        }
    }
}
