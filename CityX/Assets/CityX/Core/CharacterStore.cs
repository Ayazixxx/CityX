using System.Collections.Generic;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// The characters saved on THIS PC (<see cref="CharacterFile"/> in the game's data folder): where every character lived before they
    /// moved onto the server (<see cref="CharacterDatabase"/>), and where an older save is read from to move them.
    /// </summary>
    public static class CharacterStore
    {
        public const string FileName = CharacterFile.FileName;

        /// <summary>
        /// Overridable so tests can point at a scratch directory instead of the
        /// real save location -- otherwise running the suite would eat the
        /// characters of whoever ran it.
        /// </summary>
        public static string DirectoryOverride;

        public static string Directory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? DataFolder.Path
                : DirectoryOverride;

        public static string Path_ => Local.Path;

        private static CharacterFile Local => new CharacterFile(Directory);

        /// <inheritdoc cref="CharacterFile.Load"/>
        public static List<CharacterSave> Load() => Local.Load();

        /// <inheritdoc cref="CharacterFile.Save"/>
        public static bool Save(List<CharacterSave> characters) => Local.Save(characters);

        /// <inheritdoc cref="CharacterFile.Add"/>
        public static List<CharacterSave> Add(CharacterSave character) => Local.Add(character);

        /// <inheritdoc cref="CharacterFile.Delete"/>
        public static List<CharacterSave> Delete(string id) => Local.Delete(id);

        /// <inheritdoc cref="CharacterFile.Update"/>
        public static bool Update(CharacterSave character) => Local.Update(character);
    }
}
