using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The hitscan-weapon contract PlayerInputReader talks to -- the gun
    /// equivalent of <see cref="IEquippableWeapon"/>, and deliberately a
    /// separate interface rather than folded into it. A gun has neither a
    /// swing nor a throw, and IEquippableWeapon has neither ammo nor a
    /// reload; forcing one contract to cover both would mean every
    /// implementer stubbing out half of it. PistolWeapon (CityX.Weapons)
    /// implements this so PlayerInputReader never has to reference that
    /// assembly directly -- same reasoning IEquippableWeapon itself exists
    /// for.
    /// </summary>
    public interface IEquippableGun
    {
        /// <summary>
        /// False on a dry/empty click -- on cooldown, mid-reload, or the
        /// magazine is empty. (An empty click on a gun that still has reserve
        /// ammo also starts the reload for you, the way every modern
        /// third-person shooter does.)
        /// </summary>
        bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder);

        void TryReload();

        /// <summary>Read by the HUD's own ammo counter.</summary>
        string GetAmmoText();

        /// <summary>Mid-reload: the reticle dims, and firing is refused.</summary>
        bool IsReloading { get; }

        /// <summary>
        /// Raised for every trigger pull -- a shot, or a dry click. The camera
        /// kicks, the reticle flashes a hit marker and the tick sounds from
        /// this; the gun itself knows about none of them.
        /// </summary>
        event Action<GunShot> ShotResolved;

        /// <summary>
        /// Would a shot fired now hit something OTHER than what the crosshair
        /// is on, because scenery is in the way of the barrel (standing behind
        /// a wall corner with the camera looking past it)? Drives the
        /// reticle's "blocked" state so a shot never quietly goes into the
        /// wall while the crosshair looks clear.
        /// </summary>
        bool IsBarrelBlocked(Vector3 origin, Vector3 direction, GameObject wielder);

        /// <summary>
        /// Where a shot fired now would end, and whether scenery is taking it
        /// (one pass of the same traces IsBarrelBlocked uses). The arm aims the
        /// gun at the point, so barrel, bullet and tracer all point the same way.
        /// </summary>
        void AimTrace(Vector3 origin, Vector3 direction, GameObject wielder, out Vector3 point, out bool barrelBlocked);
    }
}
