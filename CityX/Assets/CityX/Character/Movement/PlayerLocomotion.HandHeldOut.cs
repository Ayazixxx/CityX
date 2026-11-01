using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE RIGHT HAND HOLDING SOMETHING OUT, POINTED: a lit flashlight (PlayerLocomotion.Torch.cs) and a wand casting (PlayerLocomotion.Wand.cs).
    /// Between hanging at the side and held out along a line from the shoulder, the hand turned so what it holds runs along that line out past
    /// the thumb (the hand bone's +Z), the wrist as near straight as that allows.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>
        /// Held out, where the right hand goes: <paramref name="reach"/> along <paramref name="direction"/> from <paramref name="shoulder"/>, and
        /// <paramref name="holdOffset"/> across and up off that line (metres at the built height, scaled to the body).
        /// </summary>
        private Vector3 HeldOutHand(Vector3 shoulder, Vector3 direction, float reach, Vector3 holdOffset)
        {
            float scale = WorldHeight / BodyRig.StandingHeight;
            return shoulder + (direction * reach + transform.right * holdOffset.x + Vector3.up * holdOffset.y) * scale;
        }

        /// <summary>
        /// Poses the right arm: the hand from <paramref name="carryHand"/> (from the shoulder: across, up, forward) at <paramref name="raise"/> 0 to
        /// held out (<see cref="HeldOutHand"/>) at 1, and turned to point along <paramref name="direction"/>.
        /// </summary>
        private void HoldRightHandOut(Vector3 direction, float raise, Vector3 carryHand, float reach, Vector3 holdOffset)
        {
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 forward = transform.forward, right = transform.right, up = Vector3.up;
            Vector3 shoulder = _rightUpperArm.position;

            Vector3 held = HeldOutHand(shoulder, direction, reach, holdOffset);
            Vector3 hung = shoulder + (right * carryHand.x + up * carryHand.y + forward * carryHand.z) * scale;
            PoseRightHand(Vector3.Lerp(hung, held, raise), direction,
                Vector3.Lerp(right * 0.5f - forward * 0.4f + Vector3.down, right + Vector3.down * 0.6f, raise));
        }

        /// <summary>The right hand at <paramref name="hand"/>, the elbow toward <paramref name="elbowHint"/>, turned so what it holds runs along <paramref name="direction"/>.</summary>
        private void PoseRightHand(Vector3 hand, Vector3 direction, Vector3 elbowHint)
        {
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, hand, elbowHint, 1.0f);

            Vector3 alongArm = _handR.position - _rightLowerArm.position;
            Vector3 fingers = Vector3.ProjectOnPlane(alongArm, direction);
            if (fingers.sqrMagnitude < 1e-6f)
            {
                fingers = Vector3.down;
            }
            fingers.Normalize();
            _handR.rotation = Quaternion.LookRotation(direction, Vector3.Cross(direction, fingers));
        }
    }
}
