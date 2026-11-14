using System.IO;
using CityX.Core;
using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What a weaker machine is spared (docs/PERFORMANCE_WEAK_PC.md; owner, 2026-11-13: "My friend played the game on a weaker machine and
    /// it was experiencing performance issues which it shouldn't"): the picture a machine starts on is chosen for its graphics card, a
    /// processor of four threads is not given three generator threads, and one long frame is not followed by sixteen physics steps.
    /// </summary>
    public sealed class WeakPcDefaultsTests
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
            Directory.Delete(_directory, true);
        }

        [Test]
        public void TheOwnersLaptopAndAnyBigCardStartOnHighAtFullResolutionAsBefore()
        {
            foreach ((string name, int megabytes, int width, int height) in new[]
            {
                ("NVIDIA GeForce RTX 3070 Laptop GPU", 8032, 2560, 1600),
                ("NVIDIA GeForce RTX 4090", 24000, 3840, 2160),
                ("AMD Radeon RX 6700 XT", 12000, 2560, 1440),
            })
            {
                GraphicsOptions options = HardwareDefaults.For(name, megabytes, width, height);
                Assert.AreEqual(GraphicsPreset.High, options.Preset, name);
                Assert.AreEqual(100, options.ResolutionScale, name);
            }
        }

        [Test]
        public void ABuiltInGraphicsCardStartsOnLowWithFewerPixels()
        {
            // A built-in card reports the computer's own memory as its own (8 GB here): its name is what tells.
            foreach (string name in new[] { "Intel(R) UHD Graphics 620", "Intel(R) Iris(R) Xe Graphics", "AMD Radeon(TM) Graphics", "AMD Radeon(TM) Vega 8 Graphics" })
            {
                GraphicsOptions options = HardwareDefaults.For(name, 8000, 1920, 1080);
                Assert.IsTrue(HardwareDefaults.IsBuiltIn(name), name);
                Assert.AreEqual(GraphicsPreset.Low, options.Preset, name);
                Assert.AreEqual(70, options.ResolutionScale, name);
                Assert.AreEqual(50, HardwareDefaults.For(name, 8000, 2560, 1440).ResolutionScale, name + " on a bigger screen");
            }
            Assert.AreEqual(GraphicsPreset.Low, HardwareDefaults.For("NVIDIA GeForce GT 1030", 2000, 1920, 1080).Preset, "a 2 GB card");
            foreach (string name in new[] { "NVIDIA GeForce RTX 3070 Laptop GPU", "AMD Radeon RX 6600 XT", "AMD Radeon(TM) RX 580", "Intel(R) Arc(TM) A750 Graphics" })
            {
                Assert.IsFalse(HardwareDefaults.IsBuiltIn(name), name + " is a card of its own");
            }
        }

        [Test]
        public void ASmallCardStartsOnMediumAndAMiddlingOneOnHighOnlyOnASmallScreen()
        {
            GraphicsOptions small = HardwareDefaults.For("NVIDIA GeForce GTX 1650", 4000, 1920, 1080);
            Assert.AreEqual(GraphicsPreset.Medium, small.Preset);
            Assert.AreEqual(100, small.ResolutionScale);
            Assert.AreEqual(70, HardwareDefaults.For("NVIDIA GeForce GTX 1650", 4000, 2560, 1440).ResolutionScale, "a 4 GB card on a 1440p screen draws fewer pixels");

            Assert.AreEqual(GraphicsPreset.High, HardwareDefaults.For("NVIDIA GeForce RTX 2060", 6000, 1920, 1080).Preset);
            Assert.AreEqual(GraphicsPreset.Medium, HardwareDefaults.For("NVIDIA GeForce RTX 2060", 6000, 2560, 1440).Preset);
        }

        [Test]
        public void EveryStartingPictureIsOneTheMenuOffersAndAnUnknownMachineIsLeftAsTheGameIsMade()
        {
            foreach (int megabytes in new[] { 512, 2000, 4000, 6000, 8000, 16000 })
            {
                foreach ((int width, int height) in new[] { (1280, 720), (1920, 1080), (2560, 1440), (3840, 2160) })
                {
                    GraphicsOptions options = HardwareDefaults.For("Some Card", megabytes, width, height);
                    CollectionAssert.Contains(GraphicsOptions.ResolutionScaleChoices, options.ResolutionScale);
                    Assert.AreNotEqual(GraphicsPreset.Custom, options.Preset, $"{megabytes} MB at {width}x{height} starts on no preset: the menu would say CUSTOM");
                }
            }
            GraphicsOptions unknown = HardwareDefaults.For(null, 0, 0, 0);
            Assert.AreEqual(GraphicsPreset.High, unknown.Preset);
            Assert.AreEqual(100, unknown.ResolutionScale);

            // A test's scratch directory with no file in it: the defaults as the game is made, whatever this machine is.
            GameSettingsData fresh = GameSettings.Load();
            Assert.AreEqual(GraphicsPreset.High, fresh.Graphics.Preset);
            Assert.AreEqual(100, fresh.Graphics.ResolutionScale);
            Assert.AreEqual(0, fresh.MaxFps);
        }

        [Test]
        public void ASavedChoiceIsNeverOverridden()
        {
            var saved = new GameSettingsData { Graphics = GraphicsOptions.Of(GraphicsPreset.Low) };
            saved.Graphics.ResolutionScale = 85;
            Assert.IsTrue(GameSettings.Save(saved));
            GameSettingsData loaded = GameSettings.Load();
            Assert.AreEqual(GraphicsPreset.Low, loaded.Graphics.Preset);
            Assert.AreEqual(85, loaded.Graphics.ResolutionScale);
        }

        [Test]
        public void ASmallProcessorIsLeftItsMainAndRenderThreads()
        {
            Assert.AreEqual(6, CityStreamer.SpareThreads(16), "the owner's laptop: as before");
            Assert.AreEqual(5, CityStreamer.SpareThreads(12));
            Assert.AreEqual(3, CityStreamer.SpareThreads(8), "as before");
            Assert.AreEqual(3, CityStreamer.SpareThreads(6), "as before");
            Assert.AreEqual(2, CityStreamer.SpareThreads(4), "four threads: two generators beside the game's own two, not three");
            Assert.AreEqual(1, CityStreamer.SpareThreads(2));
            for (int cores = 1; cores <= 64; cores++)
            {
                Assert.GreaterOrEqual(CityStreamer.SpareThreads(cores), 1, $"{cores} cores: nothing would generate the city");
                Assert.LessOrEqual(CityStreamer.SpareThreads(cores), 6);
            }
        }

        [Test]
        public void OneLongFrameIsFollowedByFivePhysicsStepsAtTheMost()
        {
            // (Sixteen until 2026-11-13: a third of a second. A slow machine's one bad frame made the next one worse.)
            Assert.AreEqual(0.1f, Time.maximumDeltaTime, 1e-4f);
            Assert.LessOrEqual(Mathf.RoundToInt(Time.maximumDeltaTime / Time.fixedDeltaTime), 5);
        }
    }
}
