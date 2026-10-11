using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Acid Rain (key 5), the acid tree's god-tier spell in its first, test form: a green storm over a wide area that burns everyone out in the open.
    /// The same split IPoisonBreathCaster makes: PlayerInputReader (Character) asks, the implementation in CityX.Weapons owns the storm prefab.
    /// </summary>
    public interface IAcidRainCaster
    {
        bool CanCast { get; }

        /// <summary>Seconds until it can be cast again.</summary>
        float CooldownRemaining { get; }

        /// <summary>
        /// Calls the storm down where the crosshair points (<paramref name="eye"/> along <paramref name="direction"/>, no further than the spell's
        /// reach). False on cooldown or when dead.
        /// </summary>
        bool TryCast(Vector3 eye, Vector3 direction, Collider[] ignoreColliders);
    }
}
