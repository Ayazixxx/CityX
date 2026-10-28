using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// One saved character: who they are, what they look like, and what they
    /// have earned.
    ///
    /// A PLAIN SERIALIZABLE RECORD WITH NO BEHAVIOUR, because it is the one
    /// thing in the game that has to survive the game changing. Every field is
    /// public and every default is the value a brand new character gets, so a
    /// save written by an older build loads with the new fields sensibly blank
    /// rather than failing -- JsonUtility simply leaves absent fields at their
    /// defaults, which only works if the defaults are right.
    ///
    /// The Godot build learned this the hard way and ended up with a migration
    /// pass. Getting the defaults right is what makes that unnecessary.
    /// </summary>
    [Serializable]
    public sealed class CharacterSave
    {
        /// <summary>
        /// Stable identity, never shown. Names are the player's and can repeat
        /// or change; everything that refers to a character refers to this.
        /// </summary>
        public string Id = string.Empty;

        public string Name = string.Empty;

        /// <summary>
        /// The body's base colour, under any paint.
        /// </summary>
        public Color Skin = DefaultSkin;

        /// <summary>
        /// The painted atlas: gzipped raw RGBA, base64'd because JsonUtility
        /// has no useful representation for a byte array.
        ///
        /// EMPTY IS A VALID CHARACTER, not a broken one -- somebody who typed a
        /// name and pressed enter without painting gets a plain body in their
        /// chosen skin colour, and every save written before painting existed
        /// loads exactly that way too.
        /// </summary>
        public string Paint = string.Empty;

        public bool HasPaint => !string.IsNullOrEmpty(Paint);

        /// <summary>Stores painted pixels. Null or empty clears the paint.</summary>
        public void SetPaint(byte[] compressed)
        {
            Paint = compressed == null || compressed.Length == 0
                ? string.Empty
                : System.Convert.ToBase64String(compressed);
        }

        /// <summary>
        /// The stored paint, or null. Never throws on a corrupt string -- a
        /// hand-edited save should cost you your paint, not your character.
        /// </summary>
        public byte[] GetPaint()
        {
            if (string.IsNullOrEmpty(Paint))
            {
                return null;
            }
            try
            {
                return System.Convert.FromBase64String(Paint);
            }
            catch (System.FormatException)
            {
                Debug.LogError($"CityX: {Name}'s saved paint is not valid base64 -- ignoring it.");
                return null;
            }
        }

        /// <summary>
        /// One of CharacterHair.ValidStyles. "none" is bald and is what every
        /// save written before hair existed loads as.
        /// </summary>
        public string HairStyle = "none";

        /// <summary>
        /// The colour of the hair, any colour. A save written before hair had a colour has no
        /// such field and loads as <see cref="DefaultHairColour"/>, which is what every head wore.
        /// </summary>
        public Color HairColour = DefaultHairColour;

        public int Level = 1;

        public int Experience;

        /// <summary>Unspent points, earned on level-up. Spent via PlayerVitals.SpendStatPoints.</summary>
        public int StatPoints;

        /// <summary>
        /// The points in every stat, by its key (StatCatalog in Character, 2026-10-14). Empty in a save from before: CharacterStats.Load then
        /// takes the old three below, which are still written for the character list and older builds.
        /// </summary>
        public List<StatEntry> Stats = new List<StatEntry>();

        // The old three stats' points (Max Health, Max Stamina and Melee Damage since 2026-10-14).
        public int Vitality;

        public int Endurance;

        public int Strength;

        /// <summary>
        /// Which skill data this save holds. 0 is a save written before the skill tree existed (2026-10-14): that character keeps every skill
        /// it could already use and the number keys it had (PlayerSkills.Load). Characters made since start with vaulting and
        /// <see cref="StartingSkillPoints"/>.
        /// </summary>
        public int SkillsVersion;

        public const int CurrentSkillsVersion = 1;

        /// <summary>Unspent skill points (a separate pool from the stat points: owner, 2026-10-10).</summary>
        public int SkillPoints;

        /// <summary>A new character's first point, so the first path can be chosen straight after the tutorial teaches vaulting.</summary>
        public const int StartingSkillPoints = 1;

        /// <summary>The ids of the skills learnt (SkillCatalog in Character).</summary>
        public List<string> LearnedSkills = new List<string>();

        /// <summary>The ranks bought past the first, by skill id (a learnt skill missing here is at rank 1).</summary>
        public List<SkillRankEntry> SkillRanks = new List<SkillRankEntry>();

        /// <summary>The bar, keys 1 to 0: a skill id per slot, an empty string for an empty slot.</summary>
        public List<string> Hotbar = new List<string>();

        /// <summary>
        /// The ids of the clothes the character has on (owner, 2026-10-20: "make it so items you equipped stay equipped when you close the
        /// game"). Weapons are not kept yet.
        /// </summary>
        public List<string> Wearing = new List<string>();

        /// <summary>Seconds played, shown on the select screen.</summary>
        public double PlaytimeSeconds;

        public long CreatedUnixSeconds;

        public long LastPlayedUnixSeconds;

        public static readonly Color DefaultSkin = new Color(0.78f, 0.62f, 0.53f);

        /// <summary>character.gd's own HAIR_COLOR: the colour hair has until somebody picks another.</summary>
        public static readonly Color DefaultHairColour = new Color(0.75f, 0.55f, 0.15f);

        /// <summary>The longest name the select screen can show without eliding.</summary>
        public const int MaximumNameLength = 20;

        /// <summary>
        /// A brand new character. The id is generated here rather than by the
        /// caller so there is exactly one place that decides what makes a
        /// character distinct.
        /// </summary>
        public static CharacterSave Create(string name, Color skin)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new CharacterSave
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = CleanName(name),
                Skin = skin,
                SkillsVersion = CurrentSkillsVersion,
                SkillPoints = StartingSkillPoints,
                CreatedUnixSeconds = now,
                LastPlayedUnixSeconds = now,
            };
        }

        /// <summary>
        /// Trims, collapses runs of whitespace and caps the length.
        ///
        /// A name is typed by a person and goes straight into a UI row and a
        /// file, so it gets cleaned once, here, rather than defensively at
        /// every place that displays it.
        /// </summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Nobody";
            }

            var builder = new System.Text.StringBuilder(name.Length);
            bool lastWasSpace = false;
            foreach (char c in name.Trim())
            {
                bool space = char.IsWhiteSpace(c);
                if (space && lastWasSpace)
                {
                    continue;
                }
                builder.Append(space ? ' ' : c);
                lastWasSpace = space;
            }

            string cleaned = builder.ToString();
            return cleaned.Length > MaximumNameLength
                ? cleaned.Substring(0, MaximumNameLength).TrimEnd()
                : cleaned;
        }

        /// <summary>Playtime as "3h 12m" / "12m" / "under a minute".</summary>
        public string PlaytimeText()
        {
            int totalMinutes = (int)(PlaytimeSeconds / 60.0);
            if (totalMinutes < 1)
            {
                return "under a minute";
            }
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
        }

        /// <summary>
        /// Repairs anything a hand-edited or older save could have got wrong.
        /// Returns whether it had to change something, so the caller can decide
        /// whether the file is worth rewriting.
        /// </summary>
        public bool Repair()
        {
            bool changed = false;

            if (string.IsNullOrEmpty(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            string cleaned = CleanName(Name);
            if (cleaned != Name)
            {
                Name = cleaned;
                changed = true;
            }

            if (Level < 1)
            {
                Level = 1;
                changed = true;
            }

            if (Experience < 0)
            {
                Experience = 0;
                changed = true;
            }

            if (SkillPoints < 0)
            {
                SkillPoints = 0;
                changed = true;
            }

            if (LearnedSkills == null)
            {
                LearnedSkills = new List<string>();
                changed = true;
            }

            if (SkillRanks == null)
            {
                SkillRanks = new List<SkillRankEntry>();
                changed = true;
            }

            if (Hotbar == null)
            {
                Hotbar = new List<string>();
                changed = true;
            }

            if (Wearing == null)
            {
                Wearing = new List<string>();
                changed = true;
            }

            if (Stats == null)
            {
                Stats = new List<StatEntry>();
                changed = true;
            }

            if (PlaytimeSeconds < 0.0 || double.IsNaN(PlaytimeSeconds))
            {
                PlaytimeSeconds = 0.0;
                changed = true;
            }

            // A fully transparent or unset colour renders as an invisible
            // character, which reads as the body failing to load.
            if (Skin.a < 0.99f || (Skin.r <= 0.0f && Skin.g <= 0.0f && Skin.b <= 0.0f))
            {
                Skin = DefaultSkin;
                changed = true;
            }

            // Blank rather than bald is what an older save carries, and an
            // unknown style would leave CharacterHair with nothing to build.
            if (string.IsNullOrEmpty(HairStyle))
            {
                HairStyle = "none";
                changed = true;
            }

            // Any colour is a valid hair colour -- black included, unlike skin -- but a see-through
            // one is not a colour anybody chose.
            if (HairColour.a < 0.99f)
            {
                HairColour = DefaultHairColour;
                changed = true;
            }

            if (Vitality < 0) { Vitality = 0; changed = true; }
            if (Endurance < 0) { Endurance = 0; changed = true; }
            if (Strength < 0) { Strength = 0; changed = true; }
            if (StatPoints < 0) { StatPoints = 0; changed = true; }

            return changed;
        }
    }

    /// <summary>One stat's points in a save: by key, so stats can be added, removed or reordered without breaking old saves.</summary>
    [Serializable]
    public struct StatEntry
    {
        public string Key;
        public int Points;

        public StatEntry(string key, int points)
        {
            Key = key;
            Points = points;
        }
    }

    /// <summary>One skill's rank in a save, by the skill's id (only ranks past the first are written).</summary>
    [Serializable]
    public struct SkillRankEntry
    {
        public string Id;
        public int Rank;

        public SkillRankEntry(string id, int rank)
        {
            Id = id;
            Rank = rank;
        }
    }
}
