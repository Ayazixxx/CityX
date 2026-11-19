using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A PAIR OF PISTOLS, one in each hand (Dual Pistols, owner 2026-11-19: docs/DUAL_PISTOLS.md). Carried, each hangs in its hand where the walk
    /// puts it. A shot from either raises both together, for the same half second a single pistol's shot does (NotifyGunFired): the chest square
    /// on, each gun before its own shoulder (DualPistolHold.RightGunInChest, mirrored), both barrels on the crosshair's point, each arm solved
    /// onto its own gun's grip. Neither hand reaches for the other's gun.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private Transform _leftPistol;
        private float _leftPistolBoreRise;
        private Vector3 _leftPistolHoldPosition;
        private Quaternion _leftPistolHoldRotation = Quaternion.identity;
        private IHandGripStyle _leftPistolGrip;

        /// <summary>The gun in the left hand, the second of a pair (null with none).</summary>
        public Transform LeftPistol => _leftPistol;

        /// <summary>The gun in the right hand, when it is held as a pistol (null otherwise).</summary>
        public Transform RightPistol => _pistolGun;

        /// <summary>A pistol in each hand.</summary>
        public bool HoldsPistolPair => _pistolGun != null && _leftPistol != null;

        /// <summary>The left hand holds the second pistol of a pair, its muzzle at <paramref name="muzzleLocal"/>, at the given hold in the LEFT hand bone's frame.</summary>
        public void SetLeftPistol(Transform gun, Vector3 muzzleLocal, Vector3 holdPosition, Quaternion holdRotation)
        {
            _leftPistol = gun;
            _leftPistolGrip = gun != null ? gun.GetComponent<IHandGripStyle>() : null;
            _leftPistolBoreRise = muzzleLocal.y;
            _leftPistolHoldPosition = holdPosition;
            _leftPistolHoldRotation = holdRotation;
        }

        public void ClearLeftPistol(Transform gun)
        {
            if (gun == null || _leftPistol == gun)
            {
                _leftPistol = null;
                _leftPistolGrip = null;
            }
        }

        private void ApplyPistolPairPose(Vector3 aimDirection)
        {
            if (_spine == null || _leftUpperArm == null || _leftLowerArm == null || _handL == null || _handR == null)
            {
                return;
            }
            float w = EasedGunAimBlend;
            Vector3 up = transform.up;
            Vector3 chestCentre = 0.5f * (_rightUpperArm.position + _leftUpperArm.position);
            Quaternion swing = Quaternion.FromToRotation(transform.forward, aimDirection);
            Quaternion chest = Quaternion.LookRotation(transform.forward, up);
            Vector3 right = chestCentre + swing * (chest * DualPistolHold.RightGunInChest);
            Vector3 left = chestCentre + swing * (chest * DualPistolHold.Mirror(DualPistolHold.RightGunInChest));
            HoldGunAt(_rightUpperArm, _rightLowerArm, _handR, right, aimDirection, _pistolBoreRise, _pistolHoldPosition, _pistolHoldRotation,
                -up + transform.right * 0.5f, w);
            HoldGunAt(_leftUpperArm, _leftLowerArm, _handL, left, aimDirection, _leftPistolBoreRise, _leftPistolHoldPosition, _leftPistolHoldRotation,
                -up - transform.right * 0.5f, w);
        }
    }
}
