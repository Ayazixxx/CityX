using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A PISTOL, HELD IN BOTH HANDS (owner, 2026-11-03: "he doesnt grip the glock properly", "make it hold it properly"). As with the rifle (PlayerLocomotion.RifleAim.cs) the
    /// GUN is placed and the arms follow it: raised to aim, the chest turns a little to the right, the gun goes before the shoulder, beside the head, its barrel
    /// at the crosshair's point, the shooting hand is solved onto the grip and ApplySupportHand brings the other hand round it, both elbows bent. The arm used
    /// to be swung straight out from the shoulder until the barrel pointed true, which left the free arm out of reach of the gun and the gun level with the face.
    ///
    /// The numbers match the reload's (CityX/SourceArt/Glock/glock_reload.py: READY_GUN, READY), so a reload starts and ends where the aim is.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The chest's turn to the right while a pistol is raised, degrees: it brings the free shoulder forward, and the gun out from behind the head.</summary>
        public const float PistolBladeDegrees = 15.0f;

        /// <summary>Where the gun's origin (the web of its grip) goes, from between the shoulders, in the chest's frame: out past the side of the head and over the shoulder, where the camera sees it, in reach of both arms.</summary>
        public static readonly Vector3 PistolInChest = new Vector3(0.24f, 0.05f, 0.5f);

        /// <summary>What the arm poses ease by: the cadence Tick is driven at, or on another player's copy, which nothing ticks, the frame's.</summary>
        private float PoseDeltaTime => _lastTickDeltaTime > 0.0f ? _lastTickDeltaTime : Time.deltaTime;

        /// <summary>True while the gun in hand is held as a pistol (the aim camera frames it closer: ThirdPersonCamera.PistolAim).</summary>
        public bool HoldsPistol => _pistolGun != null;

        private Transform _pistolGun;
        private float _pistolBoreRise;
        private Vector3 _pistolHoldPosition;
        private Quaternion _pistolHoldRotation = Quaternion.identity;

        /// <summary>The gun in the right hand is a pistol, its muzzle at <paramref name="muzzleLocal"/>, held at the given hold in the hand bone's frame.</summary>
        public void SetPistol(Transform gun, Vector3 muzzleLocal, Vector3 holdPosition, Quaternion holdRotation)
        {
            _pistolGun = gun;
            _pistolBoreRise = muzzleLocal.y;
            _pistolHoldPosition = holdPosition;
            _pistolHoldRotation = holdRotation;
        }

        public void ClearPistol(Transform gun)
        {
            if (gun == null || _pistolGun == gun)
            {
                _pistolGun = null;
            }
        }

        private void ApplyPistolPose(Vector3 aimDirection)
        {
            if (_spine == null || _leftUpperArm == null || _handR == null)
            {
                return;
            }
            float w = EasedGunAimBlend;
            Vector3 up = transform.up;
            _spine.rotation = Quaternion.AngleAxis(PistolBladeDegrees * w, up) * _spine.rotation;

            // Swung with the aim about the chest, so it stays before the body looking up or down.
            Vector3 chestCentre = 0.5f * (_rightUpperArm.position + _leftUpperArm.position);
            Vector3 gunPosition = chestCentre + Quaternion.FromToRotation(transform.forward, aimDirection) * (_spine.rotation * PistolInChest);
            HoldGunAt(_rightUpperArm, _rightLowerArm, _handR, gunPosition, aimDirection, _pistolBoreRise, _pistolHoldPosition, _pistolHoldRotation,
                -up + transform.right * 0.5f, w);
        }

        /// <summary>
        /// One arm onto a gun placed at <paramref name="gunPosition"/> (its grip's web), its barrel at the crosshair's point: the bore, which runs
        /// above the web, through it. The gun hangs off the hand at its hold, so the hand goes where the hold puts it.
        /// </summary>
        private void HoldGunAt(Transform upperArm, Transform lowerArm, Transform hand, Vector3 gunPosition, Vector3 aimDirection, float boreRise,
            Vector3 holdPosition, Quaternion holdRotation, Vector3 elbowHint, float w)
        {
            Vector3 up = transform.up;
            Vector3 forward = aimDirection;
            if (_hasAimedPoint)
            {
                forward = Vector3.RotateTowards(transform.forward, (_aimedPoint - (gunPosition + up * boreRise)).normalized, GunAimPitchClampDegrees * Mathf.Deg2Rad, 0.0f);
            }
            Vector3 gunUp = Vector3.ProjectOnPlane(up, forward);
            if (gunUp.sqrMagnitude < 1e-4f)
            {
                gunUp = Vector3.ProjectOnPlane(-transform.forward, forward);
            }
            Quaternion gunRotation = Quaternion.LookRotation(forward, gunUp.normalized);
            Quaternion handRotation = gunRotation * Quaternion.Inverse(holdRotation);
            Vector3 handPosition = gunPosition - handRotation * holdPosition;
            TwoBoneIK.Solve(upperArm, lowerArm, hand, handPosition, elbowHint, w);
            hand.rotation = Quaternion.Slerp(hand.rotation, handRotation, w);
            WristTwist.ShareWithForearm(lowerArm, hand);
        }
    }
}
