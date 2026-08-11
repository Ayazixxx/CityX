using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Decides where a pedestrian is walking. Ported from npc/civilian.gd's
    /// wander half.
    ///
    /// A PLAIN CLASS TAKING A DELTA, not a MonoBehaviour. The Godot original
    /// was a _physics_process, so the only way to find out whether a civilian
    /// ever actually arrived anywhere was to stand and watch one -- and the
    /// failure modes here are all slow: a target it can never reach, a timeout
    /// that never fires, an idle it never leaves. Those are minutes of watching
    /// or milliseconds of stepping.
    ///
    /// It knows nothing about colliders. Targets come from
    /// <see cref="StreetNetwork"/> and are on a pavement by construction, so
    /// there is nothing to walk into and no obstacle avoidance to get wrong.
    /// </summary>
    public sealed class CivilianBrain
    {
        // --- tuning, carried from civilian.gd ---

        public const float WalkSpeed = 1.5f;

        public const float IdleMinSeconds = 1.2f;
        public const float IdleMaxSeconds = 4.5f;

        public const float ArriveDistance = 0.6f;

        public const float MinStep = 6.0f;
        public const float MaxStep = 18.0f;

        /// <summary>
        /// Gives up on a target it has not reached. Kept from the original even
        /// though targets are now reachable by construction: a civilian shoved
        /// off the pavement by the player, or wedged against something, must
        /// still eventually pick again rather than lean on it forever.
        ///
        /// DERIVED, NOT CHOSEN. It was 12 seconds, copied across from Godot
        /// where the steps were shorter -- and 12 seconds at 1.5 m/s is 18m,
        /// which is less than the longest walk this can hand out. So the
        /// timeout was firing on perfectly healthy walks and pedestrians were
        /// abandoning targets a few metres short, over and over. A safety net
        /// that catches normal behaviour is not a safety net.
        ///
        /// The longest possible walk is a full step along a street plus a
        /// crossing of it, taken as the diagonal; the margin covers turning on
        /// the spot at either end.
        /// </summary>
        public static readonly float WalkTimeoutSeconds =
            Mathf.Sqrt(MaxStep * MaxStep +
                       4.0f * World.StreetNetwork.PavementOffset * World.StreetNetwork.PavementOffset)
            / WalkSpeed * 1.4f;

        private readonly DeterministicRandom _random;

        private bool _walking;
        private float _timer;
        private Vector3 _target;

        public CivilianBrain(int seed)
        {
            _random = new DeterministicRandom(seed);
            // Starts idle, with a stagger, so a crowd spawned on one frame does
            // not step off in perfect unison like a parade.
            _timer = (float)_random.Range(0.0, IdleMaxSeconds);
        }

        public bool IsWalking => _walking;

        public Vector3 Target => _target;

        /// <summary>Seconds until the current idle or walk gives up.</summary>
        public float TimeRemaining => _timer;

        /// <summary>
        /// Advances by one step and returns the direction to walk, or zero to
        /// stand still. Flat: the y is always 0, because gravity is the body's
        /// business and a pedestrian aiming down a hill would walk into it.
        /// </summary>
        public Vector3 Tick(Vector3 position, float deltaTime)
        {
            _timer -= deltaTime;

            if (!_walking)
            {
                if (_timer > 0.0f)
                {
                    return Vector3.zero;
                }
                BeginWalk(position);
                return Vector3.zero;   // one frame of standing, then off
            }

            Vector3 toTarget = _target - position;
            toTarget.y = 0.0f;

            if (toTarget.magnitude <= ArriveDistance || _timer <= 0.0f)
            {
                BeginIdle();
                return Vector3.zero;
            }

            return toTarget.normalized;
        }

        private void BeginIdle()
        {
            _walking = false;
            _timer = (float)_random.Range(IdleMinSeconds, IdleMaxSeconds);
        }

        private void BeginWalk(Vector3 position)
        {
            _target = StreetNetwork.NextTarget(position, _random, MinStep, MaxStep);
            _target.y = position.y;
            _walking = true;
            _timer = WalkTimeoutSeconds;
        }
    }
}
