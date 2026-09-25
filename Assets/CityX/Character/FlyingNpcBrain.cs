using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Decides which way the flyer is currently heading -- the airborne
    /// counterpart to CivilianBrain's own ground wander, same reasoning for
    /// being a plain class rather than a MonoBehaviour: a heading that never
    /// changes, or a bounds check that never fires, is a bug measured in
    /// minutes of watching the sky rather than milliseconds of stepping.
    ///
    /// PURELY HORIZONTAL AND PURELY DIRECTIONAL -- unlike CivilianBrain it
    /// never names a specific target point (there is no StreetNetwork
    /// equivalent for open sky), and it knows nothing about altitude at all;
    /// FlyingNpcSteering owns climbing/sinking and obstacle avoidance
    /// entirely separately, the same split NpcSteering already keeps from
    /// CivilianBrain.
    /// </summary>
    public sealed class FlyingNpcBrain
    {
        public const float WanderMinSeconds = 6.0f;
        public const float WanderMaxSeconds = 14.0f;

        /// <summary>
        /// Past this distance from the point it was created around, wandering
        /// stops and it heads straight back instead -- otherwise a long
        /// enough run of unlucky random headings could drift it out over
        /// unbuilt terrain forever.
        /// </summary>
        public const float WorldBoundsRadius = 220.0f;

        private readonly DeterministicRandom _random;
        private readonly Vector3 _centre;

        private Vector3 _heading;
        private float _timer;

        public FlyingNpcBrain(int seed, Vector3 centre)
        {
            _random = new DeterministicRandom(seed);
            _centre = centre;
            _heading = RandomHeading();
            // Staggered like CivilianBrain's own idle timer, so several
            // spawned together do not all turn in unison.
            _timer = (float)_random.Range(0.0, WanderMaxSeconds);
        }

        public Vector3 Heading => _heading;

        /// <summary>Seconds until the current heading is reconsidered.</summary>
        public float TimeRemaining => _timer;

        /// <summary>
        /// Advances by one step and returns the horizontal direction to fly
        /// this frame -- always a unit vector with y = 0; altitude is
        /// FlyingNpcSteering's own job entirely.
        /// </summary>
        public Vector3 Tick(Vector3 position, float deltaTime)
        {
            Vector3 fromCentre = position - _centre;
            fromCentre.y = 0.0f;
            if (fromCentre.magnitude > WorldBoundsRadius)
            {
                // Overrides the wander outright rather than blending toward
                // it -- the whole point is to GUARANTEE it turns back, not
                // just usually.
                _heading = -fromCentre.normalized;
                return _heading;
            }

            _timer -= deltaTime;
            if (_timer <= 0.0f)
            {
                _heading = RandomHeading();
                _timer = (float)_random.Range(WanderMinSeconds, WanderMaxSeconds);
            }
            return _heading;
        }

        private Vector3 RandomHeading()
        {
            float degrees = (float)_random.Range(0.0, 360.0);
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0.0f, Mathf.Cos(radians));
        }
    }
}
