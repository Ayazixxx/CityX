using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BODY'S OWN PACE, for a move that must fit round it (a sword attack: SoulsSwordCombat, SwordPoseDriver). What the keys (or a
    /// brain) have the body going at over the ground, as MovementWeight builds and brakes it: not what something else moves it by on top
    /// (an attack's own step, a blow's shove), and not its speed through the air.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>
        /// How fast the body is going over the ground by its own movement, m/s: the pace its walk is animated at (the Animator's own, which
        /// another player's copy is sent, so a copy knows it too).
        /// </summary>
        public float OwnGroundSpeed => _animator != null ? _animator.GetFloat(SpeedParameter) : _horizontalVelocity.magnitude;

        /// <summary>How fast the body is going along <paramref name="direction"/> on its own feet, m/s (0 in the air: a jump's pace is not a walk's).</summary>
        public float GroundSpeedAlong(Vector3 direction)
        {
            direction.y = 0.0f;
            if (_controller == null || !_controller.isGrounded || direction.sqrMagnitude < 1e-6f)
            {
                return 0.0f;
            }
            return Vector3.Dot(_horizontalVelocity, direction.normalized);
        }
    }
}
