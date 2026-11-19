using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SHIELD ON THE BODY (docs/SHIELD.md). It hangs from the left fist by a centre grip (the fingers close round it: the hand holds
    /// something). Lowered, it rides at the side, its face out, wherever the arm swings. Raised (the ShieldRaised parameter: this body's
    /// right-click, or another player's, synced), the left arm brings it up before the chest, face forward, the elbow out to the side, the
    /// fist hidden behind the boss -- eased in over <see cref="ShieldRaiseSeconds"/>. The body walks at
    /// <see cref="ShieldMoveSpeedShare"/> of its speed with it up. Placed after the other arm poses, so a raised shield has the left arm.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the shield takes to come up (and go down), seconds.</summary>
        public const float ShieldRaiseSeconds = 0.12f;

        /// <summary>The speed a body walks at with the round shield up, as a share of its own (a held guard's half).</summary>
        public const float ShieldMoveSpeedShare = 0.5f;

        /// <summary>What kind of shield it is (set by the shield with <see cref="Shield"/>): how it is held up, and how fast the body walks with it up.</summary>
        public ShieldTraits ShieldTraits { get; set; } = ShieldTraits.Round;


        /// <summary>From the wrist to the middle of the closed fist, along the forearm's line, metres (the body at its built height).</summary>
        public const float ShieldGripReach = 0.09f;


        private static readonly int ShieldRaisedParameter = Animator.StringToHash("ShieldRaised");

        private float _shieldRaise;

        /// <summary>The shield in this body's left hand, or null (set by the shield).</summary>
        public Transform Shield { get; set; }

        /// <summary>How far up the shield is, 0 lowered .. 1 raised.</summary>
        public float ShieldRaise => _shieldRaise;

        /// <summary>The shield up, or going up: the synced parameter, on this body and on other players' copies.</summary>
        private bool ShieldWanted => Shield != null && _animator != null && _animator.runtimeAnimatorController != null && _animator.GetBool(ShieldRaisedParameter);

        private void ApplyShieldPose()
        {
            if (Shield == null || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                _shieldRaise = 0.0f;
                return;
            }
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _shieldRaise = Mathf.MoveTowards(_shieldRaise, ShieldWanted ? 1.0f : 0.0f, deltaTime / ShieldRaiseSeconds);
            float raise = _shieldRaise * _shieldRaise * (3.0f - 2.0f * _shieldRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 forward = transform.forward, right = transform.right, up = Vector3.up;

            // A tall shield is carried with the arm bent, or it would drag through the ground: the hand goes from there up to the guard.
            bool carried = ShieldTraits.CarriedBent;
            if (raise > 0.0f || carried)
            {
                Vector3 raisedHand = carried ? Vector3.Lerp(ShieldTraits.CarriedHand, ShieldTraits.RaisedHand, raise) : ShieldTraits.RaisedHand;
                Vector3 hand = _leftUpperArm.position + (forward * raisedHand.z + right * raisedHand.x + up * raisedHand.y) * scale;
                // Carried, the elbow is back behind the hand, the forearm along the shield's back; raised, out to the side.
                Vector3 elbowOut = -right + Vector3.down * 0.6f;
                Vector3 elbowWay = carried ? Vector3.Slerp(-forward + Vector3.down * 0.3f, elbowOut, raise) : elbowOut;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, hand, elbowWay, carried ? 1.0f : raise);
            }
            // A centre grip holds the fist in line with the forearm: one bend in the arm, as an empty hand has (ApplyStraightWrists leaves a holding hand be).
            _handL.localRotation = Quaternion.Slerp(Quaternion.identity, _handL.localRotation, BakedArmsWeight);

            Vector3 elbow = _leftLowerArm.position, wrist = _handL.position;
            Vector3 alongArm = (elbow - wrist).sqrMagnitude > 1e-6f ? (elbow - wrist).normalized : up;
            Vector3 fist = wrist - alongArm * (ShieldGripReach * scale);
            Vector3 loweredUp = carried ? up : Vector3.ProjectOnPlane(alongArm, right);
            Quaternion lowered = Quaternion.LookRotation(-right, loweredUp.sqrMagnitude > 1e-6f ? loweredUp : up);
            Quaternion raised = Quaternion.LookRotation(forward, up);
            Quaternion facing = Quaternion.Slerp(lowered, raised, raise);
            // A tall shield is gripped above its middle: it hangs mostly below the fist.
            Vector3 middle = fist + facing * (Vector3.forward * ShieldTraits.StandOff + Vector3.down * ShieldTraits.GripAboveMiddle) * scale;
            Shield.SetPositionAndRotation(middle, facing);
        }
    }
}
