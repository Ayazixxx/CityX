using System.Collections.Generic;

namespace CityX.Character
{
    /// <summary>What kind of turn an attacker asks for: each target has its own few of each.</summary>
    public enum TurnKind
    {
        Melee,
        Ranged,
    }

    /// <summary>
    /// WHOSE TURN IT IS TO ATTACK (owner, 2026-10-24: "make mobs that are hostile to you work together if they are both attacking you at once ...
    /// dont just both push you at the same time completely ignoring eachother"). The attack-token scheme of DOOM (2016), The Last of Us Part II
    /// and the Arkham and Shadow of Mordor fights: each target hands out a few turns -- one to swing at it in melee, two to shoot at it -- and an
    /// enemy attacks only while it holds one; the rest keep up the pressure around it (circling further out, aiming) and wait. Turns go in the
    /// order they were asked for; a turn is given back when the attack is over (and its holder then rests a moment, so the next gets in),
    /// taken back when held too long, and forgotten when its holder stops asking (it died, lost the target, walked off).
    ///
    /// Pure: attackers and targets are any objects, time is passed in. EnemyPack holds the one the game uses.
    /// </summary>
    public sealed class AttackTurns
    {
        /// <summary>How many at once may attack one target: one swings in melee, two shoot (players can watch about one blow coming at a time).</summary>
        public const int MeleeTurns = 1;

        public const int RangedTurns = 2;

        /// <summary>One who stops asking for this long has left the queue (or lost the turn it held).</summary>
        public const float ForgetSeconds = 0.5f;

        /// <summary>A turn held this long is taken back, whatever its holder is doing.</summary>
        public const float LongestTurnSeconds = 4.0f;

        /// <summary>After giving a turn back, its holder waits this long before taking one again if another is waiting: the next in line gets in.</summary>
        public const float RestSeconds = 1.0f;

        private sealed class Claim
        {
            public object Target;
            public TurnKind Kind;
            public bool Holding;
            public float HeldSince;
            public float QueuedSince = -1.0f;
            public long QueueOrder;
            public float LastAsked;
            public float RestUntil = float.NegativeInfinity;
        }

        private readonly Dictionary<object, Claim> _claims = new Dictionary<object, Claim>();
        private long _order;

        public static int TurnsFor(TurnKind kind) => kind == TurnKind.Melee ? MeleeTurns : RangedTurns;

        /// <summary>
        /// <paramref name="who"/> wants to attack <paramref name="target"/> now: true while it holds a turn (asked every frame it wants one).
        /// Asking for another target or kind gives back whatever it held.
        /// </summary>
        public bool Ask(object who, object target, TurnKind kind, float now)
        {
            if (who == null || target == null)
            {
                return false;
            }
            if (!_claims.TryGetValue(who, out Claim claim))
            {
                claim = new Claim();
                _claims[who] = claim;
            }
            if (!ReferenceEquals(claim.Target, target) || claim.Kind != kind)
            {
                claim.Holding = false;
                claim.QueuedSince = -1.0f;
                claim.Target = target;
                claim.Kind = kind;
            }
            claim.LastAsked = now;
            Forget(now);

            if (claim.Holding)
            {
                if (now - claim.HeldSince <= LongestTurnSeconds)
                {
                    return true;
                }
                Release(claim, now);
                return false;
            }
            if (now < claim.RestUntil && OthersWaiting(claim))
            {
                return false;
            }
            if (claim.QueuedSince < 0.0f)
            {
                claim.QueuedSince = now;
                claim.QueueOrder = ++_order;
            }

            int free = TurnsFor(kind) - Holders(target, kind);
            if (free <= 0)
            {
                return false;
            }
            int ahead = 0;
            foreach (Claim other in _claims.Values)
            {
                if (other != claim && !other.Holding && other.QueuedSince >= 0.0f && ReferenceEquals(other.Target, target) && other.Kind == kind
                    && other.QueueOrder < claim.QueueOrder)
                {
                    ahead++;
                }
            }
            if (ahead >= free)
            {
                return false;
            }
            claim.Holding = true;
            claim.HeldSince = now;
            claim.QueuedSince = -1.0f;
            return true;
        }

        /// <summary>The attack is over: the turn goes to the next in line, and <paramref name="who"/> rests a moment.</summary>
        public void GiveBack(object who, float now)
        {
            if (who != null && _claims.TryGetValue(who, out Claim claim) && claim.Holding)
            {
                Release(claim, now);
            }
        }

        /// <summary>Gone (dead, despawned): whatever it held or waited for is free.</summary>
        public void Leave(object who)
        {
            if (who != null)
            {
                _claims.Remove(who);
            }
        }

        public bool Holds(object who) => who != null && _claims.TryGetValue(who, out Claim claim) && claim.Holding;

        public int Holders(object target, TurnKind kind)
        {
            int count = 0;
            foreach (Claim claim in _claims.Values)
            {
                if (claim.Holding && ReferenceEquals(claim.Target, target) && claim.Kind == kind)
                {
                    count++;
                }
            }
            return count;
        }

        public void Clear()
        {
            _claims.Clear();
        }

        /// <summary>Someone else is queued for the same target and kind (a rest only gives way to someone who is there to take the turn).</summary>
        private bool OthersWaiting(Claim claim)
        {
            foreach (Claim other in _claims.Values)
            {
                if (other != claim && !other.Holding && other.QueuedSince >= 0.0f && ReferenceEquals(other.Target, claim.Target) && other.Kind == claim.Kind)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Release(Claim claim, float now)
        {
            claim.Holding = false;
            claim.QueuedSince = -1.0f;
            claim.RestUntil = now + RestSeconds;
        }

        /// <summary>Those who stopped asking lose their place, and their turn.</summary>
        private void Forget(float now)
        {
            foreach (Claim claim in _claims.Values)
            {
                if (now - claim.LastAsked > ForgetSeconds)
                {
                    claim.Holding = false;
                    claim.QueuedSince = -1.0f;
                }
            }
        }
    }
}
