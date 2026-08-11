using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// Where saved characters live, and the only thing that reads or writes
    /// them.
    ///
    /// ONE FILE, NOT ONE PER CHARACTER. The list is small and always loaded
    /// whole -- the select screen shows all of them at once -- and a single
    /// file cannot end up half-written across characters, which a directory of
    /// files can if the game dies mid-save.
    ///
    /// WRITTEN VIA A TEMPORARY AND THEN MOVED. A save is the one file whose
    /// loss the player actually feels, and writing in place means a crash
    /// during the write leaves a truncated file where their characters used to
    /// be. Writing beside it and replacing is atomic enough that the worst case
    /// is losing the newest session rather than everything.
    /// </summary>
    public static class CharacterStore
    {
        public const string FileName = "characters.json";

        /// <summary>
        /// Overridable so tests can point at a scratch directory instead of the
        /// real save location -- otherwise running the suite would eat the
        /// characters of whoever ran it.
        /// </summary>
        public static string DirectoryOverride;

        public static string Directory =>
            string.IsNullOrEmpty(DirectoryOverride)
                ? Application.persistentDataPath
                : DirectoryOverride;

        public static string Path_ => System.IO.Path.Combine(Directory, FileName);

        /// <summary>JsonUtility cannot serialize a bare list, so it gets a container.</summary>
        [Serializable]
        private sealed class Payload
        {
            public List<CharacterSave> Characters = new List<CharacterSave>();
        }

        /// <summary>
        /// Every saved character, newest-played first.
        ///
        /// NEVER THROWS AND NEVER RETURNS NULL. A missing file is a new player,
        /// and a corrupt one is a bad day that should still let them into the
        /// game -- so both come back as an empty list with the corrupt case
        /// logged loudly and the file left alone for inspection rather than
        /// overwritten.
        /// </summary>
        public static List<CharacterSave> Load()
        {
            string path = Path_;
            if (!File.Exists(path))
            {
                return new List<CharacterSave>();
            }

            Payload payload;
            try
            {
                payload = JsonUtility.FromJson<Payload>(File.ReadAllText(path));
            }
            catch (Exception error)
            {
                Debug.LogError($"CityX: could not read {path} -- {error.Message}. Starting with " +
                               "no characters; the file has been left where it is.");
                return new List<CharacterSave>();
            }

            if (payload?.Characters == null)
            {
                return new List<CharacterSave>();
            }

            var characters = new List<CharacterSave>();
            foreach (CharacterSave character in payload.Characters)
            {
                if (character == null)
                {
                    continue;
                }
                character.Repair();
                characters.Add(character);
            }

            characters.Sort((a, b) => b.LastPlayedUnixSeconds.CompareTo(a.LastPlayedUnixSeconds));
            return characters;
        }

        /// <summary>Writes the whole list. Returns whether it got to disk.</summary>
        public static bool Save(List<CharacterSave> characters)
        {
            if (characters == null)
            {
                return false;
            }

            string directory = Directory;
            string path = Path_;
            string temporary = path + ".tmp";

            try
            {
                System.IO.Directory.CreateDirectory(directory);
                var payload = new Payload { Characters = characters };
                File.WriteAllText(temporary, JsonUtility.ToJson(payload, prettyPrint: true));

                // Replace rather than Move: Move refuses to clobber on Windows,
                // so the first save would work and every one after it would
                // silently fail with the temporary file left behind.
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporary, path);
                }
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"CityX: could not write {path} -- {error.Message}");
                return false;
            }
        }

        /// <summary>Adds a character and writes. Returns the saved list.</summary>
        public static List<CharacterSave> Add(CharacterSave character)
        {
            List<CharacterSave> characters = Load();
            characters.Insert(0, character);
            Save(characters);
            return characters;
        }

        /// <summary>Removes by id and writes. Returns the saved list.</summary>
        public static List<CharacterSave> Delete(string id)
        {
            List<CharacterSave> characters = Load();
            characters.RemoveAll(c => c.Id == id);
            Save(characters);
            return characters;
        }

        /// <summary>
        /// Writes one character back over its stored copy, matched by id.
        ///
        /// Reads the list fresh rather than trusting a copy the caller has been
        /// holding since the menu -- a session lasts as long as somebody plays,
        /// and rewriting a stale whole-list snapshot at the end of it would
        /// undo anything else that happened in between.
        /// </summary>
        public static bool Update(CharacterSave character)
        {
            if (character == null || string.IsNullOrEmpty(character.Id))
            {
                return false;
            }

            List<CharacterSave> characters = Load();
            int found = characters.FindIndex(c => c.Id == character.Id);
            if (found < 0)
            {
                characters.Insert(0, character);
            }
            else
            {
                characters[found] = character;
            }
            return Save(characters);
        }
    }
}
