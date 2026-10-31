using CityX.World;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
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

    /// <summary>Every broken piece of Old Guy's house, for a joining player.</summary>
    public struct TutorialHouseStateMessage : IBroadcast
    {
        public int[] Pieces;
    }

    /// <summary>
    /// Keeps what is broken of Old Guy's house (World.TutorialHouseDamage: the front door's planks, the windows) the same on every machine in a session,
    /// as the balcony doors are (BalconyDoorsNetwork): a piece broken goes client -> server -> every client, and a joining player asks the server for
    /// every piece broken so far. Broadcasts, not RPCs on a body: a window broken while you were across the city is broken when you get there.
    ///
    /// THE SERVER CHECKS: the piece is one the house has (TutorialHouseDamage.Acceptable, from the plan every machine has), and at most
    /// <see cref="MaxBreaksPerSecond"/> a second per player. In singleplayer nothing is started and this does nothing.
    /// </summary>
    public sealed class TutorialHouseNetwork : MonoBehaviour, ITutorialHouseRelay
    {
        /// <summary>The most pieces one player may break per second (a spray of bullets through a row of windows).</summary>
        public const int MaxBreaksPerSecond = 12;

        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        /// <summary>How many breaks and full states this machine has heard from the server (for tests).</summary>
        public int BreaksHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many pieces were broken in the last full state heard (for tests).</summary>
        public int LastStateSize { get; private set; }

        private NetworkManager _manager;
        private bool _serverHandlers;
        private bool _clientHandlers;
        private bool _requested;
        private readonly SenderRate _rate = new SenderRate(MaxBreaksPerSecond);

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
                    _manager.ServerManager.RegisterBroadcast<TutorialHouseBreakMessage>(OnBreakAtServer);
                    _manager.ServerManager.RegisterBroadcast<TutorialHouseStateRequest>(OnStateRequestAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<TutorialHouseBreakMessage>(OnBreakAtServer);
                    _manager.ServerManager.UnregisterBroadcast<TutorialHouseStateRequest>(OnStateRequestAtServer);
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<TutorialHouseBreakMessage>(OnBreakAtClient);
                    _manager.ClientManager.RegisterBroadcast<TutorialHouseStateMessage>(OnStateAtClient);
                    TutorialHouseDamage.Shared.Relay = this;
                    _requested = false;   // asked for once the server has let this client in (below)
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<TutorialHouseBreakMessage>(OnBreakAtClient);
                    _manager.ClientManager.UnregisterBroadcast<TutorialHouseStateMessage>(OnStateAtClient);
                    if (ReferenceEquals(TutorialHouseDamage.Shared.Relay, this))
                    {
                        TutorialHouseDamage.Shared.Relay = null;
                    }
                }
                _clientHandlers = client;
            }

            // A joining player asks what is broken, once the server has authenticated it (a broadcast before that is dropped).
            if (_clientHandlers && !_requested && _manager.ClientManager.Connection != null && _manager.ClientManager.Connection.IsAuthenticated)
            {
                _requested = true;
                _manager.ClientManager.Broadcast(new TutorialHouseStateRequest());
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(TutorialHouseDamage.Shared.Relay, this))
            {
                TutorialHouseDamage.Shared.Relay = null;
            }
        }

        public void Send(int piece)
        {
            if (_manager != null && _manager.IsClientStarted)
            {
                _manager.ClientManager.Broadcast(new TutorialHouseBreakMessage { Piece = piece });
            }
        }

        private void OnBreakAtServer(NetworkConnection sender, TutorialHouseBreakMessage message, Channel channel)
        {
            if (sender == null || !_rate.Allow(sender.ClientId, Time.unscaledTime) || !TutorialHouseDamage.Acceptable(message.Piece))
            {
                return;
            }
            TutorialHouseDamage.Shared.Told(message.Piece);
            _manager.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, TutorialHouseStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            _manager.ServerManager.Broadcast(sender, new TutorialHouseStateMessage { Pieces = TutorialHouseDamage.Shared.All().ToArray() });
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
        }
    }
}
