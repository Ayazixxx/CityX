using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A RIFLE, HELD LIKE ONE (owner, 2026-10-14: the AK-47). A pistol is aimed by bending the arm until the barrel points at the target
    /// (ApplyGunAimPose); a rifle is held by the STOCK IN THE SHOULDER POCKET with both hands on it, so here the GUN is placed and the arms
    /// follow it: the torso turns 30 degrees to the right (the bladed rifle stance, which brings the support shoulder forward so that hand
    /// reaches the handguard), the butt goes to the pocket in front of the right shoulder, the barrel points at the crosshair's point (the same
    /// smoothed point the pistol uses), the shooting hand is solved onto the pistol grip (the gun hangs off it, so it lands where it was
    /// placed), and ApplySupportHand puts the other hand under the handguard. Carried, it sits at LOW READY: the butt lower in the shoulder,
    /// the muzzle down and a little in; raised to aim or to fire, it comes up to the shoulder in about a seventh of a second.
    ///
    /// The numbers match the reload's (CityX/SourceArt/AK47/ak47_reload.py: BLADE, POCKET), so a reload starts and ends where the aim is.
    /// Runs on a network copy too (the gun's MountCosmetic sets it), raised by the copy's synced Aiming parameter.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The torso's turn to the right in the rifle stance, degrees.</summary>
        public const float RifleBladeDegrees = 30.0f;

        /// <summary>Where the butt sits shouldered, from the right shoulder joint, in the bladed chest's frame: a little inboard, down and forward.</summary>
        public static readonly Vector3 RifleShoulderPocket = new Vector3(-0.10f, -0.06f, 0.10f);

        /// <summary>Where it sits at low ready: lower and further in.</summary>
        public static readonly Vector3 RifleLowReadyPocket = new Vector3(-0.13f, -0.17f, 0.13f);

        /// <summary>At low ready the muzzle points this far down and this far in (to the left), degrees.</summary>
        public const float RifleLowReadyPitchDegrees = 28.0f;

        public const float RifleLowReadyYawDegrees = 10.0f;

        /// <summary>How fast the rifle comes up to the shoulder (and down again), per second; faster for a hip-fired shot.</summary>
        public const float RifleShoulderPerSecond = 7.0f;

        public const float RifleFireShoulderPerSecond = 16.0f;

        /// <summary>How fast the rifle hold takes over when the rifle is drawn, per second.</summary>
        public const float RifleHoldPerSecond = 5.0f;

        private Transform _rifleGun;
        private Vector3 _rifleButt;
        private float _rifleBoreRise;
        private Vector3 _rifleHoldPosition;
        private Quaternion _rifleHoldRotation = Quaternion.identity;
        private float _rifleShoulder;
        private float _rifleHold;
        private Ragdoll _rifleRagdoll;
        private bool _rifleRagdollLooked;

        /// <summary>True while the gun in hand is held as a rifle.</summary>
        public bool HoldsRifle => _rifleGun != null;

        /// <summary>0 (low ready) to 1 (shouldered), as an S-curve.</summary>
        public float RifleShoulderBlend => Mathf.SmoothStep(0.0f, 1.0f, _rifleShoulder);

        /// <summary>True while the player is aiming a gun (right mouse), as the last Tick said.</summary>
        public bool IsAimingGun => _isAimingGun;

        /// <summary>
        /// The gun in the right hand is a rifle: <paramref name="buttLocal"/> is its butt in its own frame, <paramref name="muzzleLocal"/> its
        /// muzzle (the bore's height over the butt is aimed, not the butt), and the hold is where it sits in the hand bone's frame.
        /// </summary>
        public void SetRifle(Transform gun, Vector3 buttLocal, Vector3 muzzleLocal, Vector3 holdPosition, Quaternion holdRotation)
        {
            if (_rifleGun != gun)
            {
                _rifleHold = 0.0f;
                _rifleShoulder = 0.0f;
            }
            _rifleGun = gun;
            _rifleButt = buttLocal;
            _rifleBoreRise = muzzleLocal.y - buttLocal.y;
            _rifleHoldPosition = holdPosition;
            _rifleHoldRotation = holdRotation;
        }

        public void ClearRifle(Transform gun)
        {
            if (gun == null || _rifleGun == gun)
            {
                _rifleGun = null;
                _rifleHold = 0.0f;
                _rifleShoulder = 0.0f;
            }
        }

        /// <summary>
        /// THE AIM POINT, SMOOTHED IN DEPTH ONLY: on the camera's ray (so the mouse turns the gun with no lag at all), at the depth of what the
        /// crosshair is on, eased in log space so a ray flicking between a window at 3 m and the room behind it at 30 m does not swing the gun.
        /// Coming nearer is eased more slowly than going further (a ledge's edge flickering under the crosshair made the gun spasm).
        /// <paramref name="from"/> is where the gun is aimed from (the shoulder): the point is kept at least GunAimPointMinMetres beyond it.
        /// </summary>
        private Vector3 SmoothedGunAimPoint(Vector3 from, float deltaTime)
        {
            Vector3 cameraForward = _gunAimCameraForward.normalized;
            float minDepth = Mathf.Max(0.5f, Vector3.Dot(from - _gunAimCameraPosition, cameraForward)) + GunAimPointMinMetres;
            float rawDepth = GunAimConvergenceDistance;
            if (_hasGunAimPoint)
            {
                rawDepth = Mathf.Max(minDepth, Vector3.Dot(_gunAimPoint - _gunAimCameraPosition, cameraForward));
            }
            float wantedLogDepth = Mathf.Log(Mathf.Max(minDepth, rawDepth));
            if (!_aimDepthValid)
            {
                _aimLogDepth = wantedLogDepth;   // starting to aim: nothing to be smooth with yet
                _aimDepthValid = true;
            }
            else
            {
                float change = wantedLogDepth - _aimLogDepth;
                if (Mathf.Abs(change) > GunAimDepthDeadZone)
                {
                    float seconds = change < 0.0f ? GunAimDepthCloserSmoothSeconds : GunAimDepthSmoothSeconds;
                    _aimLogDepth = Mathf.Lerp(_aimLogDepth, wantedLogDepth, 1.0f - Mathf.Exp(-deltaTime / seconds));
                }
            }
            return _gunAimCameraPosition + cameraForward * Mathf.Exp(_aimLogDepth);
        }

        /// <summary>Something owns the whole body (a seat, a climb, the ledge hands, a ragdoll): the rifle hold lets go and comes back after.</summary>
        private bool RifleBodyBusy()
        {
            if (!_rifleRagdollLooked)
            {
                _rifleRagdollLooked = true;
                _rifleRagdoll = GetComponent<Ragdoll>();
            }
            return IsSitting || InCarSeat || _climbWallWeight > 0.01f || _ledgeHandsWeight > 0.01f
                || (_rifleRagdoll != null && _rifleRagdoll.IsLimp);
        }

        private void ApplyRiflePose(float deltaTime)
        {
            if (_rightUpperArm == null || _rightLowerArm == null || _handR == null || _spine == null || _rifleGun == null)
            {
                return;
            }
            if (RifleBodyBusy())
            {
                _rifleHold = 0.0f;
                _rifleShoulder = 0.0f;
                _aimDepthValid = false;
                return;
            }

            // Shouldered when aiming or just fired (on a network copy: when its synced Aiming parameter says so).
            bool animatorAims = _animator != null && _animator.isActiveAndEnabled && _animator.GetBool(AimingParameter);
            bool shouldered = _isAimingGun || _gunFireHold > 0.0f || animatorAims;
            float rate = !_isAimingGun && _gunFireHold > 0.0f ? RifleFireShoulderPerSecond : RifleShoulderPerSecond;
            _rifleShoulder = Mathf.MoveTowards(_rifleShoulder, shouldered ? 1.0f : 0.0f, rate * deltaTime);
            _rifleHold = Mathf.MoveTowards(_rifleHold, 1.0f, RifleHoldPerSecond * deltaTime);
            float s = Mathf.SmoothStep(0.0f, 1.0f, _rifleShoulder);
            float w = Mathf.SmoothStep(0.0f, 1.0f, _rifleHold);

            // 1. The bladed stance: the chest turned to the right (the arms hang from it, so the shoulders move with it).
            Vector3 up = transform.up;
            _spine.rotation = Quaternion.AngleAxis(RifleBladeDegrees * w, up) * _spine.rotation;
            Quaternion chest = _spine.rotation;
            Vector3 butt = _rightUpperArm.position + chest * Vector3.Lerp(RifleLowReadyPocket, RifleShoulderPocket, s);

            // 2. Where the barrel points: at the crosshair's point when shouldered (the bore, which runs above the butt, through it), down and
            //    in at low ready.
            Vector3 bodyForward = transform.forward;
            Vector3 lowForward = Quaternion.AngleAxis(-RifleLowReadyYawDegrees, up)
                * (Quaternion.AngleAxis(RifleLowReadyPitchDegrees, transform.right) * bodyForward);
            Vector3 aimForward = bodyForward;
            _hasAimedPoint = false;
            if (_gunAimCameraForward.sqrMagnitude > 0.0001f && shouldered)
            {
                Vector3 aimPoint = SmoothedGunAimPoint(butt, deltaTime);
                _aimedPoint = aimPoint;
                _hasAimedPoint = true;
                Vector3 bore = butt + up * _rifleBoreRise;
                aimForward = Vector3.RotateTowards(bodyForward, (aimPoint - bore).normalized, GunAimPitchClampDegrees * Mathf.Deg2Rad, 0.0f);
            }
            else if (!shouldered)
            {
                _aimDepthValid = false;
            }
            Vector3 forward = Vector3.Slerp(lowForward, aimForward, s).normalized;
            Vector3 gunUp = Vector3.ProjectOnPlane(up, forward);
            if (gunUp.sqrMagnitude < 1e-4f)
            {
                gunUp = Vector3.ProjectOnPlane(-bodyForward, forward);
            }
            Quaternion gunRotation = Quaternion.LookRotation(forward, gunUp.normalized);
            Vector3 gunPosition = butt - gunRotation * _rifleButt;

            // 3. The shooting hand onto the grip: the gun hangs off the hand at its hold, so the hand goes where the gun's hold puts it. The
            //    elbow out to the side and down, as a rifle's strong arm is carried.
            Quaternion handRotation = gunRotation * Quaternion.Inverse(_rifleHoldRotation);
            Vector3 handPosition = gunPosition - handRotation * _rifleHoldPosition;
            Vector3 elbowHint = -up * 0.8f + transform.right * 0.7f - bodyForward * 0.2f;
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, handPosition, elbowHint, w);
            _handR.rotation = Quaternion.Slerp(_handR.rotation, handRotation, w);

            // 4. The head: down the sights when shouldered (the cheek on the stock), straight ahead otherwise -- turned back from the blade.
            if (_head != null)
            {
                Vector3 look = Vector3.Slerp(bodyForward, forward, s);
                Vector3 current = _head.forward;
                Vector3 wanted = Vector3.Slerp(current, look, w);
                _head.rotation = Quaternion.FromToRotation(current, wanted) * _head.rotation;
            }
        }
    }
}
