using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Skill ranks (docs/SKILLS.md, 2026-10-27, with Arcane Orb: "you can upgrade it and level it up to get stronger"): learning is rank 1, each
    /// rank after costs a point, only a skill with ranks has more, a reset gives them back, and they survive a save and the server's count.
    /// </summary>
    public sealed class SkillRankTests
    {
        [Test]
        public void LearningIsRankOneAndEachRankAfterCostsAPoint()
        {
            var book = new SkillBook();
            int points = 3;
            Assert.AreEqual(0, book.RankOf(SkillCatalog.ArcaneOrb), "not learnt: no rank");
            Assert.AreEqual(SkillBook.Refusal.NotLearnt, book.WhyNotRankUp(SkillCatalog.ArcaneOrb, points));
            Assert.AreEqual(SkillBook.Refusal.None, book.TryLearn(SkillCatalog.ArcaneOrb, ref points));
            Assert.AreEqual(1, book.RankOf(SkillCatalog.ArcaneOrb));
            Assert.AreEqual(SkillBook.Refusal.None, book.TryRankUp(SkillCatalog.ArcaneOrb, ref points));
            Assert.AreEqual(SkillBook.Refusal.None, book.TryRankUp(SkillCatalog.ArcaneOrb, ref points));
            Assert.AreEqual(3, book.RankOf(SkillCatalog.ArcaneOrb));
            Assert.AreEqual(0, points, "one to learn, one a rank");
            Assert.AreEqual(SkillBook.Refusal.NoPoints, book.TryRankUp(SkillCatalog.ArcaneOrb, ref points));
            Assert.AreEqual(2, book.PointsInRanks);
        }

        [Test]
        public void ASkillWithoutRanksStaysAtOne()
        {
            var book = new SkillBook();
            int points = 5;
            book.TryLearn(SkillCatalog.Fireball, ref points);
            Assert.AreEqual(SkillBook.Refusal.HighestRank, book.TryRankUp(SkillCatalog.Fireball, ref points));
            Assert.AreEqual(1, book.RankOf(SkillCatalog.Fireball));
            Assert.AreEqual(4, points, "nothing taken");
        }

        [Test]
        public void AResetGivesTheRanksPointsBackToo()
        {
            var book = new SkillBook();
            int points = 4;
            book.TryLearn(SkillCatalog.ArcaneOrb, ref points);
            book.TryRankUp(SkillCatalog.ArcaneOrb, ref points);
            book.TryRankUp(SkillCatalog.ArcaneOrb, ref points);
            Assert.AreEqual(3, book.ResetAll(), "the spell's point and its two ranks'");
            Assert.AreEqual(0, book.RankOf(SkillCatalog.ArcaneOrb));
            Assert.AreEqual(0, book.PointsInRanks);
        }

        [Test]
        public void RanksSurviveASaveAndAreDroppedWithTheSkill()
        {
            var holder = new GameObject("Skills");
            try
            {
                PlayerVitals vitals = holder.AddComponent<PlayerVitals>();
                PlayerSkills skills = holder.AddComponent<PlayerSkills>();
                CharacterSave fresh = CharacterSave.Create("Player", Color.white);
                fresh.SkillPoints = 4;
                skills.Load(fresh);
                vitals.SkillPoints = 4;
                Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.ArcaneOrb));
                Assert.AreEqual(SkillBook.Refusal.None, skills.TryRankUp(SkillCatalog.ArcaneOrb));
                Assert.AreEqual(SkillBook.Refusal.None, skills.TryRankUp(SkillCatalog.ArcaneOrb));
                Assert.AreEqual(1, vitals.SkillPoints);

                CharacterSave saved = CharacterSave.Create("Player", Color.white);
                skills.Save(saved);
                Assert.AreEqual(1, saved.SkillRanks.Count, "only ranks past the first are written");
                Assert.AreEqual(SkillCatalog.ArcaneOrb, saved.SkillRanks[0].Id);
                Assert.AreEqual(3, saved.SkillRanks[0].Rank);

                CharacterSave reloaded = JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(saved));
                skills.Load(reloaded);
                Assert.AreEqual(3, skills.RankOf(SkillCatalog.ArcaneOrb));

                reloaded.LearnedSkills.Remove(SkillCatalog.ArcaneOrb);
                skills.Load(reloaded);
                Assert.AreEqual(0, skills.RankOf(SkillCatalog.ArcaneOrb), "a rank goes with its skill");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheServerCountsRanksAsPointsSpent()
        {
            CharacterSave before = CharacterSave.Create("Player", Color.white);
            before.LearnedSkills = SkillCatalog.Nodes.Where(n => n.KnownFromStart).Select(n => n.Id).ToList();
            before.SkillPoints = 3;
            CharacterSave after = JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(before));
            after.LearnedSkills.Add(SkillCatalog.ArcaneOrb);
            after.SkillRanks = new List<SkillRankEntry> { new SkillRankEntry(SkillCatalog.ArcaneOrb, 3) };
            after.SkillPoints = 0;
            Assert.IsNull(new ProgressRules().Refusal(before, after, 5.0), "three points spent: the spell and two ranks");
            Assert.AreEqual(ProgressRules.SkillBudget(before), ProgressRules.SkillBudget(after));

            after.SkillRanks = new List<SkillRankEntry> { new SkillRankEntry(SkillCatalog.ArcaneOrb, 6) };
            StringAssert.Contains("skill point", new ProgressRules().Refusal(before, after, 5.0), "ranks nobody paid for");
        }
    }
}
