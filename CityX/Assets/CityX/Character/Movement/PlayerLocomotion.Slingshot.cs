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
        /// The anchor the pouch is drawn to, from the head's middle in head radii: this far to the right, up (down when negative) and forward,
        /// so it sits beside the right cheekbone and outside the head (its length is over one radius).
        /// </summary>
        public static readonly Vector3 SlingshotAnchor = new Vector3(0.85f, 0.0f, 0.75f);

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
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, pouchHand, right - aim * SlingshotElbowBack + up * SlingshotElbowUp, raise);
            // Pinching the pouch: the fingers toward the fork, the palm in toward the cheek, the thumb up (+X runs down the right hand to the
            // fingers, -Y is the palm, +Z the thumb: HandGrip). Run on along the forearm, which rises from the low elbow, it waved.
            _handR.rotation = Quaternion.Slerp(_handR.rotation, Quaternion.LookRotation(up, Vector3.Cross(up, aim)), raise);
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
