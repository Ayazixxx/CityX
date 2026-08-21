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
        /// <summary>False on a dry/empty click -- on cooldown, mid-reload, or the magazine is empty.</summary>
        bool Fire(Vector3 origin, Vector3 direction, bool aiming, GameObject wielder);

        void TryReload();

        /// <summary>Read by the HUD's own ammo counter.</summary>
        string GetAmmoText();
    }
}
