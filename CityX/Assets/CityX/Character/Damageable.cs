using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Anything a punch can land on.
    ///
    /// AN INTERFACE RATHER THAN A SHARED BASE CLASS, because the things that
    /// take damage have nothing else in common: the player is a
    /// CharacterController with stamina, an NPC is an AI, a breakable prop is
    /// neither. Making them share a base to share one method is how you end up
    /// with a Damageable that also knows about inventory.
    ///
    /// The hit carries WHERE it came from as well as how much, because
    /// knockback, blood direction and which way a ragdoll flops all need it,
    /// and reconstructing it afterwards from two positions is worse than
    /// passing it.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>
        /// Applies a hit.
        /// </summary>
        /// <param name="amount">Damage before any blocking on the receiver's side.</param>
        /// <param name="fromPosition">Where the blow came from, in world space.</param>
        /// <param name="hitstunSeconds">
        /// How long the target should be locked out of acting. Zero means the
        /// attack does something else instead -- the combo finisher ragdolls
        /// rather than stunning, and doing both would be two competing
        /// reactions to one hit.
        /// </param>
        void ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds);
    }
}
