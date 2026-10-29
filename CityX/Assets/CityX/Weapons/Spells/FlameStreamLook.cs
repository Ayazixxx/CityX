using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The flame staff's fire (docs/STAFFS.md): the flamethrower's own three-layer stream (FlamethrowerBuilder.BuildStaffFlame: a hot core, the
    /// orange flame round it, smoke off it) and its flickering light, on a prefab the caster puts at the staff's head. Its +Z is the way the
    /// fire goes. The light flickers as the flamethrower's does (Perlin noise, so it wanders rather than strobes) and is off while the fire is out.
    /// </summary>
    public sealed class FlameStreamLook : MonoBehaviour
    {
        [Tooltip("The outer flame; the core and the smoke are its children, so playing and stopping it plays and stops them.")]
        public ParticleSystem Flame;

        [Tooltip("Flickers while the fire is on, off otherwise.")]
        public Light Light;

        private float _flickerPhase;

        public bool IsOn => Flame != null && Flame.isEmitting;

        public void Run()
        {
            if (Flame != null && !Flame.isEmitting)
            {
                Flame.Play();
            }
        }

        public void Stop()
        {
            if (Flame != null && Flame.isEmitting)
            {
                Flame.Stop();
            }
            if (Light != null)
            {
                Light.intensity = 0.0f;
            }
        }

        /// <summary>Points the fire from <paramref name="from"/> along <paramref name="direction"/> and flickers its light.</summary>
        public void Aim(Vector3 from, Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude > 1e-6f)
            {
                transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            }
            if (Light == null)
            {
                return;
            }
            _flickerPhase += deltaTime * Flamethrower.FlickerSpeed;
            Light.intensity = Mathf.Lerp(Flamethrower.FlickerMinIntensity, Flamethrower.FlickerMaxIntensity, Mathf.PerlinNoise(_flickerPhase, 0.0f));
        }
    }
}
