using System.Collections.Generic;
using CityX.Core;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>A design on its way to the server (a shirt pressed on a client) or from it (one a client asked for; empty: not there yet).</summary>
    public struct DesignMessage : IBroadcast
    {
        public string Id;
        public byte[] Blob;
    }

    /// <summary>A client asking the server for a design it has seen on a shirt and does not have.</summary>
    public struct DesignRequest : IBroadcast
    {
        public string Id;
    }

    /// <summary>
    /// PRINTED DESIGNS BETWEEN MACHINES (docs/HEAT_PRESS.md; owner, 2026-11-24: "visible across computers"). A shirt only carries its design's id
    /// (in the bag, worn, dropped); the pictures themselves go through here, once each. A client sends the server each design it pressed or
    /// wears (<see cref="Upload"/>, once a session); the server keeps it on disk for good (<see cref="DesignStore.Local"/>). A client that meets
    /// an id it does not have asks for it (<see cref="Request"/>), and asks again a little later while the server has not got it yet (the
    /// wearer's upload may still be on its way). FishNet broadcasts on the city streamer, as the graffiti's are.
    ///
    /// THE SERVER CHECKS what it keeps: a sane design (Core.ShirtDesignBlob: two PNGs, at most about 1.3 MB), named by its own content, and no
    /// more than one upload every <see cref="MinSecondsBetweenUploads"/> from one client.
    /// </summary>
    public sealed class DesignNetwork : SessionRelay
    {
        public const float MinSecondsBetweenUploads = 1.0f;

        /// <summary>How long a client waits before asking again for a design the server did not have.</summary>
        public const float AskAgainSeconds = 3.0f;

        /// <summary>How many times a design is asked for before giving up on it for this session.</summary>
        public const int MostAsks = 10;

        /// <summary>The relay this machine's designs go through (null outside a session).</summary>
        public static DesignNetwork Active { get; private set; }

        private readonly Dictionary<int, float> _lastUploadFrom = new Dictionary<int, float>();
        private readonly HashSet<string> _uploaded = new HashSet<string>();
        private readonly Dictionary<string, int> _asks = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _askAgainAt = new Dictionary<string, float>();

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<DesignMessage>(server, on, OnUploadAtServer);
            Hear<DesignRequest>(server, on, OnRequestAtServer);
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<DesignMessage>(client, on, OnDesignAtClient);
            _uploaded.Clear();
            _asks.Clear();
            _askAgainAt.Clear();
            if (on)
            {
                Active = this;
            }
            else if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        /// <summary>Let in: whatever was waited on before (a shirt seen while joining) is asked for now.</summary>
        protected override void OnLetIn(ClientManager client)
        {
            foreach (string id in new List<string>(DesignLibrary.StillWaiting))
            {
                Request(id);
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (_askAgainAt.Count == 0)
            {
                return;
            }
            float now = Time.unscaledTime;
            foreach (string id in new List<string>(_askAgainAt.Keys))
            {
                if (now >= _askAgainAt[id])
                {
                    _askAgainAt.Remove(id);
                    Request(id);
                }
            }
        }

        /// <summary>Sends a design this machine has to the server (once a session; nothing outside one).</summary>
        public void Upload(string id, byte[] blob)
        {
            if (CanSend && blob != null && _uploaded.Add(id))
            {
                Session.ClientManager.Broadcast(new DesignMessage { Id = id, Blob = blob });
            }
        }

        /// <summary>Asks the server for a design (nothing outside a session, or once asked too often).</summary>
        public void Request(string id)
        {
            if (!CanSend || !DesignStore.IsValidId(id))
            {
                return;
            }
            _asks.TryGetValue(id, out int asked);
            if (asked >= MostAsks)
            {
                return;
            }
            _asks[id] = asked + 1;
            Session.ClientManager.Broadcast(new DesignRequest { Id = id });
        }

        private void OnUploadAtServer(NetworkConnection sender, DesignMessage message, Channel channel)
        {
            if (sender == null || !DesignStore.IsValidId(message.Id) || DesignStore.Local.Has(message.Id))
            {
                return;
            }
            float now = Time.unscaledTime;
            if (_lastUploadFrom.TryGetValue(sender.ClientId, out float last) && now - last < MinSecondsBetweenUploads)
            {
                return;
            }
            if (message.Blob == null || DesignStore.IdOf(message.Blob) != message.Id || DesignStore.Local.Keep(message.Blob) == null)
            {
                return;
            }
            _lastUploadFrom[sender.ClientId] = now;
        }

        private void OnRequestAtServer(NetworkConnection sender, DesignRequest request, Channel channel)
        {
            if (sender == null || !DesignStore.IsValidId(request.Id))
            {
                return;
            }
            DesignStore.Local.TryLoad(request.Id, out byte[] blob);
            Session.ServerManager.Broadcast(sender, new DesignMessage { Id = request.Id, Blob = blob ?? System.Array.Empty<byte>() });
        }

        private void OnDesignAtClient(DesignMessage message, Channel channel)
        {
            if (!DesignStore.IsValidId(message.Id))
            {
                return;
            }
            if (message.Blob == null || message.Blob.Length == 0)
            {
                _askAgainAt[message.Id] = Time.unscaledTime + AskAgainSeconds;   // not on the server yet: its wearer's upload may be on its way
                return;
            }
            DesignLibrary.Arrived(message.Id, message.Blob);
        }
    }
}
