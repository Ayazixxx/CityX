using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A FLASHLIGHT IN THE RIGHT HAND (docs/FLASHLIGHT.md). Lit, the arm holds it out before the shoulder and points it where the player looks
    /// (<see cref="TorchAimPoint"/>, the crosshair's point), following a little late as a held light does in Alan Wake or Resident Evil; off,
    /// it is carried low at the side, pointing ahead and down. The light sits in the fist, its head out past the thumb (the hand's +Z), so
    /// turning the hand points the beam. Another player's copy is told the same by its owner's FlashlightSync.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the light takes to come up when lit (and go back down), seconds.</summary>
        public const float TorchRaiseSeconds = 0.15f;

        /// <summary>How quickly the held light swings onto where the player looks: this many times the gap closes a second (a little late).</summary>
        public const float TorchFollowSharpness = 12.0f;

        /// <summary>Lit, where the hand goes from the right shoulder along the aim, and how far down and in from that line, metres (built height).</summary>
        public const float TorchReach = 0.55f;

        public static readonly Vector3 TorchHoldOffset = new Vector3(-0.06f, -0.14f, 0.0f);

        /// <summary>Off, where the hand hangs from the right shoulder (across, up, forward), and how far below ahead the light points, degrees.</summary>
        public static readonly Vector3 TorchCarryHand = new Vector3(0.2f, -0.68f, 0.2f);

        public const float TorchCarryDipDegrees = 35.0f;

        private float _torchRaise;
        private Vector3 _torchDirection;

        /// <summary>The flashlight in this body's right hand, or null (set by the flashlight).</summary>
        public Transform Torch { get; set; }

        /// <summary>The light is on: held up and pointed (set by the flashlight; on a copy, by its owner's word).</summary>
        public bool TorchLit { get; set; }

        /// <summary>Where the lit light points: the crosshair's point (on a copy, its owner's, sent).</summary>
        public Vector3 TorchAimPoint { get; set; }

        /// <summary>The way the held light points this frame (after its lag).</summary>
        public Vector3 TorchDirection => _torchDirection;

        private void ApplyTorchPose()
        {
            if (Torch == null || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                _torchRaise = 0.0f;
                _torchDirection = Vector3.zero;
                return;
            }
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _torchRaise = Mathf.MoveTowards(_torchRaise, TorchLit ? 1.0f : 0.0f, deltaTime / TorchRaiseSeconds);
            float raise = _torchRaise * _torchRaise * (3.0f - 2.0f * _torchRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 forward = transform.forward, right = transform.right, up = Vector3.up;
            Vector3 shoulder = _rightUpperArm.position;

            Vector3 carried = Quaternion.AngleAxis(TorchCarryDipDegrees, right) * forward;
            Vector3 aimed = TorchAimPoint - shoulder;
            aimed = aimed.sqrMagnitude > 0.01f ? aimed.normalized : forward;
            Vector3 wanted = Vector3.Slerp(carried, aimed, raise).normalized;
            _torchDirection = _torchDirection.sqrMagnitude < 0.5f
                ? wanted
                : Vector3.Slerp(_torchDirection, wanted, 1.0f - Mathf.Exp(-TorchFollowSharpness * deltaTime)).normalized;

            Vector3 held = shoulder + (_torchDirection * TorchReach + right * TorchHoldOffset.x + up * TorchHoldOffset.y) * scale;
            Vector3 hung = shoulder + (right * TorchCarryHand.x + up * TorchCarryHand.y + forward * TorchCarryHand.z) * scale;
            Vector3 hand = Vector3.Lerp(hung, held, raise);
            Vector3 elbowHint = Vector3.Lerp(right * 0.5f - forward * 0.4f + Vector3.down, right + Vector3.down * 0.6f, raise);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, hand, elbowHint, 1.0f);

            // The hand bone's +Z runs out past the thumb, where the light's head is: turn it along the beam, the wrist as near straight as that allows.
            Vector3 alongArm = _handR.position - _rightLowerArm.position;
            Vector3 fingers = Vector3.ProjectOnPlane(alongArm, _torchDirection);
            if (fingers.sqrMagnitude < 1e-6f)
            {
                fingers = Vector3.down;
            }
            fingers.Normalize();
            _handR.rotation = Quaternion.LookRotation(_torchDirection, Vector3.Cross(_torchDirection, fingers));
        }
    }
}
