using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A STAFF IN THE RIGHT HAND (docs/ARCANE_ORB.md). The staff sits in the fist, its shaft through the closed fingers past the thumb, and the
    /// body turns the hand so the shaft stands where it should: carried, upright at the side with the forearm forward; casting (charging a shot,
    /// or just after one, <see cref="StaffCasting"/>), the arm thrust out before the chest and the staff's head tipped toward where the body
    /// faces. Eased over <see cref="StaffRaiseSeconds"/>. Another player's copy is told the same by the caster's own replay. docs/STAFFS.md.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the staff takes to come up into the cast (and go back down), seconds.</summary>
        public const float StaffRaiseSeconds = 0.1f;

        /// <summary>Carried, where the right hand goes, from the right shoulder, metres (the body at its built height): across, up, forward.</summary>
        public static readonly Vector3 StaffCarryHand = new Vector3(0.22f, -0.62f, 0.28f);

        /// <summary>Casting, where the right hand goes: out before the chest, the arm almost straight.</summary>
        public static readonly Vector3 StaffCastHand = new Vector3(0.05f, -0.08f, 0.75f);

        /// <summary>How far forward of upright the staff leans, carried and casting, degrees.</summary>
        public const float StaffCarryLeanDegrees = 6.0f;

        public const float StaffCastLeanDegrees = 25.0f;

        /// <summary>How long one <see cref="HoldStaffOut"/> keeps the staff out, seconds: a caster calls it every frame it wants the staff out.</summary>
        public const float StaffHoldSeconds = 0.1f;

        private float _staffRaise;
        private float _staffHold;

        /// <summary>The staff in this body's right hand, or null (set by the staff).</summary>
        public Transform Staff { get; set; }

        /// <summary>Charging a shot or just after one (a caster holds the staff out, on this body and on copies).</summary>
        public bool StaffCasting => _staffHold > 0.0f;

        /// <summary>Keeps the staff thrust out a moment longer: whichever spell is charging or has just shot calls it every frame.</summary>
        public void HoldStaffOut()
        {
            _staffHold = StaffHoldSeconds;
        }

        /// <summary>How far into the cast the staff is, 0 carried .. 1 casting.</summary>
        public float StaffRaise => _staffRaise;

        private void ApplyStaffPose()
        {
            if (Staff == null || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                _staffRaise = 0.0f;
                return;
            }
            bool wanted = StaffCasting || _castPoseFraction > 0.0f;
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _staffHold = Mathf.Max(0.0f, _staffHold - deltaTime);
            _staffRaise = Mathf.MoveTowards(_staffRaise, wanted ? 1.0f : 0.0f, deltaTime / StaffRaiseSeconds);
            float raise = _staffRaise * _staffRaise * (3.0f - 2.0f * _staffRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 forward = transform.forward, right = transform.right, up = Vector3.up;

            Vector3 offset = Vector3.Lerp(StaffCarryHand, StaffCastHand, raise);
            Vector3 hand = _rightUpperArm.position + (right * offset.x + up * offset.y + forward * offset.z) * scale;
            Vector3 elbowHint = Vector3.Lerp(right * 0.6f - forward * 0.5f + Vector3.down, right + Vector3.down, raise);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, hand, elbowHint, 1.0f);

            // The hand bone's +Z runs out past the thumb, where a gripped shaft goes, and its +X on down the forearm: turn it so the shaft
            // stands as wanted with the wrist as near straight as that allows.
            float lean = Mathf.Lerp(StaffCarryLeanDegrees, StaffCastLeanDegrees, raise);
            Vector3 shaft = Quaternion.AngleAxis(lean, right) * up;
            Vector3 alongArm = _handR.position - _rightLowerArm.position;
            Vector3 fingers = Vector3.ProjectOnPlane(alongArm, shaft);
            if (fingers.sqrMagnitude < 1e-6f)
            {
                fingers = forward;
            }
            fingers.Normalize();
            _handR.rotation = Quaternion.LookRotation(shaft, Vector3.Cross(shaft, fingers));
        }
    }
}
