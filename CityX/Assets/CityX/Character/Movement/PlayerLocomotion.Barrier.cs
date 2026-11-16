using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOLDING UP ARCANE BARRIER (docs/ARCANE_BARRIER.md): both arms pushed out before the chest, the hands close together, palms out against the
    /// wall and fingers up, as though holding it there. Eased in over <see cref="BarrierRaiseSeconds"/>; after the other arm poses and the straight
    /// wrists, so the barrier has both arms. The caster calls <see cref="HoldBarrierUp"/> every frame the wall stands, on this body and on other
    /// players' copies alike.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the arms take to come up (and go down), seconds.</summary>
        public const float BarrierRaiseSeconds = 0.12f;

        /// <summary>
        /// Where the right hand goes, from the right shoulder, metres (the body at its built height): across (toward the middle), up, forward. The
        /// left mirrors it.
        /// </summary>
        public static readonly Vector3 BarrierHand = new Vector3(-0.13f, -0.15f, 0.58f);

        /// <summary>How far back from straight up the fingers lean, degrees: a hand pushed flat against something.</summary>
        public const float BarrierFingerLeanDegrees = 15.0f;

        /// <summary>How long one <see cref="HoldBarrierUp"/> keeps the arms out, seconds.</summary>
        public const float BarrierHoldSeconds = 0.1f;

        private float _barrierRaise;
        private float _barrierHold;

        /// <summary>Keeps the arms out against the barrier a moment longer: its caster calls it every frame the wall stands.</summary>
        public void HoldBarrierUp()
        {
            _barrierHold = BarrierHoldSeconds;
        }

        /// <summary>How far the arms are out, 0 down .. 1 holding the barrier.</summary>
        public float BarrierRaise => _barrierRaise;

        private void ApplyBarrierPose()
        {
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            bool wanted = _barrierHold > 0.0f;
            _barrierHold = Mathf.Max(0.0f, _barrierHold - deltaTime);
            _barrierRaise = Mathf.MoveTowards(_barrierRaise, wanted ? 1.0f : 0.0f, deltaTime / BarrierRaiseSeconds);
            if (_barrierRaise <= 0.0f || _rightUpperArm == null || _rightLowerArm == null || _handR == null
                || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                return;
            }
            float raise = _barrierRaise * _barrierRaise * (3.0f - 2.0f * _barrierRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 forward = transform.forward, right = transform.right, up = Vector3.up;
            Vector3 fingers = Quaternion.AngleAxis(-BarrierFingerLeanDegrees, right) * up;
            HoldOut(_rightUpperArm, _rightLowerArm, _handR, right, forward, fingers, scale, raise, rightHand: true);
            HoldOut(_leftUpperArm, _leftLowerArm, _handL, -right, forward, fingers, scale, raise, rightHand: false);
        }

        /// <summary>One arm out to the barrier: the hand before the chest (<paramref name="outward"/> is the arm's own side), palm forward, fingers up.</summary>
        private static void HoldOut(Transform upper, Transform lower, Transform hand, Vector3 outward, Vector3 forward, Vector3 fingers, float scale,
            float raise, bool rightHand)
        {
            Vector3 target = upper.position + (outward * BarrierHand.x + Vector3.up * BarrierHand.y + forward * BarrierHand.z) * scale;
            HoldPalmOut(upper, lower, hand, target, outward + Vector3.down * 0.8f, forward, fingers, raise, rightHand);
        }
    }
}
