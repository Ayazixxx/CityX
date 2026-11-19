using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SUPPORT HAND. A two-handed weapon is held by the hand the animation places (the right) and steadied by the other, which is
    /// reached onto the weapon here every frame. See TwoBoneIK for why.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private Transform _supportAnchor;

        private Vector3 _supportLocalPoint;

        private float _supportBlend;

        private bool _supportOnlyWhenAiming;

        private Quaternion? _supportHandRotation;

        /// <summary>How far this frame's left hand is laid on the weapon by <see cref="_supportHandRotation"/>, 0 to 1: ApplyStraightWrists leaves it that bent.</summary>
        private float _supportHandLaid;

        /// <summary>How fast the support hand takes hold (and lets go), per second: a grab, not a snap.</summary>
        public const float SupportHandBlendPerSecond = 9.0f;

        /// <summary>The palm's centre, this far along the forearm from the wrist bone.</summary>
        public const float PalmOffsetMetres = 0.08f;

        /// <summary>
        /// Tells the rig to hold <paramref name="weapon"/> with the LEFT hand as well, palm at
        /// <paramref name="weaponLocalPoint"/> (the weapon's own space, so it follows the weapon through a swing).
        /// </summary>
        /// <param name="onlyWhenAiming">
        /// True for a gun: the free hand comes up to steady it only as the gun is raised to aim (or fire), and
        /// swings free while it is carried. False for a sword, held in both hands all the time.
        /// </param>
        /// <param name="handRotation">
        /// How the hand lies on the weapon, in the weapon's frame (a pistol's grip, the palm flat on its panel); null leaves the wrist to the arm.
        /// </param>
        public void SetSupportHand(Transform weapon, Vector3 weaponLocalPoint, bool onlyWhenAiming = false, Quaternion? handRotation = null)
        {
            _supportAnchor = weapon;
            _supportLocalPoint = weaponLocalPoint;
            _supportOnlyWhenAiming = onlyWhenAiming;
            _supportHandRotation = handRotation;
        }

        /// <summary>Lets go -- only if <paramref name="weapon"/> is still the weapon being held (a swap must not drop the new one).</summary>
        public void ClearSupportHand(Transform weapon)
        {
            if (_supportAnchor == weapon)
            {
                _supportAnchor = null;
                _supportSlides = false;
            }
        }

        private bool _supportSlides;

        private Vector3 _supportSlideAlong;

        private float _supportSlideNearest, _supportSlideFurthest;

        /// <summary>
        /// A SPEAR'S FRONT HAND (docs/SPEAR.md), set after <see cref="SetSupportHand"/>: the hand is not fixed to its point on the weapon but
        /// stays before its own shoulder (SpearRules.FrontHandFromShoulder) while the shaft slides through it, between
        /// <paramref name="nearest"/> and <paramref name="furthest"/> metres from the support point along <paramref name="alongLocal"/> (the
        /// weapon's own space). It is only on the shaft while a pose has both hands on the weapon (an attack, a guard: not the carry), and never
        /// with a shield in it.
        /// </summary>
        public void SlideSupportHand(Vector3 alongLocal, float nearest, float furthest)
        {
            _supportSlides = alongLocal.sqrMagnitude > 1e-6f;
            _supportSlideAlong = alongLocal.normalized;
            _supportSlideNearest = nearest;
            _supportSlideFurthest = Mathf.Max(nearest, furthest);
        }

        /// <summary>The support hand is wanted on the weapon now: always for a handle; a spear's front hand only in a two-handed pose with no shield.</summary>
        private bool SupportHandWanted => _supportAnchor != null && _leftPistol == null   // a hand with its own pistol holds no other gun
            && (!_supportSlides || (Shield == null && _swordFrame.Active && _swordFrame.TwoHanded));

        /// <summary>Where the support hand's palm goes on the weapon, world space: its point, or for a spear the point of the shaft nearest where the front hand stays.</summary>
        private Vector3 SupportGripPoint()
        {
            Vector3 point = _supportAnchor.TransformPoint(_supportLocalPoint);
            if (!_supportSlides || _leftUpperArm == null)
            {
                return point;
            }
            Vector3 along = _supportAnchor.TransformDirection(_supportSlideAlong).normalized;
            float scale = WorldHeight / BodyRig.StandingHeight;
            Vector3 stays = _leftUpperArm.position + transform.rotation * SpearRules.FrontHandFromShoulder * scale;
            float slid = Mathf.Clamp(Vector3.Dot(stays - point, along), _supportSlideNearest * scale, _supportSlideFurthest * scale);
            return point + along * slid;
        }

        /// <summary>The support hand's palm, in world space (for tests and the tuning console); null when nothing is held.</summary>
        public bool TryGetSupportPalm(out Vector3 palm)
        {
            palm = Vector3.zero;
            if (_handL == null || _leftLowerArm == null)
            {
                return false;
            }
            // A hand laid on the weapon may bend at the wrist: its palm is along its own fingers (the left hand bone's -X), not the forearm.
            Vector3 fingers = _supportHandRotation.HasValue ? -_handL.right : (_handL.position - _leftLowerArm.position).normalized;
            palm = _handL.position + fingers * PalmOffsetMetres;
            return true;
        }

        private void ApplySupportHand()
        {
            float deltaTime = PoseDeltaTime;
            _supportBlend = Mathf.MoveTowards(_supportBlend, SupportHandWanted ? 1.0f : 0.0f, SupportHandBlendPerSecond * deltaTime);
            // A baked move (the sword spin, Octaslash) lets go of the handle: it places the free arm itself (ApplyBakedPose).
            _supportHandLaid = 0.0f;
            float weight = _supportBlend * (_supportOnlyWhenAiming ? EasedGunAimBlend : 1.0f) * (1.0f - BakedArmsWeight);
            if (weight <= 0.0f || _supportAnchor == null || _leftUpperArm == null || _leftLowerArm == null || _handL == null)
            {
                return;
            }

            Vector3 point = SupportGripPoint();
            // Elbow down and a little out, the way a person braces a heavy weapon.
            Vector3 elbowHint = -transform.up - transform.right * 0.6f;

            // A HAND LAID ON THE WEAPON: its rotation is known, so the wrist's place is too, and the hand is turned onto it.
            if (_supportHandRotation.HasValue)
            {
                Quaternion hand = _supportAnchor.rotation * _supportHandRotation.Value;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, point + hand * Vector3.right * PalmOffsetMetres, elbowHint, weight);
                _handL.rotation = Quaternion.Slerp(_handL.rotation, hand, weight);
                WristTwist.ShareWithForearm(_leftLowerArm, _handL);
                _supportHandLaid = weight;
                return;
            }

            // The wrist bone is not the palm: aim the PALM at the point. Two passes, since turning the
            // forearm turns the palm's direction too.
            Quaternion upperStart = _leftUpperArm.rotation;
            Quaternion lowerStart = _leftLowerArm.rotation;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 palmDirection = (_handL.position - _leftLowerArm.position).normalized;
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, point - palmDirection * PalmOffsetMetres, elbowHint);
            }
            if (weight < 1.0f)
            {
                // Blend the finished solution in, rather than blending each pass (which would compound).
                _leftUpperArm.rotation = Quaternion.Slerp(upperStart, _leftUpperArm.rotation, weight);
                _leftLowerArm.rotation = Quaternion.Slerp(lowerStart, _leftLowerArm.rotation, weight);
            }

            // A HANDLE IS WRAPPED, NOT JUST TOUCHED (owner, 2026-10-16: "use inverse kinematic so the hands reach the correct grip points on each
            // weapon"). With a sword, a bat (anything held in both hands all the time) the left hand is TURNED onto the handle the way the right
            // hand holds it: the thumb up the handle toward the blade, the palm facing the side its forearm comes from (the handshake grip both
            // hands take on a two-handed handle), instead of keeping whatever wrist the walk clip gave it while a cut swings the handle round
            // through it. Turned about the wrist, so the palm stays on the grip point; the twist that needs goes into the forearm.
            if (!_supportOnlyWhenAiming)
            {
                Vector3 handle = _supportAnchor.up;
                Vector3 forearm = _leftLowerArm.position - _handL.position;
                Vector3 towardElbow = forearm - handle * Vector3.Dot(forearm, handle);
                // Only as far as the forearm comes at the handle from the side: run along it, which side the palm faces is undefined and would
                // flip from frame to frame.
                float across = forearm.sqrMagnitude > 1e-8f ? towardElbow.magnitude / forearm.magnitude : 0.0f;
                float wrapWeight = weight * Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.25f, 0.6f, across));
                if (wrapWeight > 0.0f)
                {
                    // The left hand bone: -Y is the palm, +Z the thumb, +X runs back up the arm (HandGrip).
                    Quaternion wrap = Quaternion.LookRotation(handle, Vector3.Cross(handle, towardElbow.normalized));
                    _handL.rotation = Quaternion.Slerp(_handL.rotation, wrap, wrapWeight);
                    WristTwist.ShareWithForearm(_leftLowerArm, _handL, ref _supportWristTwist);
                }
                else
                {
                    _supportWristTwist = 0.0f;
                }
            }
        }
    }
}
