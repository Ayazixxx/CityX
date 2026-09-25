using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The shared weather state everything reacts to. A scene with no rain
    /// publishes nothing, and that must read as a perfectly dry, open world --
    /// otherwise every test and every scene without rain would feel weather.
    /// </summary>
    public sealed class CurrentWeatherTests
    {
        private sealed class FakeCover : IRainCover
        {
            public float Value;
            public float Shelter(Vector3 worldPoint) => Value;
        }

        [SetUp]
        public void SetUp() => CurrentWeather.Clear();

        [TearDown]
        public void TearDown() => CurrentWeather.Clear();

        [Test]
        public void NothingPublishedMeansADryOpenWorld()
        {
            Assert.IsFalse(CurrentWeather.IsRaining);
            Assert.AreEqual(0.0f, CurrentWeather.Shelter(Vector3.zero));
            Assert.AreEqual(0.0f, CurrentWeather.RainOn(Vector3.zero));
            Assert.AreEqual(0.0f, CurrentWeather.SurfaceWetness(Vector3.zero));
        }

        [Test]
        public void RainFallsOnAnOpenPointAtFullIntensity()
        {
            CurrentWeather.Intensity = 0.8f;
            Assert.IsTrue(CurrentWeather.IsRaining);
            Assert.AreEqual(0.8f, CurrentWeather.RainOn(Vector3.zero), 1e-6f, "with no cover map an unmapped world is open sky");
        }

        [Test]
        public void CoverCutsRainOffAndPartialCoverCutsItPartway()
        {
            var cover = new FakeCover();
            CurrentWeather.Cover = cover;
            CurrentWeather.Intensity = 1.0f;

            cover.Value = 1.0f;
            Assert.AreEqual(0.0f, CurrentWeather.RainOn(Vector3.zero), 1e-6f, "under a roof, no rain falls on you");
            cover.Value = 0.25f;
            Assert.AreEqual(0.75f, CurrentWeather.RainOn(Vector3.zero), 1e-6f);
            cover.Value = 0.0f;
            Assert.AreEqual(1.0f, CurrentWeather.RainOn(Vector3.zero), 1e-6f);
        }

        [Test]
        public void GroundUnderCoverStaysMuchDrierThanGroundInTheOpen()
        {
            var cover = new FakeCover();
            CurrentWeather.Cover = cover;
            CurrentWeather.Wetness = 1.0f;

            cover.Value = 0.0f;
            float open = CurrentWeather.SurfaceWetness(Vector3.zero);
            cover.Value = 1.0f;
            float sheltered = CurrentWeather.SurfaceWetness(Vector3.zero);

            Assert.AreEqual(1.0f, open, 1e-6f);
            Assert.Less(sheltered, 0.5f);
            Assert.Greater(sheltered, 0.0f, "not bone dry -- the ground under a roof is damp from splashing");
        }

        [Test]
        public void ShelterIsClampedToZeroToOneWhateverACoverImplementationReturns()
        {
            var cover = new FakeCover();
            CurrentWeather.Cover = cover;
            cover.Value = 3.0f;
            Assert.AreEqual(1.0f, CurrentWeather.Shelter(Vector3.zero));
            cover.Value = -2.0f;
            Assert.AreEqual(0.0f, CurrentWeather.Shelter(Vector3.zero));
        }

        [Test]
        public void ClearPutsEverythingBack()
        {
            CurrentWeather.Cover = new FakeCover { Value = 1.0f };
            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;

            CurrentWeather.Clear();

            Assert.IsFalse(CurrentWeather.IsRaining);
            Assert.AreEqual(0.0f, CurrentWeather.Wetness);
            Assert.IsNull(CurrentWeather.Cover);
        }
    }
}
