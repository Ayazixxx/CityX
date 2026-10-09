using System.Collections.Generic;
using CityX.World;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One light switch set, on its way between machines.</summary>
    public struct LightSwitchMessage : IBroadcast
    {
        public int Building;
        public int Floor;
        public int Room;
        public bool On;
    }

    /// <summary>A joining player asking which lights are on.</summary>
    public struct LightSwitchStateRequest : IBroadcast
    {
    }

    /// <summary>Every light that is on, for a joining player.</summary>
    public struct LightSwitchState : IBroadcast
    {
        public int[] Building;
        public int[] Floor;
        public int[] Room;
    }

    /// <summary>
    /// Keeps every machine's light switches (World.RoomLights) the same in a session: a flip goes client -> server -> every client, and a joining
    /// player asks the server for every light that is on. FishNet BROADCASTS, not RPCs on a player's body: those only reach the players within its
    /// view distance (NetworkedPlayerIdentity.ViewRadius, 100 m), and a room switched on while you were across the city must be on when you get there.
    ///
    /// THE SERVER CHECKS: a sane room (building, floor and room in range) and at most <see cref="MaxFlipsPerSecond"/> flips a second per player.
    /// In singleplayer nothing is started and this does nothing: RoomLights.Relay stays null and a flip only changes this machine.
    /// </summary>
    public sealed class RoomLightsNetwork : MonoBehaviour, IRoomLightRelay
    {
        /// <summary>The most switches one player may flip per second (a hand is not faster than this).</summary>
        public const int MaxFlipsPerSecond = 12;

        /// <summary>No building has more floors than this (sanity only).</summary>
        public const int MaxFloor = 400;

        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        /// <summary>How many switch flips and full states this machine has heard from the server (for tests).</summary>
        public int FlipsHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many lights were on in the last full state heard (for tests).</summary>
        public int LastStateSize { get; private set; }

        private NetworkManager _manager;
        private bool _serverHandlers;
        private bool _clientHandlers;
        private bool _requested;
        private readonly Dictionary<int, (float windowStart, int count)> _rate = new Dictionary<int, (float, int)>();

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
                    _manager.ServerManager.RegisterBroadcast<LightSwitchMessage>(OnFlipAtServer);
                    _manager.ServerManager.RegisterBroadcast<LightSwitchStateRequest>(OnStateRequestAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<LightSwitchMessage>(OnFlipAtServer);
                    _manager.ServerManager.UnregisterBroadcast<LightSwitchStateRequest>(OnStateRequestAtServer);
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<LightSwitchMessage>(OnFlipAtClient);
                    _manager.ClientManager.RegisterBroadcast<LightSwitchState>(OnStateAtClient);
                    RoomLights.Relay = this;
                    _requested = false;   // asked for once the server has let this client in (below)
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<LightSwitchMessage>(OnFlipAtClient);
                    _manager.ClientManager.UnregisterBroadcast<LightSwitchState>(OnStateAtClient);
                    if (ReferenceEquals(RoomLights.Relay, this))
                    {
                        RoomLights.Relay = null;
                    }
                }
                _clientHandlers = client;
            }

            // A joining player asks which lights are on, once the server has authenticated it (a broadcast before that is dropped).
            if (_clientHandlers && !_requested && _manager.ClientManager.Connection != null && _manager.ClientManager.Connection.IsAuthenticated)
            {
                _requested = true;
                _manager.ClientManager.Broadcast(new LightSwitchStateRequest());
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(RoomLights.Relay, this))
            {
                RoomLights.Relay = null;
            }
        }

        public void Send(RoomKey key, bool on)
        {
            if (_manager != null && _manager.IsClientStarted)
            {
                _manager.ClientManager.Broadcast(new LightSwitchMessage { Building = key.Building, Floor = key.Floor, Room = key.Room, On = on });
            }
        }

        private void OnFlipAtServer(NetworkConnection sender, LightSwitchMessage message, Channel channel)
        {
            if (!Acceptable(message) || sender == null || !WithinRate(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            RoomLights.Set(new RoomKey(message.Building, message.Floor, message.Room), message.On);
            _manager.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, LightSwitchStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            List<RoomKey> on = RoomLights.AllOn();
            var state = new LightSwitchState { Building = new int[on.Count], Floor = new int[on.Count], Room = new int[on.Count] };
            for (int i = 0; i < on.Count; i++)
            {
                state.Building[i] = on[i].Building;
                state.Floor[i] = on[i].Floor;
                state.Room[i] = on[i].Room;
            }
            _manager.ServerManager.Broadcast(sender, state);
        }

        private void OnFlipAtClient(LightSwitchMessage message, Channel channel)
        {
            FlipsHeard++;
            if (Acceptable(message))
            {
                RoomLights.Set(new RoomKey(message.Building, message.Floor, message.Room), message.On);
            }
        }

        private void OnStateAtClient(LightSwitchState state, Channel channel)
        {
            StatesHeard++;
            int n = state.Building == null ? 0 : Mathf.Min(state.Building.Length, Mathf.Min(state.Floor?.Length ?? 0, state.Room?.Length ?? 0));
            LastStateSize = n;
            for (int i = 0; i < n; i++)
            {
                var message = new LightSwitchMessage { Building = state.Building[i], Floor = state.Floor[i], Room = state.Room[i], On = true };
                if (Acceptable(message))
                {
                    RoomLights.Set(new RoomKey(message.Building, message.Floor, message.Room), true);
                }
            }
        }

        /// <summary>A switch message names a room that can exist. Public static so it is tested without a network.</summary>
        public static bool Acceptable(LightSwitchMessage message)
        {
            CityWorld world = CurrentWorld.Instance;
            int buildings = world != null ? world.BuildingCount : int.MaxValue;
            return message.Building >= 0 && message.Building < buildings
                && message.Floor >= -1 && message.Floor <= MaxFloor
                && (message.Room == 0 || message.Room == 1);
        }

        private bool WithinRate(int clientId, float now)
        {
            _rate.TryGetValue(clientId, out (float windowStart, int count) entry);
            if (now - entry.windowStart >= 1.0f)
            {
                entry = (now, 0);
            }
            entry.count++;
            _rate[clientId] = entry;
            return entry.count <= MaxFlipsPerSecond;
        }
    }
}
