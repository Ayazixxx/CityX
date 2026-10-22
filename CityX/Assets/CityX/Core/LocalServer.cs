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
    }
}
