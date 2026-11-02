using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ORIENTATION WARPING (OrientationWarp; docs/MOVEMENT.md): the Animator walks the nearest straight way, and here, after it, the hips and legs
    /// are turned toward where the body really travels while the chest is turned back onto the facing. From the shown way (the Animator's synced
    /// parameters) and the body's measured motion, so another player's copy warps the same.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private readonly OrientationWarp _orientationWarp = new OrientationWarp();

        private Vector3 _measuredVelocity;

        private float _warpDegrees;

        /// <summary>How far the hips are turned toward the travel now, degrees; for tests.</summary>
        public float WarpDegrees => _warpDegrees;

        private void ApplyOrientationWarp()
        {
            float target = 0.0f;
            bool warping = _hips != null && _spine != null && !InCarSeat && !IsSitting && !SwordPoseActive && _bakedPoseWeight <= 0.0f
                && _animator.GetBool(GroundedParameter);
            if (warping)
            {
                var shown = new Vector2(_animator.GetFloat(MoveXParameter), _animator.GetFloat(MoveZParameter));
                Vector3 local = transform.InverseTransformDirection(_measuredVelocity);
                target = OrientationWarp.DegreesFor(shown * SizeSpeedFactor, new Vector2(local.x, local.z));
            }
            _warpDegrees = OrientationWarp.Follow(_warpDegrees, target, Time.deltaTime);
            if (!warping || Mathf.Abs(_warpDegrees) < 0.01f)
            {
                return;
            }
            Quaternion turn = Quaternion.AngleAxis(_warpDegrees, transform.up);
            _hips.rotation = turn * _hips.rotation;
            _spine.rotation = Quaternion.Inverse(turn) * _spine.rotation;
        }
    }
}
