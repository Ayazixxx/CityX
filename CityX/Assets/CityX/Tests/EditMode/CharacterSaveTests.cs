using System.Collections.Generic;
using System.IO;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Saved characters, which are the one thing in the game whose loss the
    /// player actually feels.
    ///
    /// Pointed at a scratch directory rather than the real save location --
    /// otherwise running the suite would delete the characters of whoever ran
    /// it, which is a spectacular way for a test to fail.
    /// </summary>
    public class CharacterSaveTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
        }

        [TearDown]
        public void TearDown()
        {
            CharacterStore.DirectoryOverride = null;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void ACharacterSurvivesARoundTripThroughDisk()
        {
            CharacterSave made = CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f));
            made.Level = 7;
            made.Vitality = 3;
            made.PlaytimeSeconds = 1234.0;
            CharacterStore.Add(made);

            List<CharacterSave> loaded = CharacterStore.Load();

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("Ayaz", loaded[0].Name);
            Assert.AreEqual(made.Id, loaded[0].Id);
            Assert.AreEqual(7, loaded[0].Level);
            Assert.AreEqual(3, loaded[0].Vitality);
            Assert.AreEqual(1234.0, loaded[0].PlaytimeSeconds, 0.001);
            Assert.AreEqual(0.4f, loaded[0].Skin.g, 0.001f, "the colour did not survive");
        }

        [Test]
        public void TheClothesACharacterHasOnSurviveARoundTripThroughDisk()
        {
            CharacterSave made = CharacterSave.Create("Ayaz", Color.white);
            made.Wearing = new List<string> { "red_camo_t_shirt", "top_hat" };
            CharacterStore.Add(made);

            CollectionAssert.AreEqual(new[] { "red_camo_t_shirt", "top_hat" }, CharacterStore.Load()[0].Wearing);
        }

        [Test]
        public void ASaveFromBeforeClothesWereKeptLoadsWearingNothing()
        {
            CharacterSave old = JsonUtility.FromJson<CharacterSave>("{\"Id\":\"abc\",\"Name\":\"Old\"}");
            Assert.IsNotNull(old.Wearing);
            Assert.IsEmpty(old.Wearing);

            old.Wearing = null;
            Assert.IsTrue(old.Repair());
            Assert.IsNotNull(old.Wearing);
        }

        /// <summary>
        /// A FIRST RUN IS NOT AN ERROR. The commonest state of this file is
        /// "does not exist", and anything that throws there stops a new player
        /// at the menu.
        /// </summary>
        [Test]
        public void NoFileMeansNoCharactersRatherThanAnError()
        {
            Assert.IsEmpty(CharacterStore.Load());
        }

        /// <summary>
        /// Neither does a broken one. It gets reported and left on disk to be
        /// looked at, rather than silently overwritten with an empty list --
        /// which would turn a recoverable problem into a permanent one.
        /// </summary>
        [Test]
        public void ACorruptFileLeavesThePlayerWithAMenuRatherThanACrash()
        {
            File.WriteAllText(CharacterStore.Path_, "{ this is not json");

            LogAssert.ignoreFailingMessages = true;
            List<CharacterSave> loaded = CharacterStore.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.IsEmpty(loaded);
            Assert.IsTrue(File.Exists(CharacterStore.Path_),
                "the corrupt file was destroyed instead of being left for inspection");
        }

        [Test]
        public void SavingRepeatedlyOverwritesRatherThanFailingAfterTheFirst()
        {
            // The temporary-then-replace write has a real failure mode here:
            // File.Move refuses to clobber on Windows, so a naive version works
            // exactly once and then silently stops saving.
            for (int i = 0; i < 5; i++)
            {
                CharacterStore.Add(CharacterSave.Create($"Runner {i}", Color.white));
            }

            Assert.AreEqual(5, CharacterStore.Load().Count);
            Assert.IsFalse(File.Exists(CharacterStore.Path_ + ".tmp"),
                "a temporary file was left behind");
        }

        [Test]
        public void DeletingRemovesOnlyThatCharacter()
        {
            CharacterSave keep = CharacterSave.Create("Keep", Color.white);
            CharacterSave drop = CharacterSave.Create("Drop", Color.white);
            CharacterStore.Add(keep);
            CharacterStore.Add(drop);

            CharacterStore.Delete(drop.Id);

            List<CharacterSave> left = CharacterStore.Load();
            Assert.AreEqual(1, left.Count);
            Assert.AreEqual(keep.Id, left[0].Id);
        }

        /// <summary>
        /// Update reads the list fresh. A session lasts as long as somebody
        /// plays, and writing back a whole-list snapshot taken at the menu
        /// would undo every character made or deleted since.
        /// </summary>
        [Test]
        public void UpdatingOneCharacterDoesNotUndoChangesToTheOthers()
        {
            CharacterSave playing = CharacterSave.Create("Playing", Color.white);
            CharacterStore.Add(playing);

            // As if a second character were made after this session started.
            CharacterStore.Add(CharacterSave.Create("Made Later", Color.white));

            playing.Level = 12;
            CharacterStore.Update(playing);

            List<CharacterSave> loaded = CharacterStore.Load();
            Assert.AreEqual(2, loaded.Count, "updating one character dropped the other");
            Assert.AreEqual(12, loaded.Find(c => c.Id == playing.Id).Level);
        }

        [Test]
        public void TheListComesBackNewestPlayedFirst()
        {
            CharacterSave old = CharacterSave.Create("Old", Color.white);
            old.LastPlayedUnixSeconds = 1000;
            CharacterSave recent = CharacterSave.Create("Recent", Color.white);
            recent.LastPlayedUnixSeconds = 9000;

            CharacterStore.Save(new List<CharacterSave> { old, recent });

            Assert.AreEqual("Recent", CharacterStore.Load()[0].Name);
        }

        [Test]
        public void NamesAreCleanedRatherThanTrustedAsTyped()
        {
            Assert.AreEqual("Ayaz", CharacterSave.CleanName("   Ayaz   "));
            Assert.AreEqual("Two Words", CharacterSave.CleanName("Two    Words"));
            Assert.AreEqual("Nobody", CharacterSave.CleanName("    "));
            Assert.AreEqual("Nobody", CharacterSave.CleanName(null));
            Assert.LessOrEqual(
                CharacterSave.CleanName(new string('x', 200)).Length,
                CharacterSave.MaximumNameLength);
        }

        /// <summary>
        /// Repair is what makes new fields safe to add later: a save written by
        /// an older build loads with them blank, and blank has to be valid.
        /// </summary>
        [Test]
        public void ASaveWithNothingInItRepairsIntoAPlayableCharacter()
        {
            var blank = new CharacterSave { Level = 0, Skin = new Color(0, 0, 0, 0) };

            Assert.IsTrue(blank.Repair(), "a blank save reported nothing to fix");

            Assert.IsNotEmpty(blank.Id);
            Assert.IsNotEmpty(blank.Name);
            Assert.AreEqual(1, blank.Level);
            Assert.AreEqual(CharacterSave.DefaultSkin, blank.Skin,
                "an invisible character would read as the body failing to load");
        }

        // ---- the hair colour ----

        [Test]
        public void AHairColourSurvivesARoundTripThroughDisk()
        {
            CharacterSave made = CharacterSave.Create("Dyed", Color.white);
            made.HairStyle = "hair1";
            made.HairColour = new Color(0.2f, 0.4f, 0.85f);
            CharacterStore.Add(made);

            CharacterSave loaded = CharacterStore.Load()[0];

            Assert.AreEqual("hair1", loaded.HairStyle);
            Assert.AreEqual(0.2f, loaded.HairColour.r, 0.001f, "the hair colour did not survive");
            Assert.AreEqual(0.4f, loaded.HairColour.g, 0.001f);
            Assert.AreEqual(0.85f, loaded.HairColour.b, 0.001f);
        }

        /// <summary>A save written before hair had a colour has no such field. It must load as the colour every head wore.</summary>
        [Test]
        public void ASaveWithNoHairColourLoadsAsTheDefault()
        {
            const string old = "{\"Id\":\"abc\",\"Name\":\"Old\",\"HairStyle\":\"none\",\"Level\":2}";

            CharacterSave loaded = JsonUtility.FromJson<CharacterSave>(old);

            Assert.AreEqual(CharacterSave.DefaultHairColour, loaded.HairColour);

            loaded.Repair();
            Assert.AreEqual(CharacterSave.DefaultHairColour, loaded.HairColour,
                "repairing an old save changed its hair colour");
        }

        [Test]
        public void AnyOpaqueHairColourIsValidButASeeThroughOneIsRepaired()
        {
            CharacterSave black = CharacterSave.Create("Dark", Color.white);
            black.HairColour = Color.black;
            Assert.IsFalse(black.Repair(), "black is a hair colour -- repair replaced it");
            Assert.AreEqual(Color.black, black.HairColour);

            CharacterSave clear = CharacterSave.Create("Ghost", Color.white);
            clear.HairColour = new Color(0.5f, 0.5f, 0.5f, 0.0f);
            Assert.IsTrue(clear.Repair());
            Assert.AreEqual(CharacterSave.DefaultHairColour, clear.HairColour);
        }

        [Test]
        public void RepairLeavesAGoodSaveAlone()
        {
            CharacterSave good = CharacterSave.Create("Fine", Color.white);
            Assert.IsFalse(good.Repair(), "a freshly created character needed repairing");
        }

        [Test]
        public void RepairClampsANegativeStatPointsBalanceToZero()
        {
            CharacterSave save = CharacterSave.Create("Hand-Edited", Color.white);
            save.StatPoints = -3;

            Assert.IsTrue(save.Repair());
            Assert.AreEqual(0, save.StatPoints);
        }

        [Test]
        public void PlaytimeReadsAsSomethingAPersonWouldSay()
        {
            Assert.AreEqual("under a minute", new CharacterSave { PlaytimeSeconds = 12 }.PlaytimeText());
            Assert.AreEqual("5m", new CharacterSave { PlaytimeSeconds = 5 * 60 }.PlaytimeText());
            Assert.AreEqual("2h 3m", new CharacterSave { PlaytimeSeconds = 123 * 60 }.PlaytimeText());
        }
    }
}
