using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How the whole body tilts from how it is moving. SECOND VERSION (2026-10-13, from the owner's forwarded movement advice: "make the lean rotate
    /// FROM the head ... a delayed 'foot center' that accelerates towards its goal, within reason, where it can't quite catch up ... when running at a
    /// constant speed the central foot point is behind the head ... you get an overshoot of the feet when you come to a fast stop ... sideways leaning
    /// when turning tight corners for free, AND the head stays where it was").
    ///
    /// The feet's centre TRAILS the head: it chases the body's velocity times <see cref="LagSeconds"/> through a damped spring that is deliberately
    /// under-damped, so a steady run holds the feet behind the head (a lean into the run), setting off overshoots (a push), a hard stop swings the feet
    /// out in front (a brace), and a tight turn leaves the feet trailing on the outside (a lean into the turn): all from one lag. The body is rotated
    /// about the HEAD (PlayerLocomotion pins it: the drawn root is turned, then shifted so the head stays put) and the foot placement runs after and
    /// takes the lean back off the planted feet, so the soles stay flat on the ground. The first version tilted about the feet from acceleration and
    /// speed, which moved the head; the owner found it "very apparent" and asked for it toned down.
    ///
    /// Pure; PlayerLocomotion feeds it the body's measured motion, so a copy of another player's body leans the same way.
    /// </summary>
    public sealed class BodyLean
    {
        /// <summary>The feet trail the head by this much of the body's velocity, metres per metre per second (a 7.5 m/s sprint: 34 cm).</summary>
        public const float LagSeconds = 0.045f;

        /// <summary>The lean turns about this height: the head's centre.</summary>
        public const float PivotHeight = BodyRig.HeadCentreY;

        /// <summary>The lag's spring: how hard the feet are pulled toward where they should be, per metre, per second squared.</summary>
        public const float Stiffness = 70.0f;

        /// <summary>Under 1: the feet overshoot (a hard stop swings them out in front for a moment).</summary>
        public const float DampingRatio = 0.55f;

        /// <summary>
        /// The lean also tips toward where the feet are STILL BEING PULLED (the spring's shortfall: a lagged acceleration), this many times over: a
        /// push off, a brake and the inside of a turn. On its own the trailing foot centre cuts the inside of a turn, which leans the body OUT of it.
        /// </summary>
        public const float AccelerationShare = 2.5f;

        /// <summary>The most the body leans from its own motion, degrees.</summary>
        public const float MaxLeanDegrees = 10.0f;

        /// <summary>How much of an uphill slope's angle the body leans into it (a 20 degree hill: 7 degrees forward).</summary>
        public const float UphillLeanFraction = 0.35f;

        /// <summary>How much of a downhill slope's angle the body leans back from it.</summary>
        public const float DownhillLeanFraction = 0.2f;

        private Vector3 _lag;
        private Vector3 _lagVelocity;
        private Vector3 _tilt;

        /// <summary>The lean now, as a horizontal vector: its direction is the way the top of the body tilts, its length the angle in degrees.</summary>
        public Vector3 Tilt => _tilt;

        public float Degrees => _tilt.magnitude;

        /// <summary>How far the feet's centre trails the head, metres, horizontal (negative along the velocity is ahead of it).</summary>
        public Vector3 FootLag => _lag;

        /// <summary>
        /// One step. <paramref name="velocity"/> is the body's velocity in the world (only its horizontal part is used); <paramref name="leaning"/>
        /// is false in the air and in any state that should stand upright, and the lean eases back to nothing. <paramref name="slopeLean"/> is an
        /// extra tilt from the ground's slope, degrees, in the direction the top of the body should go (see <see cref="UphillLeanFraction"/>).
        /// </summary>
        public void Step(Vector3 velocity, bool leaning, float deltaTime, Vector3 slopeLean = default)
        {
            if (deltaTime <= 0.0f)
            {
                return;
            }
            var flat = new Vector3(velocity.x, 0.0f, velocity.z);
            Vector3 target = leaning ? flat * LagSeconds : Vector3.zero;
            // A damped spring, stepped semi-implicitly (stable at any frame rate this game runs at).
            float damping = 2.0f * DampingRatio * Mathf.Sqrt(Stiffness);
            _lagVelocity += (Stiffness * (target - _lag) - damping * _lagVelocity) * deltaTime;
            _lag += _lagVelocity * deltaTime;
            _lag.y = 0.0f;
            if (!leaning && _lag.sqrMagnitude < 1e-6f && _lagVelocity.sqrMagnitude < 1e-6f)
            {
                _lag = Vector3.zero;
                _lagVelocity = Vector3.zero;
            }

            // Where the head is over the feet: the lag, plus where the feet are still being pulled to (into a turn, a push, a brake).
            Vector3 offset = _lag + (target - _lag) * AccelerationShare;
            float metres = offset.magnitude;
            Vector3 tilt = metres > 1e-5f ? offset / metres * (Mathf.Atan(metres / PivotHeight) * Mathf.Rad2Deg) : Vector3.zero;
            tilt = Vector3.ClampMagnitude(tilt, MaxLeanDegrees);
            if (leaning)
            {
                var slope = new Vector3(slopeLean.x, 0.0f, slopeLean.z);
                tilt = Vector3.ClampMagnitude(tilt + slope, MaxLeanDegrees * 2.0f);   // a steep hill may tip the body past the motion cap, never past twice it
            }
            _tilt = tilt;
        }

        /// <summary>The lean as a rotation in the world, about the pivot.</summary>
        public Quaternion Rotation()
        {
            float degrees = _tilt.magnitude;
            if (degrees < 1e-3f)
            {
                return Quaternion.identity;
            }
            Vector3 axis = Vector3.Cross(Vector3.up, _tilt / degrees);
            return Quaternion.AngleAxis(degrees, axis);
        }

        /// <summary>Forgets everything (a teleport, a respawn).</summary>
        public void Reset()
        {
            _lag = Vector3.zero;
            _lagVelocity = Vector3.zero;
            _tilt = Vector3.zero;
        }
    }
}
