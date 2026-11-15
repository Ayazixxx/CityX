using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE A SWORD ARM'S ELBOW GOES SO THE WRIST STAYS A WRIST (owner, 2026-11-14, of the one-handed second cut: "the way he holds it is
    /// weird why is his wrist twisted like that. find out a system that makes it so we can generate animation with proper grip and wrist
    /// positioning"; docs/SWORD_GRIP_SYSTEM.md).
    ///
    /// A swing's pose says where the hand goes and how it holds the sword; the arm still has one free choice, how far round the
    /// shoulder-to-wrist line the elbow swings. Swordsmen and animators agree on how it is made: the shoulder and arm carry the arc, the
    /// forearm runs into the hand at the grip's own easy angle, and the wrist stays near straight. So the elbow is tried all the way round
    /// that circle and put where the forearm comes into the hand most nearly as <paramref name="easyForearm"/> says, with an elbow hanging
    /// easily and the elbow where it was last frame as the tie-breaks (the Octaslash's Blender solver, octaslash_pose.py, does the same).
    /// Before this the elbow went wherever a fixed rule bowed it and the wrist took up whatever was left: 154 degrees at the worst.
    /// Pure maths, tested without a scene.
    /// </summary>
    public static class SwordElbow
    {
        /// <summary>How many places round the circle are tried.</summary>
        public const int Tries = 48;

        /// <summary>A degree of wrist bend against this much for turning the elbow right round from where it hangs easily (half for a quarter turn).</summary>
        public const float EaseWeight = 40.0f;

        /// <summary>A degree of wrist bend against this many degrees of swing from last frame's elbow: it never flips round from one frame to the next.</summary>
        public const float KeepWeight = 0.3f;

        /// <summary>
        /// The way the elbow bows from the line between <paramref name="shoulder"/> and <paramref name="wrist"/> (a unit vector across that line,
        /// what <see cref="TwoBoneIK.Solve"/> takes as its hint), and in <paramref name="wristBend"/> how far the forearm then comes in off
        /// <paramref name="easyForearm"/>, degrees. <paramref name="easyBow"/>: the way it hangs easily; <paramref name="lastBow"/>: last frame's
        /// (zero for none).
        /// </summary>
        public static Vector3 Bow(Vector3 shoulder, Vector3 wrist, float upperLength, float lowerLength, Vector3 easyForearm, Vector3 easyBow,
            Vector3 lastBow, out float wristBend)
        {
            Vector3 toWrist = wrist - shoulder;
            float distance = toWrist.magnitude;
            wristBend = 0.0f;
            if (distance < 1e-4f || upperLength < 1e-4f || lowerLength < 1e-4f)
            {
                return easyBow;
            }
            Vector3 axis = toWrist / distance;
            float reach = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.01f, (upperLength + lowerLength) * TwoBoneIK.MaxReachFraction);
            float along = (upperLength * upperLength - lowerLength * lowerLength + reach * reach) / (2.0f * reach);
            float radius = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0.0f));
            Vector3 centre = shoulder + axis * along;
            Vector3 hand = shoulder + axis * reach;

            Vector3 across = Across(axis, Vector3.up);
            Vector3 aroundToo = Vector3.Cross(axis, across);
            Vector3 easy = Across(axis, easyBow);
            bool keep = lastBow.sqrMagnitude > 1e-6f;
            Vector3 last = keep ? Across(axis, lastBow) : Vector3.zero;
            Vector3 ideal = easyForearm.normalized;

            float bestCost = float.MaxValue;
            Vector3 best = easy;
            for (int i = 0; i < Tries; i++)
            {
                float angle = 2.0f * Mathf.PI * i / Tries;
                Vector3 bow = across * Mathf.Cos(angle) + aroundToo * Mathf.Sin(angle);
                Vector3 forearm = (hand - (centre + bow * radius)).normalized;
                float bend = Vector3.Angle(forearm, ideal);
                float cost = bend + EaseWeight * (1.0f - Vector3.Dot(bow, easy)) + (keep ? KeepWeight * Vector3.Angle(bow, last) : 0.0f);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = bow;
                    wristBend = bend;
                }
            }
            return best;
        }

        /// <summary><paramref name="v"/> made square to <paramref name="axis"/> and unit; another across it when it lies along it.</summary>
        private static Vector3 Across(Vector3 axis, Vector3 v)
        {
            Vector3 square = v - axis * Vector3.Dot(v, axis);
            if (square.sqrMagnitude < 1e-6f)
            {
                square = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
            }
            return square.normalized;
        }
    }
}
