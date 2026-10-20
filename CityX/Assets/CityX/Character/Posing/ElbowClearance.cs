using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// KEEPS AN ARM OUT OF THE TORSO (owner, 2026-10-19: with the baseball bat, "the elbows clipping through the body"; all the two-handed weapons
    /// did it). An elbow can turn round the line from the shoulder to the hand without moving either: this finds the turn with the elbow lowest
    /// of those that keep the elbow, the middle of the upper arm and the middle of the forearm out of the torso (a capsule round the spine), or
    /// the clearest turn when none does. The arm is then solved to it with the hand where it was.
    /// </summary>
    public static class ElbowClearance
    {
        /// <summary>How many turns round the circle are tried (every 7.5 degrees).</summary>
        private const int Steps = 48;

        /// <summary>
        /// Where the elbow goes: <paramref name="elbow"/> itself when the arm is already clear of the capsule from <paramref name="torsoBottom"/> to
        /// <paramref name="torsoTop"/> of <paramref name="radius"/> (the torso's own radius and the arm's), otherwise the lowest clear point on its
        /// circle round the shoulder-to-hand line, or the clearest one.
        /// </summary>
        public static Vector3 Elbow(Vector3 shoulder, Vector3 elbow, Vector3 hand, Vector3 torsoBottom, Vector3 torsoTop, float radius)
        {
            if (Clearance(shoulder, elbow, hand, torsoBottom, torsoTop, radius) >= 0.0f)
            {
                return elbow;
            }
            Vector3 limb = hand - shoulder;
            if (limb.sqrMagnitude < 1e-8f)
            {
                return elbow;
            }
            Vector3 axis = limb.normalized;
            Vector3 centre = shoulder + axis * Vector3.Dot(elbow - shoulder, axis);
            Vector3 out_ = elbow - centre;
            float bow = out_.magnitude;
            if (bow < 1e-4f)
            {
                return elbow;   // a straight arm: nowhere to turn the elbow
            }
            Vector3 u = out_ / bow;
            Vector3 v = Vector3.Cross(axis, u);
            // Of the clear turns, the one with the elbow LOWEST (a weapon is held elbows down; the nearest clear turn often swung the elbow up
            // level with the shoulder, a chicken wing); with none clear, the clearest.
            Vector3 up = (torsoTop - torsoBottom).normalized;
            Vector3 best = elbow;
            float bestClear = float.MinValue;
            float bestHeight = float.MaxValue;
            for (int i = 1; i < Steps; i++)
            {
                float radians = i * (360.0f / Steps) * Mathf.Deg2Rad;
                Vector3 candidate = centre + (u * Mathf.Cos(radians) + v * Mathf.Sin(radians)) * bow;
                float clear = Clearance(shoulder, candidate, hand, torsoBottom, torsoTop, radius);
                float height = Vector3.Dot(candidate, up);
                bool clearNow = clear >= 0.0f;
                bool clearBefore = bestClear >= 0.0f;
                if (clearNow && (!clearBefore || height < bestHeight) || !clearNow && !clearBefore && clear > bestClear)
                {
                    best = candidate;
                    bestClear = clear;
                    bestHeight = height;
                }
            }
            return best;
        }

        /// <summary>How far the arm's elbow and the middles of its two bones stand out of the capsule, at the least; negative is inside.</summary>
        public static float Clearance(Vector3 shoulder, Vector3 elbow, Vector3 hand, Vector3 torsoBottom, Vector3 torsoTop, float radius)
        {
            float elbowOut = Outside(elbow, torsoBottom, torsoTop, radius);
            float upperOut = Outside((shoulder + elbow) * 0.5f, torsoBottom, torsoTop, radius);
            float forearmOut = Outside((elbow + hand) * 0.5f, torsoBottom, torsoTop, radius);
            return Mathf.Min(elbowOut, Mathf.Min(upperOut, forearmOut));
        }

        private static float Outside(Vector3 point, Vector3 bottom, Vector3 top, float radius)
        {
            Vector3 along = top - bottom;
            float t = along.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - bottom, along) / along.sqrMagnitude) : 0.0f;
            return Vector3.Distance(point, bottom + along * t) - radius;
        }
    }
}
