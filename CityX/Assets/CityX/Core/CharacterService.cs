using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CityX.Core
{
    /// <summary>
    /// What a server lets a player do with their characters, whatever the player's copy of the game asks: list them, make one, delete one,
    /// play one, and save one being played. Plain, so it is tested without a network; <c>CharacterServer</c> (Net) wires it to the
    /// connections.
    ///
    /// A player only ever reaches their own characters (their <see cref="PlayerKey"/>'s folder). A new character always starts at the
    /// start, whatever it is sent with. A save's progress must add up against the last save this server accepted (<see cref="Rules"/>,
    /// docs/SERVER_SAVING.md step 2): what does not is refused, and the server keeps the character as it was. The one way past the rules is
    /// an admin's console edit, and only for a player on the admin list.
    /// </summary>
    public sealed class CharacterService
    {
        /// <summary>How many characters one player may keep on a server.</summary>
        public const int MaxCharactersPerPlayer = 20;

        /// <summary>
        /// The time allowed for a character this server has not seen played since it started (it restarted while they played): about two
        /// of the game's 30-second saves.
        /// </summary>
        public const double UnseenSeconds = 60.0;

        /// <summary>How a save's progress is judged; installed by the Character assembly as the game loads. Null: progress is taken as sent.</summary>
        public static IProgressRules Rules;

        private readonly CharacterDatabase _database;
        private readonly Func<double> _clock;
        private readonly Dictionary<string, double> _lastAccepted = new Dictionary<string, double>();

        /// <param name="clock">Seconds of real time, for how long a character has been played between saves; a test gives its own.</param>
        public CharacterService(CharacterDatabase database, Func<double> clock = null)
        {
            _database = database;
            if (clock == null)
            {
                Stopwatch watch = Stopwatch.StartNew();
                clock = () => watch.Elapsed.TotalSeconds;
            }
            _clock = clock;
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

        /// <summary>One of the player's characters, as saved, to play; null when it is not theirs. Its play time is counted from now.</summary>
        public CharacterSave Play(string playerKey, string characterId)
        {
            CharacterFile file = _database.For(playerKey);
            CharacterSave character = file != null ? Find(file, characterId) : null;
            if (character != null)
            {
                _lastAccepted[character.Id] = _clock();
            }
            return character;
        }

        /// <summary>
        /// Saves a character being played over its stored copy. Its look (the in-game redraw changes it), clothes and hotbar are always
        /// taken. Its progress (level, XP, points, stats, skills) is taken when it adds up against the last save accepted (<see cref="Rules"/>),
        /// or when <paramref name="adminEdited"/> and the player is an admin (the console sets levels and points outright). Its id, name and
        /// when it was made stay the server's. True when everything was taken; false when it is not the player's or its progress was refused.
        /// </summary>
        public bool Save(string playerKey, CharacterSave reported, bool adminEdited = false)
        {
            CharacterFile file = _database.For(playerKey);
            CharacterSave stored = file != null && reported != null ? Find(file, reported.Id) : null;
            if (stored == null)
            {
                return false;
            }
            double now = _clock();
            double played = _lastAccepted.TryGetValue(stored.Id, out double since) ? now - since : UnseenSeconds;
            bool trusted = adminEdited && _database.LoadAdmins().Includes(playerKey);
            string refusal = trusted || Rules == null ? null : Rules.Refusal(stored, reported, played);

            stored.Hotbar = reported.Hotbar;
            stored.Wearing = reported.Wearing;
            stored.LastPlayedUnixSeconds = reported.LastPlayedUnixSeconds;
            CopyLook(reported, stored);
            if (refusal != null)
            {
                UnityEngine.Debug.LogWarning($"CityX server: refused {stored.Name}'s progress ({refusal}); keeping level {stored.Level}.");
            }
            else
            {
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
                stored.SkillRanks = reported.SkillRanks;
                stored.PlaytimeSeconds = reported.PlaytimeSeconds;
                _lastAccepted[stored.Id] = now;
            }
            stored.Repair();
            return file.Update(stored) && refusal == null;
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
