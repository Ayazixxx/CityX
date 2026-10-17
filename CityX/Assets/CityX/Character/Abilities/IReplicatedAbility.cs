using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ONE THING AN ABILITY DID THAT OTHER PLAYERS SHOULD SEE: a fireball leaving the hand, an ice shard, a crescent, Bloodrage starting, a spin.
    /// Plain numbers on purpose -- it crosses the network as an RPC and has to mean the same on every machine. What the numbers mean is the
    /// ability's own business (<see cref="Kind"/> says which thing happened); the common ones are an origin and a direction in world space.
    /// </summary>
    public readonly struct AbilityEvent
    {
        /// <summary>Which of the ability's own happenings this is (0 for the usual one). Meaningless outside the ability that raised it.</summary>
        public readonly int Kind;

        public readonly Vector3 Origin;
        public readonly Vector3 Direction;

        /// <summary>Free numbers for the ability (a speed, a duration, a charge...).</summary>
        public readonly float A;

        public readonly float B;

        /// <summary>A free count (which shard of a volley, say).</summary>
        public readonly int N;

        public AbilityEvent(int kind, Vector3 origin, Vector3 direction, float a = 0.0f, float b = 0.0f, int n = 0)
        {
            Kind = kind;
            Origin = origin;
            Direction = direction;
            A = a;
            B = b;
            N = n;
        }
    }

    /// <summary>
    /// EVERY ABILITY THE PLAYER CAN USE IMPLEMENTS THIS, from the first commit. It is the whole multiplayer contract of an ability:
    ///
    ///  * The OWNER'S copy raises <see cref="Fired"/> whenever the ability does something other players should see. It does its own work as
    ///    it always did (movement, damage, the real projectile): the owner is the authority.
    ///  * Every OTHER player's copy of that body is handed the same event through <see cref="PlayRemote"/> and SHOWS it: the same projectile
    ///    flying, the same aura, the same pose and sound, but with no damage, no cooldown, no input, and never raising <see cref="Fired"/>
    ///    itself (that would bounce it back across the network for ever).
    ///
    /// NetworkedAbilitySync, on the player body, finds every IReplicatedAbility on it and does the sending; a new ability never touches
    /// FishNet. The ability must also be on the body that other players are given: see PlayerAbilityWiring, which puts the abilities on both
    /// the host's scene body and every joining player's spawned body from one list. docs/ABILITIES_MULTIPLAYER.md has the checklist, and
    /// AbilityReplicationGuardTests fails a new ability class that skips it.
    /// </summary>
    public interface IReplicatedAbility
    {
        /// <summary>A stable name for the ability (hashed for the wire; do not change it once shipped).</summary>
        string AbilityId { get; }

        /// <summary>Raised by the OWNER's copy whenever the ability does something other players should see.</summary>
        event System.Action<AbilityEvent> Fired;

        /// <summary>
        /// Shows <paramref name="evt"/> on a copy of this body that someone else owns. Visuals and sounds only: no damage, no cooldown, and it
        /// must not raise <see cref="Fired"/>. Input is trusted no further than a stranger's packet: tolerate anything (zero direction, odd numbers).
        /// </summary>
        void PlayRemote(AbilityEvent evt);
    }

    public static class AbilityIds
    {
        /// <summary>
        /// A hash of an ability's id that is the same in every process (string.GetHashCode is not): FNV-1a, 32 bit. The wire carries this rather
        /// than the string.
        /// </summary>
        public static int Hash(string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (id != null)
                {
                    foreach (char c in id)
                    {
                        hash = (hash ^ c) * 16777619u;
                    }
                }
                return (int)hash;
            }
        }
    }
}
