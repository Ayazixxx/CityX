using System;
using System.Collections.Generic;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One item lying in the world: what it is, its state, and how it left the hand that let go of it.</summary>
    public struct ItemDrop
    {
        public long Id;
        public string ItemId;
        public ItemInstanceData Data;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 Spin;

        /// <summary>
        /// Set down by hand rather than let go (a knife standing in its block, a bat leaning on a wall, docs/OLD_GUYS_HOUSE.md): it is held
        /// exactly where it was put until it is picked up. Only ever this machine's own (the tutorial house's), never shared.
        /// </summary>
        public bool Placed;
    }

    /// <summary>The way a player's drop is shared with the other players (DroppedItemsNetwork). Null in singleplayer.</summary>
    public interface IDroppedItemRelay
    {
        /// <summary>An id no other machine will give a drop.</summary>
        long NewId();

        void SendDrop(ItemDrop drop);

        void RequestTake(long id);
    }

    /// <summary>
    /// EVERY ITEM LYING IN THE WORLD ON THIS MACHINE after a body let go of it (a body that dies drops what was in its hand), and who may take
    /// one. The objects showing them are DroppedItems.
    ///
    /// A PLAYER'S drop is shared: shown here at once and told to the server (<see cref="Relay"/>), which tells everyone else. Taking a shared one
    /// asks the server, which gives it to the first player to ask and tells every machine it is gone, so two players can never both pick up the
    /// same sword. An NPC's drop stays on this machine (an NPC exists only on the machine that spawned it), as does everything in singleplayer,
    /// and is taken at once. Ids: a shared drop's comes from the relay, a drop of this machine's alone is negative.
    /// </summary>
    public sealed class DroppedItems
    {
        public static DroppedItems Shared { get; } = new DroppedItems();

        public IDroppedItemRelay Relay;

        /// <summary>The prefab an item's model is made from: the one equipping uses (any inventory's list). A test can give its own.</summary>
        public Func<string, GameObject> PrefabFor = Inventory.FindPrefab;

        private readonly Dictionary<long, ItemDrop> _drops = new Dictionary<long, ItemDrop>();
        private readonly Dictionary<long, DroppedItem> _shown = new Dictionary<long, DroppedItem>();
        private readonly Dictionary<long, Inventory> _asked = new Dictionary<long, Inventory>();

        /// <summary>This machine's drops the server would not share (bug hunt M11): this machine's alone, taken here at once, never a ghost.</summary>
        private readonly HashSet<long> _keptHere = new HashSet<long>();
        private long _lastLocalId;

        /// <summary>How many items lie in the world on this machine.</summary>
        public int Count => _drops.Count;

        /// <summary>The objects showing them.</summary>
        public IEnumerable<DroppedItem> Shown => _shown.Values;

        public bool Has(long id) => _drops.ContainsKey(id);

        public DroppedItem Find(long id) => _shown.TryGetValue(id, out DroppedItem item) && item != null ? item : null;

        /// <summary>Whether this machine's player has asked the server for <paramref name="id"/> and is waiting to hear.</summary>
        public bool IsAskedFor(long id) => _asked.ContainsKey(id);

        /// <summary>
        /// A body let go of <paramref name="drop"/> (its id is given here): shown at once, and a player's (<paramref name="shared"/>) told to the
        /// others. <paramref name="dropper"/>, the body it fell from, is not collided with; <paramref name="prefab"/> is the item's, when the
        /// dropper knows it (an NPC's weapon is not in any inventory).
        /// </summary>
        public DroppedItem Drop(ItemDrop drop, bool shared, GameObject dropper = null, GameObject prefab = null)
        {
            bool viaServer = shared && Relay != null;
            drop.Id = viaServer ? Relay.NewId() : --_lastLocalId;
            DroppedItem shown = Show(drop, dropper, prefab);
            if (viaServer)
            {
                Relay.SendDrop(drop);
            }
            return shown;
        }

        /// <summary>A drop this machine made or was told of: recorded and shown, once (a repeat changes nothing).</summary>
        public DroppedItem Show(ItemDrop drop, GameObject dropper = null, GameObject prefab = null)
        {
            if (_drops.ContainsKey(drop.Id))
            {
                return Find(drop.Id);
            }
            _drops[drop.Id] = drop;
            if (prefab == null && PrefabFor != null)
            {
                prefab = PrefabFor(drop.ItemId);
            }
            if (prefab == null)
            {
                return null;   // recorded all the same, so it can still be taken and handed on
            }
            DroppedItem made = DroppedItem.Make(prefab, drop, this, dropper);
            _shown[drop.Id] = made;
            return made;
        }

        /// <summary>
        /// This machine's player takes <paramref name="id"/> into <paramref name="into"/>: at once if the item is this machine's alone; a shared one
        /// is asked for, and arrives when the server says this player was first (<see cref="Taken"/>). False when it is not here, will not fit,
        /// or is already asked for.
        /// </summary>
        public bool Take(long id, Inventory into)
        {
            if (into == null || !_drops.TryGetValue(id, out ItemDrop drop) || _asked.ContainsKey(id) || !into.CanPickUp(drop.ItemId, drop.Data))
            {
                return false;
            }
            if (id < 0 || Relay == null || _keptHere.Contains(id))
            {
                Remove(id);
                return into.PickUp(drop.ItemId, drop.Data);
            }
            _asked[id] = into;
            Relay.RequestTake(id);
            return true;
        }

        /// <summary>
        /// The server's word: <paramref name="id"/> was taken, by this machine's player when <paramref name="byThisMachine"/> (it goes into the
        /// inventory that asked for it). Gone from here either way.
        /// </summary>
        public void Taken(long id, bool byThisMachine)
        {
            if (byThisMachine && _asked.TryGetValue(id, out Inventory into) && into != null && _drops.TryGetValue(id, out ItemDrop drop))
            {
                into.PickUp(drop.ItemId, drop.Data);
            }
            Remove(id);
        }

        /// <summary>The server's word that this machine's player may not have it from where they stand: it may be asked for again, nearer.</summary>
        public void TakeRefused(long id)
        {
            _asked.Remove(id);
        }

        /// <summary>
        /// The server's word that it would not share a drop this machine made (too many at once, or too far from its body): it stays here as this
        /// machine's alone, to be picked up as one, instead of lying as a ghost nobody may take.
        /// </summary>
        public void DropRefused(long id)
        {
            if (_drops.ContainsKey(id))
            {
                _keptHere.Add(id);
            }
        }

        /// <summary>Whether <paramref name="id"/> is a drop the server would not share, kept on this machine alone.</summary>
        public bool IsKeptHere(long id) => _keptHere.Contains(id);

        /// <summary>Every drop recorded here, as it lies now (a shown one at its object's place, at rest), for a player who joins.</summary>
        public List<ItemDrop> Current()
        {
            var list = new List<ItemDrop>(_drops.Count);
            foreach (KeyValuePair<long, ItemDrop> pair in _drops)
            {
                ItemDrop drop = pair.Value;
                DroppedItem shown = Find(pair.Key);
                if (shown != null)
                {
                    drop.Position = shown.transform.position;
                    drop.Rotation = shown.transform.rotation;
                    drop.Velocity = Vector3.zero;
                    drop.Spin = Vector3.zero;
                }
                list.Add(drop);
            }
            return list;
        }

        public void Remove(long id)
        {
            _drops.Remove(id);
            _asked.Remove(id);
            _keptHere.Remove(id);
            if (_shown.TryGetValue(id, out DroppedItem shown))
            {
                _shown.Remove(id);
                if (shown != null)
                {
                    UnityEngine.Object.Destroy(shown.gameObject);
                }
            }
        }

        /// <summary>The object showing <paramref name="id"/> is gone (the scene unloaded it): the record forgets it too.</summary>
        internal void Forget(long id, DroppedItem shown)
        {
            if (_shown.TryGetValue(id, out DroppedItem current) && ReferenceEquals(current, shown))
            {
                _shown.Remove(id);
                _drops.Remove(id);
                _asked.Remove(id);
            }
        }

        public void Clear()
        {
            foreach (long id in new List<long>(_drops.Keys))
            {
                Remove(id);
            }
            _lastLocalId = 0;
        }
    }
}
