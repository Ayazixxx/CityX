using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How far the whole body tilts, at the feet, from how it is moving (owner, 2026-10-10: "research the best dynamic movement animations for
    /// roblox like characters ... ours can be improved upon"). From that research (physics-driven lean, Roblox DevForum tilt threads, a game
    /// developer's movement tips the owner shared): a body leans INTO its acceleration -- forward as it sets off, back as it brakes, and into the
    /// inside of a turn, since turning is sideways acceleration -- and a little forward into its speed, held while running. Physically correct
    /// angles look too extreme on a cartoon body, so each is scaled down by eye, capped, and smoothed so steps do not jitter it. On the ground
    /// only: in the air it eases back upright.
    ///
    /// THE WHOLE BODY TILTS AS ONE, about the feet, not the spine: the body is separate rigid tubes, and bending the spine pulls the torso off
    /// the legs and the arms off the torso (2026-10-10, "the butt is sticking out ... the legs aren't attached"). Pure; PlayerLocomotion applies it
    /// to the visual root, from the body's measured motion, so a copy of another player's body leans the same way.
    ///
    /// TONED RIGHT DOWN (owner, 2026-10-13: "a very apparent lean when moving around in third person that doesnt really look good ... tone it down
    /// or remove it completely but make it work with the foot ground detection"): a third of the angles and under half the cap, so a sprint tips
    /// the head a couple of degrees and a hard stop or turn about six, never more. The foot placement (PlayerLocomotion.FootIK.cs) runs after it and
    /// takes the lean back off the planted feet, so the soles stay flat on the ground while the body tips. It also hands in the SLOPE under the feet:
    /// walking uphill leans into the hill, downhill a little back (research: slope posture is one of the cheapest big wins in procedural movement).
    /// </summary>
    public sealed class BodyLean
    {
        /// <summary>Degrees of forward lean per metre per second of speed (a 7.5 m/s sprint: about 2).</summary>
        public const float SpeedLeanDegreesPerMetrePerSecond = 0.3f;

        /// <summary>Degrees of lean per metre per second squared of acceleration (setting off, braking, turning).</summary>
        public const float AccelerationLeanDegreesPerMetrePerSecondSquared = 0.5f;

        /// <summary>The most the body ever leans from its own motion, degrees.</summary>
        public const float MaxLeanDegrees = 6.0f;

        /// <summary>How much of an uphill slope's angle the body leans into it (a 20 degree hill: 7 degrees forward).</summary>
        public const float UphillLeanFraction = 0.35f;

        /// <summary>How much of a downhill slope's angle the body leans back from it.</summary>
        public const float DownhillLeanFraction = 0.2f;

        /// <summary>How long the measured acceleration is averaged over (one frame of it is noise), and how fast the lean follows, seconds.</summary>
        public const float AccelerationSmoothingSeconds = 0.12f;
        public const float LeanSmoothingSeconds = 0.1f;

        private Vector3 _lastVelocity;
        private bool _hasVelocity;
        private Vector3 _acceleration;
        private Vector3 _tilt;

        /// <summary>The lean now, as a horizontal vector: its direction is the way the top of the body tilts, its length the angle in degrees.</summary>
        public Vector3 Tilt => _tilt;

        public float Degrees => _tilt.magnitude;

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
            Vector3 flat = new Vector3(velocity.x, 0.0f, velocity.z);
            if (!_hasVelocity)
            {
                _lastVelocity = flat;
                _hasVelocity = true;
            }
            Vector3 rawAcceleration = (flat - _lastVelocity) / deltaTime;
            _lastVelocity = flat;
            _acceleration = Vector3.Lerp(_acceleration, rawAcceleration, Ease(deltaTime, AccelerationSmoothingSeconds));

            Vector3 target = Vector3.zero;
            if (leaning)
            {
                target = flat * SpeedLeanDegreesPerMetrePerSecond + _acceleration * AccelerationLeanDegreesPerMetrePerSecondSquared;
                target = Vector3.ClampMagnitude(target, MaxLeanDegrees);
                var slope = new Vector3(slopeLean.x, 0.0f, slopeLean.z);
                target = Vector3.ClampMagnitude(target + slope, MaxLeanDegrees * 2.0f);   // a steep hill may tip the body past the motion cap, never past twice it
            }
            _tilt = Vector3.Lerp(_tilt, target, Ease(deltaTime, LeanSmoothingSeconds));
        }

        /// <summary>The lean as a rotation in the world, about the feet.</summary>
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

        /// <summary>Forgets the motion (a teleport, a respawn): no lean from the jump in position.</summary>
        public void Reset()
        {
            _hasVelocity = false;
            _acceleration = Vector3.zero;
            _tilt = Vector3.zero;
        }

        private static float Ease(float deltaTime, float seconds)
        {
            return seconds <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-deltaTime / seconds);
        }
    }
}
