using System.IO;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// This PC's own server (singleplayer, hosting): the characters saved on this PC before servers kept them move in the first time, and
    /// the character screen's questions are answered at once through it.
    /// </summary>
    public sealed class LocalServerTests
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
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void TheCharactersOnThisPcAreThereTheFirstTimeTheServerIsAsked()
        {
            CharacterSave old = CharacterSave.Create("From before", Color.white);
            old.Level = 12;
            CharacterStore.Add(old);

            var characters = new System.Collections.Generic.List<CharacterSave>();
            CharacterSources.ThisPc.List(list => characters = list);

            Assert.AreEqual(1, characters.Count);
            Assert.AreEqual(12, characters[0].Level, "a character moved in lost its progress");
            Assert.IsTrue(File.Exists(Path.Combine(_directory, CharacterStore.FileName)), "the old file is the backup");
        }

        [Test]
        public void ThisPcsServerMakesPlaysAndSavesAtOnce()
        {
            CharacterSave made = null;
            CharacterSources.ThisPc.Create(CharacterSave.Create("New", Color.white), m => made = m);
            Assert.IsNotNull(made);

            CharacterSave served = null;
            bool isAdmin = false;
            CharacterSources.ThisPc.Play(made.Id, (s, admin) => { served = s; isAdmin = admin; });
            Assert.AreEqual(made.Id, served.Id);
            Assert.IsTrue(isAdmin);

            served.Experience = 40;
            CharacterSources.ThisPc.Save(served, adminEdited: false);
            Assert.AreEqual(40, LocalServer.Characters.Play(PlayerKey.Local, made.Id).Experience);
        }

        [Test]
        public void TheCharacterScreenUsesThisPcsServerUntilAnotherIsJoined()
        {
            Assert.AreSame(CharacterSources.ThisPc, CharacterSources.Current);
        }
    }
}
