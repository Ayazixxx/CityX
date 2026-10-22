using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// Where a server keeps every player's characters: a folder per player (<see cref="PlayerKey"/>) under the server's data folder, each
    /// holding one <see cref="CharacterFile"/>, and the server's admin list beside them. Folders on the server's own disk for now; the
    /// design's shared database, which any server could read (docs/GAME_DESIGN.md, Servers), would sit behind these same calls.
    /// </summary>
    public sealed class CharacterDatabase
    {
        public const string AccountsFolder = "accounts";
        public const string AdminsFileName = "admins.json";

        public CharacterDatabase(string root)
        {
            Root = root;
        }

        public string Root { get; }

        /// <summary>The data folder of a server run by this copy of the game (singleplayer, or hosting).</summary>
        public static string LocalServerRoot => Path.Combine(CharacterStore.Directory, "server");

        /// <summary>A player's characters, or null for a key that is not one (nothing is ever read or written outside the accounts folder).</summary>
        public CharacterFile For(string playerKey) =>
            PlayerKey.IsValid(playerKey) ? new CharacterFile(Path.Combine(Root, AccountsFolder, playerKey)) : null;

        /// <summary>
        /// Moves the characters saved on this PC before they lived on the server into <paramref name="playerKey"/>'s folder, once: only when
        /// that player has no characters file yet. The old file is copied, not moved, and stays where it was as the backup.
        /// Returns how many characters came across.
        /// </summary>
        public int MoveInLocalCharacters(string playerKey, string localDirectory)
        {
            CharacterFile account = For(playerKey);
            var local = new CharacterFile(localDirectory);
            if (account == null || account.Exists || !local.Exists)
            {
                return 0;
            }
            List<CharacterSave> characters = local.Load();
            return account.Save(characters) ? characters.Count : 0;
        }

        /// <summary>
        /// Who may use the admin tools (the console). Owner, 2026-10-22: "i only want me and other admins to have access to console right
        /// now anyone with access to the game would be an admin": while <see cref="EveryoneIsAdmin"/> is on, every player is one.
        /// </summary>
        [Serializable]
        public sealed class Admins
        {
            public bool EveryoneIsAdmin = true;

            /// <summary>The player keys of the admins, for when <see cref="EveryoneIsAdmin"/> is turned off.</summary>
            public List<string> Keys = new List<string>();

            public bool Includes(string playerKey) => EveryoneIsAdmin || (playerKey != null && Keys.Contains(playerKey));
        }

        /// <summary>The admin list, written with everyone an admin the first time it is read, so the file is there to edit.</summary>
        public Admins LoadAdmins()
        {
            string path = Path.Combine(Root, AdminsFileName);
            try
            {
                if (File.Exists(path))
                {
                    return JsonUtility.FromJson<Admins>(File.ReadAllText(path)) ?? new Admins();
                }
                var admins = new Admins();
                System.IO.Directory.CreateDirectory(Root);
                File.WriteAllText(path, JsonUtility.ToJson(admins, prettyPrint: true));
                return admins;
            }
            catch (Exception error)
            {
                // Unreadable: nobody is an admin, rather than everybody.
                Debug.LogError($"CityX: could not read {path} -- {error.Message}. No one is an admin until it is fixed.");
                return new Admins { EveryoneIsAdmin = false };
            }
        }
    }
}
