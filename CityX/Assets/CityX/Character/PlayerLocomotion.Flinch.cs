using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE RECOIL FROM A BLOW, applied to the rig (see <see cref="HitReactions"/> / <see cref="FlinchPoseDriver"/>): the spine thrown away from
    /// the blow and the head whipped further, before the arm poses (so a sword pose or a guard is solved from the thrown shoulders).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private FlinchFrame _flinchFrame;

        public void SetFlinchPose(in FlinchFrame frame)
        {
            _flinchFrame = frame;
        }

        public void ClearFlinchPose()
        {
            _flinchFrame = default;
        }

        /// <summary>True while a hit reaction is being applied.</summary>
        public bool FlinchPoseActive => _flinchFrame.Active && _flinchFrame.Weight > 0.001f;

        private void ApplyFlinchPose()
        {
            if (!FlinchPoseActive)
            {
                return;
            }
            float w = Mathf.Clamp01(_flinchFrame.Weight);
            // A forward lean is a turn about the body's right; a tip toward the right is a turn about the body's forward, the other way.
            Quaternion lean = Quaternion.AngleAxis(_flinchFrame.LeanDegrees * w, transform.right);
            Quaternion tilt = Quaternion.AngleAxis(-_flinchFrame.TiltRightDegrees * w, transform.forward);
            if (_spine != null)
            {
                _spine.rotation = lean * tilt * _spine.rotation;
            }
            if (_head != null && Mathf.Abs(_flinchFrame.HeadDegrees) > 0.01f)
            {
                float sign = Mathf.Abs(_flinchFrame.LeanDegrees) > 0.01f ? Mathf.Sign(_flinchFrame.LeanDegrees) : 0.0f;
                float side = Mathf.Abs(_flinchFrame.TiltRightDegrees) > 0.01f ? Mathf.Sign(_flinchFrame.TiltRightDegrees) : 0.0f;
                Quaternion headLean = Quaternion.AngleAxis(sign * _flinchFrame.HeadDegrees * w, transform.right);
                Quaternion headTilt = Quaternion.AngleAxis(-side * _flinchFrame.HeadDegrees * w, transform.forward);
                _head.rotation = headLean * headTilt * _head.rotation;
            }
        }
    }
}
