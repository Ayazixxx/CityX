using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A stream of fire (docs/FIRE.md, "The fire spells"): the flame staff's (docs/STAFFS.md), the flamethrower's, a jetpack's exhaust, on an
    /// object whose +Z is the way the fire goes. While it is on, each frame it pours burning gas along the stream (FireScene.Jet: white-hot at
    /// the source, swelling and cooling through the fire's bands to smoke at its end, embers and a soft glow along it): as far as the stream
    /// reaches, or to the first thing in the way, where it splashes into flames and, a big enough stream, leaves a smudge of soot on a wall (the
    /// ground catches by the weapon's own rules: FireScene.IgniteAlongStream). Rain shortens it. The gas keeps the velocity of what pours it, so
    /// a jetpack's flame stays under the flyer. Its light, if it has one, flickers as the flamethrower's does (Perlin noise, so it wanders
    /// rather than strobes) and is off while the fire is out. Another player's copy runs the same, from where its owner points it.
    /// </summary>
    public sealed class FlameStreamLook : MonoBehaviour
    {
        [Tooltip("Flickers while the fire is on, off otherwise. Optional (the flamethrower lights its own).")]
        public Light Light;

        [Tooltip("How far the stream reaches dry, metres (rain shortens it to half). The flame staff's by default.")]
        public float Reach = FlameStreamRules.Range;

        [Tooltip("Its size against the flame staff's stream: the flamethrower's a little smaller, a jetpack's exhaust far smaller.")]
        public float Scale = 1.0f;

        [Tooltip("Whether a wall it plays on is sooted (not by a jetpack's exhaust).")]
        public bool Soots = true;

        /// <summary>The smallest stream that soots a wall it plays on.</summary>
        public const float SootsFromScale = 0.5f;

        /// <summary>The most of its source's velocity the gas keeps, metres a second (a jetpack's boost).</summary>
        public const float MostCarried = 16.0f;

        private bool _on;
        private float _share = 1.0f;
        private float _flickerPhase;
        private float _sootDue;
        private FireJetDebts _debts;
        private Vector3 _lastFrom;
        private bool _hasLast;

        public bool IsOn => _on;

        /// <summary>Burns at <paramref name="share"/> of full strength (a jetpack's hover burns less than its climb).</summary>
        public void Run(float share = 1.0f)
        {
            _on = true;
            _share = Mathf.Clamp01(share);
        }

        public void Stop()
        {
            _on = false;
            _hasLast = false;
            if (Light != null)
            {
                Light.intensity = 0.0f;
            }
        }

        /// <summary>Points the fire from <paramref name="from"/> along <paramref name="direction"/>.</summary>
        public void Aim(Vector3 from, Vector3 direction)
        {
            if (direction.sqrMagnitude > 1e-6f)
            {
                transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction));
            }
        }

        private void Update()
        {
            if (!_on)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            if (Light != null)
            {
                _flickerPhase += deltaTime * Flamethrower.FlickerSpeed;
                Light.intensity = Mathf.Lerp(Flamethrower.FlickerMinIntensity, Flamethrower.FlickerMaxIntensity, Mathf.PerlinNoise(_flickerPhase, 0.0f)) * _share;
            }
            Vector3 from = transform.position;
            Vector3 direction = transform.forward;
            // Capped: a copy's stream is re-aimed ten times a second, and the jump to where it was pointed is not a velocity.
            Vector3 carried = _hasLast && deltaTime > 1e-5f ? Vector3.ClampMagnitude((from - _lastFrom) / deltaTime, MostCarried) : Vector3.zero;
            _lastFrom = from;
            _hasLast = true;
            float strength = Weather.FlameStrength(CurrentWeather.QuenchingOn(from));
            float reach = Reach * (0.5f + 0.5f * Mathf.Clamp01(strength));
            bool hit = BloodSplats.Surface(from, direction, reach, transform.root, out RaycastHit wall);
            float end = hit ? wall.distance : reach;
            FireScene.Jet(from, direction, reach, end, hit, wall.point, wall.normal, strength * _share, Scale, carried, ref _debts, deltaTime);
            _sootDue -= deltaTime;
            if (Soots && Scale >= SootsFromScale && hit && _sootDue <= 0.0f && wall.normal.y < 0.5f)
            {
                _sootDue = SpellFire.SootEverySeconds;
                FireMarks.ScorchOn(wall.point, wall.normal, SpellFire.SootMetres * Scale, false);
            }
        }
    }
}
