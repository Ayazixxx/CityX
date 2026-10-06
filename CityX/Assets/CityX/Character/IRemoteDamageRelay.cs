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
        /// <summary>True (and the hit was sent on) when this body is another player's copy; false when the hit should be applied here as usual.</summary>
        bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds);

        bool TryRelayPoison(float amount);
    }
}
