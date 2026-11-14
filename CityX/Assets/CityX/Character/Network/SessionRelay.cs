using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A piece of the world kept the same on every machine by FishNet broadcasts (the light switches, the balcony doors, the broken glass,
    /// the graffiti, the dropped items, the weather, the enemies). It finds the session; as this machine starts or stops being the session's
    /// server, and a client of it, the relay hooks or unhooks what it hears (<see cref="HearAsServer"/>, <see cref="HearAsClient"/>); and
    /// once the server has let this client in, the relay can ask for what it missed (<see cref="OnLetIn"/>: a broadcast sent before that
    /// is dropped). With no session running (singleplayer) it does nothing.
    /// </summary>
    public abstract class SessionRelay : MonoBehaviour
    {
        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        private bool _hearsAsServer;
        private bool _letIn;

        /// <summary>The session, once one has been found.</summary>
        protected NetworkManager Session { get; private set; }

        /// <summary>This machine is hooked up as a client of the session.</summary>
        protected bool HearsAsClient { get; private set; }

        /// <summary>This machine is a started client of the session: what it broadcasts reaches the server.</summary>
        protected bool CanSend => Session != null && Session.IsClientStarted;

        protected virtual void Update()
        {
            if (Session == null)
            {
                Session = Manager != null ? Manager : InstanceFinder.NetworkManager;
                if (Session == null)
                {
                    return;
                }
            }

            bool server = Session.IsServerStarted;
            if (server != _hearsAsServer)
            {
                HearAsServer(Session.ServerManager, server);
                _hearsAsServer = server;
            }

            bool client = Session.IsClientStarted;
            if (client != HearsAsClient)
            {
                HearAsClient(Session.ClientManager, client);
                HearsAsClient = client;
                _letIn = false;
            }

            if (HearsAsClient && !_letIn && Session.ClientManager.Connection != null && Session.ClientManager.Connection.IsAuthenticated)
            {
                _letIn = true;
                OnLetIn(Session.ClientManager);
            }
        }

        /// <summary>This machine has started (<paramref name="on"/>) or stopped being the session's server: hook or unhook what it hears from clients.</summary>
        protected abstract void HearAsServer(ServerManager server, bool on);

        /// <summary>This machine has started (<paramref name="on"/>) or stopped being a client of the session: hook or unhook what it hears from the server.</summary>
        protected abstract void HearAsClient(ClientManager client, bool on);

        /// <summary>The server has let this client in: the moment a joining player asks for the state it missed.</summary>
        protected virtual void OnLetIn(ClientManager client)
        {
        }

        /// <summary>Hooks (<paramref name="on"/>) or unhooks what the server does with a <typeparamref name="T"/> from a client.</summary>
        protected static void Hear<T>(ServerManager server, bool on, System.Action<NetworkConnection, T, Channel> handler) where T : struct, IBroadcast
        {
            if (on)
            {
                server.RegisterBroadcast(handler);
            }
            else
            {
                server.UnregisterBroadcast(handler);
            }
        }

        /// <summary>Hooks (<paramref name="on"/>) or unhooks what this client does with a <typeparamref name="T"/> from the server.</summary>
        protected static void Hear<T>(ClientManager client, bool on, System.Action<T, Channel> handler) where T : struct, IBroadcast
        {
            if (on)
            {
                client.RegisterBroadcast(handler);
            }
            else
            {
                client.UnregisterBroadcast(handler);
            }
        }
    }
}
