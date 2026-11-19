using System.IO;

namespace CityX.Core
{
    /// <summary>
    /// The server this copy of the game runs when it is one: in singleplayer and when hosting, this PC is the server, so its characters and
    /// admin list are read here directly; a dedicated server reads the same way. Its data lives under the data folder
    /// (<see cref="CharacterDatabase.LocalServerRoot"/>). The first time it is used for a data folder, the characters saved on this PC
    /// before servers kept them move in under this copy's own <see cref="PlayerKey"/>.
    /// </summary>
    public static class LocalServer
    {
        private static CharacterDatabase _database;
        private static CharacterService _characters;

        public static CharacterDatabase Database
        {
            get
            {
                string root = CharacterDatabase.LocalServerRoot;
                if (_database == null || _database.Root != root)
                {
                    _database = new CharacterDatabase(root);
                    _characters = new CharacterService(_database);
                    _database.MoveInLocalCharacters(PlayerKey.Local, Path.GetDirectoryName(root));
                }
                return _database;
            }
        }

        public static CharacterService Characters
        {
            get
            {
                CharacterDatabase _ = Database;
                return _characters;
            }
        }

        public static bool IsAdmin(string playerKey) => Database.LoadAdmins().Includes(playerKey);

        // Which character each connection to this server plays (its id as the network knows it -> the character's id): set when the
        // connection asks to play one (CharacterServer), dropped when it leaves.
        private static readonly System.Collections.Generic.Dictionary<int, string> Playing = new System.Collections.Generic.Dictionary<int, string>();

        /// <summary>Connection <paramref name="clientId"/> now plays <paramref name="characterId"/>.</summary>
        public static void Plays(int clientId, string characterId) => Playing[clientId] = characterId;

        /// <summary>Connection <paramref name="clientId"/> left.</summary>
        public static void Left(int clientId) => Playing.Remove(clientId);

        /// <summary>
        /// This server gave the player on connection <paramref name="clientId"/> one <paramref name="itemId"/> (a dropped item, a fallen enemy's
        /// gear: docs/SERVER_SAVING.md, step 3b). Nothing for a connection playing no character here (this machine's own player's bag is its own).
        /// </summary>
        public static void HandTo(int clientId, string itemId)
        {
            if (Playing.TryGetValue(clientId, out string characterId))
            {
                Characters.Hand(characterId, itemId);
            }
        }
    }
}
