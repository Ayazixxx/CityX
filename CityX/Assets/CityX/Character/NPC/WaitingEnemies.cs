using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Spots where an enemy put down waits to see a player before it fights (Old Guy's swordsman, docs/TUTORIAL.md). Whoever puts it down marks
    /// the spot; the enemy's own mind claims it on the machine that runs it, so it holds whether the enemy was made here or brought into a
    /// session by the server (which hands back no object to set it on).
    /// </summary>
    public static class WaitingEnemies
    {
        /// <summary>How near the spot an enemy must stand to claim it, metres.</summary>
        public const float ClaimMetres = 1.5f;

        /// <summary>How long a mark waits to be claimed: a session's enemy arrives within EnemyNetwork.ArrivalWaitSeconds.</summary>
        public const float MarkSeconds = 10.0f;

        private static readonly List<(Vector3 Spot, float Until)> _marks = new List<(Vector3, float)>();

        public static void Mark(Vector3 spot)
        {
            _marks.Add((spot, Time.realtimeSinceStartup + MarkSeconds));
        }

        /// <summary>Whether an enemy standing at <paramref name="position"/> was put down on a marked spot; the mark is used up.</summary>
        public static bool Claim(Vector3 position)
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                if (now > _marks[i].Until)
                {
                    _marks.RemoveAt(i);
                }
                else if ((_marks[i].Spot - position).sqrMagnitude <= ClaimMetres * ClaimMetres)
                {
                    _marks.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
