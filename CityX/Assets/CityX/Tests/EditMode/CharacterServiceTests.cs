using System.Collections.Generic;
using System.IO;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Characters kept on the server (owner, 2026-10-22: "ok do server side saving"): each player reaches only their own, a new character
    /// starts at the start whatever it is sent with, the characters already on this PC move in once without being lost, and the admin list.
    /// </summary>
    public sealed class CharacterServiceTests
    {
        private const string Ayaz = "0123456789abcdef0123456789abcdef";
        private const string Other = "fedcba9876543210fedcba9876543210";

        private string _directory;
        private CharacterDatabase _database;
        private CharacterService _service;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            _database = new CharacterDatabase(Path.Combine(_directory, "server"));
            _service = new CharacterService(_database);
            CharacterStore.DirectoryOverride = _directory;
        }

        [TearDown]
        public void TearDown()
        {
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void ANewCharacterKeepsItsNameAndLookButStartsAtTheStart()
        {
            CharacterSave asked = CharacterSave.Create("Ayaz", new Color(0.3f, 0.4f, 0.5f));
            asked.HairStyle = "hyde";
            asked.Level = 99;
            asked.SkillPoints = 500;
            asked.Wearing = new List<string> { "knight_helmet" };
            asked.Id = "chosen-by-the-client";

            CharacterSave made = _service.Create(Ayaz, asked);

            Assert.IsNotNull(made);
            Assert.AreEqual("Ayaz", made.Name);
            Assert.AreEqual("hyde", made.HairStyle);
            Assert.AreEqual(0.4f, made.Skin.g, 0.001f);
            Assert.AreEqual(1, made.Level, "a new character took the level it was sent with");
            Assert.AreEqual(CharacterSave.StartingSkillPoints, made.SkillPoints);
            Assert.IsEmpty(made.Wearing);
            Assert.AreNotEqual("chosen-by-the-client", made.Id, "the server makes the id");
            Assert.AreEqual(1, _service.List(Ayaz).Count);
        }

        [Test]
        public void APlayerReachesOnlyTheirOwnCharacters()
        {
            CharacterSave mine = _service.Create(Ayaz, CharacterSave.Create("Mine", Color.white));

            Assert.IsEmpty(_service.List(Other));
            Assert.IsNull(_service.Play(Other, mine.Id), "another player could play my character");
            Assert.IsFalse(_service.Delete(Other, mine.Id), "another player could delete my character");
            CharacterSave forged = _service.Play(Ayaz, mine.Id);
            forged.Level = 50;
            Assert.IsFalse(_service.Save(Other, forged), "another player could save over my character");
            Assert.AreEqual(1, _service.Play(Ayaz, mine.Id).Level);
        }

        [Test]
        public void AKeyThatIsNotOneReachesNothingOnDisk()
        {
            foreach (string bad in new[] { null, "", "..", "../../evil", "0123456789ABCDEF0123456789ABCDEF" })
            {
                Assert.IsNull(_service.Create(bad, CharacterSave.Create("X", Color.white)), $"\"{bad}\" made a character");
                Assert.IsEmpty(_service.List(bad));
            }
            Assert.IsFalse(Directory.Exists(Path.Combine(_database.Root, CharacterDatabase.AccountsFolder)), "something was written for a bad key");
        }

        [Test]
        public void ASaveKeepsTheProgressClothesAndWeaponButNotANewNameOrId()
        {
            CharacterSave made = _service.Create(Ayaz, CharacterSave.Create("Ayaz", Color.white));
            CharacterSave playing = _service.Play(Ayaz, made.Id);
            playing.Level = 7;
            playing.Experience = 120;
            playing.Wearing = new List<string> { "t_shirt" };
            playing.PrimaryWeapon = "baseball_bat";
            playing.Name = "Renamed";
            long created = playing.CreatedUnixSeconds;
            playing.CreatedUnixSeconds = 1;

            Assert.IsTrue(_service.Save(Ayaz, playing));
            CharacterSave stored = _service.Play(Ayaz, made.Id);
            Assert.AreEqual(7, stored.Level);
            Assert.AreEqual(120, stored.Experience);
            CollectionAssert.AreEqual(new[] { "t_shirt" }, stored.Wearing);
            Assert.AreEqual("baseball_bat", stored.PrimaryWeapon, "the weapon in hand was not saved, so it was gone after a restart");
            Assert.AreEqual("Ayaz", stored.Name, "a save renamed the character");
            Assert.AreEqual(created, stored.CreatedUnixSeconds);
        }

        [Test]
        public void APlayerCannotMakeMoreThanTheirShare()
        {
            for (int i = 0; i < CharacterService.MaxCharactersPerPlayer; i++)
            {
                Assert.IsNotNull(_service.Create(Ayaz, CharacterSave.Create($"C{i}", Color.white)));
            }
            Assert.IsNull(_service.Create(Ayaz, CharacterSave.Create("One too many", Color.white)));
        }

        [Test]
        public void TheCharactersOnThisPcMoveInOnceAndTheOldFileStays()
        {
            var local = new CharacterFile(_directory);
            local.Add(CharacterSave.Create("Old one", Color.white));
            local.Add(CharacterSave.Create("Older one", Color.white));

            Assert.AreEqual(2, _database.MoveInLocalCharacters(Ayaz, _directory));
            Assert.AreEqual(2, _service.List(Ayaz).Count);
            Assert.IsTrue(local.Exists, "the old file is the backup and must stay");

            _service.Delete(Ayaz, _service.List(Ayaz)[0].Id);
            Assert.AreEqual(0, _database.MoveInLocalCharacters(Ayaz, _directory), "the move ran twice and brought a deleted character back");
            Assert.AreEqual(1, _service.List(Ayaz).Count);
        }

        [Test]
        public void EveryoneIsAnAdminUntilTheListIsTurnedOn()
        {
            CharacterDatabase.Admins admins = _database.LoadAdmins();
            Assert.IsTrue(admins.Includes(Ayaz));
            Assert.IsTrue(File.Exists(Path.Combine(_database.Root, CharacterDatabase.AdminsFileName)), "the admin list was not written to edit");

            admins.EveryoneIsAdmin = false;
            admins.Keys.Add(Ayaz);
            File.WriteAllText(Path.Combine(_database.Root, CharacterDatabase.AdminsFileName), JsonUtility.ToJson(admins));
            CharacterDatabase.Admins reread = _database.LoadAdmins();
            Assert.IsTrue(reread.Includes(Ayaz));
            Assert.IsFalse(reread.Includes(Other));
        }

        [Test]
        public void ThisCopysKeyIsMadeOnceAndKept()
        {
            string key = PlayerKey.Local;
            Assert.IsTrue(PlayerKey.IsValid(key));
            Assert.AreEqual(key, File.ReadAllText(Path.Combine(_directory, PlayerKey.FileName)).Trim());
            Assert.AreEqual(key, PlayerKey.Local);
        }
    }
}
