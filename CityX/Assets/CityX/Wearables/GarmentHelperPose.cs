using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The maths of a garment's helper bones (games' "skirt bones" and half-turning shoulder bones). A helper sits on a joint (a hip, a shoulder),
    /// bound with its parent's orientation, and turns with the parent plus a SHARE of the child bone's swing since the bind pose:
    ///  * a coat panel in front of a thigh follows the thigh nearly fully when the knee comes forward (so the knee never comes through it) and
    ///    barely when the leg goes back, and the panel behind it the other way round: a share that depends on which way the leg swings;
    ///  * round the armpit, half the arm's swing: a point between torso and arm turns about the joint instead of being pulled along the straight
    ///    line between the two (the linear blend that pinches and stretches the armpit).
    /// </summary>
    public static class GarmentHelperPose
    {
        /// <summary>How much of a thigh's swing the panel on the side it swings toward follows, and the panel on the other side.</summary>
        public const float LeadingShare = 0.95f;
        public const float TrailingShare = 0.15f;

        /// <summary>Half the arm's swing round the armpit.</summary>
        public const float ShoulderShare = 0.5f;

        /// <summary>How far round (the knee's lean along the body's forward, -1..1) a swing goes from trailing to fully leading.</summary>
        private const float LeadStart = -0.1f;
        private const float LeadFull = 0.3f;

        /// <summary>
        /// The helper's rotation. Rotations are in one frame (world at runtime); the bind ones are the parent's and the child's in the bind pose.
        /// </summary>
        public static Quaternion Follow(Quaternion parent, Quaternion parentBind, Quaternion child, Quaternion childBind, float share)
        {
            Quaternion rest = Quaternion.Inverse(parentBind) * childBind;
            Quaternion now = Quaternion.Inverse(parent) * child;
            Quaternion swing = now * Quaternion.Inverse(rest);
            return parent * Quaternion.Slerp(Quaternion.identity, swing, share);
        }

        /// <summary>
        /// The share of a thigh's swing taken by the panel in front of it (<paramref name="front"/>) or behind it, from the knee's lean along the
        /// body's forward (the unit hip-to-knee direction dotted with the body's forward: about 0 standing, positive with the knee forward).
        /// </summary>
        public static float SkirtShare(float kneeLean, bool front)
        {
            float lead = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(LeadStart, LeadFull, front ? kneeLean : -kneeLean));
            return Mathf.Lerp(TrailingShare, LeadingShare, lead);
        }

        /// <summary>How far out to the side (the knee's lean along the body's right, toward its own side) a step goes from not carrying the coat's side to fully.</summary>
        private const float OutStart = 0.15f;
        private const float OutFull = 0.5f;

        /// <summary>
        /// The share as <see cref="SkirtShare(float, bool)"/>, but a thigh stepping out to the side (<paramref name="kneeOut"/>: the unit hip-to-knee
        /// direction dotted with the body's right, toward the leg's own side) carries both panels with it, front and back: the coat's side seam lies on
        /// both, and in the wide stances of the melee swings (owner, 2026-11-21: "the knee clips through the jacket") it was left where it hung while the
        /// thigh went out through it. A panel carried fully by its thigh is as clear of it as at rest.
        /// </summary>
        public static float SkirtShare(float kneeLean, float kneeOut, bool front)
        {
            float outward = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(OutStart, OutFull, kneeOut));
            return Mathf.Max(SkirtShare(kneeLean, front), Mathf.Lerp(TrailingShare, LeadingShare, outward));
        }
    }
}
