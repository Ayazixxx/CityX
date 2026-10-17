using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FOREARM TURNS, NOT THE WRIST. When a hand is posed to point a blade (the whole hand rotation is set from the blade's direction and
    /// edge), the roll about the forearm's axis that the pose needs has to come from somewhere: a real wrist cannot twist, the forearm pronates
    /// and supinates instead. Left on the wrist bone, a 90-degree roll reads as a broken joint (the hand screwed round on the end of the arm).
    /// This moves the twist component of the wrist's rotation into the forearm bone, up to a limit, and leaves the hand's world rotation exactly
    /// where it was. Pure maths on Transforms, like TwoBoneIK, so it can be tested without a scene.
    /// </summary>
    public static class WristTwist
    {
        /// <summary>How far a forearm turns about its own length before the rest has to stay in the wrist, degrees each way.</summary>
        public const float MaxForearmDegrees = 100.0f;

        /// <summary>
        /// Takes the twist about the forearm's axis out of the wrist (<paramref name="hand"/> relative to <paramref name="lower"/>) and gives it to the
        /// forearm, up to <paramref name="maxDegrees"/>. The hand keeps its world rotation. Returns the degrees of twist left in the wrist.
        /// </summary>
        public static float ShareWithForearm(Transform lower, Transform hand, float maxDegrees = MaxForearmDegrees)
        {
            if (lower == null || hand == null)
            {
                return 0.0f;
            }
            Vector3 axisWorld = hand.position - lower.position;
            if (axisWorld.sqrMagnitude < 1e-8f)
            {
                return 0.0f;
            }
            axisWorld.Normalize();
            Quaternion handWorld = hand.rotation;
            Quaternion lowerWorld = lower.rotation;
            Quaternion relative = Quaternion.Inverse(lowerWorld) * handWorld;
            Vector3 axisLocal = Quaternion.Inverse(lowerWorld) * axisWorld;

            float twist = TwistDegrees(relative, axisLocal);
            float take = Mathf.Clamp(twist, -maxDegrees, maxDegrees);
            if (Mathf.Abs(take) > 0.01f)
            {
                lower.rotation = lowerWorld * Quaternion.AngleAxis(take, axisLocal);
                hand.rotation = handWorld;
            }
            return twist - take;
        }

        /// <summary>
        /// The same, CONTINUOUS from frame to frame: <paramref name="last"/> carries the twist the wrist had last frame (start it at 0), and this
        /// frame's is taken as whichever of its whole-turn equivalents is nearest to it. A hand turning through the half turn (a backhand rolling
        /// the edge over) would otherwise read +179 then -179 degrees and the forearm, clamped either way, would flip 200 degrees in one frame.
        /// </summary>
        public static float ShareWithForearm(Transform lower, Transform hand, ref float last, float maxDegrees = MaxForearmDegrees)
        {
            if (lower == null || hand == null)
            {
                return 0.0f;
            }
            Vector3 axisWorld = hand.position - lower.position;
            if (axisWorld.sqrMagnitude < 1e-8f)
            {
                return 0.0f;
            }
            axisWorld.Normalize();
            Quaternion handWorld = hand.rotation;
            Quaternion lowerWorld = lower.rotation;
            Quaternion relative = Quaternion.Inverse(lowerWorld) * handWorld;
            Vector3 axisLocal = Quaternion.Inverse(lowerWorld) * axisWorld;

            float twist = Unwrapped(TwistDegrees(relative, axisLocal), last);
            last = twist;
            float take = Mathf.Clamp(twist, -maxDegrees, maxDegrees);
            if (Mathf.Abs(take) > 0.01f)
            {
                lower.rotation = lowerWorld * Quaternion.AngleAxis(take, axisLocal);
                hand.rotation = handWorld;
            }
            return twist - take;
        }

        /// <summary>
        /// <paramref name="degrees"/> moved by whole turns to lie nearest <paramref name="last"/>, but never more than three quarters of a turn
        /// either way (so a twist cannot wind up over many frames into turns it does not have).
        /// </summary>
        public static float Unwrapped(float degrees, float last)
        {
            degrees += 360.0f * Mathf.Round((last - degrees) / 360.0f);
            if (degrees > 270.0f) degrees -= 360.0f;
            if (degrees < -270.0f) degrees += 360.0f;
            return degrees;
        }

        /// <summary>The signed angle, in degrees, of a rotation's twist about <paramref name="axis"/> (unit): the swing-twist decomposition's twist.</summary>
        public static float TwistDegrees(Quaternion q, Vector3 axis)
        {
            float d = q.x * axis.x + q.y * axis.y + q.z * axis.z;
            var twist = new Quaternion(axis.x * d, axis.y * d, axis.z * d, q.w);
            float length = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (length < 1e-6f)
            {
                return 180.0f;   // a pure 180-degree swing has no twist to speak of; treat it as a half turn
            }
            twist.x /= length; twist.y /= length; twist.z /= length; twist.w /= length;
            float angle = 2.0f * Mathf.Atan2(d / length, twist.w) * Mathf.Rad2Deg;
            if (angle > 180.0f) angle -= 360.0f;
            if (angle < -180.0f) angle += 360.0f;
            return angle;
        }
    }
}
