using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SWORD ATTACKS, APPLIED TO THE RIG (see <see cref="SwordStrikes"/> for what they are and <see cref="SwordPoseDriver"/> for their timing).
    /// After the Animator, each frame: the torso is turned, leaned and tilted into the cut; the sword arm is solved (two-bone IK) to put the wrist
    /// where the frame says; the hand is turned so the BLADE points the way the frame says with its EDGE leading, which is what makes a cut read
    /// as a cut and not a wave; the twist that needs goes into the forearm, not the wrist (<see cref="WristTwist"/>); with a one-handed sword the
    /// free hand is placed too; with a two-handed one the support hand follows the grip as it always has (ApplySupportHand runs right after).
    /// The hips' drop is handed to the foot IK, which bends the legs so the feet stay planted under it.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private SwordFrame _swordFrame;
        private Transform _posedSword;
        private ISwordBlade _posedBlade;

        /// <summary>The blade's tip is kept at least this far above the ground under the body, metres: a low follow-through pitches up rather than through the floor.</summary>
        public const float TipFloorClearance = 0.06f;

        public void SetSwordPose(in SwordFrame frame)
        {
            _swordFrame = frame;
        }

        public void ClearSwordPose()
        {
            _swordFrame = default;
        }

        /// <summary>True while a sword pose (an attack, a guard or the ready stance) is being applied.</summary>
        public bool SwordPoseActive => _swordFrame.Active && _swordFrame.Weight > 0.001f;

        /// <summary>The sword pose being applied this frame (for tests and captures).</summary>
        public SwordFrame CurrentSwordFrame => _swordFrame;

        /// <summary>How far the hips are sunk by the pose this frame, metres: the foot IK takes it off the root with the legs kept planted.</summary>
        private float PoseHipDrop => _swordFrame.Active ? Mathf.Max(0.0f, _swordFrame.HipDrop) * Mathf.Clamp01(_swordFrame.Weight) : 0.0f;

        /// <summary>The blade's axis in the right hand bone's own frame when nothing says otherwise: out past the thumb.</summary>
        private static readonly Vector3 FallbackBladeInHand = new Vector3(0.26f, 0.0f, 0.966f).normalized;

        private static readonly Vector3 FallbackEdgeInHand = new Vector3(0.966f, 0.0f, -0.26f).normalized;

        /// <summary>The sword under the right hand, if one is mounted there (its own transform; the blade runs along its +Y and the edge faces its -X).</summary>
        private Transform PosedSword()
        {
            if (_handR == null)
            {
                return null;
            }
            if (_posedSword != null && _posedSword.parent == _handR)
            {
                return _posedSword;
            }
            _posedSword = null;
            _posedBlade = null;
            for (int i = 0; i < _handR.childCount; i++)
            {
                Transform child = _handR.GetChild(i);
                ISwordBlade blade = child.GetComponent<ISwordBlade>();
                if (blade != null)
                {
                    _posedSword = child;
                    _posedBlade = blade;
                    break;
                }
            }
            return _posedSword;
        }

        /// <summary>
        /// If the pose has put the blade's tip into the ground, the hand is pitched up about the axis across the blade until the tip clears it:
        /// a slam on a slope, or a follow-through authored a little too low, scrapes the floor instead of passing through it.
        /// </summary>
        private void KeepTipAboveGround(float weight)
        {
            if (_posedBlade == null || weight <= 0.001f)
            {
                return;
            }
            float floor = transform.position.y + TipFloorClearance;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 root = _posedBlade.BladeBaseWorld;
                Vector3 tip = _posedBlade.BladeTipWorld;
                if (tip.y >= floor)
                {
                    return;
                }
                Vector3 blade = tip - root;
                float length = blade.magnitude;
                if (length < 1e-4f)
                {
                    return;
                }
                blade /= length;
                Vector3 axis = Vector3.Cross(blade, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f)
                {
                    return;   // straight down: nothing across it to pitch about
                }
                axis.Normalize();
                float pitchNow = Mathf.Asin(Mathf.Clamp(blade.y, -1.0f, 1.0f));
                float pitchWanted = Mathf.Asin(Mathf.Clamp((floor - root.y) / length, -1.0f, 1.0f));
                float degrees = (pitchWanted - pitchNow) * Mathf.Rad2Deg * weight;
                Quaternion before = _handR.rotation;
                _handR.rotation = Quaternion.AngleAxis(degrees, axis) * before;
                if (_posedBlade.BladeTipWorld.y < tip.y)
                {
                    _handR.rotation = Quaternion.AngleAxis(-degrees, axis) * before;   // the other way round the axis
                }
            }
        }

        private void ApplySwordPose()
        {
            if (!SwordPoseActive || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                return;
            }
            float w = Mathf.Clamp01(_swordFrame.Weight);

            // The torso into the cut, before the arm is solved from the shoulder it moved. How much of the authored twist and lean the body
            // does is the grip settings' torso amount (1 as authored, 0 upright), read live so it can be tuned while the game runs.
            float torso = w * Mathf.Clamp01(TorsoFollowStrength);
            if (_spine != null && torso > 0.0f)
            {
                Quaternion twist = Quaternion.AngleAxis(_swordFrame.TwistDegrees * torso, transform.up);
                Quaternion lean = Quaternion.AngleAxis(_swordFrame.LeanDegrees * torso, transform.right);
                Quaternion tilt = Quaternion.AngleAxis(_swordFrame.TiltDegrees * torso, transform.forward);
                _spine.rotation = twist * lean * tilt * _spine.rotation;
            }

            // The sword arm: the wrist to the frame's point, the elbow down or out as it says.
            float armLength = Vector3.Distance(_rightUpperArm.position, _rightLowerArm.position) + Vector3.Distance(_rightLowerArm.position, _handR.position);
            Vector3 h = _swordFrame.Hand;
            Vector3 target = _rightUpperArm.position + (transform.right * h.x + transform.up * h.y + transform.forward * h.z) * armLength;
            float elbowOut = Mathf.Clamp01(_swordFrame.ElbowOut);
            Vector3 hint = -transform.up * (1.0f - 0.8f * elbowOut) + transform.right * (0.55f + 0.9f * elbowOut);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, target, hint, w);

            // The hand turned so the blade points where the frame says, edge first.
            Vector3 bladeWorld = (transform.right * _swordFrame.Blade.x + transform.up * _swordFrame.Blade.y + transform.forward * _swordFrame.Blade.z).normalized;
            Vector3 edgeWorld = transform.right * _swordFrame.Edge.x + transform.up * _swordFrame.Edge.y + transform.forward * _swordFrame.Edge.z;
            edgeWorld -= bladeWorld * Vector3.Dot(edgeWorld, bladeWorld);
            if (edgeWorld.sqrMagnitude < 1e-6f)
            {
                edgeWorld = Vector3.Cross(bladeWorld, Vector3.up);
            }
            Vector3 bladeInHand = FallbackBladeInHand;
            Vector3 edgeInHand = FallbackEdgeInHand;
            Transform sword = PosedSword();
            if (sword != null)
            {
                bladeInHand = sword.localRotation * Vector3.up;
                edgeInHand = sword.localRotation * Vector3.left;
            }
            Quaternion wanted = Quaternion.LookRotation(bladeWorld, edgeWorld.normalized) * Quaternion.Inverse(Quaternion.LookRotation(bladeInHand, edgeInHand));
            _handR.rotation = Quaternion.Slerp(_handR.rotation, wanted, w);
            KeepTipAboveGround(w);
            WristTwist.ShareWithForearm(_rightLowerArm, _handR);

            // The free hand, when the sword leaves it free.
            if (!_swordFrame.TwoHanded)
            {
                PlaceFist(_leftUpperArm, _leftLowerArm, _handL, _swordFrame.FreeHand, w, _swordFrame.FreeElbowOut, -1.0f);
            }
        }
    }
}
