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

    /// <summary>
    /// One piece of a design on its way from a client to the server. A client may send the server no more than 20 KB at once (FishNet's
    /// TransportManager.MaximumClientPacketSize), and a drawn or loaded design is hundreds: whole, it was refused, and a joined player's
    /// printed shirt showed plain to everyone else (bug sweep 2026-11-25).
    /// </summary>
    public struct DesignPart : IBroadcast
    {
        public string Id;

        /// <summary>The whole design's length, and which piece this is.</summary>
        public int Total;
        public int Index;

        public byte[] Bytes;
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
    /// wearer's upload may still be on its way, and later and later while it is not there). FishNet broadcasts on the city streamer, as the
    /// graffiti's are.
    ///
    /// UPLOADS GO IN PIECES (<see cref="DesignPart"/>, <see cref="PartBytes"/> each, one a frame, one design after another), as a client's
    /// messages may not be bigger. THE SERVER CHECKS what it keeps: the pieces in order, no bigger than a design can be, the whole a sane design
    /// (Core.ShirtDesignBlob: two PNGs, at most about 1.3 MB) named by its own content, and no more than <see cref="MostDesignsPerClient"/> from
    /// one client in a session.
    /// </summary>
    public sealed class DesignNetwork : SessionRelay
    {
        /// <summary>The most of a design one piece carries, bytes (under the 20 KB a client may send, with room for the rest of the message).</summary>
        public const int PartBytes = 16 * 1024;

        /// <summary>The most designs the server keeps from one client in a session.</summary>
        public const int MostDesignsPerClient = 100;

        /// <summary>How long a client first waits before asking again for a design the server did not have, and the longest it ever waits.</summary>
        public const float AskAgainSeconds = 3.0f;
        public const float AskAgainAtMostSeconds = 30.0f;

        /// <summary>The relay this machine's designs go through (null outside a session).</summary>
        public static DesignNetwork Active { get; private set; }

        private readonly Dictionary<int, int> _keptFrom = new Dictionary<int, int>();
        private readonly Dictionary<int, Incoming> _incoming = new Dictionary<int, Incoming>();
        private readonly HashSet<string> _uploaded = new HashSet<string>();
        private readonly Queue<(string Id, byte[] Blob)> _outbox = new Queue<(string Id, byte[] Blob)>();
        private (string Id, byte[] Blob) _sending;
        private int _sentParts;

        /// <summary>A design coming in at the server from one client, piece by piece.</summary>
        private sealed class Incoming
        {
            public string Id;
            public byte[] Bytes;
            public int Next;
        }
        private readonly Dictionary<string, int> _asks = new Dictionary<string, int>();
        private readonly Dictionary<string, float> _askAgainAt = new Dictionary<string, float>();

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<DesignPart>(server, on, OnPartAtServer);
            Hear<DesignRequest>(server, on, OnRequestAtServer);
            _incoming.Clear();
            _keptFrom.Clear();
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<DesignMessage>(client, on, OnDesignAtClient);
            _uploaded.Clear();
            _outbox.Clear();
            _sending = default;
            _sentParts = 0;
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
            SendAPart();
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

        /// <summary>Sends a design this machine has to the server (once a session; nothing outside one), in pieces over the next frames.</summary>
        public void Upload(string id, byte[] blob)
        {
            if (CanSend && blob != null && blob.Length > 0 && blob.Length <= ShirtDesignBlob.MaxBytes && _uploaded.Add(id))
            {
                _outbox.Enqueue((id, blob));
            }
        }

        /// <summary>Whether every design handed to <see cref="Upload"/> has gone.</summary>
        public bool AllSent => _sending.Blob == null && _outbox.Count == 0;

        /// <summary>The next piece of the design going up, one a frame (more in one frame would be bundled past what a client may send).</summary>
        private void SendAPart()
        {
            if (_sending.Blob == null)
            {
                if (_outbox.Count == 0)
                {
                    return;
                }
                _sending = _outbox.Dequeue();
                _sentParts = 0;
            }
            if (!CanSend)
            {
                return;
            }
            int from = _sentParts * PartBytes;
            int length = System.Math.Min(PartBytes, _sending.Blob.Length - from);
            var bytes = new byte[length];
            System.Buffer.BlockCopy(_sending.Blob, from, bytes, 0, length);
            Session.ClientManager.Broadcast(new DesignPart { Id = _sending.Id, Total = _sending.Blob.Length, Index = _sentParts, Bytes = bytes });
            _sentParts++;
            if (_sentParts * PartBytes >= _sending.Blob.Length)
            {
                _sending = default;
            }
        }

        /// <summary>Asks the server for a design (nothing outside a session).</summary>
        public void Request(string id)
        {
            if (!CanSend || !DesignStore.IsValidId(id))
            {
                return;
            }
            _asks.TryGetValue(id, out int asked);
            _asks[id] = asked + 1;
            Session.ClientManager.Broadcast(new DesignRequest { Id = id });
        }

        /// <summary>How long to wait before asking again after <paramref name="asked"/> asks the server could not answer: longer each time, never giving up.</summary>
        public static float AskAgainAfter(int asked) =>
            Mathf.Min(AskAgainAtMostSeconds, AskAgainSeconds * (1 << System.Math.Min(System.Math.Max(asked - 1, 0), 10)));

        private void OnPartAtServer(NetworkConnection sender, DesignPart part, Channel channel)
        {
            if (sender == null || !DesignStore.IsValidId(part.Id) || part.Bytes == null || part.Total <= 0 || part.Total > ShirtDesignBlob.MaxBytes)
            {
                return;
            }
            int from = part.Index * PartBytes;
            bool fits = part.Index >= 0 && from < part.Total && part.Bytes.Length == System.Math.Min(PartBytes, part.Total - from);
            if (part.Index == 0)
            {
                _incoming.Remove(sender.ClientId);   // a new design from this client: whatever came before it is dropped
                _keptFrom.TryGetValue(sender.ClientId, out int kept);
                if (!fits || kept >= MostDesignsPerClient || DesignStore.Local.Has(part.Id))
                {
                    return;
                }
                _incoming[sender.ClientId] = new Incoming { Id = part.Id, Bytes = new byte[part.Total] };
            }
            if (!_incoming.TryGetValue(sender.ClientId, out Incoming incoming) || !fits || incoming.Id != part.Id
                || incoming.Bytes.Length != part.Total || incoming.Next != part.Index)
            {
                _incoming.Remove(sender.ClientId);   // out of order or not what was begun: nothing of it is kept
                return;
            }
            System.Buffer.BlockCopy(part.Bytes, 0, incoming.Bytes, from, part.Bytes.Length);
            incoming.Next++;
            if (from + part.Bytes.Length < part.Total)
            {
                return;
            }
            _incoming.Remove(sender.ClientId);
            if (DesignStore.IdOf(incoming.Bytes) == incoming.Id && DesignStore.Local.Keep(incoming.Bytes) != null)
            {
                _keptFrom.TryGetValue(sender.ClientId, out int kept);
                _keptFrom[sender.ClientId] = kept + 1;
            }
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
                _asks.TryGetValue(message.Id, out int asked);
                _askAgainAt[message.Id] = Time.unscaledTime + AskAgainAfter(asked);   // not on the server yet: its wearer's upload may be on its way
                return;
            }
            DesignLibrary.Arrived(message.Id, message.Blob);
        }
    }
}
