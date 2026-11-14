using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How a Wraith floats about (owner, 2026-10-26: "make him float around"): the pure maths of its drifting, bobbing and hovering, driven
    /// through <see cref="Tick"/> with an explicit time step so a test can run an hour of haunting in milliseconds. Pure, like
    /// FlyingNpcBrain and CivilianBrain; the body (Wraith) steers round walls and moves.
    ///
    /// It drifts a while toward a heading, then lingers, hanging in the air and turning slowly, then picks another heading and drifts again:
    /// a ghost haunting a place, not a bird crossing it. It turns through its headings rather than snapping to them (TurnDegreesPerSecond),
    /// so its path curves, and its speed eases in and out (Acceleration), so it glides. Past HomeRadius from where it was born it heads home.
    /// It bobs up and down on a slow sine (<see cref="Bob"/>) over its hover height.
    /// </summary>
    public sealed class WraithDrift
    {
        /// <summary>Cruising speed, metres per second: a slow walk, as a ghost drifts.</summary>
        public const float CruiseSpeed = 1.5f;

        /// <summary>How fast it gets up to speed and slows to a hang, metres per second per second.</summary>
        public const float Acceleration = 1.2f;

        /// <summary>How fast its heading comes round, degrees per second: a wide, slow curve.</summary>
        public const float TurnDegreesPerSecond = 45.0f;

        public const float DriftMinSeconds = 4.0f;
        public const float DriftMaxSeconds = 9.0f;
        public const float LingerMinSeconds = 2.0f;
        public const float LingerMaxSeconds = 5.0f;

        /// <summary>How far it haunts from where it was born, metres: past this it turns for home.</summary>
        public const float HomeRadius = 45.0f;

        /// <summary>
        /// How far over the ground the body's root (the ground point of a standing body) hovers, metres: its hips at 1.2 m, its head a
        /// little over a player's, the tails of its wisps clear of the street.
        /// </summary>
        public const float HoverHeight = 0.3f;

        /// <summary>The bob: this far up and down (metres) once every BobSeconds.</summary>
        public const float BobAmplitude = 0.12f;
        public const float BobSeconds = 2.8f;

        /// <summary>How fast the height eases onto its target, per second.</summary>
        public const float HoverEase = 4.0f;

        private readonly DeterministicRandom _random;
        private readonly Vector3 _home;

        private Vector3 _heading;
        private Vector3 _goal;
        private float _speed;
        private bool _lingering;
        private float _timer;

        public WraithDrift(int seed, Vector3 home)
        {
            _random = new DeterministicRandom(seed);
            _home = home;
            _heading = RandomHeading();
            _goal = _heading;
            _timer = (float)_random.Range(DriftMinSeconds, DriftMaxSeconds);
        }

        /// <summary>The way it faces and drifts: a unit vector, level.</summary>
        public Vector3 Heading => _heading;

        /// <summary>How fast it is drifting, metres per second (0 lingering).</summary>
        public float Speed => _speed;

        /// <summary>Hanging in the air between drifts.</summary>
        public bool IsLingering => _lingering;

        /// <summary>Seconds until it next changes its mind.</summary>
        public float TimeRemaining => _timer;

        /// <summary>
        /// One step: picks a new heading or starts or ends a linger when its timer runs out, turns toward its goal and eases its speed, and
        /// returns the velocity to drift at this frame (metres per second, level).
        /// </summary>
        public Vector3 Tick(Vector3 position, float deltaTime)
        {
            Vector3 fromHome = position - _home;
            fromHome.y = 0.0f;
            if (fromHome.magnitude > HomeRadius)
            {
                // Home, whatever it was doing: drifting away for ever is the one thing it must never do.
                _goal = -fromHome.normalized;
                _lingering = false;
            }
            else
            {
                _timer -= deltaTime;
                if (_timer <= 0.0f)
                {
                    _lingering = !_lingering;
                    if (_lingering)
                    {
                        _timer = (float)_random.Range(LingerMinSeconds, LingerMaxSeconds);
                        // It turns to look somewhere new while it hangs there.
                        _goal = RandomHeading();
                    }
                    else
                    {
                        _timer = (float)_random.Range(DriftMinSeconds, DriftMaxSeconds);
                        _goal = RandomHeading();
                    }
                }
            }

            _heading = Vector3.RotateTowards(_heading, _goal, TurnDegreesPerSecond * Mathf.Deg2Rad * deltaTime, 0.0f);
            _heading.y = 0.0f;
            _heading.Normalize();
            float wanted = _lingering ? 0.0f : CruiseSpeed;
            _speed = Mathf.MoveTowards(_speed, wanted, Acceleration * deltaTime);
            return _heading * _speed;
        }

        /// <summary>How far above its hover height it bobs at <paramref name="time"/>, metres.</summary>
        public static float Bob(float time) => BobAmplitude * Mathf.Sin(time * (2.0f * Mathf.PI / BobSeconds));

        /// <summary>Where the root wants to be over ground at <paramref name="groundY"/>, at <paramref name="time"/>.</summary>
        public static float HoverTargetY(float groundY, float time) => groundY + HoverHeight + Bob(time);

        private Vector3 RandomHeading()
        {
            float radians = (float)_random.Range(0.0, 2.0 * Mathf.PI);
            return new Vector3(Mathf.Sin(radians), 0.0f, Mathf.Cos(radians));
        }
    }
}
