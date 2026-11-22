using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE SECOND OF A PAIR (Dual Pistols, owner 2026-11-19: docs/DUAL_PISTOLS.md). The same gun in the LEFT hand, held as the right hand holds
    /// its own seen in a mirror (DualPistolHold). Right-click fires it; its shot goes out under its own name (GunReload.FireLeftName), so another
    /// player's copy flashes the left-hand gun. The two guns find each other on the body, whose locomotion knows both, and are reloaded
    /// TOGETHER: R starts the right-hand gun's reload, which plays the pair's motion (both magazines out, both in, both slides home) and takes
    /// the left-hand gun along, each filled from its own reserve as the magazines seat.
    /// </summary>
    public sealed partial class PistolWeapon
    {
        private bool _leftHand;
        private PistolWeapon _pair;

        /// <summary>Held in the left hand, the second of a pair.</summary>
        public bool InLeftHand => _leftHand;

        /// <summary>The other gun of the pair this one is in (null when it is held alone).</summary>
        public PistolWeapon Pair => _pair;

        /// <summary>The reload of a pair of these guns, or null when there is none.</summary>
        public BakedMotion PairMotion => string.IsNullOrEmpty(Profile.PairMotionResource) ? null : BakedMotion.Load(Profile.PairMotionResource);

        public void OnEquipOffHand(GameObject player, ItemInstanceData instanceData) => Equip(player, instanceData, true);

        public void MountCosmeticOffHand(GameObject body) => Mount(body, true);

        /// <summary>In somebody's hand: the owner's, or on another player's copy.</summary>
        private bool InAHand => _equipped || _cosmeticBody != null;

        /// <summary>The magazine has room and there are rounds to fill it with.</summary>
        private bool TakesRounds => MagazineAmmo < Profile.MagazineSize && SpareRounds > 0;

        /// <summary>Where the gun sits on its hand: the hold (tunable live: /pistol), mirrored in the left hand.</summary>
        private Vector3 HeldPosition => _leftHand ? DualPistolHold.Mirror(_liveGripPosition) : _liveGripPosition;

        private Quaternion HeldRotation =>
            _leftHand ? DualPistolHold.Mirror(Quaternion.Euler(_liveGripRotation)) : Quaternion.Euler(_liveGripRotation);

        /// <summary>A moving part's name in the reload motion: the left-hand gun's go by their own (the pair's motion moves both guns' parts).</summary>
        private string PartName(string part) => _leftHand ? DualPistolHold.LeftHandPart(part) : part;

        private void HoldInLeftHand(PlayerLocomotion locomotion)
        {
            locomotion.SetLeftPistol(transform, Profile.MuzzleLocal, DualPistolHold.Mirror(Profile.HoldPosition), DualPistolHold.Mirror(Profile.HoldRotation));
        }

        /// <summary>The gun in the body's other hand, if it is one of these held as a pistol, becomes this one's pair (either may come first).</summary>
        private void FindPair(GameObject body)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            Transform other = locomotion == null ? null : _leftHand ? locomotion.RightPistol : locomotion.LeftPistol;
            PistolWeapon gun = other != null ? other.GetComponent<PistolWeapon>() : null;
            if (gun != null && gun != this)
            {
                _pair = gun;
                gun._pair = this;
            }
        }

        private void Unpair()
        {
            if (_pair != null && _pair._pair == this)
            {
                _pair._pair = null;
            }
            _pair = null;
        }

        /// <summary>
        /// The pair's reload started (by the right-hand gun): this gun goes along with it, on the same motion's events or the same timer, and is
        /// filled as the magazines seat.
        /// </summary>
        private void FollowReload(bool empty, bool byMotion, float duration)
        {
            if (!_equipped || IsReloading)
            {
                return;
            }
            _reloadEmpty = empty;
            _reloadAmmoIn = false;
            _reloadReadyToFire = false;
            IsReloading = true;
            _reloadByMotion = byMotion;
            _reloadDuration = duration;
            _reloadTimeRemaining = duration;
        }
    }
}
