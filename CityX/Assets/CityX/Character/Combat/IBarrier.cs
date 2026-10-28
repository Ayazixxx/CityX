using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Something standing between a body and what comes at it that takes the blow instead (Arcane Barrier, docs/ARCANE_BARRIER.md). The body's
    /// PlayerVitals asks it first, on the machine that decides the body's hits, so a bullet, a spell or a blade at its face hurts the wall.
    /// </summary>
    public interface IBarrier
    {
        /// <summary>Whether a blow from <paramref name="fromPosition"/> comes at the wall's face.</summary>
        bool Covers(Vector3 fromPosition);

        /// <summary>The wall takes the blow: <paramref name="amount"/> off its health, shown where it struck.</summary>
        void Absorb(float amount, Vector3 fromPosition);
    }
}
