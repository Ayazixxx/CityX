using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A WAND IN THE RIGHT HAND (docs/MAGIC_WAND.md), held as in Hogwarts Legacy: carried low at the side pointing ahead and down; to shoot or
    /// cast, the arm comes up and points it straight at what the crosshair is on (<see cref="WandAimPoint"/>), as a lit flashlight is pointed
    /// (PlayerLocomotion.HandHeldOut.cs). A spell cast through it leaves its tip, so the wand snaps out with the cast (<see cref="PointWandNow"/>)
    /// instead of easing up after the spell has gone. Another player's copy is pointed by the wand's shots (MagicSpeckCaster) and lifted by its
    /// spells' cast pose.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the wand takes to come up (and go back down), seconds.</summary>
        public const float WandRaiseSeconds = 0.08f;

        /// <summary>
        /// Pointed, the arm straight out from the shoulder at what the crosshair is on, the wand running on along the arm's line (the owner,
        /// 2026-10-31, with Hogwarts Legacy's casting: "just have him extend his arm out toward whatever he is aiming at"): the hand this share
        /// of the arm's whole length from the shoulder, a hair short of locked straight (0.96 still left the elbow bent a third, 0.99 by 16 degrees).
        /// </summary>
        public const float WandArmStraightShare = 0.995f;

        /// <summary>
        /// The wand lies across the fist on a slant, out between the thumb and the first knuckle as a pen is held: its shaft turned this far from
        /// the thumb's line toward the knuckles (MagicWand's grip). Held straight out of the fist along the thumb, a wand pointed down a straight
        /// arm needed the wrist bent square, and the hand spun about the arm at random (owner, 2026-11-01, with a photo of their own arm: "the
        /// arm is twisted all weird when casting", "you gotta rotate the hand").
        /// </summary>
        public const float WandGripTiltDegrees = 50.0f;

        /// <summary>Pointed, the straight arm this far out and down from the aim, so the wrist bends a little and the wand points on inside it.</summary>
        public const float WandArmOutDegrees = 12.0f;

        public const float WandArmDownDegrees = 8.0f;

        /// <summary>
        /// Carried, the arm hanging relaxed at the side: the hand this share of the arm's length below the shoulder, swung this far forward and
        /// out, the elbow a little back; the wand pointing on from the hand this much further forward than the forearm, so the wrist barely
        /// bends (the old carry put the hand before the hip with the elbow out and back and the wrist cocked: "like his arm is bent the wrong way").
        /// </summary>
        public const float WandCarryShare = 0.93f;

        public const float WandCarryForwardDegrees = 14.0f;

        public const float WandCarryOutDegrees = 6.0f;

        public const float WandCarryWristDegrees = 40.0f;

        /// <summary>How long one <see cref="HoldWandOut"/> keeps the wand pointed, seconds: whoever wants it out calls it every frame.</summary>
        public const float WandHoldSeconds = 0.1f;

        /// <summary>How long an aim point holds once given, seconds: past it the wand points straight ahead (a copy told of a spell, not where).</summary>
        public const float WandAimFreshSeconds = 0.3f;

        private float _wandRaise;
        private float _wandHold;
        private float _wandAimAge = float.MaxValue;
        private Vector3 _wandAimPoint;

        /// <summary>The wand in this body's right hand, or null (set by the wand), and its tip.</summary>
        public Transform Wand { get; set; }

        public Transform WandTip { get; set; }

        /// <summary>Where the wand points when out: the crosshair's point (given every frame by the reader; on a copy, by its shots).</summary>
        public Vector3 WandAimPoint
        {
            get => _wandAimPoint;
            set
            {
                _wandAimPoint = value;
                _wandAimAge = 0.0f;
            }
        }

        /// <summary>The wand held out to shoot (the body then faces the crosshair, as with a staff or a gun).</summary>
        public bool WandPointing => Wand != null && _wandHold > 0.0f;

        /// <summary>How far up the wand is, 0 carried .. 1 pointed.</summary>
        public float WandRaise => _wandRaise;

        /// <summary>Keeps the wand pointed at <paramref name="aimPoint"/> a moment longer.</summary>
        public void HoldWandOut(Vector3 aimPoint)
        {
            WandAimPoint = aimPoint;
            _wandHold = WandHoldSeconds;
        }

        /// <summary>
        /// Points the wand at <paramref name="aimPoint"/> this very frame, no easing, and says where its tip is: something is leaving it. The arm
        /// is posed at once (and again the same after the Animator), so what leaves starts at the tip the frame shows.
        /// </summary>
        public Vector3 PointWandNow(Vector3 aimPoint)
        {
            HoldWandOut(aimPoint);
            _wandRaise = 1.0f;
            if (!CanPoseWand)
            {
                return transform.position + Vector3.up * (WorldHeight * 0.68f);
            }
            // The body faces where it points (Tick does too while the wand is out): turned first, so the arm is posed from the shoulder as drawn.
            Vector3 flat = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(flat);
            }
            PoseWand(1.0f);
            return WandTip != null ? WandTip.position : Wand.position;
        }

        private bool CanPoseWand => Wand != null && _rightUpperArm != null && _rightLowerArm != null && _handR != null;

        private void ApplyWandPose()
        {
            if (!CanPoseWand)
            {
                _wandRaise = 0.0f;
                return;
            }
            bool wanted = _wandHold > 0.0f || _castPoseFraction > 0.0f;
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _wandHold = Mathf.Max(0.0f, _wandHold - deltaTime);
            _wandAimAge += deltaTime;
            _wandRaise = Mathf.MoveTowards(_wandRaise, wanted ? 1.0f : 0.0f, deltaTime / WandRaiseSeconds);
            PoseWand(_wandRaise * _wandRaise * (3.0f - 2.0f * _wandRaise));
        }

        /// <summary>The arm <paramref name="raise"/> of the way from carrying the wand to pointing it at <see cref="WandAimPoint"/>.</summary>
        private void PoseWand(float raise)
        {
            Vector3 forward = transform.forward, right = transform.right;
            Vector3 shoulder = _rightUpperArm.position;
            float arm = Vector3.Distance(shoulder, _rightLowerArm.position) + Vector3.Distance(_rightLowerArm.position, _handR.position);

            // Carried: hanging at the side, the wand on past the hand a little forward of the forearm's line.
            Vector3 hang = Quaternion.AngleAxis(-WandCarryForwardDegrees, right) * Quaternion.AngleAxis(-WandCarryOutDegrees, forward) * Vector3.down;
            Vector3 carriedHand = shoulder + hang * (arm * WandCarryShare);
            Vector3 carried = Quaternion.AngleAxis(-WandCarryWristDegrees, right) * hang;

            // Pointed: the arm straight out a little outside and under the aim, the wand from the hand at the aim.
            Vector3 pointed = forward;
            Vector3 toAim = _wandAimPoint - shoulder;
            bool aimed = _wandAimAge <= WandAimFreshSeconds && toAim.sqrMagnitude > 0.01f;
            if (aimed)
            {
                pointed = toAim.normalized;
            }
            Vector3 aside = Vector3.Cross(Vector3.up, pointed);
            aside = aside.sqrMagnitude > 1e-4f ? aside.normalized : right;
            Vector3 armLine = Quaternion.AngleAxis(WandArmOutDegrees, Vector3.up) * Quaternion.AngleAxis(WandArmDownDegrees, aside) * pointed;
            Vector3 pointedHand = shoulder + armLine * (arm * WandArmStraightShare);
            Vector3 handToAim = _wandAimPoint - pointedHand;
            Vector3 wandLine = aimed && handToAim.sqrMagnitude > 0.01f ? handToAim.normalized : pointed;

            Vector3 direction = Vector3.Slerp(carried, wandLine, raise).normalized;
            Vector3 elbowHint = Vector3.Lerp(-forward * 0.6f + right * 0.2f + Vector3.down, right * 0.3f + Vector3.down, raise);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, Vector3.Lerp(carriedHand, pointedHand, raise), elbowHint, 1.0f);
            TurnWandHand(direction, right + Vector3.down * 0.5f);
        }

        /// <summary>
        /// The right hand turned so the wand (<see cref="WandGripTiltDegrees"/> across the fist) runs along <paramref name="direction"/>, the
        /// knuckles as near the forearm's line as that leaves them, so the wrist bends only by what is left over and the back of the hand faces
        /// the forearm's side of the wand (up and out when pointed, out when carried). <paramref name="outward"/> picks that side when the
        /// forearm runs along the wand.
        /// </summary>
        private void TurnWandHand(Vector3 direction, Vector3 outward)
        {
            Vector3 across = Vector3.ProjectOnPlane(_handR.position - _rightLowerArm.position, direction);
            if (across.sqrMagnitude < 1e-6f)
            {
                across = Vector3.ProjectOnPlane(outward, direction);
            }
            across.Normalize();
            float tilt = WandGripTiltDegrees * Mathf.Deg2Rad;
            Vector3 thumb = Mathf.Cos(tilt) * direction - Mathf.Sin(tilt) * across;
            Vector3 knuckles = Mathf.Sin(tilt) * direction + Mathf.Cos(tilt) * across;
            _handR.rotation = Quaternion.LookRotation(thumb, Vector3.Cross(thumb, knuckles));
        }
    }
}
