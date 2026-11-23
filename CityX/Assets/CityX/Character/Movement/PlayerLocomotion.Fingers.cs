using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE HANDS CLOSE ROUND WHAT THEY HOLD (see <see cref="HandGrip"/>). After every other pose, each hand's finger bones are folded by how much
    /// that hand is holding something: the right hand whenever an item is mounted on it (a sword, the pistol, a bottle), the left whenever it is
    /// reached onto a weapon as the support hand (the two-handed sword, the pistol when aimed). Runs on a network copy the same way, from the
    /// cosmetic model mounted there, so other players see the grip too.
    ///
    /// WHAT SHAPE THE GRIP TAKES is the item's say (<see cref="IHandGripStyle"/>): a gun keeps the trigger finger straight along its frame and
    /// the thumb forward, and pulls the trigger finger when it fires; anything else is wrapped. A baked reload poses the fingers itself
    /// (BakedMotion.HasFingers) and the grip keeps off them by its weight.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How fast the fingers close and open, per second: a grab, not a snap.</summary>
        public const float GripBlendPerSecond = 8.0f;

        private Transform[] _rightDigits;
        private Transform[] _leftDigits;
        private bool _digitsLooked;

        /// <summary>A move's own shape for the left hand's fingers this frame, and how much of it (<see cref="SetLeftFingers"/>).</summary>
        private FingerAmounts _leftFingersByMove;

        private float _leftFingersByMoveWeight;

        /// <summary>
        /// The left hand's fingers shaped by a move for this frame (the drink's finger hooked under a can's tab: CanDrinker), <paramref name="weight"/> 0 to 1 of
        /// the way from what they would be. Asked for every frame it is wanted (in Update): it lasts the one frame.
        /// </summary>
        public void SetLeftFingers(in FingerAmounts amounts, float weight)
        {
            _leftFingersByMove = amounts;
            _leftFingersByMoveWeight = Mathf.Clamp01(weight);
        }
        private float _rightGrip;
        private float _leftGrip;
        private int _rightHandChildren = -1;
        private int _leftHandChildren = -1;
        private bool _rightHandHolds;
        private bool _leftHandHolds;
        private IHandGripStyle _gripStyle;
        private int _gripStyleChildren = -1;

        /// <summary>The right hand's fingers are in a slingshot's pinch, closing on the pouch or opening off it.</summary>
        private bool _rightPinches;

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

        /// <summary>The held item's own grip shape, if the item mounted on the right hand has one (re-read when the hand's children change).</summary>
        private IHandGripStyle GripStyle
        {
            get
            {
                if (_handR == null)
                {
                    return null;
                }
                int children = _handR.childCount;
                if (children != _gripStyleChildren)
                {
                    _gripStyleChildren = children;
                    _gripStyle = null;
                    for (int i = 0; i < children; i++)
                    {
                        IHandGripStyle style = _handR.GetChild(i).GetComponent<IHandGripStyle>();
                        if (style != null)
                        {
                            _gripStyle = style;
                            break;
                        }
                    }
                }
                return _gripStyle;
            }
        }

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
            // In an unarmed fight both hands are fists: an open hand thrown at somebody is a slap.
            bool fists = FistPoseActive;
            // The hand a spell is cast from is open, its palm to the target (PlayerLocomotion.Casting.cs), in a fist fight too.
            bool castOpen = _castRaise > 0.3f && CastsFromHand;
            bool castLeft = castOpen && CastsWithLeftHand;
            bool castRight = castOpen && !castLeft;
            // A slingshot's pouch is pinched (PlayerLocomotion.Slingshot.cs); the hand keeps the pinch's shape as it opens off the loose.
            bool pinching = SlingshotPinching;
            bool rightHolds = RightHandHolds || LadderHandsGrip || (fists && !castRight) || pinching;
            _rightPinches = pinching || (_rightPinches && _rightGrip > 0.0f && !(RightHandHolds || LadderHandsGrip || fists));
            // The support hand is on the weapon once its reach has blended in (a sword's second hand, the pistol's once it is aimed).
            bool leftHolds = LeftHandHolds || LadderHandsGrip || (fists && !castLeft)
                || (!castLeft && _supportAnchor != null && _supportBlend > 0.5f && (!_supportOnlyWhenAiming || EasedGunAimBlend > 0.5f));
            _rightGrip = Mathf.MoveTowards(_rightGrip, rightHolds ? 1.0f : 0.0f, GripBlendPerSecond * deltaTime);
            _leftGrip = Mathf.MoveTowards(_leftGrip, leftHolds ? 1.0f : 0.0f, GripBlendPerSecond * deltaTime);
            IHandGripStyle style = GripStyle;
            FingerAmounts right = _rightPinches ? FingerAmounts.Lerp(FingerAmounts.Open, SlingshotPinch, _rightGrip)
                : style != null ? FingerAmounts.Lerp(FingerAmounts.Open, style.StrongHandGrip, _rightGrip) : FingerAmounts.Wrap(_rightGrip);
            // The left hand's own pistol of a pair is gripped as the right hand grips its own (docs/DUAL_PISTOLS.md).
            IHandGripStyle leftGun = _leftPistol != null ? _leftPistol.GetComponent<IHandGripStyle>() : null;
            FingerAmounts left = _leftPistolGrip != null ? FingerAmounts.Lerp(FingerAmounts.Open, _leftPistolGrip.StrongHandGrip, _leftGrip)
                : style != null && !LeftHandHolds ? FingerAmounts.Lerp(FingerAmounts.Open, style.SupportHandGrip, _leftGrip) : FingerAmounts.Wrap(_leftGrip);
            if (_leftFingersByMoveWeight > 0.0f)
            {
                left = FingerAmounts.Lerp(left, _leftFingersByMove, _leftFingersByMoveWeight);
                _leftFingersByMoveWeight = 0.0f;
            }
            HandGrip.Apply(_rightDigits, true, right, _bakedFingerWeight);
            HandGrip.Apply(_leftDigits, false, left, _bakedFingerWeight);
        }
    }
}
