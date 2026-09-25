using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The pure part of the grab/carry system: the point in front of the
    /// eye a carried body is pulled toward, and the bounded PD spring that
    /// pulls it there. Ported from the maths inside player.gd's
    /// _apply_carry_force/_start_carry.
    /// </summary>
    public static class GrabCarryMath
    {
        /// <summary>The point a carried body is pulled toward -- straight out from the eye.</summary>
        public static Vector3 CarryTarget(Vector3 eyePosition, Vector3 eyeForward, float distance)
        {
            return eyePosition + eyeForward * distance;
        }

        /// <summary>How far in front of the eye a grab starts, clamped to a comfortable arm's-length range.</summary>
        public static float ClampCarryDistance(float rawDistance, float min, float max)
        {
            return Mathf.Clamp(rawDistance, min, max);
        }

        /// <summary>
        /// A real force via F = kx - cv, not a direct position/velocity
        /// override, so heavier bodies (a torso) resist and lighter ones (a
        /// hand, a dropped prop) snap into place off the SAME already-tuned
        /// per-body masses -- no per-object tuning needed. Clamped so a
        /// target wedged behind geometry cannot blow up the solver.
        /// </summary>
        public static Vector3 SpringForce(Vector3 target, Vector3 currentPosition, Vector3 currentVelocity,
            float springK, float dampingK, float maxForce)
        {
            Vector3 error = target - currentPosition;
            Vector3 force = error * springK - currentVelocity * dampingK;
            return Vector3.ClampMagnitude(force, maxForce);
        }

        /// <summary>True once the carried body has drifted too far from its own target to be worth chasing -- auto-drop rather than let the force keep growing.</summary>
        public static bool ShouldAutoDrop(Vector3 target, Vector3 currentPosition, float maxDistance)
        {
            return Vector3.Distance(target, currentPosition) > maxDistance;
        }
    }
}
