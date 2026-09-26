using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The standard analytic two-bone reach (shoulder-elbow-wrist): put the wrist on a target by
    /// bending the elbow, choosing WHICH WAY the elbow points from a hint.
    ///
    /// Why this exists: Mixamo's two-handed weapon clips were authored for Mixamo's own skeleton,
    /// where the chest twists and the collar bones swing forward to bring both hands onto the one
    /// weapon. This rig has no Chest, UpperChest or Shoulder bone (measured, not assumed), so
    /// Humanoid retargeting silently drops all of that and the hands land wherever the arm angles
    /// alone put them -- 40-125cm apart on a grip 20cm long. Every practical guide to mocap for
    /// two-handed weapons ends in the same place: keep the animation's placement for the hand that
    /// owns the weapon and IK the other hand onto it. This is that IK.
    ///
    /// Pure geometry on Transforms -- no Animator, no state -- so it can run at the end of any
    /// LateUpdate and be tested without a scene.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <summary>Never stretch the arm to the last millimetre: a fully straight limb has no elbow direction to keep.</summary>
        public const float MaxReachFraction = 0.995f;

        /// <summary>
        /// Rotates <paramref name="upper"/> and <paramref name="lower"/> so <paramref name="end"/> lands
        /// on <paramref name="target"/> (or as close as the limb reaches), and returns how far short of
        /// the target the wrist stayed, in metres (0 when it got there).
        /// </summary>
        /// <param name="elbowHint">A DIRECTION the elbow should bow toward (need not be perpendicular to the limb).</param>
        /// <param name="weight">0 leaves the pose untouched, 1 is the full solution; in between blends the two rotations.</param>
        public static float Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 elbowHint, float weight = 1.0f)
        {
            if (upper == null || lower == null || end == null || weight <= 0.0f)
            {
                return 0.0f;
            }

            Vector3 shoulder = upper.position;
            Vector3 elbow = lower.position;
            Vector3 wrist = end.position;
            float upperLength = Vector3.Distance(shoulder, elbow);
            float lowerLength = Vector3.Distance(elbow, wrist);
            Vector3 toTarget = target - shoulder;
            float distance = toTarget.magnitude;
            if (upperLength < 1e-4f || lowerLength < 1e-4f || distance < 1e-4f)
            {
                return 0.0f;
            }

            float maxReach = (upperLength + lowerLength) * MaxReachFraction;
            float minReach = Mathf.Abs(upperLength - lowerLength) + 0.01f;
            float reach = Mathf.Clamp(distance, minReach, maxReach);
            Vector3 axis = toTarget / distance;

            // Law of cosines: how far along the shoulder->target line the elbow sits, and how far off it.
            float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2.0f * reach);
            float off = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0.0f));

            Vector3 bow = elbowHint - axis * Vector3.Dot(elbowHint, axis);
            if (bow.sqrMagnitude < 1e-6f)
            {
                // The hint lies along the limb: keep the elbow where the animation already had it.
                Vector3 current = elbow - shoulder;
                bow = current - axis * Vector3.Dot(current, axis);
                if (bow.sqrMagnitude < 1e-6f)
                {
                    bow = Vector3.Cross(axis, Vector3.up);
                }
            }
            bow.Normalize();

            Vector3 wantedElbow = shoulder + axis * along + bow * off;
            Vector3 wantedWrist = shoulder + axis * reach;

            Quaternion upperStart = upper.rotation;
            Quaternion lowerStart = lower.rotation;

            upper.rotation = Quaternion.FromToRotation(elbow - shoulder, wantedElbow - shoulder) * upperStart;
            // The elbow now sits exactly on wantedElbow, and turning the upper arm turned the forearm with it
            // (a child): aim the forearm from where it is NOW, not from where it started.
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, wantedWrist - lower.position) * lower.rotation;

            if (weight < 1.0f)
            {
                Quaternion upperFull = upper.rotation;
                Quaternion lowerFull = lower.rotation;
                upper.rotation = Quaternion.Slerp(upperStart, upperFull, weight);
                lower.rotation = Quaternion.Slerp(lowerStart, lowerFull, weight);
            }

            return Vector3.Distance(end.position, target);
        }
    }
}
