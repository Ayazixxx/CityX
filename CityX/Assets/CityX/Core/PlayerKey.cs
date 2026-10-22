using System;
using System.IO;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// Who this copy of the game is to a server: a random key made the first time the game runs and kept in its data folder. A server files
    /// a player's characters under it and checks it against its admin list. A stand-in for an account: Steam's id replaces it later.
    ///
    /// Anyone holding someone's key is them to a server, as with any login, so it is never shown or sent anywhere but the server joined.
    /// </summary>
    public static class PlayerKey
    {
        public const string FileName = "player.key";

        /// <summary>The game's data folder, beside the characters saved on this PC (so a test's scratch folder covers it too).</summary>
        public static string Directory => CharacterStore.Directory;

        private static string _cached;
        private static string _cachedFrom;

        /// <summary>This copy's key: read from its file, or made and written the first time.</summary>
        public static string Local
        {
            get
            {
                string path = Path.Combine(Directory, FileName);
                if (_cached != null && _cachedFrom == path)
                {
                    return _cached;
                }
                string key = null;
                try
                {
                    key = File.Exists(path) ? File.ReadAllText(path).Trim() : null;
                }
                catch (Exception error)
                {
                    Debug.LogError($"CityX: could not read {path} -- {error.Message}");
                }
                if (!IsValid(key))
                {
                    key = Guid.NewGuid().ToString("N");
                    try
                    {
                        System.IO.Directory.CreateDirectory(Directory);
                        File.WriteAllText(path, key);
                    }
                    catch (Exception error)
                    {
                        Debug.LogError($"CityX: could not write {path} -- {error.Message}. This session has a key of its own only.");
                    }
                }
                _cached = key;
                _cachedFrom = path;
                return key;
            }
        }

        /// <summary>
        /// A key as one is made: 32 lowercase hex digits. A server files characters in a folder named by the key, so anything else (a path,
        /// "..", an empty string) is refused before it gets near the disk.
        /// </summary>
        public static bool IsValid(string key)
        {
            if (key == null || key.Length != 32)
            {
                return false;
            }
            foreach (char c in key)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
