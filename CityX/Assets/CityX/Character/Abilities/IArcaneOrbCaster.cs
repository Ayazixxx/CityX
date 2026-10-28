using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs to cast Arcane Orb from the bar, without depending on the Weapons assembly (ArcaneOrbCaster implements it),
    /// the same split IFireballCaster makes.
    /// </summary>
    public interface IArcaneOrbCaster
    {
        /// <summary>A cast's short raised-hand window (it suppresses the jetpack as aiming does).</summary>
        bool IsCasting { get; }

        /// <summary>1 just after a cast, easing to 0: the raised-hand pose's strength.</summary>
        float CastFraction { get; }

        void Tick(float deltaTime);

        /// <summary>Casts down <paramref name="direction"/> from <paramref name="origin"/>: false, and nothing happens, on cooldown, dead or short of mana.</summary>
        bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders);
    }
}
