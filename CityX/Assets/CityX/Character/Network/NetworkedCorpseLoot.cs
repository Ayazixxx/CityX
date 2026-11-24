using System.Collections.Generic;
using CityX.Items;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A fallen enemy's gear in a session (docs/LOOTING.md, bodies): a player's take is asked of the server, which gives each piece to the first
    /// to ask and tells every machine it is gone, so it comes off every copy of the body and goes into the taker's inventory alone. Buffered
    /// (the whole list taken so far), so a player who arrives later finds the body as it is now.
    ///
    /// THE SERVER CHECKS: a known item, not taken yet, still worn by the server's own copy of the body, and the asker's body within
    /// <see cref="TakeReachMetres"/> of it. (Whether it fell is the asker's to see: a dedicated server's copy is never told.)
    /// </summary>
    [RequireComponent(typeof(CorpseLoot))]
    public sealed class NetworkedCorpseLoot : NetworkBehaviour, ICorpseLootRelay
    {
        /// <summary>How far from the body the asker's own body may be, metres (the ragdoll lies a little differently on each machine).</summary>
        public const float TakeReachMetres = 12.0f;

        private CorpseLoot _loot;
        private readonly List<string> _taken = new List<string>();

        /// <summary>How many of each item this machine has heard taken.</summary>
        private readonly Dictionary<string, int> _heard = new Dictionary<string, int>();

        /// <summary>The takes this machine has been told of (for tests).</summary>
        public int TakesHeard { get; private set; }

        private void Awake()
        {
            _loot = GetComponent<CorpseLoot>();
            _loot.Relay = this;
        }

        public bool IsShared => NetworkObject != null && IsSpawned;

        public void AskToTake(string itemId)
        {
            SendTakeToServer(itemId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendTakeToServer(string itemId, NetworkConnection sender = null)
        {
            if (sender == null || string.IsNullOrEmpty(itemId) || !ItemRegistry.TryGet(itemId, out _))
            {
                return;   // unknown
            }
            // Worn, one of it; carried, as many as it carried (a hobo's three bottles): taken that many times already, someone was first.
            int there = _loot.Wears(itemId) ? 1 : _loot.Carries(itemId);
            if (CountOf(_taken, itemId) >= there)
            {
                return;   // not on the body, or all taken
            }
            Vector3 body = _loot.Container != null ? _loot.Container.Centre : transform.position;
            if (!DroppedItemsNetwork.Near(sender, body, TakeReachMetres))
            {
                return;
            }
            _taken.Add(itemId);
            CityX.Core.LocalServer.HandTo(sender.ClientId, itemId);   // theirs now: their next saves may carry it (docs/SERVER_SAVING.md, step 3b)
            BroadcastTaken(_taken.ToArray(), itemId, sender.ClientId);
        }

        private static int CountOf(List<string> list, string itemId)
        {
            int count = 0;
            foreach (string id in list)
            {
                if (id == itemId)
                {
                    count++;
                }
            }
            return count;
        }

        [ObserversRpc(BufferLast = true)]
        private void BroadcastTaken(string[] taken, string itemId, int takerClientId)
        {
            TakesHeard++;
            bool mine = LocalConnection != null && LocalConnection.ClientId == takerClientId;
            // The whole list comes each time: only the takes not heard yet are new (the same item may be taken more than once, a carried one).
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < taken.Length; i++)
            {
                string id = taken[i];
                seen.TryGetValue(id, out int count);
                seen[id] = ++count;
                _heard.TryGetValue(id, out int heard);
                if (count > heard)
                {
                    _heard[id] = count;
                    _loot.Taken(id, mine && i == taken.Length - 1);
                }
            }
        }
    }
}
