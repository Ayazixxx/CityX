using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// UNARMED STRIKES: the fists and the torso (see FistStrikes / FistPoseDriver), and the body turned to face a blow.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private FistFrame _fistFrame;

        public void SetFistPose(in FistFrame frame)
        {
            _fistFrame = frame;
        }

        public void ClearFistPose()
        {
            _fistFrame = default;
        }

        /// <summary>True while a fist pose is being applied.</summary>
        public bool FistPoseActive => _fistFrame.Active;

        /// <summary>How much the feet should stand in the fists' stance and step (SwordFootwork, as the sword's) this frame, 0 to 1.</summary>
        private float FistFootworkWanted => _fistFrame.Active ? Mathf.Clamp01(_fistFrame.Footwork) : 0.0f;

        /// <summary>How far the fists' stance sinks the hips this frame, metres.</summary>
        private float FistHipDrop => _fistFrame.Active ? Mathf.Max(0.0f, _fistFrame.HipDrop) : 0.0f;

        // THE FACING A BLOW TURNS THE BODY TO. A punch goes where the body faces, and the body only turned to the camera while moving: standing still
        // after turning the camera, the punch went the way you USED to face. A blow asks for a quick turn to the camera (or to a target near it).
        private bool _facingOverride;

        private float _facingOverrideYaw;

        private float _facingOverrideRate;

        private float _facingOverrideRemaining;

        /// <summary>Turn the body to <paramref name="yawDegrees"/> at <paramref name="degreesPerSecond"/> for the next <paramref name="seconds"/>, whatever it is otherwise doing.</summary>
        public void SetFacingOverride(float yawDegrees, float degreesPerSecond, float seconds)
        {
            _facingOverride = true;
            _facingOverrideYaw = yawDegrees;
            _facingOverrideRate = degreesPerSecond;
            _facingOverrideRemaining = seconds;
        }

        private void TickFacingOverride(float deltaTime)
        {
            if (!_facingOverride)
            {
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0.0f, _facingOverrideYaw, 0.0f), _facingOverrideRate * deltaTime);
            _facingOverrideRemaining -= deltaTime;
            if (_facingOverrideRemaining <= 0.0f)
            {
                _facingOverride = false;
            }
        }

        /// <summary>After the Animator: turn and lean the torso into the blow, then put each fist where the frame says (two-bone IK), the other held down at the side.</summary>
        private void ApplyFistPose()
        {
            if (!_fistFrame.Active)
            {
                return;
            }
            if (_spine != null && (Mathf.Abs(_fistFrame.TwistDegrees) > 0.01f || Mathf.Abs(_fistFrame.LeanDegrees) > 0.01f))
            {
                Quaternion twist = Quaternion.AngleAxis(_fistFrame.TwistDegrees, transform.up);
                Quaternion lean = Quaternion.AngleAxis(_fistFrame.LeanDegrees, transform.right);
                _spine.rotation = twist * lean * _spine.rotation;
            }
            PlaceFist(_rightUpperArm, _rightLowerArm, _handR, _fistFrame.RightHand, _fistFrame.RightWeight, _fistFrame.RightElbowOut, 1.0f);
            PlaceFist(_leftUpperArm, _leftLowerArm, _handL, _fistFrame.LeftHand, _fistFrame.LeftWeight, _fistFrame.LeftElbowOut, -1.0f);
        }

        private void PlaceFist(Transform upper, Transform lower, Transform hand, Vector3 offset, float weight, float elbowOut, float side)
        {
            if (weight <= 0.001f || upper == null || lower == null || hand == null)
            {
                return;
            }
            float length = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            Vector3 target = upper.position + (transform.right * offset.x + transform.up * offset.y + transform.forward * offset.z) * length;
            // The elbow goes down and out; a blow swung across (the overhand) pushes it right out to the side.
            Vector3 hint = -transform.up * (1.0f - 0.8f * elbowOut) + transform.right * side * (0.55f + 0.9f * elbowOut);
            TwoBoneIK.Solve(upper, lower, hand, target, hint, weight);
        }
    }
}
