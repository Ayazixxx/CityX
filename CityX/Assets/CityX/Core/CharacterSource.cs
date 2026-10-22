using System;
using System.Collections.Generic;

namespace CityX.Core
{
    /// <summary>
    /// Where the character screen gets this player's characters from: the server played on. Answers come back through callbacks, because a
    /// server across the network answers later; this PC's own server answers at once.
    /// </summary>
    public interface ICharacterSource
    {
        void List(Action<List<CharacterSave>> done);

        /// <summary>Makes a character from the creator's choices; the server's new character, or null when it refused.</summary>
        void Create(CharacterSave requested, Action<CharacterSave> done);

        void Delete(string characterId, Action<bool> done);

        /// <summary>The character to play as the server keeps it, and whether the player is an admin; null when it refused.</summary>
        void Play(string characterId, Action<CharacterSave, bool> done);

        /// <summary>Saves the character being played.</summary>
        void Save(CharacterSave character);
    }

    /// <summary>The server this copy runs itself (singleplayer, hosting), asked directly: it answers at once.</summary>
    public sealed class LocalCharacterSource : ICharacterSource
    {
        public void List(Action<List<CharacterSave>> done) => done(LocalServer.Characters.List(PlayerKey.Local));

        public void Create(CharacterSave requested, Action<CharacterSave> done) => done(LocalServer.Characters.Create(PlayerKey.Local, requested));

        public void Delete(string characterId, Action<bool> done) => done(LocalServer.Characters.Delete(PlayerKey.Local, characterId));

        public void Play(string characterId, Action<CharacterSave, bool> done) =>
            done(LocalServer.Characters.Play(PlayerKey.Local, characterId), LocalServer.IsAdmin(PlayerKey.Local));

        public void Save(CharacterSave character) => LocalServer.Characters.Save(PlayerKey.Local, character);
    }

    /// <summary>The server characters come from now: this PC's own, until the title joins someone else's (which puts its own in).</summary>
    public static class CharacterSources
    {
        public static readonly ICharacterSource ThisPc = new LocalCharacterSource();

        private static ICharacterSource _current;

        public static ICharacterSource Current
        {
            get => _current ?? ThisPc;
            set => _current = value;
        }
    }
}
