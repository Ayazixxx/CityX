using System.Collections.Generic;
using CityX.World;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
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
    public sealed class RoomLightsNetwork : SessionRelay, IRoomLightRelay
    {
        /// <summary>The most switches one player may flip per second (a hand is not faster than this).</summary>
        public const int MaxFlipsPerSecond = 12;

        /// <summary>No building has more floors than this (sanity only).</summary>
        public const int MaxFloor = 400;

        /// <summary>How many switch flips and full states this machine has heard from the server (for tests).</summary>
        public int FlipsHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many lights were on in the last full state heard (for tests).</summary>
        public int LastStateSize { get; private set; }

        private readonly SenderRate _rate = new SenderRate(MaxFlipsPerSecond);

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<LightSwitchMessage>(server, on, OnFlipAtServer);
            Hear<LightSwitchStateRequest>(server, on, OnStateRequestAtServer);
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<LightSwitchMessage>(client, on, OnFlipAtClient);
            Hear<LightSwitchState>(client, on, OnStateAtClient);
            if (on)
            {
                RoomLights.Relay = this;
            }
            else
            {
                if (ReferenceEquals(RoomLights.Relay, this))
                {
                    RoomLights.Relay = null;
                }
            }
        }

        // A joining player asks which lights are on, once the server has authenticated it (a broadcast before that is dropped).
        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new LightSwitchStateRequest());
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
            if (CanSend)
            {
                Session.ClientManager.Broadcast(new LightSwitchMessage { Building = key.Building, Floor = key.Floor, Room = key.Room, On = on });
            }
        }

        private void OnFlipAtServer(NetworkConnection sender, LightSwitchMessage message, Channel channel)
        {
            if (!Acceptable(message) || sender == null || !_rate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            RoomLights.Set(new RoomKey(message.Building, message.Floor, message.Room), message.On);
            Session.ServerManager.Broadcast(message);
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
            Session.ServerManager.Broadcast(sender, state);
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
                && message.Room >= 0 && message.Room <= RoomLighting.StairwellRoom;   // hall, corner room, stairwell
        }
    }
}
