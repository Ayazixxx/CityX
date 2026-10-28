using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs to raise and hold Arcane Barrier from the bar, without depending on the Weapons assembly (ArcaneBarrierCaster
    /// implements it), the same split IArcaneOrbCaster makes.
    /// </summary>
    public interface IArcaneBarrierCaster
    {
        /// <summary>This body's wall is up: it stands still behind it and does nothing else.</summary>
        bool IsUp { get; }

        void Tick(float deltaTime);

        /// <summary>Raises the wall before the body, turned to <paramref name="facing"/>: false, and nothing happens, on cooldown, dead or short of mana.</summary>
        bool TryRaise(Vector3 facing);

        /// <summary>Every frame the wall is up: whether its key is still held. Let go (or not told for a moment: stunned, knocked down), it fades.</summary>
        void Hold(bool held);
    }
}
