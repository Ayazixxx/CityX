using System.Collections.Generic;

namespace CityX.Core
{
    /// <summary>
    /// What a server lets a player do with their characters, whatever the player's copy of the game asks: list them, make one, delete one,
    /// play one, and save one being played. Plain, so it is tested without a network; <c>CharacterServer</c> (Net) wires it to the
    /// connections.
    ///
    /// A player only ever reaches their own characters (their <see cref="PlayerKey"/>'s folder). A new character always starts at the
    /// start, whatever it is sent with. A save still takes the progress the player's copy reports: deciding XP, levels and items on the
    /// server is the next step (docs/GAME_DESIGN.md, Technical foundations 1).
    /// </summary>
    public sealed class CharacterService
    {
        /// <summary>How many characters one player may keep on a server.</summary>
        public const int MaxCharactersPerPlayer = 20;

        private readonly CharacterDatabase _database;

        public CharacterService(CharacterDatabase database)
        {
            _database = database;
        }

        /// <summary>The player's characters, newest-played first; none for a key that is not one.</summary>
        public List<CharacterSave> List(string playerKey)
        {
            CharacterFile file = _database.For(playerKey);
            return file != null ? file.Load() : new List<CharacterSave>();
        }

        /// <summary>
        /// Makes a new character from what the creator chose: its name and look only (skin, paint, hair). Its id, level, stats, skills,
        /// clothes and playtime are a new character's, whatever was sent. Null when the key is not one or the player has no room left.
        /// </summary>
        public CharacterSave Create(string playerKey, CharacterSave requested)
        {
            CharacterFile file = _database.For(playerKey);
            if (file == null || requested == null || file.Load().Count >= MaxCharactersPerPlayer)
            {
                return null;
            }
            CharacterSave made = CharacterSave.Create(requested.Name, requested.Skin);
            CopyLook(requested, made);
            made.Repair();
            file.Add(made);
            return made;
        }

        /// <summary>Deletes one of the player's characters. False when it is not theirs.</summary>
        public bool Delete(string playerKey, string characterId)
        {
            CharacterFile file = _database.For(playerKey);
            if (file == null || Find(file, characterId) == null)
            {
                return false;
            }
            file.Delete(characterId);
            return true;
        }

        /// <summary>One of the player's characters, as saved, to play; null when it is not theirs.</summary>
        public CharacterSave Play(string playerKey, string characterId)
        {
            CharacterFile file = _database.For(playerKey);
            return file != null ? Find(file, characterId) : null;
        }

        /// <summary>
        /// Saves a character being played over its stored copy: its progress, its look (the in-game redraw changes it), its clothes and
        /// playtime. Its id, name and when it was made stay the server's. False when it is not the player's.
        /// </summary>
        public bool Save(string playerKey, CharacterSave reported)
        {
            CharacterFile file = _database.For(playerKey);
            CharacterSave stored = file != null && reported != null ? Find(file, reported.Id) : null;
            if (stored == null)
            {
                return false;
            }
            stored.Level = reported.Level;
            stored.Experience = reported.Experience;
            stored.StatPoints = reported.StatPoints;
            stored.Stats = reported.Stats;
            stored.Vitality = reported.Vitality;
            stored.Endurance = reported.Endurance;
            stored.Strength = reported.Strength;
            stored.SkillsVersion = reported.SkillsVersion;
            stored.SkillPoints = reported.SkillPoints;
            stored.LearnedSkills = reported.LearnedSkills;
            stored.Hotbar = reported.Hotbar;
            stored.Wearing = reported.Wearing;
            stored.PlaytimeSeconds = reported.PlaytimeSeconds;
            stored.LastPlayedUnixSeconds = reported.LastPlayedUnixSeconds;
            CopyLook(reported, stored);
            stored.Repair();
            return file.Update(stored);
        }

        private static void CopyLook(CharacterSave from, CharacterSave to)
        {
            to.Skin = from.Skin;
            to.Paint = from.Paint;
            to.HairStyle = from.HairStyle;
            to.HairColour = from.HairColour;
        }

        private static CharacterSave Find(CharacterFile file, string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                return null;
            }
            return file.Load().Find(c => c.Id == characterId);
        }
    }
}
