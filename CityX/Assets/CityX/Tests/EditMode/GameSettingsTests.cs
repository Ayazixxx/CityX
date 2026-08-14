using System.IO;
using CityX.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Saved settings. What matters here is the same thing that matters for
    /// CharacterStore: a missing or corrupt file must never stop the player
    /// from getting into the game, just quietly fall back to defaults.
    /// </summary>
    public sealed class GameSettingsTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            GameSettings.DirectoryOverride = _directory;
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.DirectoryOverride = null;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void NoFileMeansDefaultsRatherThanAnError()
        {
            GameSettingsData data = GameSettings.Load();

            Assert.AreEqual(1.0f, data.MasterVolume, 0.001f);
            Assert.IsTrue(data.Fullscreen);
        }

        [Test]
        public void SettingsSurviveARoundTripThroughDisk()
        {
            var saved = new GameSettingsData
            {
                MasterVolume = 0.42f,
                Fullscreen = false,
                ResolutionIndex = 1,
            };
            GameSettings.Save(saved);

            GameSettingsData loaded = GameSettings.Load();

            Assert.AreEqual(0.42f, loaded.MasterVolume, 0.001f);
            Assert.IsFalse(loaded.Fullscreen);
            Assert.AreEqual(1, loaded.ResolutionIndex);
        }

        [Test]
        public void SavingRepeatedlyOverwritesRatherThanFailingAfterTheFirst()
        {
            GameSettings.Save(new GameSettingsData { MasterVolume = 0.5f });
            GameSettings.Save(new GameSettingsData { MasterVolume = 0.9f });

            Assert.AreEqual(0.9f, GameSettings.Load().MasterVolume, 0.001f);
        }

        [Test]
        public void ACorruptFileLeavesThePlayerWithDefaultsRatherThanACrash()
        {
            File.WriteAllText(GameSettings.Path_, "{ not json at all");

            LogAssert.ignoreFailingMessages = true;
            GameSettingsData data = GameSettings.Load();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(1.0f, data.MasterVolume, 0.001f);
            Assert.IsTrue(File.Exists(GameSettings.Path_), "the corrupt file should be left alone, not deleted");
        }
    }
}
