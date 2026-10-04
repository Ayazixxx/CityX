using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The ice shards spell (key 3): a volley of shards fired one after another down the crosshair. The same split IFireballCaster makes:
    /// PlayerInputReader (Character) asks, the implementation in CityX.Weapons owns the projectile prefab, so Character does not depend on
    /// Weapons. An innate ability like the fireball: found once with GetComponent, nothing to equip.
    /// </summary>
    public interface IIceShardCaster
    {
        bool CanCast { get; }

        /// <summary>True while the volley is being fired and for a short follow-through after it: folded into the same "casting" checks the fireball uses.</summary>
        bool IsCasting { get; }

        /// <summary>True only while shards are still to be fired.</summary>
        bool IsVolleying { get; }

        /// <summary>1 while the volley is going out, easing to 0 over the follow-through: drives the raised-arm cast pose (PlayerLocomotion.SetCastPose).</summary>
        float CastFraction { get; }

        void Tick(float deltaTime);

        /// <summary>
        /// Starts the volley: the first shard leaves now, the rest at fixed intervals. Origin and direction are handed in (the crosshair-converged
        /// direction from the chest, as for the fireball) so it is drivable without a camera. False when it is on cooldown or the caster is dead.
        /// </summary>
        bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders);

        /// <summary>While the volley is still going out, where the rest of it should come from and go: the shards follow the crosshair, they are not all committed at the press.</summary>
        void Aim(Vector3 origin, Vector3 direction);
    }
}
