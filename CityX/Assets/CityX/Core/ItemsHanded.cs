using System.Collections.Generic;

namespace CityX.Core
{
    /// <summary>
    /// WHAT A SERVER HAS HANDED EACH CHARACTER since its last save (docs/SERVER_SAVING.md, step 3b): every item the server gave a player it
    /// decided between (a dropped item picked up, a piece of a fallen enemy's gear taken). A save from a player on someone else's server may carry
    /// what it carried before and these, nothing more (IBagRules). A save that shows a handed item uses it up; one never shown (picked up and
    /// dropped again before the save) is forgotten after <see cref="RememberSeconds"/>, so it cannot be spent later on something else. Pure:
    /// the clock is the caller's.
    /// </summary>
    public sealed class ItemsHanded
    {
        /// <summary>How long a handed item waits for a save to show it: past four of the game's 30-second saves, with time for the trip.</summary>
        public const double RememberSeconds = 150.0;

        private struct Handing
        {
            public string ItemId;
            public int Count;
            public double At;
        }

        private readonly Dictionary<string, List<Handing>> _byCharacter = new Dictionary<string, List<Handing>>();

        public void Hand(string characterId, string itemId, int count, double now)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return;
            }
            if (!_byCharacter.TryGetValue(characterId, out List<Handing> list))
            {
                list = new List<Handing>();
                _byCharacter[characterId] = list;
            }
            list.Add(new Handing { ItemId = itemId, Count = count, At = now });
        }

        /// <summary>How many of <paramref name="itemId"/> the character has been handed and not yet shown in a save.</summary>
        public int Count(string characterId, string itemId, double now)
        {
            int count = 0;
            foreach (Handing handing in Live(characterId, now))
            {
                if (handing.ItemId == itemId)
                {
                    count += handing.Count;
                }
            }
            return count;
        }

        /// <summary>A save showed <paramref name="shown"/> more of <paramref name="itemId"/> than before: that many handed ones are used up, the oldest first.</summary>
        public void UseUp(string characterId, string itemId, int shown, double now)
        {
            List<Handing> list = Live(characterId, now);
            for (int i = 0; i < list.Count && shown > 0; i++)
            {
                if (list[i].ItemId != itemId)
                {
                    continue;
                }
                int used = System.Math.Min(shown, list[i].Count);
                shown -= used;
                Handing left = list[i];
                left.Count -= used;
                list[i] = left;
            }
            list.RemoveAll(h => h.Count <= 0);
        }

        private List<Handing> Live(string characterId, double now)
        {
            if (string.IsNullOrEmpty(characterId) || !_byCharacter.TryGetValue(characterId, out List<Handing> list))
            {
                return new List<Handing>();
            }
            list.RemoveAll(h => now - h.At > RememberSeconds);
            return list;
        }
    }
}
