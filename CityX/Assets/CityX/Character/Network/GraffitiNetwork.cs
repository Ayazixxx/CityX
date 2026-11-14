using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One piece of graffiti on its way between machines (a GraffitiPiece, as plain fields).</summary>
    public struct GraffitiMessage : IBroadcast
    {
        public int Id;
        public Vector3 Centre;
        public Vector3 Normal;
        public float Size;
        public byte[] Paint;

        public GraffitiPiece Piece => new GraffitiPiece { Id = Id, Centre = Centre, Normal = Normal, Size = Size, Paint = Paint };

        public static GraffitiMessage Of(GraffitiPiece piece) => new GraffitiMessage
        {
            Id = piece.Id, Centre = piece.Centre, Normal = piece.Normal, Size = piece.Size, Paint = piece.Paint,
        };
    }

    /// <summary>A joining player asking for every piece already up.</summary>
    public struct GraffitiStateRequest : IBroadcast
    {
    }

    /// <summary>
    /// Keeps every machine's graffiti the same in a session (owner, 2026-10-15: "have the drawing stay on that wall and visible to other
    /// players online"): a finished piece goes client -> server -> every client, and a joining player asks the server for every piece already
    /// up and is sent them one by one. FishNet BROADCASTS, as the light switches are (RoomLightsNetwork): a piece painted across the city
    /// must be there when you get there, beyond any body's view distance.
    ///
    /// THE SERVER CHECKS a piece is sane (GraffitiWall.IsSane: its size, its paint's weight, finite numbers), that the painter's own body is
    /// near it (<see cref="MaxReachMetres"/>), and that nobody puts up more than one piece every <see cref="MinSecondsBetween"/>. In
    /// singleplayer nothing is started and this does nothing: a piece is only put up on this machine.
    /// </summary>
    public sealed class GraffitiNetwork : SessionRelay
    {
        /// <summary>The furthest a painter's body may be from the piece they put up, metres.</summary>
        public const float MaxReachMetres = 12.0f;

        /// <summary>The least time between two pieces from one painter, seconds.</summary>
        public const float MinSecondsBetween = 1.0f;

        [Tooltip("The material graffiti is drawn with: URP Lit, see-through (built by the scene builder). Lit, so the city's light falls on it.")]
        public Material PaintMaterial;

        /// <summary>The network this machine sends its pieces through (null in singleplayer).</summary>
        public static GraffitiNetwork Active { get; private set; }

        /// <summary>Pieces heard from the server, and the last one's id (for tests).</summary>
        public int PiecesHeard { get; private set; }

        public int LastHeardId { get; private set; }

        private readonly Dictionary<int, float> _lastFrom = new Dictionary<int, float>();

        private void Awake()
        {
            if (PaintMaterial != null)
            {
                GraffitiWall.PaintMaterial = PaintMaterial;
            }
        }

        protected override void HearAsServer(ServerManager server, bool on)
        {
            Hear<GraffitiMessage>(server, on, OnPieceAtServer);
            Hear<GraffitiStateRequest>(server, on, OnStateRequestAtServer);
        }

        protected override void HearAsClient(ClientManager client, bool on)
        {
            Hear<GraffitiMessage>(client, on, OnPieceAtClient);
            if (on)
            {
                Active = this;
            }
            else
            {
                if (ReferenceEquals(Active, this))
                {
                    Active = null;
                }
            }
        }

        // A joining player asks for every piece already up, once the server has let it in.
        protected override void OnLetIn(ClientManager client)
        {
            client.Broadcast(new GraffitiStateRequest());
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        /// <summary>
        /// Puts a piece up for everyone: on this machine at once, and to the others through the server. In singleplayer (nothing started) only
        /// here. False when the piece is not a sane one.
        /// </summary>
        public static bool PutForEveryone(GraffitiPiece piece)
        {
            if (!GraffitiWall.Put(piece))
            {
                return false;
            }
            GraffitiNetwork network = Active;
            if (network != null)
            {
                network.Send(piece);
            }
            return true;
        }

        /// <summary>Sends a piece to the server, for every other machine (nothing when this machine is not in a session).</summary>
        public void Send(GraffitiPiece piece)
        {
            if (CanSend)
            {
                Session.ClientManager.Broadcast(GraffitiMessage.Of(piece));
            }
        }

        private void OnPieceAtServer(NetworkConnection sender, GraffitiMessage message, Channel channel)
        {
            if (sender == null || !GraffitiWall.IsSane(message.Piece) || !Near(sender, message.Centre) || !Spaced(sender.ClientId))
            {
                return;
            }
            // A host's own client already put it up; a dedicated server keeps it too, for whoever joins later.
            GraffitiWall.Put(message.Piece);
            Session.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, GraffitiStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            foreach (GraffitiPiece piece in GraffitiWall.All())
            {
                Session.ServerManager.Broadcast(sender, GraffitiMessage.Of(piece));
            }
        }

        private void OnPieceAtClient(GraffitiMessage message, Channel channel)
        {
            PiecesHeard++;
            LastHeardId = message.Id;
            GraffitiWall.Put(message.Piece);   // checked again in Put: a client trusts the server's relay no further than the server trusted it
        }

        private bool Near(NetworkConnection sender, Vector3 centre)
        {
            foreach (NetworkObject owned in sender.Objects)
            {
                if (owned != null && Vector3.Distance(owned.transform.position, centre) <= MaxReachMetres)
                {
                    return true;
                }
            }
            return false;
        }

        private bool Spaced(int clientId)
        {
            float now = Time.unscaledTime;
            if (_lastFrom.TryGetValue(clientId, out float last) && now - last < MinSecondsBetween)
            {
                return false;
            }
            _lastFrom[clientId] = now;
            return true;
        }
    }
}
