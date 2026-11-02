using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A BODY WITH WEIGHT (docs/MOVEMENT.md; owner, 2026-11-02: "do movement weight"): on foot, out of an aim or a lock-on, speed builds and falls over a
    /// few steps instead of an eighth of a second. Only speed along the way the body goes is slow to change: sideways drift is gripped away hard,
    /// so a turn holds its line rather than sliding as on ice. Pure, so the feel's numbers are tested without a scene.
    /// </summary>
    public static class MovementWeight
    {
        /// <summary>Speeding up, m/s per second: a walk in 0.3 s, a sprint from a stand in about half a second.</summary>
        public const float Acceleration = 14.0f;

        /// <summary>Slowing down: a sprint stops in under 0.4 s, about 1.4 m on; a walk in 0.2 s.</summary>
        public const float Braking = 20.0f;

        /// <summary>How hard the feet take sideways drift off the body.</summary>
        public const float Grip = 40.0f;

        /// <summary>
        /// The velocity a step later, going for <paramref name="target"/>. <paramref name="scale"/> scales all three (a giant's speeds are bigger, in the
        /// same time; wet ground takes traction away).
        /// </summary>
        public static Vector3 Step(Vector3 velocity, Vector3 target, float scale, float deltaTime)
        {
            float wanted = target.magnitude;
            if (wanted < 1e-4f)
            {
                return Vector3.MoveTowards(velocity, Vector3.zero, Braking * scale * deltaTime);
            }
            Vector3 way = target / wanted;
            float along = Vector3.Dot(velocity, way);
            Vector3 drift = velocity - way * along;
            float rate = along >= 0.0f && along < wanted ? Acceleration : Braking;
            along = Mathf.MoveTowards(along, wanted, rate * scale * deltaTime);
            return way * along + Vector3.MoveTowards(drift, Vector3.zero, Grip * scale * deltaTime);
        }
    }
}
