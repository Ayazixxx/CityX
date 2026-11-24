using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE HANDS ON A PLANK (docs/WOOD_PLANK.md; the owner's rule, 2026-11-20: hands that hold, no fingers through a weapon). A board is no round
    /// handle: a fist closed round it went into the wood. Each palm lies flat on a broad face, the fingers along it and over its far edge, the
    /// thumb along the other side. The folds are degrees on the modelled curl (FingerAmounts.Authored), measured on the body's skin against the
    /// board (WoodPlankGripPlayTests).
    /// </summary>
    public sealed class WoodPlankGrip : MonoBehaviour, IHandGripStyle
    {
        /// <summary>The upper (right) hand's fingers: folded at the knuckle, again at the middle, to come over the board's edge.</summary>
        public const float FingerKnuckleDegrees = 35.0f;
        public const float FingerTipDegrees = 40.0f;
        public const float ThumbRootDegrees = -30.0f;
        public const float ThumbTipDegrees = 0.0f;

        /// <summary>The lower (left) hand's.</summary>
        public const float SupportKnuckleDegrees = 55.0f;
        public const float SupportTipDegrees = 45.0f;

        public FingerAmounts StrongHandGrip => Laid(FingerKnuckleDegrees, FingerTipDegrees);

        public FingerAmounts SupportHandGrip => Laid(SupportKnuckleDegrees, SupportTipDegrees);

        private static FingerAmounts Laid(float knuckle, float tip) => new FingerAmounts(0.0f, 0.0f, 0.0f, 0.0f, 0.0f)
        {
            Authored = 1.0f,
            IndexRootDegrees = knuckle,
            IndexTipDegrees = tip,
            FingerRootDegrees = knuckle,
            FingerTipDegrees = tip,
            ThumbRootDegrees = ThumbRootDegrees,
            ThumbTipDegrees = ThumbTipDegrees,
        };
    }
}
