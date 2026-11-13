using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BODY'S SPEED HANDED TO A MOVE THAT CARRIES IT (a sword attack thrown on the move, SoulsSwordCombat). On foot the body's pace builds
    /// and falls with its weight (MovementWeight); with the keys taken away it brakes to a stop in a fifth of a second. A move that means to
    /// keep the pace takes the speed along its own way out of that braking and carries the body itself, and gives back whatever is left if
    /// it is cut short.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
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

        /// <summary>
        /// Takes the body's speed along <paramref name="direction"/> off its feet and returns it, m/s: whoever asked carries the body that
        /// way from here. What it had across that way is left to brake as it does.
        /// </summary>
        public float TakeMomentumAlong(Vector3 direction)
        {
            float along = GroundSpeedAlong(direction);
            if (along > 0.0f)
            {
                direction.y = 0.0f;
                _horizontalVelocity -= direction.normalized * along;
            }
            return Mathf.Max(0.0f, along);
        }

        /// <summary>Gives the body <paramref name="velocity"/> back on its feet (a carrying move cut short): it brakes from there as from any pace.</summary>
        public void GiveMomentum(Vector3 velocity)
        {
            velocity.y = 0.0f;
            _horizontalVelocity += velocity;
        }
    }
}
