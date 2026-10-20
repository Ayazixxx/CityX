using UnityEngine;

namespace CityX.Character
{
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The torso the arms are kept out of: its own radius and most of an arm's (the elbow's skin, not its bone, must clear it).</summary>
        private const float ArmClearRadius = FusedBodyShape.TorsoRadius + FusedBodyShape.ArmRadius * 0.8f;

        /// <summary>
        /// While a sword is posed, each arm holding it is turned at the shoulder, round its own shoulder-to-hand line, until the elbow and both
        /// bones are out of the torso (ElbowClearance): the hands stay on the grip, the elbows go round the body instead of through it.
        /// </summary>
        private void KeepArmsOutOfTorso()
        {
            if (!SwordPoseActive || _hips == null || _neck == null)
            {
                return;
            }
            float weight = Mathf.Clamp01(_swordFrame.Weight);
            KeepArmOutOfTorso(_rightUpperArm, _rightLowerArm, _handR, weight);
            if (_swordFrame.TwoHanded && _supportAnchor != null)
            {
                KeepArmOutOfTorso(_leftUpperArm, _leftLowerArm, _handL, weight);
            }
        }

        private void KeepArmOutOfTorso(Transform upper, Transform lower, Transform hand, float weight)
        {
            if (upper == null || lower == null || hand == null)
            {
                return;
            }
            Vector3 elbow = lower.position;
            Vector3 clear = ElbowClearance.Elbow(upper.position, elbow, hand.position, _hips.position, _neck.position, ArmClearRadius);
            if ((clear - elbow).sqrMagnitude < 1e-8f)
            {
                return;
            }
            Quaternion handRotation = hand.rotation;
            TwoBoneIK.Solve(upper, lower, hand, hand.position, clear - upper.position, weight);
            hand.rotation = handRotation;
        }
    }
}
