using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The server's check on a save's progress (docs/SERVER_SAVING.md, step 2), judged on what changed since the last save it accepted, so
    /// it holds for any character, however its numbers got where they are (an old save, an admin's console edit):
    /// <list type="bullet">
    /// <item>the XP gained fits the time played: at most <see cref="XpPerSecond"/> a second, plus one burst of <see cref="XpBurst"/>;</item>
    /// <item>the XP towards the next level is less than that level needs;</item>
    /// <item>the stat and skill points (spent and unspent together) grow by no more than the levels gained bring;</item>
    /// <item>every skill newly learnt is a real one, and what it requires is learnt too;</item>
    /// <item>playtime grows no faster than real time.</item>
    /// </list>
    /// XP itself comes from kills the player's own copy decides, as long as enemies run on each player's machine (GAME_DESIGN, Technical
    /// foundations 3): until they run on the server, the speed limit is what holds it.
    /// </summary>
    public sealed class ProgressRules : IProgressRules
    {
        /// <summary>
        /// The most XP a second of play can earn: kills give 20-80 and a spell can take a crowd, so this is far above playing well, and far
        /// below giving yourself thousands of levels.
        /// </summary>
        public const double XpPerSecond = 100.0;

        /// <summary>One big moment on top (a crowd caught by Self Smite).</summary>
        public const double XpBurst = 1000.0;

        /// <summary>The playtime a save may run ahead of the server's clock (the two clocks start a moment apart).</summary>
        public const double PlaytimeSlackSeconds = 5.0;

        /// <summary>Installs the rules on every server this copy of the game runs, before any scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            CharacterService.Rules = new ProgressRules();
        }

        public string Refusal(CharacterSave stored, CharacterSave reported, double secondsPlayed)
        {
            if (reported.Level < 1 || reported.Experience < 0 || reported.Experience >= ExperienceToNext(reported.Level))
            {
                return $"level {reported.Level} with {reported.Experience} XP is not a place on the curve";
            }
            double gained = TotalExperience(reported) - TotalExperience(stored);
            if (gained > XpPerSecond * secondsPlayed + XpBurst)
            {
                return $"{gained:0} XP in {secondsPlayed:0} s";
            }
            if (reported.PlaytimeSeconds - stored.PlaytimeSeconds > secondsPlayed + PlaytimeSlackSeconds)
            {
                return $"{reported.PlaytimeSeconds - stored.PlaytimeSeconds:0} s of play in {secondsPlayed:0} s";
            }
            (int statsEarned, int skillsEarned) = PointsBetween(stored.Level, reported.Level);
            int statsGrew = StatBudget(reported) - StatBudget(stored);
            if (statsGrew > statsEarned)
            {
                return $"{statsGrew} stat point(s) more, {statsEarned} earned";
            }
            if (stored.SkillsVersion >= CharacterSave.CurrentSkillsVersion)
            {
                // A save from before the skill tree kept every skill it could already use, for free: its first save after is not counted.
                int skillsGrew = SkillBudget(reported) - SkillBudget(stored);
                if (skillsGrew > skillsEarned)
                {
                    return $"{skillsGrew} skill point(s) more, {skillsEarned} earned";
                }
                string unreachable = UnreachableSkill(stored, reported);
                if (unreachable != null)
                {
                    return unreachable;
                }
            }
            return null;
        }

        /// <summary>The XP a level needs to reach the next (PlayerVitals.ExperienceToNextLevel).</summary>
        public static long ExperienceToNext(int level) => (long)PlayerVitals.XpPerLevelBase * level;

        /// <summary>Every XP earned since level 1: what the levels below took, and the XP towards the next.</summary>
        public static double TotalExperience(CharacterSave save)
        {
            double level = Mathf.Max(1, save.Level);
            return PlayerVitals.XpPerLevelBase * (level - 1.0) * level / 2.0 + Mathf.Max(0, save.Experience);
        }

        /// <summary>The stat and skill points the levels after <paramref name="from"/> up to <paramref name="to"/> bring (PlayerVitals.AddXp).</summary>
        public static (int stats, int skills) PointsBetween(int from, int to)
        {
            if (to <= from)
            {
                return (0, 0);
            }
            int stats = (to - from) * PlayerVitals.StatPointsPerLevel;
            int skills = to / PlayerVitals.SkillPointEveryNLevels - from / PlayerVitals.SkillPointEveryNLevels;
            return (stats, skills);
        }

        /// <summary>Stat points spent and unspent: what the character has had to spend.</summary>
        public static int StatBudget(CharacterSave save)
        {
            int spent = 0;
            if (save.Stats != null && save.Stats.Count > 0)
            {
                foreach (StatEntry entry in save.Stats)
                {
                    spent += Mathf.Max(0, entry.Points);
                }
            }
            else
            {
                spent = save.Vitality + save.Endurance + save.Strength;
            }
            return spent + save.StatPoints;
        }

        /// <summary>Skill points spent (each skill learnt that nobody knows from the start) and unspent.</summary>
        public static int SkillBudget(CharacterSave save)
        {
            int spent = 0;
            if (save.LearnedSkills != null)
            {
                foreach (string id in save.LearnedSkills)
                {
                    SkillNode node = SkillCatalog.Find(id);
                    if (node == null || !node.KnownFromStart)
                    {
                        spent += SkillCatalog.LearnCost;
                    }
                }
            }
            return spent + save.SkillPoints;
        }

        /// <summary>The first skill newly learnt that is not one, or whose requirements are not learnt; null when there is none.</summary>
        private static string UnreachableSkill(CharacterSave stored, CharacterSave reported)
        {
            var had = new HashSet<string>(stored.LearnedSkills ?? new List<string>());
            var has = new HashSet<string>(reported.LearnedSkills ?? new List<string>());
            var real = new HashSet<string>();
            foreach (SkillNode node in SkillCatalog.RealSkills())
            {
                real.Add(node.Id);
            }
            foreach (string id in has)
            {
                if (had.Contains(id))
                {
                    continue;
                }
                SkillNode node = SkillCatalog.Find(id);
                if (node == null || (!real.Contains(id) && !node.KnownFromStart))
                {
                    return $"\"{id}\" is not a skill that can be learnt";
                }
                foreach (string required in node.Requires)
                {
                    if (!has.Contains(required))
                    {
                        return $"\"{id}\" learnt without \"{required}\"";
                    }
                }
            }
            return null;
        }
    }
}
