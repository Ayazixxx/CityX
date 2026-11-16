using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CAST POSE (owner, 2026-11-15: "when you are holding a 1 handed weapon and use a projectile spell like fireball or ice shards, it comes
    /// out of your hand that isnt holding a weapon so typically your left ... not really a whole animation he just lifts his left arm up and
    /// faces the palm of his hand toward the target. make the right hand unarmed projectile magic cast also face the palm of the hand toward
    /// the target"): the free hand raised out toward where the spell goes, the arm nearly straight, the palm to the target and the fingers up,
    /// open. The right hand, or the left when the right holds something (a sword, a knife, the grapple gun, a pistol); a spell from the bar
    /// leaves from that palm (<see cref="CastPalmPoint"/>). A staff or a wand in the hand casts through itself instead (their own poses), so
    /// this stays down then. After the straight wrists, which it bends (the palm turned back off the forearm), as the barrier's hands are.
    /// It used to swing the right upper arm up 130 degrees about a fixed axis: no elbow, no wrist, the palm wherever the swing left it.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the hand takes to come up into the cast, seconds (it drops as the spell's own pose eases out).</summary>
        public const float CastRaiseSeconds = 0.07f;

        /// <summary>
        /// Where the palm goes at full cast, from the casting arm's shoulder, metres (the body at its built height): along the aim, out to the
        /// arm's own side, up. Out and a little low, so the game's camera behind the shoulder sees it past the head (straight out from the
        /// shoulder, the left hand hid behind it).
        /// </summary>
        public const float CastReach = 0.72f;

        public const float CastOut = 0.3f;
        public const float CastLift = -0.2f;

        /// <summary>How far the fingers lean out to the hand's own side from straight up, degrees.</summary>
        public const float CastFingerOutDegrees = 15.0f;

        /// <summary>How far in front of the palm a spell appears, metres at the built height (so it does not start inside the hand).</summary>
        public const float CastLeaveAhead = 0.08f;

        private float _castPoseFraction;
        private float _castRaise;
        private Vector3 _castAim;

        /// <summary>
        /// How strongly the hand is raised for a spell this frame, 0 to 1 (the spell's own pose: held at the cast, then easing out), toward
        /// <paramref name="aim"/>, the way the spell goes (zero: the way the body faces). Called every frame by PlayerInputReader on the owner
        /// and by RemoteAbilityPose on another player's copy.
        /// </summary>
        public void SetCastPose(float fraction, Vector3 aim)
        {
            _castPoseFraction = Mathf.Clamp01(fraction);
            _castAim = aim;
        }

        public void SetCastPose(float fraction) => SetCastPose(fraction, Vector3.zero);

        /// <summary>How far up the casting hand is, 0 down .. 1 raised.</summary>
        public float CastRaise => _castRaise;

        /// <summary>A spell from the bar is cast from the left hand: the right holds something.</summary>
        public bool CastsWithLeftHand => RightHandHolds;

        /// <summary>The hand casts (no staff or wand to cast through).</summary>
        private bool CastsFromHand => Staff == null && Wand == null;

        /// <summary>
        /// Where a spell cast toward <paramref name="aim"/> leaves the free hand: in front of the palm where the cast pose puts it. Worked out
        /// from the shoulder rather than read off the hand, which has not come up yet on the frame the spell is cast.
        /// </summary>
        public Vector3 CastPalmPoint(Vector3 aim)
        {
            Transform shoulder = CastsWithLeftHand ? _leftUpperArm : _rightUpperArm;
            Vector3 from = shoulder != null ? shoulder.position : transform.position + Vector3.up * (WorldHeight * 0.8f);
            Vector3 direction = CastDirection(aim);
            float scale = WorldHeight / BodyRig.StandingHeight;
            return PalmAt(from, direction, CastsWithLeftHand ? -transform.right : transform.right, scale) + direction * (CastLeaveAhead * scale);
        }

        private Vector3 CastDirection(Vector3 aim) => aim.sqrMagnitude > 1e-6f ? aim.normalized : transform.forward;

        private static Vector3 PalmAt(Vector3 shoulder, Vector3 aim, Vector3 outward, float scale) =>
            shoulder + (aim * CastReach + outward * CastOut + Vector3.up * CastLift) * scale;

        /// <summary>The fingers of a palm facing <paramref name="palm"/>: up, leaning out to the hand's side, square to the palm.</summary>
        private static Vector3 FingersUp(Vector3 palm, Vector3 outward)
        {
            Vector3 up = Quaternion.AngleAxis(CastFingerOutDegrees, Vector3.Cross(outward, Vector3.up).normalized) * Vector3.up;
            Vector3 fingers = Vector3.ProjectOnPlane(up, palm);
            if (fingers.sqrMagnitude < 1e-6f)
            {
                fingers = Vector3.ProjectOnPlane(Vector3.Cross(palm, outward), palm);
            }
            return fingers.normalized;
        }

        private void ApplyCastingPose()
        {
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _castRaise = _castPoseFraction > _castRaise ? Mathf.MoveTowards(_castRaise, _castPoseFraction, deltaTime / CastRaiseSeconds) : _castPoseFraction;
            if (_castRaise <= 0.0f || !CastsFromHand)
            {
                return;
            }
            bool left = CastsWithLeftHand;
            Transform upper = left ? _leftUpperArm : _rightUpperArm;
            Transform lower = left ? _leftLowerArm : _rightLowerArm;
            Transform hand = left ? _handL : _handR;
            if (upper == null || lower == null || hand == null)
            {
                return;
            }
            float raise = _castRaise * _castRaise * (3.0f - 2.0f * _castRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 aim = CastDirection(_castAim);
            Vector3 outward = left ? -transform.right : transform.right;
            Vector3 fingers = FingersUp(aim, outward);
            // The wrist sits a palm's length back down the fingers from the palm's middle.
            Vector3 wrist = PalmAt(upper.position, aim, outward, scale) - fingers * (PalmOffsetMetres * scale);
            HoldPalmOut(upper, lower, hand, wrist, outward + Vector3.down * 0.8f, aim, fingers, raise, !left);
        }

        /// <summary>
        /// An arm reaching its wrist to <paramref name="wrist"/> (two-bone IK, the elbow toward <paramref name="elbowHint"/>), the hand turned so
        /// its palm faces <paramref name="palm"/> with the fingers along <paramref name="fingers"/>, <paramref name="weight"/> of the way from the
        /// pose it had. The cast and the Arcane Barrier's hands.
        /// </summary>
        private static void HoldPalmOut(Transform upper, Transform lower, Transform hand, Vector3 wrist, Vector3 elbowHint, Vector3 palm,
            Vector3 fingers, float weight, bool rightHand)
        {
            TwoBoneIK.Solve(upper, lower, hand, wrist, elbowHint, weight);
            // The hand bone: +X to the fingertips on the right hand and back up the arm on the left, -Y the palm, +Z the thumb (HandGrip).
            Quaternion held = rightHand
                ? Quaternion.LookRotation(Vector3.Cross(fingers, -palm), -palm)
                : Quaternion.LookRotation(Vector3.Cross(fingers, palm), -palm);
            hand.rotation = Quaternion.Slerp(hand.rotation, held, weight);
        }
    }
}
