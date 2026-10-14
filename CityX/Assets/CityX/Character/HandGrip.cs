using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE FINGERS CLOSE ROUND WHAT THE HAND HOLDS (owner, 2026-10-13: "can you make the fingers on the model grip the swords as well"). The
    /// hands are modelled with a loose curl (FusedBodyShape.AddHand); each finger has two bones of its own (BodyRig: root to knuckle, knuckle to
    /// tip) that the Animator never touches, so a grip is a rotation of each bone on top of the modelled curl: the fingers fold toward the palm,
    /// the thumb folds across. In the hand bone's own frame +X runs down the arm to the fingertips (the LEFT hand's runs the other way), -Y is the
    /// palm's side and +Z the thumb's, for both hands.
    ///
    /// Pure maths, so the angles can be tested without a scene; PlayerLocomotion.ApplyFingerGrips decides when a hand is holding something.
    /// </summary>
    public static class HandGrip
    {
        /// <summary>How far a finger's root bone folds toward the palm when the hand closes round a handle, degrees (on top of the modelled curl).</summary>
        public const float FingerProximalDegrees = 55.0f;

        /// <summary>How far the finger's tip bone folds on, degrees.</summary>
        public const float FingerIntermediateDegrees = 50.0f;

        /// <summary>How far the thumb folds across the handle, degrees, root then tip.</summary>
        public const float ThumbProximalDegrees = 35.0f;

        public const float ThumbIntermediateDegrees = 25.0f;

        /// <summary>The digits a hand has, in the order <see cref="Apply"/> takes them: index, middle, ring, little, thumb -- two bones each, root first.</summary>
        public const int BonesPerHand = 10;

        /// <summary>A finger bone's fold toward the palm: about the hand's Z, the sign depending on which way the fingers run (+X on the right hand, -X on the left).</summary>
        public static Quaternion FingerCurl(bool rightHand, float degrees)
        {
            return Quaternion.AngleAxis(rightHand ? -degrees : degrees, Vector3.forward);
        }

        /// <summary>The thumb's fold across the palm: about the hand's X, toward -Y, the same for both hands (both thumbs point +Z).</summary>
        public static Quaternion ThumbCurl(float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.right);
        }

        /// <summary>
        /// Poses one hand's ten finger bones closed by <paramref name="amount"/> (0: open, the modelled curl; 1: closed round a handle).
        /// <paramref name="digits"/> is index1, index2, middle1, middle2, ring1, ring2, little1, little2, thumb1, thumb2; a null entry is skipped.
        /// </summary>
        public static void Apply(Transform[] digits, bool rightHand, float amount)
        {
            if (digits == null)
            {
                return;
            }
            amount = Mathf.Clamp01(amount);
            for (int i = 0; i < digits.Length && i < BonesPerHand; i++)
            {
                Transform bone = digits[i];
                if (bone == null)
                {
                    continue;
                }
                bone.localRotation = CurlFor(i, rightHand, amount);
            }
        }

        /// <summary>The rotation of the i-th finger bone (in <see cref="Apply"/>'s order) at the given closedness.</summary>
        public static Quaternion CurlFor(int index, bool rightHand, float amount)
        {
            bool thumb = index >= 8;
            bool root = index % 2 == 0;
            float degrees = thumb
                ? (root ? ThumbProximalDegrees : ThumbIntermediateDegrees)
                : (root ? FingerProximalDegrees : FingerIntermediateDegrees);
            degrees *= Mathf.Clamp01(amount);
            return thumb ? ThumbCurl(degrees) : FingerCurl(rightHand, degrees);
        }
    }
}
