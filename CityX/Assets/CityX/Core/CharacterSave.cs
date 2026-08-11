using System;
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
        /// The body's colour. The Godot build let you paint a whole texture in
        /// its creator; this is one colour, which is the honest subset that
        /// exists so far rather than a stand-in pretending to be the rest.
        /// </summary>
        public Color Skin = DefaultSkin;

        public int Level = 1;

        public int Experience;

        // Stat points, matching PlayerVitals' own three.
        public int Vitality;

        public int Endurance;

        public int Strength;

        /// <summary>Seconds played, shown on the select screen.</summary>
        public double PlaytimeSeconds;

        public long CreatedUnixSeconds;

        public long LastPlayedUnixSeconds;

        public static readonly Color DefaultSkin = new Color(0.78f, 0.62f, 0.53f);

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

            if (Vitality < 0) { Vitality = 0; changed = true; }
            if (Endurance < 0) { Endurance = 0; changed = true; }
            if (Strength < 0) { Strength = 0; changed = true; }

            return changed;
        }
    }
}
