using System.IO;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The frame-rate cap in the settings: the list of choices, how a saved number maps back onto one, how a
    /// cap is put into force, and that it survives a save and load (and a settings file from before it existed).
    /// </summary>
    public sealed class MaxFpsSettingTests
    {
        private string _directory;
        private int _targetBefore;
        private int _vSyncBefore;

        [SetUp]
        public void SetUp()
        {
            _targetBefore = Application.targetFrameRate;
            _vSyncBefore = QualitySettings.vSyncCount;
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            GameSettings.DirectoryOverride = _directory;
        }

        [TearDown]
        public void TearDown()
        {
            Application.targetFrameRate = _targetBefore;
            QualitySettings.vSyncCount = _vSyncBefore;
            GameSettings.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void TheChoicesRiseAndEndWithNoCap()
        {
            int[] options = DisplaySettings.FpsOptions;
            Assert.AreEqual(0, options[options.Length - 1], "the last choice is 'unlimited'");
            for (int i = 1; i < options.Length - 1; i++)
            {
                Assert.Greater(options[i], options[i - 1], "the capped choices must rise");
            }
            CollectionAssert.Contains(options, 60, "60 has to be a choice");
            CollectionAssert.Contains(options, 144, "a 144Hz display's rate has to be a choice");
        }

        [Test]
        public void ASavedNumberLandsOnTheNearestChoice()
        {
            Assert.AreEqual(60, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(60)]);
            Assert.AreEqual(90, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(100)], "100 is nearer 90 than 120");
            Assert.AreEqual(120, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(110)]);
            Assert.AreEqual(30, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(1)]);
            Assert.AreEqual(240, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(1000)], "a huge cap is the highest cap, not 'unlimited'");
        }

        [Test]
        public void ZeroOrLessMeansNoCap()
        {
            Assert.AreEqual(0, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(0)]);
            Assert.AreEqual(0, DisplaySettings.FpsOptions[DisplaySettings.FpsIndexFor(-5)]);
        }

        [Test]
        public void EveryChoiceHasAReadableLabel()
        {
            Assert.AreEqual("UNLIMITED", DisplaySettings.FpsLabel(0));
            Assert.AreEqual("60 FPS", DisplaySettings.FpsLabel(60));
            foreach (int fps in DisplaySettings.FpsOptions)
            {
                Assert.IsNotEmpty(DisplaySettings.FpsLabel(fps));
            }
        }

        [Test]
        public void ACapIsPutIntoForceAndVSyncIsSwitchedOffSoItIsNotIgnored()
        {
            QualitySettings.vSyncCount = 1;

            DisplaySettings.ApplyMaxFps(60);

            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount, "with V-Sync on, Unity ignores the frame-rate cap");
        }

        [Test]
        public void NoCapRemovesTheLimit()
        {
            DisplaySettings.ApplyMaxFps(60);
            DisplaySettings.ApplyMaxFps(0);

            Assert.AreEqual(-1, Application.targetFrameRate);
        }

        [Test]
        public void ACapSurvivesASaveAndALoad()
        {
            GameSettings.Save(new GameSettingsData { MaxFps = 144 });

            Assert.AreEqual(144, GameSettings.Load().MaxFps);
        }

        [Test]
        public void ASettingsFileFromBeforeTheCapExistedLoadsAsUncapped()
        {
            File.WriteAllText(GameSettings.Path_, "{\"MasterVolume\":0.5,\"MusicVolume\":1.0,\"Fullscreen\":true,\"ResolutionIndex\":3}");

            GameSettingsData data = GameSettings.Load();

            Assert.AreEqual(0.5f, data.MasterVolume, 0.001f, "the rest of the old file must still load");
            Assert.AreEqual(0, data.MaxFps, "an old file has no cap, which is what the game always did");
        }

        [Test]
        public void AFreshInstallIsUncappedAsBefore()
        {
            Assert.AreEqual(0, DisplaySettings.DefaultMaxFps);
            Assert.AreEqual(0, new GameSettingsData().MaxFps);
        }
    }
}
