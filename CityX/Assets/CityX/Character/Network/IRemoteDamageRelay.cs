using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Whoever deals damage to a player body on THIS machine may be hitting another player's COPY (a stand-in for a body that lives on someone else's
    /// machine). The copy's own health means nothing: the real one is the owner's. A relay forwards the hit to the owner instead. Implemented by
    /// <see cref="NetworkedDamage"/>; <see cref="PlayerVitals"/> asks it before applying anything.
    /// </summary>
    public interface IRemoteDamageRelay
    {
        /// <summary>
        /// True (and the hit was sent on) when this body is another player's copy; false when the hit should be applied here as usual.
        /// <paramref name="poiseDamage"/> is the blow's poise damage, or -1 when the blow has none to speak of (it staggers as any blow did).
        /// </summary>
        bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage);

        bool TryRelayPoison(float amount);

        /// <summary>
        /// True (and the knockdown was sent on) when this body is another player's copy: the blow that throws it (a combo finisher, Self Smite's
        /// burst) throws the real body, on its owner's machine, at <paramref name="velocity"/>.
        /// </summary>
        bool TryRelayKnockdown(Vector3 velocity);

        /// <summary>A heal on another player's copy, sent to its owner (docs/HEALING.md); false on the owner's own body.</summary>
        bool TryRelayHeal(float amount);
    }
}
