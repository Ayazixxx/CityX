using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How far each digit of a hand folds, 0 (the modelled loose curl) to 1 (folded round a handle), plus the thumb lying FORWARD along a
    /// gun's frame instead of across the handle, and the trigger finger's pull. The named grips are what the game's weapons use
    /// (<see cref="HandGrip"/>); CityX/SourceArt/Firearms/firearm_pose.py authors the reloads with the same numbers.
    /// </summary>
    [System.Serializable]
    public struct FingerAmounts
    {
        public float Index;
        public float Middle;
        public float Ring;
        public float Little;
        public float Thumb;

        /// <summary>Degrees the thumb is turned to lie forward along the frame (0: across the handle as a wrap has it).</summary>
        public float ThumbForward;

        /// <summary>Extra fold of the index finger: the trigger pulled.</summary>
        public float Trigger;

        public FingerAmounts(float index, float middle, float ring, float little, float thumb, float thumbForward = 0.0f, float trigger = 0.0f)
        {
            Index = index;
            Middle = middle;
            Ring = ring;
            Little = little;
            Thumb = thumb;
            ThumbForward = thumbForward;
            Trigger = trigger;
        }

        /// <summary>The hand as modelled: nothing folded.</summary>
        public static FingerAmounts Open => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, 0.0f);

        /// <summary>Every digit folded round a handle (a sword, a bottle) by <paramref name="amount"/>.</summary>
        public static FingerAmounts Wrap(float amount) => new FingerAmounts(amount, amount, amount, amount, amount);

        /// <summary>
        /// The shooting hand on a pistol grip: three fingers wrapped round the front strap, the INDEX straight along the frame above the
        /// trigger (on it only while <paramref name="trigger"/> pulls), the thumb forward along the frame (the thumbs-forward grip).
        /// </summary>
        public static FingerAmounts PistolStrong(float trigger = 0.0f) => new FingerAmounts(0.12f, 1.0f, 1.0f, 1.0f, 0.25f, 55.0f, 0.55f * Mathf.Clamp01(trigger));

        /// <summary>The support hand on a pistol: wrapped over the shooting hand's fingers, the thumb forward under the slide.</summary>
        public static FingerAmounts PistolSupport => new FingerAmounts(0.95f, 0.95f, 0.95f, 0.9f, 0.2f, 50.0f);

        /// <summary>The shooting hand on a rifle's pistol grip.</summary>
        public static FingerAmounts RifleStrong(float trigger = 0.0f) => new FingerAmounts(0.12f, 1.0f, 1.0f, 1.0f, 0.35f, 40.0f, 0.55f * Mathf.Clamp01(trigger));

        /// <summary>The support hand under a rifle's handguard: fingers wrapped under it, the thumb over the top.</summary>
        public static FingerAmounts RifleSupport => new FingerAmounts(0.75f, 0.8f, 0.8f, 0.75f, 1.0f);

        public static FingerAmounts Lerp(in FingerAmounts a, in FingerAmounts b, float t)
        {
            t = Mathf.Clamp01(t);
            return new FingerAmounts(Mathf.Lerp(a.Index, b.Index, t), Mathf.Lerp(a.Middle, b.Middle, t), Mathf.Lerp(a.Ring, b.Ring, t),
                Mathf.Lerp(a.Little, b.Little, t), Mathf.Lerp(a.Thumb, b.Thumb, t), Mathf.Lerp(a.ThumbForward, b.ThumbForward, t),
                Mathf.Lerp(a.Trigger, b.Trigger, t));
        }
    }

    /// <summary>
    /// THE FINGERS CLOSE ROUND WHAT THE HAND HOLDS (owner, 2026-10-13: "can you make the fingers on the model grip the swords as well"). The
    /// hands are modelled with a loose curl (FusedBodyShape.AddHand); each finger has two bones of its own (BodyRig: root to knuckle, knuckle to
    /// tip) that the Animator never touches, so a grip is a rotation of each bone on top of the modelled curl: the fingers fold toward the palm,
    /// the thumb folds across. In the hand bone's own frame +X runs down the arm to the fingertips (the LEFT hand's runs the other way), -Y is the
    /// palm's side and +Z the thumb's, for both hands.
    ///
    /// A GUN IS HELD DIFFERENTLY (owner, 2026-10-14: "the fingers to be in place as they would a real gun"): the trigger finger lies straight
    /// along the frame until it fires, the thumb lies forward along the frame, the support hand wraps over the shooting hand's fingers
    /// (<see cref="FingerAmounts"/>). Pure maths, so the angles can be tested without a scene; PlayerLocomotion.ApplyFingerGrips decides
    /// when a hand is holding something, and what.
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

        /// <summary>The digits a hand has, in the order <see cref="Apply(Transform[], bool, float)"/> takes them: index, middle, ring, little, thumb -- two bones each, root first.</summary>
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

        /// <summary>The thumb turned from across the palm to forward along the fingers (about the hand's Y; the left hand's fingers run the other way).</summary>
        public static Quaternion ThumbForward(bool rightHand, float degrees)
        {
            return Quaternion.AngleAxis(rightHand ? degrees : -degrees, Vector3.up);
        }

        /// <summary>
        /// Poses one hand's ten finger bones closed by <paramref name="amount"/> (0: open, the modelled curl; 1: closed round a handle).
        /// <paramref name="digits"/> is index1, index2, middle1, middle2, ring1, ring2, little1, little2, thumb1, thumb2; a null entry is skipped.
        /// </summary>
        public static void Apply(Transform[] digits, bool rightHand, float amount)
        {
            Apply(digits, rightHand, FingerAmounts.Wrap(Mathf.Clamp01(amount)), 0.0f);
        }

        /// <summary>
        /// Poses one hand's finger bones to <paramref name="amounts"/>. <paramref name="keepCurrent"/> (0 to 1) keeps that much of the rotation
        /// the bones already have: a baked reload has posed the fingers itself and this must not undo it.
        /// </summary>
        public static void Apply(Transform[] digits, bool rightHand, in FingerAmounts amounts, float keepCurrent)
        {
            if (digits == null)
            {
                return;
            }
            keepCurrent = Mathf.Clamp01(keepCurrent);
            for (int i = 0; i < digits.Length && i < BonesPerHand; i++)
            {
                Transform bone = digits[i];
                if (bone == null)
                {
                    continue;
                }
                Quaternion grip = CurlFor(i, rightHand, amounts);
                bone.localRotation = keepCurrent > 0.0f ? Quaternion.Slerp(grip, bone.localRotation, keepCurrent) : grip;
            }
        }

        /// <summary>The rotation of the i-th finger bone (in <see cref="Apply(Transform[], bool, float)"/>'s order) at the given closedness.</summary>
        public static Quaternion CurlFor(int index, bool rightHand, float amount)
        {
            return CurlFor(index, rightHand, FingerAmounts.Wrap(Mathf.Clamp01(amount)));
        }

        /// <summary>The rotation of the i-th finger bone for the given amounts.</summary>
        public static Quaternion CurlFor(int index, bool rightHand, in FingerAmounts amounts)
        {
            bool thumb = index >= 8;
            bool root = index % 2 == 0;
            if (thumb)
            {
                float t = Mathf.Clamp01(amounts.Thumb);
                Quaternion curl = ThumbCurl((root ? ThumbProximalDegrees : ThumbIntermediateDegrees) * t);
                return root ? ThumbForward(rightHand, amounts.ThumbForward) * curl : curl;
            }
            float amount;
            switch (index / 2)
            {
                case 0: amount = amounts.Index + amounts.Trigger; break;
                case 1: amount = amounts.Middle; break;
                case 2: amount = amounts.Ring; break;
                default: amount = amounts.Little; break;
            }
            amount = Mathf.Clamp01(amount);
            return FingerCurl(rightHand, (root ? FingerProximalDegrees : FingerIntermediateDegrees) * amount);
        }
    }

    /// <summary>A held item that wants its hands' fingers placed its own way (a gun), rather than wrapped round a handle.</summary>
    public interface IHandGripStyle
    {
        /// <summary>The fingers of the hand the item is mounted on.</summary>
        FingerAmounts StrongHandGrip { get; }

        /// <summary>The fingers of the other hand when it is reached onto the item as the support hand.</summary>
        FingerAmounts SupportHandGrip { get; }
    }
}
