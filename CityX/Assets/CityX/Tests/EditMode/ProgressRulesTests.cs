using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The server's check on a save's progress (docs/SERVER_SAVING.md, step 2): playing passes, and each way of making progress up is
    /// refused, judged on what changed since the last save the server accepted. An admin's console edit passes for an admin only.
    /// </summary>
    public sealed class ProgressRulesTests
    {
        private const string Player = "0123456789abcdef0123456789abcdef";
        private readonly ProgressRules _rules = new ProgressRules();

        private static CharacterSave Fresh()
        {
            CharacterSave save = CharacterSave.Create("Player", Color.white);
            save.LearnedSkills = SkillCatalog.Nodes.Where(n => n.KnownFromStart).Select(n => n.Id).ToList();
            return save;
        }

        private static CharacterSave Copy(CharacterSave save) => JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(save));

        /// <summary>Plays <paramref name="xp"/> into <paramref name="save"/> as the game does (PlayerVitals.AddXp).</summary>
        private static CharacterSave Earn(CharacterSave save, int xp)
        {
            var vitals = new GameObject("Vitals").AddComponent<PlayerVitals>();
            try
            {
                vitals.Level = save.Level;
                vitals.Experience = save.Experience;
                vitals.StatPoints = save.StatPoints;
                vitals.SkillPoints = save.SkillPoints;
                vitals.AddXp(xp);
                CharacterSave after = Copy(save);
                after.Level = vitals.Level;
                after.Experience = vitals.Experience;
                after.StatPoints = vitals.StatPoints;
                after.SkillPoints = vitals.SkillPoints;
                return after;
            }
            finally
            {
                Object.DestroyImmediate(vitals.gameObject);
            }
        }

        [Test]
        public void PlayingAndLevellingUpPasses()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Earn(before, 450);   // past level 2 (100) and 3 (200)
            after.PlaytimeSeconds = 30.0;
            Assert.AreEqual(3, after.Level, "setup: two levels gained");
            Assert.IsNull(_rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void SpendingStatPointsOnStatsPasses()
        {
            CharacterSave before = Earn(Fresh(), 450);
            CharacterSave after = Copy(before);
            after.StatPoints -= 3;
            after.Stats = new List<StatEntry> { new StatEntry("max-health", 3) };
            Assert.IsNull(_rules.Refusal(before, after, 5.0));
        }

        [Test]
        public void MoreXpThanTheTimeAllowsIsRefused()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Earn(before, 50000);
            StringAssert.Contains("XP in", _rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void ALevelSetOutrightIsRefused()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Copy(before);
            after.Level = 50;
            Assert.IsNotNull(_rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void XpPastWhatTheLevelNeedsIsRefused()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Copy(before);
            after.Experience = 150;   // level 1 needs 100
            StringAssert.Contains("not a place on the curve", _rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void StatPointsFromNowhereAreRefused()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Copy(before);
            after.StatPoints += 10;
            StringAssert.Contains("stat point", _rules.Refusal(before, after, 30.0));
            after = Copy(before);
            after.Stats = new List<StatEntry> { new StatEntry("max-health", 10) };
            StringAssert.Contains("stat point", _rules.Refusal(before, after, 30.0), "stats raised without spending points");
        }

        [Test]
        public void SkillsMustBePaidForAndReachable()
        {
            // A real skill learnable from the start (first), and a real skill that needs it (second).
            SkillNode second = SkillCatalog.RealSkills().First(n => n.Requires.Any(r => LearnableFromTheStart(SkillCatalog.Find(r))));
            SkillNode first = SkillCatalog.Find(second.Requires.First(r => LearnableFromTheStart(SkillCatalog.Find(r))));
            CharacterSave before = Fresh();
            before.SkillPoints = 1;

            CharacterSave learnt = Copy(before);
            learnt.SkillPoints = 0;
            learnt.LearnedSkills.Add(first.Id);
            Assert.IsNull(_rules.Refusal(before, learnt, 30.0), $"learning {first.Id} with the point for it was refused");

            CharacterSave free = Copy(before);
            free.LearnedSkills.Add(first.Id);
            StringAssert.Contains("skill point", _rules.Refusal(before, free, 30.0), "a skill learnt for nothing passed");

            CharacterSave skipped = Copy(before);
            skipped.SkillPoints = 0;
            skipped.LearnedSkills.Add(second.Id);
            StringAssert.Contains("without", _rules.Refusal(before, skipped, 30.0), $"{second.Id} learnt without {first.Id} passed");

            CharacterSave made = Copy(before);
            made.SkillPoints = 0;
            made.LearnedSkills.Add("not-a-skill");
            StringAssert.Contains("not a skill", _rules.Refusal(before, made, 30.0));
        }

        private static bool LearnableFromTheStart(SkillNode node) =>
            node != null && !node.KnownFromStart && SkillCatalog.RealSkills().Contains(node) && node.Requires.All(r => SkillCatalog.Find(r).KnownFromStart);

        [Test]
        public void ResettingSkillsForTheirPointsBackPasses()
        {
            SkillNode first = SkillCatalog.RealSkills().First(LearnableFromTheStart);
            CharacterSave before = Fresh();
            before.LearnedSkills.Add(first.Id);
            CharacterSave reset = Copy(before);
            reset.LearnedSkills.Remove(first.Id);
            reset.SkillPoints += SkillCatalog.LearnCost;
            Assert.IsNull(_rules.Refusal(before, reset, 5.0));
        }

        [Test]
        public void PlaytimeFasterThanRealTimeIsRefused()
        {
            CharacterSave before = Fresh();
            CharacterSave after = Copy(before);
            after.PlaytimeSeconds = 3600.0;
            StringAssert.Contains("s of play", _rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void ASaveFromBeforeTheSkillTreeKeepsItsSkillsOnItsFirstSave()
        {
            CharacterSave before = Fresh();
            before.SkillsVersion = 0;
            CharacterSave after = Copy(before);
            after.SkillsVersion = CharacterSave.CurrentSkillsVersion;
            after.LearnedSkills.AddRange(SkillCatalog.RealSkills().Take(5).Select(n => n.Id));
            Assert.IsNull(_rules.Refusal(before, after, 30.0));
        }

        [Test]
        public void TheServerKeepsRefusedProgressButTakesTheLookAndClothes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            IProgressRules previous = CharacterService.Rules;
            try
            {
                CharacterService.Rules = _rules;
                double now = 0.0;
                Directory.CreateDirectory(directory);
                var database = new CharacterDatabase(directory);
                var service = new CharacterService(database, () => now);
                File.WriteAllText(Path.Combine(directory, CharacterDatabase.AdminsFileName), "{\"EveryoneIsAdmin\":false,\"Keys\":[]}");
                CharacterSave made = service.Create(Player, Fresh());
                CharacterSave playing = service.Play(Player, made.Id);

                now = 30.0;
                CharacterSave cheat = Copy(playing);
                cheat.Level = 99;
                cheat.Wearing = new List<string> { "t_shirt" };
                cheat.HairStyle = "hyde";
                Assert.IsFalse(service.Save(Player, cheat), "a made-up level was taken");
                CharacterSave stored = service.Play(Player, made.Id);
                Assert.AreEqual(1, stored.Level);
                CollectionAssert.AreEqual(new[] { "t_shirt" }, stored.Wearing, "the clothes were refused with the progress");
                Assert.AreEqual("hyde", stored.HairStyle);

                Assert.IsFalse(service.Save(Player, cheat, adminEdited: true), "a console edit passed for a player who is not an admin");
                File.WriteAllText(Path.Combine(directory, CharacterDatabase.AdminsFileName), $"{{\"EveryoneIsAdmin\":false,\"Keys\":[\"{Player}\"]}}");
                Assert.IsTrue(service.Save(Player, cheat, adminEdited: true), "an admin's console edit was refused");
                Assert.AreEqual(99, service.Play(Player, made.Id).Level);
            }
            finally
            {
                CharacterService.Rules = previous;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
