using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// UNARMED STRIKES: the stance under them and their stride (see FistStrikes / FistPoseDriver; the blows themselves are baked poses), and
    /// the body turned to face a blow.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private FistFrame _fistFrame;

        private float _fistStride = 1.0f;

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

        /// <summary>
        /// How long the blow's stride really is, as a share of the clip's: a punch's clip plants its feet on ground the body is carried over,
        /// and a body stopped short has gone less far (FistPoseDriver). 1 leaves the clip's legs as they were made.
        /// </summary>
        public void SetFistStride(float share)
        {
            _fistStride = share;
        }

        /// <summary>The stride the legs are being drawn to (for tests).</summary>
        public float FistStride => _fistStride;

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

        /// <summary>
        /// After the baked pose: the legs of a punch's clip drawn in (or out) to the stride the body really took. Each foot keeps its height and
        /// its turn and goes <see cref="_fistStride"/> of its distance ahead of or behind the body; the knee bends for it.
        /// </summary>
        private void ApplyFistStride()
        {
            if (!_fistFrame.Active || _bakedPoseWeight <= 0.0f || BakedPoseIsUpperBodyOnly || Mathf.Abs(_fistStride - 1.0f) < 0.02f)
            {
                return;
            }
            float share = Mathf.Lerp(1.0f, _fistStride, _bakedPoseWeight);
            DrawFootToStride(_leftUpperLeg, _leftLowerLeg, _leftFoot, share);
            DrawFootToStride(_rightUpperLeg, _rightLowerLeg, _rightFoot, share);
        }

        private void DrawFootToStride(Transform thigh, Transform shin, Transform foot, float share)
        {
            if (thigh == null || shin == null || foot == null)
            {
                return;
            }
            Vector3 place = transform.InverseTransformPoint(foot.position);
            place.z *= share;
            Quaternion turn = foot.rotation;
            // The knee keeps bowing the way the clip had it.
            Vector3 knee = shin.position - (thigh.position + foot.position) * 0.5f;
            TwoBoneIK.Solve(thigh, shin, foot, transform.TransformPoint(place), knee.sqrMagnitude > 1e-6f ? knee : transform.forward);
            foot.rotation = turn;
        }
    }
}
