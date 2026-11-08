using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT THE ENEMIES FIGHT: the players' bodies (<see cref="PlayerBodies"/>) and every summon on the players' side (the Wraith Staff's Wraith,
    /// which joins while it is summoned; owner, 2026-11-08: "have the enemies view the summon as an opponent as well"). An enemy goes for the
    /// nearest, so one that wanders off from its summoner is fought on its own. A summon is the players' on every machine: no player's blow
    /// hurts it, and its kills are its summoner's (DamageContext.Strike).
    /// </summary>
    public static class Opponents
    {
        private static readonly List<IOpponent> _summons = new List<IOpponent>();

        /// <summary>The summons fighting on the players' side just now, on this machine (each machine's copies too).</summary>
        public static IReadOnlyList<IOpponent> Summons => _summons;

        /// <summary><paramref name="summon"/> fights on the players' side until it <see cref="Leave"/>s.</summary>
        public static void Join(IOpponent summon)
        {
            if (summon != null && !_summons.Contains(summon))
            {
                _summons.Add(summon);
            }
        }

        public static void Leave(IOpponent summon)
        {
            _summons.Remove(summon);
        }

        /// <summary>Whether <paramref name="body"/> is a summon on the players' side.</summary>
        public static bool IsSummon(Object body)
        {
            foreach (IOpponent summon in _summons)
            {
                if (Here(summon) && (summon as Object == body || summon.gameObject == body))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether <paramref name="striker"/>'s blow would land on its own side: a player's (or a summon's) on a summon fighting for the players.
        /// </summary>
        public static bool SameSide(GameObject striker, IDamageable target)
        {
            if (striker == null || !(target is Component hit) || !IsSummon(hit.gameObject))
            {
                return false;
            }
            PlayerVitals wielder = striker.GetComponentInParent<PlayerVitals>();
            GameObject body = wielder != null ? wielder.gameObject : striker;
            PlayerVitals summoner = SummonerOf(hit.gameObject);
            return PlayerBodies.IsPlayer(body) || IsSummon(body) || (summoner != null && summoner.gameObject == body);
        }

        /// <summary>Who summoned <paramref name="striker"/>, when it is a summon: its kills are theirs. Null otherwise.</summary>
        public static PlayerVitals SummonerOf(GameObject striker)
        {
            Wraith summon = striker != null ? striker.GetComponentInParent<Wraith>() : null;
            return summon != null && summon.IsSummoned ? summon.Master : null;
        }

        /// <summary>The nearest living opponent within <paramref name="reach"/> metres of <paramref name="from"/>: a player's body or a summon; null for none.</summary>
        public static IOpponent NearestAlive(Vector3 from, float reach)
        {
            IOpponent best = PlayerBodies.NearestAlive(from, reach);
            float nearest = best != null ? Vector3.Distance(from, best.transform.position) : reach;
            foreach (IOpponent summon in _summons)
            {
                if (!Alive(summon))
                {
                    continue;
                }
                float distance = Vector3.Distance(from, summon.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                    best = summon;
                }
            }
            return best;
        }

        /// <summary>Still there (not destroyed) and alive: an enemy lets go of one that is not.</summary>
        public static bool Alive(IOpponent opponent) => Here(opponent) && opponent.IsAlive;

        /// <summary>Not null and not destroyed (Unity's own null, which an interface reference does not see).</summary>
        public static bool Here(IOpponent opponent) => opponent is Object body && body != null;
    }
}
