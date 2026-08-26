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
        /// <returns>
        /// What the target did with it. THE ATTACKER HAS TO BE TOLD, because a
        /// timed block punishes whoever threw the punch -- and only the
        /// receiver knows it was blocked. Reporting it back is what makes a
        /// block a real answer rather than a slightly cheaper way of being hit.
        /// </returns>
        HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds);
    }

    /// <summary>
    /// Optional add-on for something IDamageable that owes XP to whoever lands
    /// the killing blow. Not part of IDamageable itself: most damageable
    /// things (the player, a sparring dummy) have nothing to award, and
    /// civilian.gd's own take_damage() checks for an add_xp method by
    /// duck-typing rather than every target carrying a reward field it
    /// usually leaves at zero.
    /// </summary>
    public interface IXpReward
    {
        int XpReward { get; }
    }

    /// <summary>What a target did with a hit.</summary>
    public enum HitResponse
    {
        /// <summary>Nothing happened -- already dead, or dashing through it.</summary>
        Ignored = 0,

        Landed = 1,

        /// <summary>Caught on a timed block. The attacker gets punished for it.</summary>
        Blocked = 2,
    }
}
