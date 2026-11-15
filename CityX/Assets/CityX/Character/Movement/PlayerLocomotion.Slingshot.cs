using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A SLINGSHOT HELD AND DRAWN AS BULLY'S JIMMY DOES (docs/SLINGSHOT.md; the owner, 2026-11-01, with two Bully screenshots: "they actually put
    /// the slingshot in the left hand but angled it way better", "hold it further away from his head unbend his arm"). Aimed, the body turns
    /// side-on, the left shoulder toward the target and the head turned back to look along the aim; the left arm is straight, the fork upright in
    /// the fist and leaning in toward the drawing hand; the right hand draws the pouch back to an anchor beside the right jaw, outside the head,
    /// the elbow out and back at shoulder height. The fork is placed on the line from that anchor along the aim, as a real shooter lines it up
    /// with the eye, which also keeps it clear of the head from the camera over the right shoulder. Posed after the Animator by two-bone IK, the
    /// wrists kept straight (PlayerLocomotion.Bow.cs calls it for a slingshot's draw style).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How far the torso turns side-on while aimed (the left shoulder leading), degrees; the head turns back by the same.</summary>
        public const float SlingshotSideOnDegrees = 45.0f;

        /// <summary>How far the fork leans in from upright toward the drawing hand, about the aim, degrees (Bully's tilted fork).</summary>
        public const float SlingshotCantDegrees = 30.0f;

        /// <summary>
        /// The anchor the pouch is drawn to, from the head's middle in head radii: this far to the right, up (down when negative) and forward.
        /// Out beside the jaw, clear of the face (owner, 2026-11-15: "make sure his hand or the string isnt clipping through his face, make it
        /// held away from his face enough so theres no clipping and it is visible and looks good from the players perspective"): at the
        /// cheekbone (0.85 right, 0.75 forward) the pinching fingers, which curl in toward the face, went into it, and from the camera over the
        /// right shoulder the hand and the bands lay over the head. Out here the camera sees them past the head's edge.
        /// </summary>
        public static readonly Vector3 SlingshotAnchor = new Vector3(1.55f, -0.2f, 0.6f);

        /// <summary>
        /// THE PINCH (owner, 2026-11-15: "make him pinch the pouch part with his index middle and thumb fingers"): the index and middle fingers
        /// a little bent and reaching on toward the fork, the thumb folded across to meet their pads, the pouch's pebble between, and the ring
        /// and little fingers folded away into the palm. Found by search against the hand's own digits (FusedBodyShape.Digits): the thumb's tip
        /// a finger's width, its own and the pebble's from each of theirs. Pressed tip to tip (the index and middle bent two thirds to meet the
        /// thumb) the hand read as a fist with the thumb on top.
        /// </summary>
        public static FingerAmounts SlingshotPinch => new FingerAmounts(0.4f, 0.35f, 1.0f, 1.0f, 0.7f, 65.0f);

        /// <summary>Where the pinch holds the pouch, in the right hand bone's frame at the hand's modelled size: amid the thumb's, the index's and the middle finger's tips.</summary>
        public static Vector3 SlingshotPinchInHand
        {
            get
            {
                FingerAmounts pinch = SlingshotPinch;
                return (HandGrip.TipInHand(0, pinch) + HandGrip.TipInHand(1, pinch) + HandGrip.TipInHand(4, pinch)) / 3.0f;
            }
        }

        /// <summary>
        /// The pouch is pinched: the slingshot up and its draw begun, here or on another player's copy (the draw is replicated), so the fingers
        /// close on it as it comes into the hand and let go as it is loosed.
        /// </summary>
        private bool SlingshotPinching => _bow != null && _bowStyle.Slingshot && _bowRaise > 0.5f && BowPull > 0.0f;

        /// <summary>
        /// The hand's size against the modelled one it was laid out from, read off the index finger's own bone (its knuckle's distance from its
        /// root), so a point given at the modelled size lands on this rig's hand whatever its height.
        /// </summary>
        private float HandScale
        {
            get
            {
                Transform[] digits = _rightDigits ?? FindDigits(_handR, true);
                FusedBodyShape.Digit index = FusedBodyShape.Digits[0];
                float modelled = (index.Knuckle - index.Root).magnitude;
                return digits != null && digits[1] != null && modelled > 1e-6f ? digits[1].localPosition.magnitude / modelled : 1.0f;
            }
        }

        /// <summary>Where the right hand pinches the pouch now (world).</summary>
        private Vector3 SlingshotPinchPoint => _handR.TransformPoint(SlingshotPinchInHand * HandScale);

        /// <summary>The fork arm's reach as a share of the arm's whole length: straight, a hair short of locked (0.97 still bent it 29 degrees).</summary>
        public const float SlingshotArmStraight = 0.995f;

        /// <summary>
        /// Aimed but not drawn, the pouch hand waits this share of the way back from the fork to the anchor, with the bands still slack: holding
        /// the click is what pulls them (owner, 2026-11-05: "make it so the string goes back as you hold click not just when aiming in").
        /// </summary>
        public const float SlingshotRestPull = 0.45f;

        /// <summary>
        /// Which way the drawing elbow points: out to the right, this much back along the aim and this much up (down: the forearm rising from a low
        /// elbow to the cheek, as Jimmy's; an elbow at shoulder height hid the fork from the camera behind that shoulder).
        /// </summary>
        public const float SlingshotElbowBack = 0.4f;

        public const float SlingshotElbowUp = -0.6f;

        /// <summary>
        /// Poses both arms for an aimed slingshot <paramref name="raise"/> of the way up, and gives back the way the fork's limbs point (upright,
        /// leaning in by <see cref="SlingshotCantDegrees"/>), for the fork placed in the fist.
        /// </summary>
        private Vector3 PoseSlingshotArms(float raise, Vector3 aim, Vector3 up, Vector3 right, float scale)
        {
            // Side-on: the torso turned so the left shoulder leads, the head turned back so the face still looks along the aim.
            if (_spine != null)
            {
                Quaternion turn = Quaternion.AngleAxis(SlingshotSideOnDegrees * raise, Vector3.up);
                _spine.rotation = turn * _spine.rotation;
                if (_head != null)
                {
                    _head.rotation = Quaternion.Inverse(turn) * _head.rotation;
                }
            }

            float headRadius = FusedBodyShape.HeadRadius * scale;
            Vector3 headCentre = _head != null ? _head.position : _rightUpperArm.position + up * (0.4f * scale);
            Vector3 anchor = headCentre + (right * SlingshotAnchor.x + up * SlingshotAnchor.y + aim * SlingshotAnchor.z) * headRadius;
            Vector3 lean = Quaternion.AngleAxis(-SlingshotCantDegrees, aim) * up;

            // The fork arm, straight: the fist placed so the pouch between the prongs is on the line from the anchor along the aim, the arm's
            // reach from the left shoulder (the nearest point of that line when it is out of reach).
            Vector3 shoulder = _leftUpperArm.position;
            float reach = (Vector3.Distance(shoulder, _leftLowerArm.position) + Vector3.Distance(_leftLowerArm.position, _handL.position)) * SlingshotArmStraight;
            Vector3 lineStart = anchor - lean * (_bowStyle.NockAbove * scale);
            Vector3 fromShoulder = lineStart - shoulder;
            float b = Vector3.Dot(aim, fromShoulder);
            float c = fromShoulder.sqrMagnitude - reach * reach;
            float along = b * b - c >= 0.0f ? -b + Mathf.Sqrt(b * b - c) : -b;
            Vector3 forkHand = lineStart + aim * along;
            TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, forkHand, -right + Vector3.down * 0.3f, raise);
            _handL.rotation = Quaternion.Slerp(_handL.rotation, StraightWrist(_handL, _leftLowerArm, lean), raise);

            // The pouch hand: resting part way back from the fork, drawn to the anchor, flicked back off the pouch on the loose.
            Vector3 pouchAtFork = forkHand + lean * (_bowStyle.NockAbove * scale);
            Vector3 rest = Vector3.Lerp(pouchAtFork, anchor, SlingshotRestPull);
            Vector3 pouchHand = Vector3.Lerp(rest, anchor, Mathf.SmoothStep(0.0f, 1.0f, BowPull));
            if (_bowRelease > 0.0f)
            {
                float flick = Mathf.Sin(_bowRelease / BowReleaseSeconds * Mathf.PI);
                pouchHand += (right * BowReleaseFlick.x + up * BowReleaseFlick.y + aim * BowReleaseFlick.z) * (scale * flick);
            }
            // Pinching the pouch: the fingers toward the fork, the palm in toward the cheek, the thumb up (+X runs down the right hand to the
            // fingers, -Y is the palm, +Z the thumb: HandGrip). Run on along the forearm, which rises from the low elbow, it waved. The wrist goes
            // where that hand puts its pinch on the pouch.
            Quaternion pinching = Quaternion.LookRotation(up, Vector3.Cross(up, aim));
            Vector3 wrist = pouchHand - pinching * (SlingshotPinchInHand * HandScale * _handR.lossyScale.x);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, wrist, right - aim * SlingshotElbowBack + up * SlingshotElbowUp, raise);
            _handR.rotation = Quaternion.Slerp(_handR.rotation, pinching, raise);
            return lean;
        }

        /// <summary>
        /// The left hand turned to run on along its forearm (the wrist straight), the thumb toward <paramref name="thumb"/>: +X runs back up the
        /// left hand toward the elbow, -Y is the palm, +Z the thumb (HandGrip).
        /// </summary>
        private static Quaternion StraightWrist(Transform hand, Transform forearm, Vector3 thumb)
        {
            Vector3 x = (forearm.position - hand.position).normalized;
            Vector3 z = Vector3.ProjectOnPlane(thumb, x);
            z = z.sqrMagnitude > 1e-6f ? z.normalized : Vector3.up;
            return Quaternion.LookRotation(z, Vector3.Cross(z, x));
        }
    }
}
