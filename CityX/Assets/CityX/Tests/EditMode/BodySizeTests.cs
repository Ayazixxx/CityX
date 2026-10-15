using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// GROW AND SHRINK, THE PURE PART (owner, 2026-10-15: "one that makes you 10x as big and one that makes you 10x as small", "30 seconds each",
    /// in a new "Wizard" tree): the sizes, the 30 s, the change there and back, casting again to end it, growing only where there is room, and
    /// how a resized body moves.
    /// </summary>
    public sealed class BodySizeTests
    {
        private static void Run(BodySize size, float seconds, System.Func<float, bool> fits = null)
        {
            const float step = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += step)
            {
                size.Tick(step, fits);
            }
        }

        [Test]
        public void GrowIsTenTimesForThirtySecondsThenTheBodyComesBack()
        {
            var size = new BodySize();
            Assert.AreEqual(BodySize.Spell.Grow, size.Cast(BodySize.Spell.Grow));
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(10.0f, size.Scale, 0.01f, "Grow is ten times");
            Assert.IsFalse(size.IsChanging);

            Run(size, BodySize.HoldSeconds - BodySize.ChangeSeconds - 0.3f);
            Assert.AreEqual(10.0f, size.Scale, 0.01f, "still a giant just before 30 s");
            Assert.AreEqual(BodySize.Spell.Grow, size.Active);

            Run(size, 0.4f + BodySize.ChangeSeconds);
            Assert.AreEqual(1.0f, size.Scale, 0.01f, "back to its own size after 30 s and the change back");
            Assert.AreEqual(BodySize.Spell.None, size.Active);
            Assert.IsFalse(size.IsResized);
        }

        [Test]
        public void ShrinkIsATenthAndTheSpellCastAgainEndsItEarly()
        {
            var size = new BodySize();
            size.Cast(BodySize.Spell.Shrink);
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(0.1f, size.Scale, 0.001f, "Shrink is a tenth");

            Assert.AreEqual(BodySize.Spell.None, size.Cast(BodySize.Spell.Shrink), "the same spell again ends it");
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(1.0f, size.Scale, 0.01f, "ended early: back to its own size");
        }

        [Test]
        public void TheOtherSpellGoesStraightToTheOtherSizeForAFreshThirtySeconds()
        {
            var size = new BodySize();
            size.Cast(BodySize.Spell.Grow);
            Run(size, 20.0f);
            Assert.AreEqual(BodySize.Spell.Shrink, size.Cast(BodySize.Spell.Shrink));
            Assert.AreEqual(BodySize.HoldSeconds, size.Remaining, 0.001f, "a fresh 30 s");
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(0.1f, size.Scale, 0.001f, "from a giant straight to tiny");
            Run(size, 15.0f);
            Assert.AreEqual(0.1f, size.Scale, 0.001f, "the giant's old 30 s does not cut the new spell short");
        }

        [Test]
        public void TheChangeLooksEvenItGoesByRatio()
        {
            Assert.AreEqual(Mathf.Sqrt(10.0f), BodySize.Eased(1.0f, 10.0f, 0.5f), 0.001f, "halfway from 1 to 10 by eye is 3.16, not 5.5");
            Assert.AreEqual(1.0f, BodySize.Eased(1.0f, 10.0f, 0.0f), 1e-5f);
            Assert.AreEqual(10.0f, BodySize.Eased(1.0f, 10.0f, 1.0f), 1e-4f);
            float last = 1.0f;
            for (float t = 0.05f; t <= 1.0f; t += 0.05f)
            {
                float now = BodySize.Eased(1.0f, 10.0f, t);
                Assert.Greater(now, last, "it never shrinks on the way up");
                last = now;
            }
        }

        [Test]
        public void GrowingWaitsForRoomAndCarriesOnOnceThereIsSome()
        {
            var size = new BodySize();
            size.Cast(BodySize.Spell.Grow);
            Run(size, 3.0f, scale => scale <= 4.0f);   // a ceiling: room for four times, no more
            Assert.LessOrEqual(size.Scale, 4.0f, "it grew through the ceiling");
            Assert.Greater(size.Scale, 3.0f, "it should grow as far as there is room");
            Assert.IsTrue(size.IsWaitingForRoom);
            Run(size, BodySize.ChangeSeconds, scale => true);   // out from under it
            Assert.AreEqual(10.0f, size.Scale, 0.01f, "it did not carry on growing once out");

            bool asked = false;
            size.Cast(BodySize.Spell.Shrink);
            Run(size, BodySize.ChangeSeconds + 0.05f, scale => { asked = true; return false; });
            Assert.IsFalse(asked, "shrinking always fits: room is never asked for");
            Assert.AreEqual(0.1f, size.Scale, 0.001f);
        }

        [Test]
        public void AGiantMovesAndStepsAsAHeavyBodyDoesAndATinyOneScurries()
        {
            var size = new BodySize();
            size.Cast(BodySize.Spell.Grow);
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(Mathf.Sqrt(10.0f), size.SpeedFactor, 0.01f, "a giant moves sqrt(10) = 3.16 times as fast");
            Assert.AreEqual(1.0f / Mathf.Sqrt(10.0f), size.StrideRate, 0.01f, "and steps 3.16 times as slowly, its legs ten times as long");
            size.Cast(BodySize.Spell.Shrink);
            Run(size, BodySize.ChangeSeconds + 0.05f);
            Assert.AreEqual(Mathf.Sqrt(0.1f), size.SpeedFactor, 0.01f);
            Assert.AreEqual(Mathf.Sqrt(10.0f), size.StrideRate, 0.01f, "a tiny body's feet patter 3.16 times as fast");
            size.Reset();
            Assert.AreEqual(1.0f, size.Scale);
            Assert.AreEqual(BodySize.Spell.None, size.Active);
        }

        [Test]
        public void TheWizardTreeHasGrowAndShrinkAndGrowIsOnTheBar()
        {
            SkillTree wizard = SkillCatalog.FindTree("wizard");
            Assert.IsNotNull(wizard, "no Wizard tree");
            Assert.AreEqual("Wizard", wizard.Name);
            foreach (string id in new[] { SkillCatalog.Grow, SkillCatalog.Shrink })
            {
                SkillNode node = SkillCatalog.Find(id);
                Assert.IsNotNull(node, id);
                Assert.AreEqual("wizard", node.TreeId);
                Assert.AreEqual(SkillKind.Spell, node.Kind, "a hotbar spell");
                Assert.IsTrue(SkillBook.AsBeforeTheTree().Knows(id), "a body without a character should be able to cast it");
            }
            CollectionAssert.Contains(SkillCatalog.DefaultBar, SkillCatalog.Grow, "Grow is on the default bar");
        }

        [Test]
        public void AGiantsFallIsCountedInItsOwnLengths()
        {
            var go = new GameObject("Vitals");
            try
            {
                var vitals = go.AddComponent<PlayerVitals>();
                vitals.FallScale = 10.0f;
                vitals.TickFallDamage(false, 30.0f);
                Assert.AreEqual(0.0f, vitals.TickFallDamage(true, 10.0f), "20 m is a hop for a 26 m giant");
                vitals.FallScale = 1.0f;
                vitals.TickFallDamage(false, 30.0f);
                Assert.Greater(vitals.TickFallDamage(true, 10.0f), 0.0f, "but it hurts anyone else");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
