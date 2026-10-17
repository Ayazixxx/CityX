using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SWORD SPIN'S POSE. Link's spin has the sword arm straight out to the side with the blade carrying on the line of the arm,
    /// the free arm out the other way for balance, and the whole body turning. No clip does that (the swing clips are all two-handed
    /// chops), and a hand-posed clip is far harder to keep right than three IK calls, so the arms are placed here, after the Animator,
    /// the way the gun-aim pose and the support hand are.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How fast the spin pose takes hold and lets go, as a fraction per second: about a seventh of a second.</summary>
        public const float SpinPoseBlendPerSecond = 7.0f;

        private float _spinPoseTarget;

        private float _spinPoseBlend;

        private float _spinBladeBackDegrees;

        private float _spinBladePitchDegrees;

        private ISwordBlade _spinBlade;

        /// <summary>How far the spin pose has taken hold, 0 to 1.</summary>
        public float SwordSpinPoseBlend => _spinPoseBlend;

        /// <summary>The shoulder of the sword arm (the right), or null on a body without one.</summary>
        public Transform SwordArmShoulder => _rightUpperArm;

        /// <summary>
        /// Asks for the spin pose (<paramref name="target"/> 1) or to let it go (0). <paramref name="bladeBackDegrees"/> swings the
        /// blade from straight out to the right toward the back (a wind-up), <paramref name="bladePitchDegrees"/> tips it up.
        /// <paramref name="blade"/> is the sword in hand, so the hand can be turned to lay it along the arm; null keeps the hand's own pose.
        /// </summary>
        public void SetSwordSpinPose(float target, float bladeBackDegrees = 0.0f, float bladePitchDegrees = 0.0f, ISwordBlade blade = null)
        {
            _spinPoseTarget = Mathf.Clamp01(target);
            _spinBladeBackDegrees = bladeBackDegrees;
            _spinBladePitchDegrees = bladePitchDegrees;
            _spinBlade = blade;
        }

        /// <summary>Where the sword arm's hand is put, relative to the shoulder, as a fraction of the arm's length: all the way out.</summary>
        private const float SpinArmReach = 0.99f;

        private void ApplySwordSpinPose()
        {
            float deltaTime = _lastTickDeltaTime > 0.0f ? _lastTickDeltaTime : Time.deltaTime;
            _spinPoseBlend = Mathf.MoveTowards(_spinPoseBlend, _spinPoseTarget, SpinPoseBlendPerSecond * deltaTime);
            if (_spinPoseBlend <= 0.0f || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                return;
            }
            float weight = Mathf.SmoothStep(0.0f, 1.0f, _spinPoseBlend);

            // The sword arm: straight out to the side (and back, and up, as asked), elbow down so it does not buckle upward.
            Vector3 outward = Quaternion.AngleAxis(_spinBladeBackDegrees, transform.up) * transform.right;
            float pitch = _spinBladePitchDegrees * Mathf.Deg2Rad;
            outward = outward * Mathf.Cos(pitch) + transform.up * Mathf.Sin(pitch);
            float armLength = Vector3.Distance(_rightUpperArm.position, _rightLowerArm.position)
                + Vector3.Distance(_rightLowerArm.position, _handR.position);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, _rightUpperArm.position + outward * armLength * SpinArmReach,
                -transform.up + transform.forward * 0.2f, weight);

            // The blade carries on the line of the arm: turn the hand about the wrist until the blade lies along it.
            if (_spinBlade != null)
            {
                Vector3 bladeNow = _spinBlade.BladeTipWorld - _spinBlade.BladeBaseWorld;
                if (bladeNow.sqrMagnitude > 1e-6f)
                {
                    Quaternion turn = Quaternion.FromToRotation(bladeNow, outward);
                    _handR.rotation = Quaternion.Slerp(_handR.rotation, turn * _handR.rotation, weight);
                }

                // And roll it about its own length so the CUTTING EDGE leads, the way the body is going to turn: the sword sweeps
                // forward across the front (the body turns the other way round to a clock seen from above), so its edge faces forward
                // and the flat of the blade up and down. Without this the blade is a paddle, edge down, slapping flat-first.
                Vector3 edge = _spinBlade.EdgeDirectionWorld;
                Vector3 axis = _spinBlade.BladeTipWorld - _spinBlade.BladeBaseWorld;
                if (edge.sqrMagnitude > 1e-6f && axis.sqrMagnitude > 1e-6f)
                {
                    Vector3 edgeFlat = Vector3.ProjectOnPlane(edge, axis);
                    Vector3 leading = Vector3.ProjectOnPlane(transform.forward, axis);
                    if (edgeFlat.sqrMagnitude > 1e-6f && leading.sqrMagnitude > 1e-6f)
                    {
                        float roll = Vector3.SignedAngle(edgeFlat, leading, axis);
                        _handR.rotation = Quaternion.AngleAxis(roll * weight, axis) * _handR.rotation;
                    }
                }
            }

            // The free arm balances it: out the other side, a little down and back.
            if (_leftUpperArm != null && _leftLowerArm != null && _handL != null)
            {
                float leftLength = Vector3.Distance(_leftUpperArm.position, _leftLowerArm.position)
                    + Vector3.Distance(_leftLowerArm.position, _handL.position);
                Vector3 balance = (-transform.right * 0.85f - transform.up * 0.4f - transform.forward * 0.2f).normalized;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, _leftUpperArm.position + balance * leftLength * SpinArmReach,
                    -transform.up - transform.forward * 0.3f, weight);
            }
        }
    }
}
