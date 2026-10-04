using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The sword's slash projectile (key 7). The same split IFireballCaster makes: PlayerInputReader (Character) asks, the implementation in
    /// CityX.Weapons owns the projectile prefab, so Character does not depend on Weapons.
    /// </summary>
    public interface ISwordWaveCaster
    {
        void Tick(float deltaTime);

        /// <summary>
        /// Starts the cast: the caller has the sword swing now, and the crescent leaves the blade a moment later, down the given direction (the
        /// crosshair, as for the fireball). False when it is still on cooldown.
        /// </summary>
        bool TryCast(float chestHeight, Vector3 direction, Collider[] ignoreColliders);

        bool CanCast { get; }
    }
}
