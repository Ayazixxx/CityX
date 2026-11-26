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

        private CarriedLoot _carried;
        private int _carriedTold;

        private void Awake()
        {
            _loot = GetComponent<CorpseLoot>();
            _loot.Relay = this;
            _carried = GetComponent<CarriedLoot>();
            _carriedTold = _carried != null ? _carried.Items.Count : 0;
        }

        /// <summary>
        /// WHAT HE STILL CARRIES, KEPT THE SAME ON EVERY SCREEN, LATE ARRIVALS INCLUDED (bug sweep 2026-11-25). A copy of a hobo carries one
        /// bottle fewer as it is told each throw, but a player who joined after a throw was never told: their copy listed bottles already
        /// thrown on his body, which the server then would not give. The machine that runs him tells the server what he carries whenever it
        /// goes down; the server keeps it (buffered) for everyone, late arrivals too.
        /// </summary>
        private void Update()
        {
            if (_carried == null || NetworkObject == null || !IsSpawned || !IsOwner || _carried.Items.Count == _carriedTold)
            {
                return;
            }
            _carriedTold = _carried.Items.Count;
            SendCarriedToServer(_carried.Items.ToArray());
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendCarriedToServer(string[] items)
        {
            if (_carried == null || items == null || !CarriesNoMoreThan(items, _carried.Items))
            {
                return;   // what he carries only ever goes down: nothing new appears on him this way
            }
            _carried.Items = new List<string>(items);
            BroadcastCarried(items);
        }

        [ObserversRpc(BufferLast = true, ExcludeOwner = true)]
        private void BroadcastCarried(string[] items)
        {
            if (_carried != null && items != null)
            {
                _carried.Items = new List<string>(items);
            }
        }

        /// <summary>Whether <paramref name="items"/> holds no more of anything than <paramref name="had"/> did.</summary>
        public static bool CarriesNoMoreThan(string[] items, List<string> had)
        {
            var left = new Dictionary<string, int>();
            foreach (string id in had)
            {
                left.TryGetValue(id, out int count);
                left[id] = count + 1;
            }
            foreach (string id in items)
            {
                if (id == null || !left.TryGetValue(id, out int count) || count == 0)
                {
                    return false;
                }
                left[id] = count - 1;
            }
            return true;
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
