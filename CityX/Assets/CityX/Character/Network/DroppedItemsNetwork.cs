using System.Collections.Generic;
using CityX.Items;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// An item a player's body let go of, on its way between machines. Durability is -1 for an item that has none; a gun's rounds go with
    /// it (HasAmmo), so one dropped and picked up again is not reloaded.
    /// </summary>
    public struct ItemDropMessage : IBroadcast
    {
        public long Id;
        public string ItemId;
        public int Durability;
        public bool HasAmmo;
        public int Magazine;
        public int Reserve;
        public bool HasContents;
        public float Contents;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 Spin;
    }

    /// <summary>A player asking for a dropped item.</summary>
    public struct ItemTakeRequest : IBroadcast
    {
        public long Id;

        /// <summary>Where it lies on the asker's machine: it may have fallen a long way from where it was let go.</summary>
        public Vector3 At;
    }

    /// <summary>The server's word to one player that they may not have that item from where they stand.</summary>
    public struct ItemTakeRefusedMessage : IBroadcast
    {
        public long Id;
    }

    /// <summary>The server's word to one player that it will not share a drop of theirs (too many at once, or too far from their body).</summary>
    public struct ItemDropRefusedMessage : IBroadcast
    {
        public long Id;
    }

    /// <summary>The server's word that a dropped item was taken, and by which player.</summary>
    public struct ItemTakenMessage : IBroadcast
    {
        public long Id;
        public int TakerClientId;
    }

    /// <summary>A joining player asking what lies around.</summary>
    public struct ItemDropStateRequest : IBroadcast
    {
    }

    /// <summary>Every dropped item lying in the world as it lies now, for a joining player (one entry per item across the arrays).</summary>
    public struct ItemDropStateMessage : IBroadcast
    {
        public long[] Id;
        public string[] ItemId;
        public int[] Durability;
        public bool[] HasAmmo;
        public int[] Magazine;
        public int[] Reserve;
        public bool[] HasContents;
        public float[] Contents;
        public Vector3[] Position;
        public Quaternion[] Rotation;
    }

    /// <summary>
    /// Keeps the items players dropped the same on every machine in a session (DroppedItems): a drop goes dropper -> server -> every client; a
    /// player picking one up asks the server, which gives it to the first to ask and tells everyone it is gone (only then does it go into that
    /// player's inventory, so two players can never both take it); a joining player is sent everything lying around. FishNet BROADCASTS, as the
    /// glass and the doors (GlassDamageNetwork): an RPC on a body stops at its view distance, and a sword dropped across the city must still be
    /// lying there when you arrive.
    ///
    /// THE SERVER CHECKS: a known item, sane numbers, an id of the sender's own, the sender's body near the drop (and near it to pick it up), at
    /// most <see cref="MaxDropsPerSecond"/> a second and <see cref="MaxLying"/> lying at once. In singleplayer nothing is started and this does
    /// nothing: DroppedItems.Relay stays null, and drops and pickups happen on this machine alone.
    /// </summary>
    public sealed class DroppedItemsNetwork : MonoBehaviour, IDroppedItemRelay
    {
        /// <summary>How far from the dropper's body a drop may start, metres (its hand, and a killing blow's fling).</summary>
        public const float DropReachMetres = 8.0f;

        /// <summary>How far from where it was dropped a player's body may be to pick it up, metres (it tumbles, and lands a little differently on each machine).</summary>
        public const float TakeReachMetres = 12.0f;

        /// <summary>How far along the ground from where it was let go an item may have come to rest, metres: it falls, it does not fly across town.</summary>
        public const float FallDriftMetres = 30.0f;

        public const float MaxSpeed = 40.0f;

        /// <summary>The most rounds a dropped gun may say it has in its magazine, or spare: more than any gun carries.</summary>
        public const int MaxRounds = 999;

        /// <summary>The most a dropped thing may say is left in it (a jerry can holds 20 litres).</summary>
        public const float MaxContents = 1000.0f;

        /// <summary>
        /// A spam guard, no more: a sword spin through a pack, or a sledgehammer smash, kills several bodies at once, each letting go of its weapon
        /// and losing its helmet with its head (4 a second made the 5th a ghost: bug hunt M11).
        /// </summary>
        public const int MaxDropsPerSecond = 30;

        /// <summary>The most items lying in the world at once; past it the oldest is cleared away for the new one.</summary>
        public const int MaxLying = 300;

        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        /// <summary>This machine's drops (DroppedItems.Shared in the game; a test gives each of its machines its own).</summary>
        public DroppedItems Target = DroppedItems.Shared;

        /// <summary>What this machine has heard from the server (for tests).</summary>
        public int DropsHeard { get; private set; }

        public int TakesHeard { get; private set; }

        public int StatesHeard { get; private set; }

        public int LastStateSize { get; private set; }

        private NetworkManager _manager;
        private bool _serverHandlers;
        private bool _clientHandlers;
        private bool _requested;
        private int _counter;
        private readonly Dictionary<long, ItemDropMessage> _lying = new Dictionary<long, ItemDropMessage>();
        private readonly List<long> _order = new List<long>();
        private readonly SenderRate _rate = new SenderRate(MaxDropsPerSecond);

        private void Update()
        {
            if (_manager == null)
            {
                _manager = Manager != null ? Manager : InstanceFinder.NetworkManager;
                if (_manager == null)
                {
                    return;
                }
            }

            bool server = _manager.IsServerStarted;
            if (server != _serverHandlers)
            {
                if (server)
                {
                    _manager.ServerManager.RegisterBroadcast<ItemDropMessage>(OnDropAtServer);
                    _manager.ServerManager.RegisterBroadcast<ItemTakeRequest>(OnTakeAtServer);
                    _manager.ServerManager.RegisterBroadcast<ItemDropStateRequest>(OnStateRequestAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<ItemDropMessage>(OnDropAtServer);
                    _manager.ServerManager.UnregisterBroadcast<ItemTakeRequest>(OnTakeAtServer);
                    _manager.ServerManager.UnregisterBroadcast<ItemDropStateRequest>(OnStateRequestAtServer);
                    _lying.Clear();
                    _order.Clear();
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<ItemDropMessage>(OnDropAtClient);
                    _manager.ClientManager.RegisterBroadcast<ItemTakenMessage>(OnTakenAtClient);
                    _manager.ClientManager.RegisterBroadcast<ItemDropStateMessage>(OnStateAtClient);
                    _manager.ClientManager.RegisterBroadcast<ItemTakeRefusedMessage>(OnTakeRefusedAtClient);
                    _manager.ClientManager.RegisterBroadcast<ItemDropRefusedMessage>(OnDropRefusedAtClient);
                    Target.Relay = this;
                    _requested = false;   // asked for once the server has let this client in (below)
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<ItemDropMessage>(OnDropAtClient);
                    _manager.ClientManager.UnregisterBroadcast<ItemTakenMessage>(OnTakenAtClient);
                    _manager.ClientManager.UnregisterBroadcast<ItemDropStateMessage>(OnStateAtClient);
                    _manager.ClientManager.UnregisterBroadcast<ItemTakeRefusedMessage>(OnTakeRefusedAtClient);
                    _manager.ClientManager.UnregisterBroadcast<ItemDropRefusedMessage>(OnDropRefusedAtClient);
                    if (ReferenceEquals(Target.Relay, this))
                    {
                        Target.Relay = null;
                    }
                }
                _clientHandlers = client;
            }

            // A joining player asks what lies around, once the server has authenticated it (a broadcast before that is dropped).
            if (_clientHandlers && !_requested && _manager.ClientManager.Connection != null && _manager.ClientManager.Connection.IsAuthenticated)
            {
                _requested = true;
                _manager.ClientManager.Broadcast(new ItemDropStateRequest());
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Target.Relay, this))
            {
                Target.Relay = null;
            }
        }

        // ---- this machine, through the relay ----

        /// <summary>The high half is this client's own (its id + 1), so ids from different machines never meet; the low half counts.</summary>
        public long NewId()
        {
            int client = _manager != null && _manager.ClientManager.Connection != null ? _manager.ClientManager.Connection.ClientId : 0;
            return ((long)(client + 1) << 32) | (uint)++_counter;
        }

        public void SendDrop(ItemDrop drop)
        {
            if (_manager != null && _manager.IsClientStarted)
            {
                _manager.ClientManager.Broadcast(ToMessage(drop));
            }
        }

        public void RequestTake(long id)
        {
            if (_manager != null && _manager.IsClientStarted)
            {
                DroppedItem shown = Target.Find(id);
                _manager.ClientManager.Broadcast(new ItemTakeRequest { Id = id, At = shown != null ? shown.transform.position : Vector3.zero });
            }
        }

        // ---- the server ----

        private void OnDropAtServer(NetworkConnection sender, ItemDropMessage message, Channel channel)
        {
            if (sender == null || IdOwner(message.Id) != sender.ClientId || _lying.ContainsKey(message.Id))
            {
                return;
            }
            if (!Acceptable(message) || !Near(sender, message.Position, DropReachMetres) || !_rate.Allow(sender.ClientId, Time.unscaledTime))
            {
                // Told, or it lies on the dropper's screen as a ghost that can never be picked up (bug hunt M11): there it stays theirs alone.
                _manager.ServerManager.Broadcast(sender, new ItemDropRefusedMessage { Id = message.Id });
                return;
            }
            while (_order.Count >= MaxLying)
            {
                long oldest = _order[0];
                _order.RemoveAt(0);
                if (_lying.Remove(oldest))
                {
                    _manager.ServerManager.Broadcast(new ItemTakenMessage { Id = oldest, TakerClientId = -1 });
                }
            }
            _lying[message.Id] = message;
            _order.Add(message.Id);
            _manager.ServerManager.Broadcast(message);
        }

        private void OnTakeAtServer(NetworkConnection sender, ItemTakeRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            if (!_lying.TryGetValue(request.Id, out ItemDropMessage drop))
            {
                // Gone already (someone was first, its word lost) or never shared: told it is gone, or it stays "asked for" there for good.
                _manager.ServerManager.Broadcast(sender, new ItemTakenMessage { Id = request.Id, TakerClientId = -1 });
                return;
            }
            bool nearWhereItFell = CouldHaveFallenTo(drop.Position, request.At) && Near(sender, request.At, TakeReachMetres);
            if (!nearWhereItFell && !Near(sender, drop.Position, TakeReachMetres))
            {
                // Asked for from across the city. Told, or it stays "asked for" on their machine and they can never pick it up.
                _manager.ServerManager.Broadcast(sender, new ItemTakeRefusedMessage { Id = request.Id });
                return;
            }
            _lying.Remove(request.Id);
            _order.Remove(request.Id);
            _manager.ServerManager.Broadcast(new ItemTakenMessage { Id = request.Id, TakerClientId = sender.ClientId });
        }

        /// <summary>Whether an item let go at <paramref name="released"/> could have come to rest at <paramref name="at"/>: not above it, nor far off to one side.</summary>
        public static bool CouldHaveFallenTo(Vector3 released, Vector3 at)
        {
            var along = new Vector2(at.x - released.x, at.z - released.z);
            return WireChecks.Finite(at) && along.magnitude <= FallDriftMetres && at.y <= released.y + TakeReachMetres;
        }

        private void OnStateRequestAtServer(NetworkConnection sender, ItemDropStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            int n = _lying.Count;
            var state = new ItemDropStateMessage
            {
                Id = new long[n], ItemId = new string[n], Durability = new int[n], HasAmmo = new bool[n], Magazine = new int[n], Reserve = new int[n],
                HasContents = new bool[n], Contents = new float[n], Position = new Vector3[n], Rotation = new Quaternion[n],
            };
            int i = 0;
            foreach (long id in _order)
            {
                if (!_lying.TryGetValue(id, out ItemDropMessage drop) || i >= n)
                {
                    continue;
                }
                // Where it lies now on the server's own machine, when it shows it there (a host does).
                DroppedItem shown = Target.Find(id);
                state.Id[i] = id;
                state.ItemId[i] = drop.ItemId;
                state.Durability[i] = drop.Durability;
                state.HasAmmo[i] = drop.HasAmmo;
                state.Magazine[i] = drop.Magazine;
                state.Reserve[i] = drop.Reserve;
                state.HasContents[i] = drop.HasContents;
                state.Contents[i] = drop.Contents;
                state.Position[i] = shown != null ? shown.transform.position : drop.Position;
                state.Rotation[i] = shown != null ? shown.transform.rotation : drop.Rotation;
                i++;
            }
            _manager.ServerManager.Broadcast(sender, state);
        }

        // ---- every client ----

        private void OnDropAtClient(ItemDropMessage message, Channel channel)
        {
            DropsHeard++;
            if (Acceptable(message))
            {
                Target.Show(ToDrop(message));   // the dropper's own machine already shows it: a repeat changes nothing
            }
        }

        private void OnTakeRefusedAtClient(ItemTakeRefusedMessage message, Channel channel)
        {
            Target.TakeRefused(message.Id);
        }

        /// <summary>How many of this machine's drops the server would not share (for tests).</summary>
        public int DropsRefused { get; private set; }

        private void OnDropRefusedAtClient(ItemDropRefusedMessage message, Channel channel)
        {
            DropsRefused++;
            Target.DropRefused(message.Id);
        }

        private void OnTakenAtClient(ItemTakenMessage message, Channel channel)
        {
            TakesHeard++;
            bool mine = _manager.ClientManager.Connection != null && message.TakerClientId == _manager.ClientManager.Connection.ClientId;
            Target.Taken(message.Id, mine);
        }

        private void OnStateAtClient(ItemDropStateMessage state, Channel channel)
        {
            StatesHeard++;
            int n = state.Id == null ? 0 : state.Id.Length;
            n = Mathf.Min(n, Mathf.Min(state.ItemId?.Length ?? 0, Mathf.Min(state.Durability?.Length ?? 0,
                Mathf.Min(state.Position?.Length ?? 0, state.Rotation?.Length ?? 0))));
            n = Mathf.Min(n, Mathf.Min(state.HasAmmo?.Length ?? 0, Mathf.Min(state.Magazine?.Length ?? 0, state.Reserve?.Length ?? 0)));
            n = Mathf.Min(n, Mathf.Min(state.HasContents?.Length ?? 0, state.Contents?.Length ?? 0));
            LastStateSize = n;
            for (int i = 0; i < n; i++)
            {
                var message = new ItemDropMessage
                {
                    Id = state.Id[i], ItemId = state.ItemId[i], Durability = state.Durability[i],
                    HasAmmo = state.HasAmmo[i], Magazine = state.Magazine[i], Reserve = state.Reserve[i],
                    HasContents = state.HasContents[i], Contents = state.Contents[i],
                    Position = state.Position[i], Rotation = state.Rotation[i],
                };
                if (Acceptable(message))
                {
                    Target.Show(ToDrop(message));
                }
            }
        }

        // ---- the rules ----

        /// <summary>A drop message names an item that exists, with finite, sane numbers. Public static so it is tested without a network.</summary>
        public static bool Acceptable(ItemDropMessage message)
        {
            if (string.IsNullOrEmpty(message.ItemId) || !ItemRegistry.Has(message.ItemId) || message.Id <= 0)
            {
                return false;
            }
            if (!WireChecks.Finite(message.Position) || !WireChecks.Finite(message.Velocity) || !WireChecks.Finite(message.Spin)
                || float.IsNaN(message.Rotation.x) || float.IsNaN(message.Rotation.y) || float.IsNaN(message.Rotation.z) || float.IsNaN(message.Rotation.w))
            {
                return false;
            }
            if (message.HasAmmo && (message.Magazine < 0 || message.Reserve < 0 || message.Magazine > MaxRounds || message.Reserve > MaxRounds))
            {
                return false;
            }
            if (message.HasContents && !(message.Contents >= 0.0f && message.Contents <= MaxContents))
            {
                return false;   // (not-a-number fails both comparisons)
            }
            return message.Velocity.magnitude <= MaxSpeed && message.Spin.magnitude <= MaxSpeed && Mathf.Abs(message.Position.y) < 100000.0f;
        }

        /// <summary>Which client gave the drop its id (the high half, less one): only that client may have dropped it.</summary>
        public static int IdOwner(long id) => (int)(id >> 32) - 1;

        public static ItemDropMessage ToMessage(ItemDrop drop) => new ItemDropMessage
        {
            Id = drop.Id, ItemId = drop.ItemId, Durability = drop.Data.HasDurability ? drop.Data.Durability : -1,
            HasAmmo = drop.Data.HasAmmo, Magazine = drop.Data.Magazine, Reserve = drop.Data.Reserve,
            HasContents = drop.Data.HasContents, Contents = drop.Data.Contents,
            Position = drop.Position, Rotation = drop.Rotation, Velocity = drop.Velocity, Spin = drop.Spin,
        };

        public static ItemDrop ToDrop(ItemDropMessage message) => new ItemDrop
        {
            Id = message.Id, ItemId = message.ItemId,
            Data = new ItemInstanceData
            {
                HasDurability = message.Durability >= 0, Durability = Mathf.Max(0, message.Durability),
                HasAmmo = message.HasAmmo, Magazine = message.HasAmmo ? message.Magazine : 0, Reserve = message.HasAmmo ? message.Reserve : 0,
                HasContents = message.HasContents, Contents = message.HasContents ? message.Contents : 0.0f,
            },
            Position = message.Position, Rotation = message.Rotation, Velocity = message.Velocity, Spin = message.Spin,
        };

        /// <summary>Whether any body <paramref name="sender"/> owns is within <paramref name="metres"/> of <paramref name="point"/>.</summary>
        public static bool Near(NetworkConnection sender, Vector3 point, float metres)
        {
            foreach (NetworkObject owned in sender.Objects)
            {
                if (owned != null && Vector3.Distance(owned.transform.position, point) <= metres)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
