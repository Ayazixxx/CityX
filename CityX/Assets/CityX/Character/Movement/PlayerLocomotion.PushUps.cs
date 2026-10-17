using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE HANDS ON THE FLOOR IN A PUSH-UP (/pushups, 2026-10-15). The push-up clips were made for Mixamo's body; on this one, whose arms are
    /// long for its legs, the plank came out half a metre low: at the top of each push-up the straightened arms reached 0.5 m through the
    /// floor while the shoulders barely rose (measured in PushUpCapture: shoulders 0.25 to 0.36 m, wrists down to -0.5). So while a push-up
    /// phase plays, after the clips have posed the body, the whole body is tipped up about its lowest leg contact (the toes in the plank, the
    /// knees kneeling on the way down) by just enough to put the lower hand's palm back on the floor: at the top of a push-up the body rises
    /// round the toes, at the bottom it is left alone -- an incline push-up's arc, with the feet where they were. Runs on other players'
    /// copies too (the phase is sent).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How far above the floor the wrist sits when the palm is on it, metres (the palm's half thickness and a little).</summary>
        public const float PushUpWristClearance = FusedBodyShape.PalmHalfThickness + 0.02f;

        /// <summary>The most the body is tipped up, degrees: a guard against a hand reaching far below for some other reason.</summary>
        public const float PushUpMaxTipDegrees = 35.0f;

        /// <summary>The last tip applied, degrees (for tests).</summary>
        public float PushUpTipDegrees { get; private set; }

        private Transform _handLeftBone;
        private Transform _handRightBone;
        private Transform _footLeftBone;
        private Transform _footRightBone;
        private Transform _kneeLeftBone;
        private Transform _kneeRightBone;

        private void ApplyPushUpGrounding()
        {
            PushUpTipDegrees = 0.0f;
            if (_hips == null || _animator == null || !PushUpOwnsTheBody)
            {
                return;
            }
            if (_handLeftBone == null)
            {
                _handLeftBone = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
                _handRightBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                _footLeftBone = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _footRightBone = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                _kneeLeftBone = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _kneeRightBone = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            if (_handLeftBone == null || _handRightBone == null || _footLeftBone == null || _footRightBone == null)
            {
                return;
            }

            float floor = transform.position.y - (_controller != null ? _controller.skinWidth : 0.0f);
            float lowestHand = Mathf.Min(_handLeftBone.position.y, _handRightBone.position.y);
            float deficit = floor + PushUpWristClearance - lowestHand;
            if (deficit <= 0.0f)
            {
                return;   // the hands are on the floor or above it (going down, standing up): the clip's own pose
            }

            // The pivot: the lowest of the feet and the knees (the toes in the plank, the knees while kneeling), on the floor.
            Vector3 pivot = _footLeftBone.position;
            foreach (Transform t in new[] { _footRightBone, _kneeLeftBone, _kneeRightBone })
            {
                if (t != null && t.position.y < pivot.y)
                {
                    pivot = t.position;
                }
            }
            pivot.y = Mathf.Min(pivot.y, floor);

            Vector3 hands = (_handLeftBone.position + _handRightBone.position) * 0.5f;
            Vector3 reach = hands - pivot;
            if (new Vector2(reach.x, reach.z).sqrMagnitude < 0.04f)
            {
                return;   // the hands right over the pivot: tipping cannot lift them
            }
            Quaternion tip = Quaternion.FromToRotation(reach, reach + Vector3.up * deficit);
            tip.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > PushUpMaxTipDegrees)
            {
                tip = Quaternion.AngleAxis(PushUpMaxTipDegrees, axis);
                degrees = PushUpMaxTipDegrees;
            }
            _hips.SetPositionAndRotation(pivot + tip * (_hips.position - pivot), tip * _hips.rotation);
            PushUpTipDegrees = degrees;
        }
    }
}
