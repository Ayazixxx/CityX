using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a body carries besides what it wears and holds (docs/LOOTING.md, bodies): a hobo's spare bottles (docs/HOBO.md). It is spent as it
    /// is used (a bottle thrown), and what is left is listed on the fallen body with what it wears (CorpseLoot). Every machine keeps its own
    /// copy of the list the same way: the throws that spend it are told to every copy (BottleThrower).
    /// </summary>
    public sealed class CarriedLoot : MonoBehaviour
    {
        [Tooltip("Item ids carried, one entry each (three glass bottles are three entries).")]
        public List<string> Items = new List<string>();

        /// <summary>How many of <paramref name="itemId"/> are carried.</summary>
        public int Count(string itemId)
        {
            int count = 0;
            foreach (string id in Items)
            {
                if (id == itemId)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>One <paramref name="itemId"/> used up (thrown): false when none was carried.</summary>
        public bool Spend(string itemId) => Items.Remove(itemId);
    }
}
