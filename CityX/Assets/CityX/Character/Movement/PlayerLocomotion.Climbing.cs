using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// PROCEDURAL CLIMBING: hands and feet planted on the wall, stepping in diagonal pairs (see ClimbLimbStepper). And HANDS ON A
    /// LEDGE: while climbing toward a ledge and while pulling up over it, both palms are put ON the ledge's lip (two-bone IK, after the
    /// Animator), so a climb reads as gripping the edge rather than as a generic clip whose hands float wherever it was authored.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private readonly ClimbLimbStepper _climbStepper = new ClimbLimbStepper();

        private float _climbIkBlend;

        private int _lastClimbDirection = 1;

        private static readonly int ClimbDirectionParameterHash = Animator.StringToHash("ClimbDirection");

        /// <summary>True while the limbs are being placed on the wall by the climb stepper.</summary>
        public bool ClimbLimbsActive => _climbIkBlend > 0.01f;

        private Vector3 SnapToClimbWall(Vector3 point, Vector3 normal)
        {
            // Onto the real surface (a wall is rarely a perfect plane: reveals, sills, panels), keeping the hold's own small offset from it.
            float offset = Vector3.Dot(point - _climbWallPoint, normal);
            if (LedgeProbe.SolidHit(point + normal * 0.5f, -normal, 1.0f, transform, out RaycastHit hit) && Mathf.Abs(hit.normal.y) < 0.5f)
            {
                return hit.point + normal * Mathf.Clamp(offset, 0.0f, 0.17f);
            }
            return point;
        }

        private static float LimbLength(Transform upper, Transform lower, Transform end)
        {
            return Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, end.position);
        }

        /// <summary>
        /// After the Animator: while climbing, the four limbs go where the stepper puts them on the wall: the hands and feet touch it, the body leans in, and nothing waves
        /// in the air or bends through it. The climb clip is left to move the hips and spine, nothing else.
        /// </summary>
        private void ApplyClimbLimbs()
        {
            bool bones = _leftUpperArm != null && _leftLowerArm != null && _handL != null && _rightUpperArm != null && _rightLowerArm != null && _handR != null
                && _leftUpperLeg != null && _leftLowerLeg != null && _leftFoot != null && _rightUpperLeg != null && _rightLowerLeg != null && _rightFoot != null;
            float deltaTime = Time.deltaTime;
            bool wanted = bones && _climbWallWeight > 0.01f && _animator != null;
            _climbIkBlend = Mathf.MoveTowards(_climbIkBlend, wanted ? 1.0f : 0.0f, deltaTime * 8.0f);
            if (!wanted)
            {
                if (_climbIkBlend <= 0.0f)
                {
                    _climbStepper.Reset();
                }
                return;
            }

            int direction = _animator.GetInteger(ClimbDirectionParameterHash);
            if (direction != 0)
            {
                _lastClimbDirection = direction;
            }
            // Where the body is going on the wall: what the climb (or, on a copy, the network) said; failing that, the Animator's up or down.
            Vector2 travel = _climbTravel.sqrMagnitude > 1e-4f ? _climbTravel : new Vector2(0.0f, direction);
            Vector3 n = _climbWallNormal;
            var body = new ClimbLimbStepper.Body
            {
                LeftShoulder = _leftUpperArm.position,
                RightShoulder = _rightUpperArm.position,
                LeftHip = _leftUpperLeg.position,
                RightHip = _rightUpperLeg.position,
                ArmLength = 0.5f * (LimbLength(_leftUpperArm, _leftLowerArm, _handL) + LimbLength(_rightUpperArm, _rightLowerArm, _handR)),
                LegLength = 0.5f * (LimbLength(_leftUpperLeg, _leftLowerLeg, _leftFoot) + LimbLength(_rightUpperLeg, _rightLowerLeg, _rightFoot)),
            };
            _climbStepper.Tick(deltaTime, body, _climbWallPoint, n, travel, SnapToClimbWall);

            float weight = Mathf.SmoothStep(0.0f, 1.0f, _climbIkBlend) * Mathf.Clamp01(_climbWallWeight);
            Vector3 across = ClimbLimbStepper.Across(n);   // the body's right when it faces the wall

            // The chest leans in to the wall, turns a little toward whichever hand reaches, and tips toward the way it is shuffling.
            if (_spine != null)
            {
                float reach = (_climbStepper.IsStepping(ClimbLimbStepper.Limb.RightHand) ? 1.0f : 0.0f) - (_climbStepper.IsStepping(ClimbLimbStepper.Limb.LeftHand) ? 1.0f : 0.0f);
                _spine.rotation = Quaternion.AngleAxis(-reach * 7.0f * weight, Vector3.up) * Quaternion.AngleAxis(ClimbChestLeanDegrees * weight, across)
                    * Quaternion.AngleAxis(-travel.x * ClimbChestSideTiltDegrees * weight, -n) * _spine.rotation;
            }
            // The head looks where the climb is going: up the wall, down at the feet, along the wall when shuffling.
            if (_head != null)
            {
                float pitch = (ClimbHeadLookUpDegrees * Mathf.Clamp01(travel.y) - ClimbHeadLookDownDegrees * Mathf.Clamp01(-travel.y)) * weight;
                float yaw = travel.x * ClimbHeadLookAlongDegrees * weight;
                _head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, across) * _head.rotation;
            }

            // The elbows go out to the sides and down, the knees out to the sides (a frog's), both a little away from the wall.
            Vector3 elbowLeft = -Vector3.up * 0.5f - across * 0.9f + n * 0.3f;
            Vector3 elbowRight = -Vector3.up * 0.5f + across * 0.9f + n * 0.3f;
            Vector3 kneeLeft = -across + n * ClimbKneeOffWall + Vector3.up * ClimbKneeUp;
            Vector3 kneeRight = across + n * ClimbKneeOffWall + Vector3.up * ClimbKneeUp;
            TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, _climbStepper.Target(ClimbLimbStepper.Limb.LeftHand, n), elbowLeft, weight);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, _climbStepper.Target(ClimbLimbStepper.Limb.RightHand, n), elbowRight, weight);
            _handL.rotation = Quaternion.Slerp(_handL.rotation, PalmOnWall(n, true), weight);
            _handR.rotation = Quaternion.Slerp(_handR.rotation, PalmOnWall(n, false), weight);

            // ON THE TOES: the stepper's foot hold is where the toe touches the wall; the foot points into the wall, so the ankle stands off it by the
            // length of the foot (ClimbToeHold).
            Quaternion toesLeft = ClimbToeHold.Rotation(n, true);
            Quaternion toesRight = ClimbToeHold.Rotation(n, false);
            Vector3 ankleLeft = ClimbToeHold.AnkleFor(_climbStepper.Target(ClimbLimbStepper.Limb.LeftFoot, n), toesLeft, n, _leftFoot.lossyScale.y);
            Vector3 ankleRight = ClimbToeHold.AnkleFor(_climbStepper.Target(ClimbLimbStepper.Limb.RightFoot, n), toesRight, n, _rightFoot.lossyScale.y);
            TwoBoneIK.Solve(_leftUpperLeg, _leftLowerLeg, _leftFoot, ankleLeft, kneeLeft, weight);
            TwoBoneIK.Solve(_rightUpperLeg, _rightLowerLeg, _rightFoot, ankleRight, kneeRight, weight);
            _leftFoot.rotation = Quaternion.Slerp(_leftFoot.rotation, toesLeft, weight);
            _rightFoot.rotation = Quaternion.Slerp(_rightFoot.rotation, toesRight, weight);
        }

        /// <summary>How far a climbing knee bows away from the wall and up, for each unit it bows out to its side.</summary>
        public const float ClimbKneeOffWall = 0.45f;

        public const float ClimbKneeUp = 0.25f;

        /// <summary>How far a climbing hand's fingers lean out from straight up the wall, toward its own side, degrees.</summary>
        public const float ClimbFingersOutDegrees = 15.0f;

        /// <summary>
        /// A climbing hand's rotation: the PALM FLAT ON THE WALL, the fingers up it and leaning out, a hand holding the wall rather than a fist held at it. The
        /// hand bones are the rig's own (built in a T-pose, every rotation the identity): the fingers run +X on the right hand and -X on the left, the palm is -Y.
        /// </summary>
        public static Quaternion PalmOnWall(Vector3 normal, bool left)
        {
            Vector3 up = Vector3.up - normal * Vector3.Dot(Vector3.up, normal);
            up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.Cross(normal, Vector3.right).normalized;
            Vector3 side = ClimbLimbStepper.Across(normal) * (left ? -1.0f : 1.0f);
            float lean = ClimbFingersOutDegrees * Mathf.Deg2Rad;
            Vector3 fingers = up * Mathf.Cos(lean) + side * Mathf.Sin(lean);
            Vector3 x = left ? -fingers : fingers;
            return Quaternion.LookRotation(Vector3.Cross(x, normal), normal);
        }

        /// <summary>How far the chest leans in toward the wall while climbing, degrees.</summary>
        public const float ClimbChestLeanDegrees = 9.0f;

        /// <summary>How far the chest tips toward the leading side while shuffling sideways, degrees.</summary>
        public const float ClimbChestSideTiltDegrees = 6.0f;

        /// <summary>The head looks up the wall climbing up, down at the feet climbing down, along the wall shuffling sideways: degrees at full travel.</summary>
        public const float ClimbHeadLookUpDegrees = 14.0f;

        public const float ClimbHeadLookDownDegrees = 10.0f;

        public const float ClimbHeadLookAlongDegrees = 25.0f;

        private float _climbWallWeight;

        private Vector3 _climbWallPoint;

        private Vector3 _climbWallNormal = Vector3.forward;

        /// <summary>
        /// Hands and feet keep at least this far off the wall plane while climbing, in metres. Under the stepper's own holds (ClimbLimbStepper.HandOffWall), so
        /// the safety net leaves a palm on the wall alone: at 0.09 it re-bent every arm toward the camera, the forearms in front of the face.
        /// </summary>
        public const float HandWallClearance = 0.04f;

        public const float FootWallClearance = 0.11f;

        /// <summary>The wall plane the limbs are being kept off (read back so a network copy can be told it).</summary>
        public float ClimbWallWeight => _climbWallWeight;

        public Vector3 ClimbWallPoint => _climbWallPoint;

        public Vector3 ClimbWallNormal => _climbWallNormal;

        private Vector2 _climbTravel;

        /// <summary>Which way the body is going on the wall: x along it (its right positive), y up; zero holding. Drives the limb steps (ClimbLimbStepper); read back for a network copy.</summary>
        public Vector2 ClimbTravel => _climbTravel;

        public void SetClimbTravel(Vector2 travel)
        {
            _climbTravel = Vector2.ClampMagnitude(travel, 1.0f);
        }

        /// <summary>Where the hands are being put on a ledge (read back so a network copy can be told it).</summary>
        public Vector3 LedgeLip => _ledgeLip;

        public Vector3 LedgeNormal => _ledgeNormal;

        /// <summary>
        /// While climbing, tells the body where the wall is (a point on its face and its outward normal) so that no hand, foot, elbow or knee can be
        /// animated THROUGH it: the climb clip's limbs swing wherever it likes, and the body hangs a capsule's radius from the wall.
        /// </summary>
        public void SetClimbWall(Vector3 point, Vector3 normal, float weight)
        {
            _climbWallWeight = Mathf.Clamp01(weight);
            _climbWallPoint = point;
            _climbWallNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
        }

        /// <summary>
        /// After the Animator: any hand or foot (or elbow / knee) that the animation put behind the wall plane is moved out to just in front of it and
        /// its limb is solved to reach there (two-bone IK), so the hands and feet TOUCH the wall and never go through it. A limb already in front is not
        /// touched, so the animation is only corrected where it is wrong.
        /// </summary>
        private void ApplyClimbWallLimbs()
        {
            if (_climbWallWeight <= 0.01f)
            {
                return;
            }
            Vector3 n = _climbWallNormal;
            Vector3 elbowHint = n * 0.8f + Vector3.down * 0.2f;
            Vector3 kneeHint = n * 0.5f + Vector3.up * 0.5f;
            for (int pass = 0; pass < 2; pass++)   // a second pass takes out what the first one left (an arm at full stretch, a blend that moved)
            {
                KeepOffWall(_rightUpperArm, _rightLowerArm, _handR, HandWallClearance, elbowHint);
                KeepOffWall(_leftUpperArm, _leftLowerArm, _handL, HandWallClearance, elbowHint);
                KeepOffWall(_rightUpperLeg, _rightLowerLeg, _rightFoot, FootWallClearance, kneeHint);
                KeepOffWall(_leftUpperLeg, _leftLowerLeg, _leftFoot, FootWallClearance, kneeHint);
            }
        }

        private void KeepOffWall(Transform upper, Transform lower, Transform end, float clearance, Vector3 hint)
        {
            if (upper == null || lower == null || end == null)
            {
                return;
            }
            float endDepth = Vector3.Dot(end.position - _climbWallPoint, _climbWallNormal);
            float midDepth = Vector3.Dot(lower.position - _climbWallPoint, _climbWallNormal);
            float push = Mathf.Max(clearance - endDepth, 0.0f);
            if (push <= 0.001f && midDepth >= 0.02f)
            {
                return;
            }
            Vector3 target = end.position + _climbWallNormal * push;
            TwoBoneIK.Solve(upper, lower, end, target, hint, _climbWallWeight);
        }

        private float _ledgeHandsWeight;

        private Vector3 _ledgeLip;

        private Vector3 _ledgeNormal;

        /// <summary>The ledge the hands reach for: 0 weight lets go. <paramref name="lip"/> is the wall's top edge, <paramref name="outward"/> the wall's normal.</summary>
        public void SetLedgeHands(float weight, Vector3 lip, Vector3 outward)
        {
            _ledgeHandsWeight = Mathf.Clamp01(weight);
            _ledgeLip = lip;
            _ledgeNormal = outward;
        }

        /// <summary>How strongly the hands are on a ledge right now (0 to 1).</summary>
        public float LedgeHandsWeight => _ledgeHandsWeight;

        private void ApplyLedgeHands()
        {
            if (_ledgeHandsWeight <= 0.01f || _rightUpperArm == null || _rightLowerArm == null || _handR == null
                || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                return;
            }
            Vector3 across = Vector3.Cross(Vector3.up, _ledgeNormal).normalized;
            // Palms on top of the lip, a shoulder's width apart, a little back from the edge.
            Vector3 centre = _ledgeLip - _ledgeNormal * 0.06f + Vector3.up * 0.03f;
            Vector3 hint = _ledgeNormal * 0.6f - Vector3.up;
            float weight = Mathf.SmoothStep(0.0f, 1.0f, _ledgeHandsWeight);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, centre - across * 0.28f, hint, weight);   // facing the wall (-normal), the right hand is on the -across side
            TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, centre + across * 0.28f, hint, weight);
        }
    }
}
