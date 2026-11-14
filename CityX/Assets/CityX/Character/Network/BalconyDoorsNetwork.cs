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
    /// <summary>One balcony door opened or shut, on its way between machines.</summary>
    public struct BalconyDoorMessage : IBroadcast
    {
        public int Building;
        public int Unit;
        public int Row;
        public bool Open;
    }

    /// <summary>A joining player asking which balcony doors stand open.</summary>
    public struct BalconyDoorStateRequest : IBroadcast
    {
    }

    /// <summary>Every balcony door that stands open, for a joining player.</summary>
    public struct BalconyDoorStateMessage : IBroadcast
    {
        public int[] Building;
        public int[] Unit;
        public int[] Row;
    }

    /// <summary>
    /// Keeps every machine's balcony doors (World.BalconyDoorState) the same in a session, as the light switches are (RoomLightsNetwork): a door
    /// opened or shut goes client -> server -> every client, and a joining player asks the server for every door that stands open. FishNet
    /// BROADCASTS, not RPCs on a player's body: those only reach the players within its view distance, and a door opened while you were across the
    /// city must stand open when you get there.
    ///
    /// THE SERVER CHECKS: the door is one that exists and opens (World.BalconyDoors, from the plan every machine has), and at most
    /// <see cref="MaxUsesPerSecond"/> a second per player. In singleplayer nothing is started and this does nothing: BalconyDoorState.Relay stays
    /// null and a door only changes on this machine.
    /// </summary>
    public sealed class BalconyDoorsNetwork : SessionRelay, IBalconyDoorRelay
    {
        /// <summary>The most doors one player may open or shut per second (a hand is not faster than this).</summary>
        public const int MaxUsesPerSecond = 8;

        /// <summary>How many door changes and full states this machine has heard from the server (for tests).</summary>
        public int UsesHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many doors stood open in the last full state heard (for tests).</summary>
        public int LastStateSize { get; private set; }

        private readonly SenderRate _rate = new SenderRate(MaxUsesPerSecond);
        private readonly List<BalconyDoor> _doors = new List<BalconyDoor>();

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<BalconyDoorMessage>(server, on, OnUseAtServer);
            Hear<BalconyDoorStateRequest>(server, on, OnStateRequestAtServer);
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<BalconyDoorMessage>(client, on, OnUseAtClient);
            Hear<BalconyDoorStateMessage>(client, on, OnStateAtClient);
            if (on)
            {
                BalconyDoorState.Shared.Relay = this;
            }
            else
            {
                if (ReferenceEquals(BalconyDoorState.Shared.Relay, this))
                {
                    BalconyDoorState.Shared.Relay = null;
                }
            }
        }

        // A joining player asks which doors stand open, once the server has authenticated it (a broadcast before that is dropped).
        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new BalconyDoorStateRequest());
        }

        private void OnDisable()
        {
            if (ReferenceEquals(BalconyDoorState.Shared.Relay, this))
            {
                BalconyDoorState.Shared.Relay = null;
            }
        }

        public void Send(int building, int unit, int row, bool open)
        {
            if (CanSend)
            {
                Session.ClientManager.Broadcast(new BalconyDoorMessage { Building = building, Unit = unit, Row = row, Open = open });
            }
        }

        private void OnUseAtServer(NetworkConnection sender, BalconyDoorMessage message, Channel channel)
        {
            if (sender == null || !_rate.Allow(sender.ClientId, Time.unscaledTime) || !Acceptable(message, _doors))
            {
                return;
            }
            BalconyDoorState.Shared.Set(message.Building, message.Unit, message.Row, message.Open);
            Session.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, BalconyDoorStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            List<long> open = BalconyDoorState.Shared.AllOpen();
            var state = new BalconyDoorStateMessage { Building = new int[open.Count], Unit = new int[open.Count], Row = new int[open.Count] };
            for (int i = 0; i < open.Count; i++)
            {
                BalconyDoorState.Unkey(open[i], out state.Building[i], out state.Unit[i], out state.Row[i]);
            }
            Session.ServerManager.Broadcast(sender, state);
        }

        private void OnUseAtClient(BalconyDoorMessage message, Channel channel)
        {
            UsesHeard++;
            if (Acceptable(message, _doors))
            {
                BalconyDoorState.Shared.Set(message.Building, message.Unit, message.Row, message.Open);
            }
        }

        private void OnStateAtClient(BalconyDoorStateMessage state, Channel channel)
        {
            StatesHeard++;
            int n = state.Building == null ? 0 : Mathf.Min(state.Building.Length, Mathf.Min(state.Unit?.Length ?? 0, state.Row?.Length ?? 0));
            LastStateSize = n;
            for (int i = 0; i < n; i++)
            {
                var message = new BalconyDoorMessage { Building = state.Building[i], Unit = state.Unit[i], Row = state.Row[i], Open = true };
                if (Acceptable(message, _doors))
                {
                    BalconyDoorState.Shared.Set(message.Building, message.Unit, message.Row, true);
                }
            }
        }

        /// <summary>
        /// A door message names a door that exists and opens, in the world every machine builds from the same seed. Without a world yet (a
        /// machine still loading) only the numbers are checked. Public static so it is tested without a network; <paramref name="scratch"/> is
        /// refilled.
        /// </summary>
        public static bool Acceptable(BalconyDoorMessage message, List<BalconyDoor> scratch)
        {
            if (message.Building < 0 || message.Unit < 0 || message.Unit > 0xFFF || message.Row < 1 || message.Row > 0xFFF)
            {
                return false;
            }
            CityWorld world = CurrentWorld.Instance;
            if (world == null)
            {
                return true;
            }
            if (message.Building >= world.BuildingCount)
            {
                return false;
            }
            BuildingSite site = world.Building(message.Building);
            BalconyDoors.For(site, WindowPlan.For(site), scratch);
            int i = BalconyDoors.Find(scratch, message.Unit, message.Row);
            return i >= 0 && scratch[i].Opens;
        }
    }
}
