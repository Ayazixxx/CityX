using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A drink in the player's hand (Weapons' HeldDrink: the can of PHIZZ, the full PHIZZ bottle; docs/PHIZZ_CAN.md): a click drinks it; right-click held
    /// raises it to throw (it is an IAimedThrow too), and a click from there throws it (owner, 2026-11-23: "click to drink hold right click to aim and click
    /// to throw when aiming"). Nothing is swung or blocked with a drink in hand.
    /// </summary>
    public interface IHeldDrink
    {
        /// <summary>Begins drinking. False if it cannot (already drinking, nothing to drink it with).</summary>
        bool Drink();

        /// <summary>It can be thrown now (not while it is being drunk, or already thrown).</summary>
        bool CanThrow { get; }

        /// <summary>Thrown from <paramref name="origin"/> toward <paramref name="direction"/> (the crosshair).</summary>
        void Throw(GameObject player, Vector3 origin, Vector3 direction);
    }
}
