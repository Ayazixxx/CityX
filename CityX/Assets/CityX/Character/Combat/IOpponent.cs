using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A body the enemies fight: a player's, or a summon fighting on the players' side (the Wraith Staff's Wraith; owner, 2026-11-08: "have the
    /// enemies view the summon as an opponent as well"). What an enemy reads of what it fights; a move only a player can take (the Crimson
    /// Claw's grab, a swordsman reading a sword's swing) asks for the player behind it (<c>as PlayerVitals</c>).
    /// </summary>
    public interface IOpponent : IDamageable
    {
        /// <summary>Its body (a component's own).</summary>
        Transform transform { get; }

        GameObject gameObject { get; }

        /// <summary>Dashing, or otherwise not to be hit just now: a blow or a catch slips it.</summary>
        bool IsInvulnerable { get; }

        /// <summary>Holding a guard up.</summary>
        bool IsBlocking { get; }

        /// <summary>Reeling from a blow.</summary>
        bool IsStunned { get; }

        /// <summary>How much longer it reels, seconds.</summary>
        float HitstunRemaining { get; }
    }
}
