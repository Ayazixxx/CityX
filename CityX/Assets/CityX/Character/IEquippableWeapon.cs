using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The combat contract MeleeAttacker and PlayerInputReader talk to.
    /// Neither ever references a concrete weapon class -- a future sword or
    /// gun costs nothing extra here, it just implements this.
    /// </summary>
    public interface IEquippableWeapon
    {
        float GetMeleeDamage();
        float MeleeCooldownSeconds { get; }
        void RegisterLandedMeleeHit();
        bool CanThrow();
        float ThrowSpeed { get; }
        void Throw(GameObject player, Vector3 origin, Vector3 direction);
    }
}
