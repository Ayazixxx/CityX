using CityX.World;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>A plank or a pane of Old Guy's house broken, on its way between machines.</summary>
    public struct TutorialHouseBreakMessage : IBroadcast
    {
        public int Piece;
    }

    /// <summary>A joining player asking what of Old Guy's house is broken.</summary>
    public struct TutorialHouseStateRequest : IBroadcast
    {
    }

    /// <summary>Old Guy's house's power turned on (its fuse box's lever thrown), on its way between machines.</summary>
    public struct TutorialHousePowerMessage : IBroadcast
    {
    }

    /// <summary>Every broken piece of Old Guy's house, and whether its power is on, for a joining player.</summary>
    public struct TutorialHouseStateMessage : IBroadcast
    {
        public int[] Pieces;
        public bool PowerOn;
    }

    /// <summary>
    /// Keeps what is broken of Old Guy's house (World.TutorialHouseDamage: the front door's planks, the windows) the same on every machine in a session,
    /// as the balcony doors are (BalconyDoorsNetwork): a piece broken goes client -> server -> every client, and a joining player asks the server for
    /// every piece broken so far. Broadcasts, not RPCs on a body: a window broken while you were across the city is broken when you get there.
    ///
    /// THE SERVER CHECKS: the piece is one the house has (TutorialHouseDamage.Acceptable, from the plan every machine has), and at most
    /// <see cref="MaxBreaksPerSecond"/> a second per player. In singleplayer nothing is started and this does nothing.
    /// </summary>
    public sealed class TutorialHouseNetwork : SessionRelay, ITutorialHouseRelay
    {
        /// <summary>The most pieces one player may break per second (a spray of bullets through a row of windows).</summary>
        public const int MaxBreaksPerSecond = 12;

        /// <summary>How many breaks and full states this machine has heard from the server (for tests).</summary>
        public int BreaksHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many pieces were broken in the last full state heard (for tests).</summary>
        public int LastStateSize { get; private set; }

        private readonly SenderRate _rate = new SenderRate(MaxBreaksPerSecond);

        /// <summary>The server's own word on the power, for joiners: not read off TutorialHousePower.Shared, which a dedicated server never hears.</summary>
        private bool _powerOn;

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<TutorialHouseBreakMessage>(server, on, OnBreakAtServer);
            Hear<TutorialHouseStateRequest>(server, on, OnStateRequestAtServer);
            Hear<TutorialHousePowerMessage>(server, on, OnPowerAtServer);
            if (!on)
            {
                _powerOn = false;
            }
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<TutorialHouseBreakMessage>(client, on, OnBreakAtClient);
            Hear<TutorialHouseStateMessage>(client, on, OnStateAtClient);
            Hear<TutorialHousePowerMessage>(client, on, OnPowerAtClient);
            if (on)
            {
                TutorialHouseDamage.Shared.Relay = this;
                TutorialHousePower.Shared.Relay = this;
            }
            else
            {
                LetGoOfTheHouse();
            }
        }

        // A joining player asks what is broken, once the server has authenticated it (a broadcast before that is dropped).
        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new TutorialHouseStateRequest());
        }

        private void OnDisable()
        {
            LetGoOfTheHouse();
        }

        private void LetGoOfTheHouse()
        {
            if (ReferenceEquals(TutorialHouseDamage.Shared.Relay, this))
            {
                TutorialHouseDamage.Shared.Relay = null;
            }
            if (ReferenceEquals(TutorialHousePower.Shared.Relay, this))
            {
                TutorialHousePower.Shared.Relay = null;
            }
        }

        public void SendPowerOn()
        {
            if (CanSend)
            {
                Session.ClientManager.Broadcast(new TutorialHousePowerMessage());
            }
        }

        /// <summary>How many times this machine has heard the power come on (for tests).</summary>
        public int PowerHeard { get; private set; }

        private void OnPowerAtServer(NetworkConnection sender, TutorialHousePowerMessage message, Channel channel)
        {
            if (sender == null || !_rate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            _powerOn = true;
            Session.ServerManager.Broadcast(message);
        }

        private void OnPowerAtClient(TutorialHousePowerMessage message, Channel channel)
        {
            PowerHeard++;
            TutorialHousePower.Shared.Told();
        }

        public void Send(int piece)
        {
            if (CanSend)
            {
                Session.ClientManager.Broadcast(new TutorialHouseBreakMessage { Piece = piece });
            }
        }

        private void OnBreakAtServer(NetworkConnection sender, TutorialHouseBreakMessage message, Channel channel)
        {
            if (sender == null || !_rate.Allow(sender.ClientId, Time.unscaledTime) || !TutorialHouseDamage.Acceptable(message.Piece))
            {
                return;
            }
            TutorialHouseDamage.Shared.Told(message.Piece);
            Session.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, TutorialHouseStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            Session.ServerManager.Broadcast(sender, new TutorialHouseStateMessage { Pieces = TutorialHouseDamage.Shared.All().ToArray(), PowerOn = _powerOn });
        }

        private void OnBreakAtClient(TutorialHouseBreakMessage message, Channel channel)
        {
            BreaksHeard++;
            TutorialHouseDamage.Shared.Told(message.Piece);
        }

        private void OnStateAtClient(TutorialHouseStateMessage state, Channel channel)
        {
            StatesHeard++;
            LastStateSize = state.Pieces?.Length ?? 0;
            for (int i = 0; i < LastStateSize; i++)
            {
                TutorialHouseDamage.Shared.Told(state.Pieces[i]);
            }
            if (state.PowerOn)
            {
                TutorialHousePower.Shared.Told();
            }
        }
    }
}
