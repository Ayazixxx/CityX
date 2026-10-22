using CityX.Core;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Net
{
    /// <summary>
    /// Answers players' questions about their characters while this copy is a server (hosting, or a dedicated server), from the characters
    /// it keeps (<see cref="LocalServer"/>, under <see cref="CharacterService"/>'s rules). Sits beside the NetworkManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class CharacterServer : MonoBehaviour
    {
        private NetworkManager _manager;
        private bool _listening;

        private void Awake()
        {
            _manager = GetComponent<NetworkManager>();
            _manager.ServerManager.OnServerConnectionState += OnServerState;
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.ServerManager.OnServerConnectionState -= OnServerState;
                StopListening();
            }
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && !_listening)
            {
                _manager.ServerManager.RegisterBroadcast<CharacterRequest>(OnRequest, requireAuthentication: false);
                _listening = true;
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                StopListening();
            }
        }

        private void StopListening()
        {
            if (_listening)
            {
                _manager.ServerManager.UnregisterBroadcast<CharacterRequest>(OnRequest);
                _listening = false;
            }
        }

        private void OnRequest(NetworkConnection connection, CharacterRequest request, Channel channel)
        {
            ServerManager server = _manager.ServerManager;
            CharacterService characters = LocalServer.Characters;
            var reply = new CharacterReply { Ticket = request.Ticket, Kind = request.Kind };
            switch (request.Kind)
            {
                case CharacterRequest.List:
                    foreach (CharacterSave character in characters.List(request.PlayerKey))
                    {
                        server.Broadcast(connection, new CharacterReply { Ticket = request.Ticket, Kind = CharacterReply.Item, Ok = true, Payload = JsonUtility.ToJson(character) }, false);
                    }
                    reply.Kind = CharacterReply.Done;
                    reply.Ok = true;
                    break;
                case CharacterRequest.Create:
                    CharacterSave made = characters.Create(request.PlayerKey, Parse(request.Payload));
                    reply.Ok = made != null;
                    reply.Payload = made != null ? JsonUtility.ToJson(made) : null;
                    break;
                case CharacterRequest.Delete:
                    reply.Ok = characters.Delete(request.PlayerKey, request.Payload);
                    break;
                case CharacterRequest.Play:
                    CharacterSave served = characters.Play(request.PlayerKey, request.Payload);
                    reply.Ok = served != null;
                    reply.Payload = served != null ? JsonUtility.ToJson(served) : null;
                    reply.IsAdmin = LocalServer.IsAdmin(request.PlayerKey);
                    break;
                case CharacterRequest.Save:
                    characters.Save(request.PlayerKey, Parse(request.Payload), request.AdminEdited);
                    return;   // saving needs no answer
                default:
                    return;
            }
            server.Broadcast(connection, reply, false);
        }

        /// <summary>A character sent as JSON, or null for anything that is not one.</summary>
        public static CharacterSave Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                return JsonUtility.FromJson<CharacterSave>(json);
            }
            catch (System.ArgumentException)
            {
                return null;
            }
        }
    }
}
