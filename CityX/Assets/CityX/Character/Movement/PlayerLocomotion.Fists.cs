using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// UNARMED STRIKES: the stance under them (see FistStrikes / FistPoseDriver; the blows themselves are baked poses, their stride drawn to
    /// how far the body went: PlayerLocomotion.BakedPose.cs), and the body turned to face a blow.
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

        /// <summary>True while the fists' stance is being held (under a blow, and for a moment after the last).</summary>
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
    }
}
