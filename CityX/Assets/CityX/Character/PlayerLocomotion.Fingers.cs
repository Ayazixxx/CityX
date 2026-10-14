using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE HANDS CLOSE ROUND WHAT THEY HOLD (see <see cref="HandGrip"/>). After every other pose, each hand's finger bones are folded by how much
    /// that hand is holding something: the right hand whenever an item is mounted on it (a sword, the pistol, a bottle), the left whenever it is
    /// reached onto a weapon as the support hand (the two-handed sword, the pistol when aimed). Runs on a network copy the same way, from the
    /// cosmetic model mounted there, so other players see the grip too.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How fast the fingers close and open, per second: a grab, not a snap.</summary>
        public const float GripBlendPerSecond = 8.0f;

        private Transform[] _rightDigits;
        private Transform[] _leftDigits;
        private bool _digitsLooked;
        private float _rightGrip;
        private float _leftGrip;
        private int _rightHandChildren = -1;
        private int _leftHandChildren = -1;
        private bool _rightHandHolds;
        private bool _leftHandHolds;

        /// <summary>How closed each hand is, 0 (open) to 1 (round a handle), for tests.</summary>
        public float RightGrip => _rightGrip;

        public float LeftGrip => _leftGrip;

        /// <summary>The finger bones under a hand, in HandGrip's order (null where the rig has none: an older body).</summary>
        public static Transform[] FindDigits(Transform hand, bool right)
        {
            if (hand == null)
            {
                return null;
            }
            string suffix = right ? "_r" : "_l";
            var digits = new Transform[HandGrip.BonesPerHand];
            bool any = false;
            for (int d = 0; d < FusedBodyShape.Digits.Length && d * 2 + 1 < digits.Length; d++)
            {
                string name = FusedBodyShape.Digits[d].Name;
                Transform root = hand.Find(name + "1" + suffix);
                digits[d * 2] = root;
                digits[d * 2 + 1] = root != null ? root.Find(name + "2" + suffix) : null;
                any |= root != null;
            }
            return any ? digits : null;
        }

        /// <summary>
        /// True when something other than the rig's own bones hangs off the hand: a held item is an object with components of its own, a finger
        /// bone or the fingertip leaf is a bare transform. Re-read only when the hand's children change.
        /// </summary>
        private static bool HandHoldsSomething(Transform hand, ref int lastChildren, ref bool lastHolds)
        {
            if (hand == null)
            {
                return false;
            }
            int children = hand.childCount;
            if (children == lastChildren)
            {
                return lastHolds;
            }
            lastChildren = children;
            lastHolds = false;
            for (int i = 0; i < children; i++)
            {
                if (hand.GetChild(i).GetComponents<Component>().Length > 1)
                {
                    lastHolds = true;
                    break;
                }
            }
            return lastHolds;
        }

        /// <summary>True when the right hand has an item mounted on it (used by the wrist rule too: a held thing keeps the clip's wrist).</summary>
        private bool RightHandHolds => HandHoldsSomething(_handR, ref _rightHandChildren, ref _rightHandHolds);

        private bool LeftHandHolds => HandHoldsSomething(_handL, ref _leftHandChildren, ref _leftHandHolds);

        private void ApplyFingerGrips()
        {
            if (!_digitsLooked)
            {
                _digitsLooked = true;
                _rightDigits = FindDigits(_handR, true);
                _leftDigits = FindDigits(_handL, false);
            }
            if (_rightDigits == null && _leftDigits == null)
            {
                return;
            }
            float deltaTime = Mathf.Max(_lastTickDeltaTime, Time.deltaTime);
            bool rightHolds = RightHandHolds;
            // The support hand is on the weapon once its reach has blended in (a sword's second hand, the pistol's once it is aimed).
            bool leftHolds = LeftHandHolds || (_supportAnchor != null && _supportBlend > 0.5f && (!_supportOnlyWhenAiming || EasedGunAimBlend > 0.5f));
            _rightGrip = Mathf.MoveTowards(_rightGrip, rightHolds ? 1.0f : 0.0f, GripBlendPerSecond * deltaTime);
            _leftGrip = Mathf.MoveTowards(_leftGrip, leftHolds ? 1.0f : 0.0f, GripBlendPerSecond * deltaTime);
            HandGrip.Apply(_rightDigits, true, _rightGrip);
            HandGrip.Apply(_leftDigits, false, _leftGrip);
        }
    }
}
