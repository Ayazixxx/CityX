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
        /// <summary>How many characters one player may keep on a server (owner, 2026-11-15: "increase the max characters to 36").</summary>
        public const int MaxCharactersPerPlayer = 36;

        /// <summary>
        /// The time allowed for a character this server has not seen played since it started (it restarted while they played): about two
        /// of the game's 30-second saves.
        /// </summary>
        public const double UnseenSeconds = 60.0;

        /// <summary>How a save's progress is judged; installed by the Character assembly as the game loads. Null: progress is taken as sent.</summary>
        public static IProgressRules Rules;

        /// <summary>How a far player's bag is judged (step 3b); installed by the Character assembly as the game loads. Null: the bag is taken as sent.</summary>
        public static IBagRules BagRules;

        /// <summary>What this server has handed each character since its last save (a dropped item picked up, a fallen enemy's gear): see <see cref="Hand"/>.</summary>
        private readonly ItemsHanded _handed = new ItemsHanded();

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
        /// Saves a character being played over its stored copy. Its look (the in-game redraw changes it), clothes, weapon in hand and hotbar are always
        /// taken. Its progress (level, XP, points, stats, skills) is taken when it adds up against the last save accepted (<see cref="Rules"/>),
        /// or when <paramref name="adminEdited"/> and the player is an admin (the console sets levels and points outright). Its id, name and
        /// when it was made stay the server's. Its bag is taken as sent from this machine's own player; from a player on this server from
        /// another machine (<paramref name="fromAfar"/>) only what it held before and what this server has handed it since (<see cref="BagRules"/>,
        /// step 3b), unless it is an admin's console edit. True when everything was taken; false when it is not the player's or its progress was
        /// refused.
        /// </summary>
        public bool Save(string playerKey, CharacterSave reported, bool adminEdited = false, bool fromAfar = false)
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
            stored.PrimaryWeapon = reported.PrimaryWeapon;
            if (reported.BagVersion >= CharacterSave.CurrentBagVersion)
            {
                stored.Bag = fromAfar && !trusted && BagRules != null ? Judged(stored, reported.Bag, now) : reported.Bag;
                stored.BagVersion = reported.BagVersion;
                stored.WeaponInHand = reported.WeaponInHand;
            }
            stored.TutorialDone |= reported.TutorialDone;
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

        /// <summary>
        /// This server gave <paramref name="characterId"/> one <paramref name="itemId"/> (it decided between the players: a dropped item picked
        /// up, a piece of a fallen enemy's gear taken), so the next saves may show it.
        /// </summary>
        public void Hand(string characterId, string itemId, int count = 1) => _handed.Hand(characterId, itemId, count, _clock());

        /// <summary>
        /// A far player's bag as the rules let it be: no more of anything than the bag held before and what was handed since. What a kept save
        /// shows more of uses that many handed ones up.
        /// </summary>
        private List<BagEntry> Judged(CharacterSave stored, List<BagEntry> reported, double now)
        {
            List<BagEntry> before = stored.BagVersion >= CharacterSave.CurrentBagVersion ? stored.Bag : BagFromBefore(stored);
            var takenOut = new List<string>();
            List<BagEntry> kept = BagRules.Keep(before ?? new List<BagEntry>(), reported ?? new List<BagEntry>(), id => _handed.Count(stored.Id, id, now), takenOut);
            if (takenOut.Count > 0)
            {
                UnityEngine.Debug.LogWarning($"CityX server: took out of {stored.Name}'s bag what it never got here: {string.Join(", ", takenOut)}.");
            }
            var counts = new Dictionary<string, int>();
            foreach (BagEntry entry in before ?? new List<BagEntry>())
            {
                counts[entry.ItemId] = (counts.TryGetValue(entry.ItemId, out int n) ? n : 0) - System.Math.Max(1, entry.Count);
            }
            foreach (BagEntry entry in kept)
            {
                counts[entry.ItemId] = (counts.TryGetValue(entry.ItemId, out int n) ? n : 0) + System.Math.Max(1, entry.Count);
            }
            foreach (KeyValuePair<string, int> more in counts)
            {
                if (more.Value > 0)
                {
                    _handed.UseUp(stored.Id, more.Key, more.Value, now);
                }
            }
            return kept;
        }

        /// <summary>What a character from before the bag was kept carried, as far as its save said: its clothes and its primary weapon.</summary>
        private static List<BagEntry> BagFromBefore(CharacterSave stored)
        {
            var bag = new List<BagEntry>();
            foreach (string itemId in stored.Wearing ?? new List<string>())
            {
                bag.Add(new BagEntry { Slot = "worn", ItemId = itemId });
            }
            if (!string.IsNullOrEmpty(stored.PrimaryWeapon))
            {
                bag.Add(new BagEntry { Slot = "worn", ItemId = stored.PrimaryWeapon });
            }
            return bag;
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
