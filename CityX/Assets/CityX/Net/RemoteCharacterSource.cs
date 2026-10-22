using System;
using System.Collections.Generic;
using CityX.Core;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Net
{
    /// <summary>
    /// The characters of a server joined across the network: each question goes to its <see cref="CharacterServer"/> with this copy's
    /// <see cref="PlayerKey"/>, and the answer comes back later. Put in <see cref="CharacterSources.Current"/> by the title when a join
    /// succeeds.
    /// </summary>
    public sealed class RemoteCharacterSource : ICharacterSource, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly Dictionary<int, Action<CharacterReply>> _waiting = new Dictionary<int, Action<CharacterReply>>();
        private readonly Dictionary<int, List<CharacterSave>> _lists = new Dictionary<int, List<CharacterSave>>();
        private int _nextTicket = 1;

        public RemoteCharacterSource(NetworkManager manager)
        {
            _manager = manager;
            _manager.ClientManager.RegisterBroadcast<CharacterReply>(OnReply);
        }

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.ClientManager.UnregisterBroadcast<CharacterReply>(OnReply);
            }
        }

        public void List(Action<List<CharacterSave>> done)
        {
            _lists[_nextTicket] = new List<CharacterSave>();   // the ticket Ask is about to use: ready before any answer can arrive
            Ask(CharacterRequest.List, null, reply => done(TakeList(reply.Ticket)));
        }

        public void Create(CharacterSave requested, Action<CharacterSave> done) =>
            Ask(CharacterRequest.Create, JsonUtility.ToJson(requested), reply => done(reply.Ok ? CharacterServer.Parse(reply.Payload) : null));

        public void Delete(string characterId, Action<bool> done) =>
            Ask(CharacterRequest.Delete, characterId, reply => done(reply.Ok));

        public void Play(string characterId, Action<CharacterSave, bool> done) =>
            Ask(CharacterRequest.Play, characterId, reply => done(reply.Ok ? CharacterServer.Parse(reply.Payload) : null, reply.IsAdmin));

        public void Save(CharacterSave character, bool adminEdited) => Ask(CharacterRequest.Save, JsonUtility.ToJson(character), null, adminEdited);

        private int Ask(string kind, string payload, Action<CharacterReply> answered, bool adminEdited = false)
        {
            int ticket = _nextTicket++;
            if (answered != null)
            {
                _waiting[ticket] = answered;
            }
            _manager.ClientManager.Broadcast(new CharacterRequest { Ticket = ticket, Kind = kind, PlayerKey = PlayerKey.Local, Payload = payload, AdminEdited = adminEdited });
            return ticket;
        }

        private void OnReply(CharacterReply reply, Channel channel)
        {
            if (reply.Kind == CharacterReply.Item)
            {
                CharacterSave character = CharacterServer.Parse(reply.Payload);
                if (character != null && _lists.TryGetValue(reply.Ticket, out List<CharacterSave> list))
                {
                    list.Add(character);
                }
                return;
            }
            if (_waiting.TryGetValue(reply.Ticket, out Action<CharacterReply> answered))
            {
                _waiting.Remove(reply.Ticket);
                answered(reply);
            }
        }

        private List<CharacterSave> TakeList(int ticket)
        {
            List<CharacterSave> list = _lists.TryGetValue(ticket, out List<CharacterSave> found) ? found : new List<CharacterSave>();
            _lists.Remove(ticket);
            return list;
        }
    }
}
