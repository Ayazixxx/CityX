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

    /// <summary>
    /// Optional add-on for something IDamageable that can be sent flying: the aerial sword spin's last blow launches whatever it
    /// reaches upward. Separate from IDamageable for the same reason IXpReward is: most targets (a dummy, a prop) have no way to
    /// be thrown and simply ignore it.
    /// </summary>
    public interface ILaunchable
    {
        /// <summary>Throws the body with this velocity (metres per second, world space); gravity takes it from there.</summary>
        void Launch(Vector3 velocity);
    }

    /// <summary>Throwing a struck body limp, whatever it is.</summary>
    public static class Throws
    {
        /// <summary>
        /// <paramref name="target"/> thrown at <paramref name="velocity"/>: a player's body knocked down limp (sent to its owner when it is another
        /// player's copy), anything else that can be thrown (a civilian) launched.
        /// </summary>
        public static void Limp(IDamageable target, Vector3 velocity)
        {
            if (target is PlayerVitals vitals)
            {
                vitals.TriggerLaunchKnockdown(velocity);
            }
            else if (target is ILaunchable launchable)
            {
                launchable.Launch(velocity);
            }
        }
    }

    /// <summary>
    /// Optional add-on for something IDamageable that takes poison as plain damage over time, WITHOUT the reactions a blow causes (no flinch, no
    /// hit sound, no blocking, no invulnerability frames): a poisoned player coughing and flinching every half second would be a different
    /// effect. Anything that does not implement it takes each tick as an ordinary zero-stun hit instead (see PoisonEffect).
    /// </summary>
    public interface IPoisonable
    {
        /// <summary>Takes <paramref name="amount"/> of poison damage.</summary>
        void TakePoison(float amount);
    }

    /// <summary>
    /// Optional marker for something IDamageable that does not bleed: no blood bursts from a blow on it. A ghost (the Wraith) is smoke, and
    /// red blood spraying from it would break its look. Anything else bleeds, as it always has.
    /// </summary>
    public interface IBloodless
    {
    }

    /// <summary>
    /// Marker for an ENEMY (the Wraith, the Crimson Claw, the swordsman, the Shoota): what fights the players. A bullet turned aside by a
    /// player's guard may fly into one; never into a civilian or anyone else who is not one (BulletRicochet).
    /// </summary>
    public interface IHostile
    {
    }

    /// <summary>What a target did with a hit.</summary>
    public enum HitResponse
    {
        /// <summary>Nothing happened -- already dead, or dashing through it.</summary>
        Ignored = 0,

        Landed = 1,

        /// <summary>Caught on a timed block. The attacker gets punished for it.</summary>
        Blocked = 2,

        /// <summary>
        /// Soaked by a HELD guard (the soulslike sword's block): the defender
        /// paid stamina for it and took reduced damage, and the attacker's
        /// blade bounces off it -- a shorter stun than a well-timed block's
        /// (SoulsSwordCombat.GuardReboundSeconds against BlockedRecoilSeconds;
        /// owner, 2026-10-17: "implement a stagger system for if you swing at
        /// a block"). Kept separate from Blocked so the two can punish by
        /// different amounts.
        /// </summary>
        Guarded = 3,
    }
}
