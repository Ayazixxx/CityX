using System.Collections.Generic;

namespace CityX.Character
{
    /// <summary>
    /// What this machine has asked the server to take off a fallen body, one ask each, and into which bag each goes when the server says it is
    /// this machine's (CorpseLoot). Counted, not one each per kind: a hobo's three spare bottles are three asks of the same id, and keyed once
    /// the first answer cleared them all, so one bottle came into the bag and the other two vanished from the body (bug sweep 2026-11-25).
    /// </summary>
    public sealed class LootAsks
    {
        private readonly Dictionary<string, (Inventory Into, int Count)> _asks = new Dictionary<string, (Inventory Into, int Count)>();

        /// <summary>One more ask for <paramref name="itemId"/>, for <paramref name="into"/>.</summary>
        public void Ask(string itemId, Inventory into)
        {
            _asks.TryGetValue(itemId, out (Inventory Into, int Count) asked);
            _asks[itemId] = (into, asked.Count + 1);
        }

        /// <summary>How many asks for <paramref name="itemId"/> are still unanswered.</summary>
        public int Waiting(string itemId) => _asks.TryGetValue(itemId, out (Inventory Into, int Count) asked) ? asked.Count : 0;

        /// <summary>The server gave this machine one <paramref name="itemId"/>: the bag that asked for it (one ask answered); null when none did.</summary>
        public Inventory Answer(string itemId)
        {
            if (!_asks.TryGetValue(itemId, out (Inventory Into, int Count) asked))
            {
                return null;
            }
            if (asked.Count <= 1)
            {
                _asks.Remove(itemId);
            }
            else
            {
                _asks[itemId] = (asked.Into, asked.Count - 1);
            }
            return asked.Into;
        }
    }
}
