using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE SKILL TREES AND THE BAR (owner, 2026-10-14: "implement all the skills we have so far including the movements like dash, climb, super
    /// jump ... with the ability to add them as hotbar spells"; vaulting first, for everyone, then Dash, Climb and Super Jump; movement keeps its
    /// own controls; keys 1 to 0, all equal). The map's data, the rules for learning, the bar and the save.
    /// </summary>
    public sealed class SkillTreeTests
    {
        private static readonly string[] EverySkillTheGameHas =
        {
            SkillCatalog.Vault, SkillCatalog.Dash, SkillCatalog.Climb, SkillCatalog.SuperJump, SkillCatalog.WallStick,
            SkillCatalog.Fireball, SkillCatalog.IceShards, SkillCatalog.PoisonBreath, SkillCatalog.AcidRain,
            SkillCatalog.SwordSpin, SkillCatalog.SwordWave, SkillCatalog.Octaslash, SkillCatalog.StabDash, SkillCatalog.Bloodrage,
            SkillCatalog.SelfSmite, SkillCatalog.Grow, SkillCatalog.Shrink, SkillCatalog.ArcaneOrb, SkillCatalog.ArcaneBarrier,
        };

        [Test]
        public void EveryStarHasItsOwnIdAndATreeThatExists()
        {
            var seen = new HashSet<string>();
            foreach (SkillNode node in SkillCatalog.Nodes)
            {
                Assert.IsTrue(seen.Add(node.Id), $"two stars are called {node.Id}");
                Assert.IsNotNull(SkillCatalog.FindTree(node.TreeId), $"{node.Id} is in a tree that does not exist ({node.TreeId})");
                Assert.IsFalse(string.IsNullOrEmpty(node.Description), $"{node.Id} has nothing to say about itself");
            }
            Assert.AreEqual(SkillCatalog.Trees.Count, SkillCatalog.Trees.Select(t => t.Id).Distinct().Count(), "two trees share an id");
        }

        [Test]
        public void EveryPrerequisiteExistsAndNothingNeedsItself()
        {
            foreach (SkillNode node in SkillCatalog.Nodes)
            {
                foreach (string required in node.Requires)
                {
                    Assert.IsNotNull(SkillCatalog.Find(required), $"{node.Id} requires {required}, which is not on the map");
                }
                // Walk down: a loop would mean a star nobody can ever reach.
                var stack = new Stack<string>(node.Requires);
                var visited = new HashSet<string>();
                while (stack.Count > 0)
                {
                    string below = stack.Pop();
                    Assert.AreNotEqual(node.Id, below, $"{node.Id} ends up requiring itself");
                    if (!visited.Add(below))
                    {
                        continue;
                    }
                    foreach (string deeper in SkillCatalog.Find(below).Requires)
                    {
                        stack.Push(deeper);
                    }
                }
            }
        }

        [Test]
        public void EverySkillTheGameHasIsAStarAndTheSpellsKeepTheirAbilityIds()
        {
            foreach (string id in EverySkillTheGameHas)
            {
                SkillNode node = SkillCatalog.Find(id);
                Assert.IsNotNull(node, $"{id} is not on the skill map");
                Assert.IsFalse(node.IsPlaceholder, $"{id} is an empty star");
                Assert.IsTrue(SkillCatalog.FindTree(node.TreeId).InGame, $"{id} is in a tree that is not in the game");
            }
            Assert.AreEqual(EverySkillTheGameHas.Length, SkillCatalog.RealSkills().Count(), "a star is a real skill the test does not know about");

            // The same names the abilities send to other players, so the two never drift apart.
            Assert.AreEqual(FireballCaster.Id, SkillCatalog.Fireball);
            Assert.AreEqual(IceShardCaster.Id, SkillCatalog.IceShards);
            Assert.AreEqual(PoisonBreathCaster.Id, SkillCatalog.PoisonBreath);
            Assert.AreEqual(AcidRainCaster.Id, SkillCatalog.AcidRain);
            Assert.AreEqual(SwordWaveCaster.Id, SkillCatalog.SwordWave);
            Assert.AreEqual(OctaslashAbility.Id, SkillCatalog.Octaslash);
            Assert.AreEqual(StabDashAbility.Id, SkillCatalog.StabDash);
            Assert.AreEqual(BloodrageAbility.Id, SkillCatalog.Bloodrage);
            Assert.AreEqual(MeleeAttacker.SpinAbilityId, SkillCatalog.SwordSpin);
            Assert.AreEqual(SelfSmiteAbility.Id, SkillCatalog.SelfSmite);
            Assert.AreEqual(ArcaneOrbCaster.Id, SkillCatalog.ArcaneOrb);
            Assert.AreEqual(ArcaneBarrierCaster.Id, SkillCatalog.ArcaneBarrier);
        }

        [Test]
        public void FreerunnerIsVaultingForEveryoneThenDashClimbAndSuperJump()
        {
            SkillNode vault = SkillCatalog.Find(SkillCatalog.Vault);
            Assert.IsTrue(vault.KnownFromStart, "vaulting is taught in the tutorial: everyone has it");
            Assert.IsEmpty(vault.Requires);
            foreach (string path in new[] { SkillCatalog.Dash, SkillCatalog.Climb, SkillCatalog.SuperJump })
            {
                SkillNode node = SkillCatalog.Find(path);
                Assert.AreEqual("freerunner", node.TreeId);
                CollectionAssert.AreEqual(new[] { SkillCatalog.Vault }, node.Requires, $"{path} should open straight from vaulting");
                Assert.IsFalse(node.KnownFromStart, $"{path} has to be learnt");
            }
            Assert.AreEqual(1, SkillCatalog.Nodes.Count(n => n.KnownFromStart), "only vaulting is known from the start");
        }

        [Test]
        public void MovementKeepsItsOwnControlsAndTheSpellsAndTechniquesGoOnTheBar()
        {
            foreach (string id in new[] { SkillCatalog.Vault, SkillCatalog.Dash, SkillCatalog.Climb, SkillCatalog.SuperJump, SkillCatalog.WallStick })
            {
                Assert.IsFalse(SkillCatalog.Find(id).GoesOnBar, $"{id} is movement: it keeps its own controls (owner)");
            }
            foreach (string id in SkillCatalog.DefaultBar.Where(id => id != null))
            {
                Assert.IsTrue(SkillCatalog.Find(id).GoesOnBar, $"{id} should be able to go on the bar");
            }
        }

        [Test]
        public void TheSwordTechniquesNeedASwordAndNothingElseDoes()
        {
            var needs = SkillCatalog.Nodes.Where(n => n.NeedsSword).Select(n => n.Id).OrderBy(id => id).ToArray();
            CollectionAssert.AreEqual(new[] { SkillCatalog.Octaslash, SkillCatalog.StabDash, SkillCatalog.SwordSpin, SkillCatalog.SwordWave }
                .OrderBy(id => id).ToArray(), needs);
        }

        [Test]
        public void AHybridSitsBetweenItsParentsAndTheTreesNotInTheGameHoldOnlyEmptyStars()
        {
            SkillTree steam = SkillCatalog.FindTree("steam");
            Assert.IsTrue(steam.IsHybrid);
            int index = SkillCatalog.Trees.ToList().IndexOf(steam);
            Assert.AreEqual(steam.Parents[0], SkillCatalog.Trees[index - 1].Id, "Steam stands right after one parent...");
            Assert.AreEqual(steam.Parents[1], SkillCatalog.Trees[index + 1].Id, "...and right before the other");
            foreach (SkillTree tree in SkillCatalog.Trees.Where(t => !t.InGame))
            {
                Assert.IsTrue(SkillCatalog.InTree(tree.Id).All(n => n.IsPlaceholder), $"{tree.Name} is not in the game but has a real skill");
            }
        }

        [Test]
        public void TheDefaultBarIsTheOldNumberKeysThenSelfSmiteOnNineAndGrowOnZero()
        {
            Hotbar bar = Hotbar.WithDefaults();
            string[] expected =
            {
                SkillCatalog.Fireball, SkillCatalog.SwordSpin, SkillCatalog.IceShards, SkillCatalog.PoisonBreath, SkillCatalog.AcidRain,
                SkillCatalog.Octaslash, SkillCatalog.SwordWave, SkillCatalog.Bloodrage, SkillCatalog.SelfSmite, SkillCatalog.Grow,
            };
            for (int slot = 0; slot < Hotbar.SlotCount; slot++)
            {
                Assert.AreEqual(expected[slot], bar[slot], $"key {Hotbar.KeyLabel(slot)}");
            }
        }

        // --- learning ---

        [Test]
        public void ANewCharacterKnowsOnlyVaulting()
        {
            var book = new SkillBook();
            Assert.IsTrue(book.Knows(SkillCatalog.Vault));
            Assert.AreEqual(1, book.Count);
            foreach (string id in EverySkillTheGameHas.Where(id => id != SkillCatalog.Vault))
            {
                Assert.IsFalse(book.Knows(id), id);
            }
        }

        [Test]
        public void LearningNeedsAPointAndWhatComesBeforeIt()
        {
            var book = new SkillBook();
            int points = 0;
            Assert.AreEqual(SkillBook.Refusal.NoPoints, book.TryLearn(SkillCatalog.Climb, ref points));
            Assert.AreEqual(SkillBook.Refusal.NeedsPrerequisite, book.WhyNot(SkillCatalog.WallStick, 5), "Wall Stick sits above Climb");
            Assert.AreEqual(SkillBook.Refusal.NeedsPrerequisite, book.WhyNot(SkillCatalog.AcidRain, 5), "Acid Rain sits above Poison Breath");

            points = 2;
            Assert.AreEqual(SkillBook.Refusal.None, book.TryLearn(SkillCatalog.Climb, ref points));
            Assert.AreEqual(1, points, "a star costs one point");
            Assert.IsTrue(book.Knows(SkillCatalog.Climb));
            Assert.AreEqual(SkillBook.Refusal.AlreadyLearnt, book.WhyNot(SkillCatalog.Climb, points));
            Assert.AreEqual(SkillBook.Refusal.None, book.TryLearn(SkillCatalog.WallStick, ref points));
            Assert.AreEqual(0, points);
        }

        [Test]
        public void EmptyStarsAndTreesNotInTheGameCannotBeLearnt()
        {
            var book = SkillBook.Everything();
            foreach (SkillNode node in SkillCatalog.Nodes.Where(n => n.IsPlaceholder))
            {
                Assert.AreEqual(SkillBook.Refusal.NotInGame, book.WhyNot(node.Id, 99), node.Id);
            }
            Assert.AreEqual(SkillBook.Refusal.Unknown, book.WhyNot("teleport", 99));
        }

        [Test]
        public void ResettingGivesEveryPointBackAndKeepsVaulting()
        {
            var book = new SkillBook();
            int points = 3;
            book.TryLearn(SkillCatalog.Fireball, ref points);
            book.TryLearn(SkillCatalog.Dash, ref points);
            book.TryLearn(SkillCatalog.Climb, ref points);
            Assert.AreEqual(0, points);

            Assert.AreEqual(3, book.ResetAll(), "every point spent comes back");
            Assert.IsTrue(book.Knows(SkillCatalog.Vault), "vaulting is never forgotten");
            Assert.AreEqual(1, book.Count);
            Assert.AreEqual(EverySkillTheGameHas.Length - 1, SkillBook.Everything().ResetAll(), "everything but vaulting was worth a point");
        }

        [Test]
        public void BeforeTheTreeEveryoneHadEverySkillButTheChargedJump()
        {
            SkillBook before = SkillBook.AsBeforeTheTree();
            foreach (string id in EverySkillTheGameHas)
            {
                Assert.AreEqual(id != SkillCatalog.SuperJump, before.Knows(id), id);
            }
        }

        [Test]
        public void ASavedListDropsWhatTheGameDoesNotHaveAndAlwaysKeepsVaulting()
        {
            var book = new SkillBook();
            book.SetLearnt(new[] { SkillCatalog.Fireball, "teleport", "pyromancer-1", null });
            Assert.IsTrue(book.Knows(SkillCatalog.Fireball));
            Assert.IsTrue(book.Knows(SkillCatalog.Vault));
            Assert.IsFalse(book.Knows("teleport"));
            Assert.IsFalse(book.Knows("pyromancer-1"), "an empty star can never be learnt, even from a save");
            Assert.AreEqual(2, book.Count);
        }

        // --- the bar ---

        [Test]
        public void TheKeysAreOneToNineThenZero()
        {
            string[] labels = Enumerable.Range(0, Hotbar.SlotCount).Select(Hotbar.KeyLabel).ToArray();
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }, labels);
        }

        [Test]
        public void ASkillSitsInOneSlotAndMovesWhenPutElsewhere()
        {
            var bar = new Hotbar();
            Assert.IsTrue(bar.Put(0, SkillCatalog.Fireball));
            Assert.IsTrue(bar.Put(9, SkillCatalog.Fireball), "key 0 takes a skill like any other");
            Assert.IsNull(bar[0], "it left its old slot");
            Assert.AreEqual(9, bar.SlotOf(SkillCatalog.Fireball));
            Assert.IsTrue(bar.Put(9, SkillCatalog.IceShards), "a skill put on a full slot replaces what was there");
            Assert.AreEqual(-1, bar.SlotOf(SkillCatalog.Fireball));
            Assert.IsTrue(bar.Clear(9));
            Assert.IsNull(bar[9]);
        }

        [Test]
        public void MovementAndUnknownSkillsAreRefused()
        {
            var bar = new Hotbar();
            Assert.IsFalse(bar.Put(0, SkillCatalog.Dash));
            Assert.IsFalse(bar.Put(0, SkillCatalog.Climb));
            Assert.IsFalse(bar.Put(0, "teleport"));
            Assert.IsFalse(bar.Put(10, SkillCatalog.Fireball));
            Assert.AreEqual(-1, bar.PlaceInFirstEmpty(SkillCatalog.SuperJump));
        }

        [Test]
        public void ANewlyLearntSkillGoesInTheFirstEmptySlot()
        {
            Hotbar bar = Hotbar.WithDefaults();
            bar.Clear(2);
            Assert.AreEqual(2, bar.PlaceInFirstEmpty(SkillCatalog.IceShards));
            bar.Clear(0);
            bar.Remove(SkillCatalog.Fireball);
            Assert.AreEqual(0, bar.PlaceInFirstEmpty(SkillCatalog.Fireball));
            Assert.AreEqual(0, bar.PlaceInFirstEmpty(SkillCatalog.Fireball), "already on the bar: stays where it is");
        }

        [Test]
        public void TheBarSurvivesASaveWithItsEmptySlots()
        {
            var bar = new Hotbar();
            bar.Put(3, SkillCatalog.Bloodrage);
            bar.Put(9, SkillCatalog.Fireball);
            List<string> saved = bar.ToList();
            Assert.AreEqual(Hotbar.SlotCount, saved.Count);
            Assert.AreEqual(string.Empty, saved[0], "an empty slot is an empty string");

            var loaded = new Hotbar();
            loaded.Load(saved);
            Assert.AreEqual(SkillCatalog.Bloodrage, loaded[3]);
            Assert.AreEqual(SkillCatalog.Fireball, loaded[9]);
            Assert.IsNull(loaded[0]);

            loaded.Load(new List<string> { SkillCatalog.Dash, SkillCatalog.Fireball, SkillCatalog.Fireball });
            Assert.IsNull(loaded[0], "movement never loads onto the bar");
            Assert.AreEqual(1, loaded.SlotOf(SkillCatalog.Fireball), "and a skill loads into one slot only");
            Assert.IsNull(loaded[2]);
        }

        // --- skill points and the save ---

        [Test]
        public void EverySecondLevelBringsASkillPoint()
        {
            var holder = new GameObject("Vitals");
            try
            {
                PlayerVitals vitals = holder.AddComponent<PlayerVitals>();
                vitals.AddXp(PlayerVitals.XpPerLevelBase);   // to level 2
                Assert.AreEqual(2, vitals.Level);
                Assert.AreEqual(1, vitals.SkillPoints, "level 2 brings a point (Godot's SKILL_POINT_EVERY_N_LEVELS)");
                vitals.AddXp(PlayerVitals.XpPerLevelBase * 2);   // to level 3
                Assert.AreEqual(1, vitals.SkillPoints, "level 3 does not");
                vitals.AddXp(PlayerVitals.XpPerLevelBase * 3);   // to level 4
                Assert.AreEqual(2, vitals.SkillPoints);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ASuperJumpsOwnHeightNeverHurtsItsLanding()
        {
            var holder = new GameObject("Vitals");
            try
            {
                PlayerVitals vitals = holder.AddComponent<PlayerVitals>();
                float high = PlayerVitals.SafeFallDistance + 4.0f;

                // A leap from the street back down to it: free.
                vitals.ForgiveFallAbove(0.0f);
                vitals.TickFallDamage(false, 0.0f);
                vitals.TickFallDamage(false, high);
                Assert.AreEqual(0.0f, vitals.TickFallDamage(true, 0.0f), "landing back on the street it left");

                // The same height without a super jump (a fall from a ledge) hurts.
                vitals.TickFallDamage(false, high);
                Assert.Greater(vitals.TickFallDamage(true, 0.0f), 0.0f, "an ordinary fall from that high");

                // A leap off a roof: the drop below the take-off still counts, as stepping off would.
                float roof = PlayerVitals.SafeFallDistance + 2.0f;
                vitals.ForgiveFallAbove(roof);
                vitals.TickFallDamage(false, roof);
                vitals.TickFallDamage(false, roof + high);
                float damage = vitals.TickFallDamage(true, 0.0f);
                Assert.AreEqual(2.0f * PlayerVitals.BaseMaxHealth * PlayerVitals.FallDamagePerMetreFraction, damage, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ANewCharacterStartsWithAPointAndASaveFromBeforeTheTreeSaysSo()
        {
            CharacterSave made = CharacterSave.Create("New", Color.white);
            Assert.AreEqual(CharacterSave.CurrentSkillsVersion, made.SkillsVersion);
            Assert.AreEqual(CharacterSave.StartingSkillPoints, made.SkillPoints);
            Assert.IsEmpty(made.LearnedSkills);

            // A character written before the skill tree has none of these fields at all.
            var old = JsonUtility.FromJson<CharacterSave>("{\"Id\":\"abc\",\"Name\":\"Old\",\"Level\":4}");
            Assert.AreEqual(0, old.SkillsVersion, "an old save reads as version 0, which keeps everything it could use");
            Assert.IsNotNull(old.LearnedSkills);
            Assert.IsNotNull(old.Hotbar);

            made.LearnedSkills.Add(SkillCatalog.Fireball);
            made.Hotbar = new Hotbar().ToList();
            var back = JsonUtility.FromJson<CharacterSave>(JsonUtility.ToJson(made));
            CollectionAssert.AreEqual(made.LearnedSkills, back.LearnedSkills);
            Assert.AreEqual(Hotbar.SlotCount, back.Hotbar.Count);
        }
    }
}
