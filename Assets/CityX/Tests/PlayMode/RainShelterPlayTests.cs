using System.Collections;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The rain, with real physics under it: a real ground, a real roof, real
    /// particles. This is where "no rain falls under a roof" is MEASURED --
    /// counting live drops in a box -- because a grid of ray casts that agrees
    /// with itself proves nothing about what the particle system does with it.
    ///
    /// Built from primitives, not the city, so it tests the rain and not the
    /// art; the city's own overhangs are checked separately by the screenshot
    /// under a highway deck.
    /// </summary>
    public sealed class RainShelterPlayTests
    {
        private const float RoofTop = 6.2f;

        private GameObject _ground;
        private GameObject _roof;
        private GameObject _host;
        private RainEffect _rain;
        private AudioClip _clip;

        private sealed class TestDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds) => HitResponse.Landed;
        }

        [SetUp]
        public void SetUp()
        {
            CurrentWeather.Clear();

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(300.0f, 1.0f, 300.0f);

            // A 12m square roof, 6m up: x and z from -6 to 6, top at 6.2.
            _roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _roof.name = "Roof";
            _roof.transform.position = new Vector3(0.0f, 6.0f, 0.0f);
            _roof.transform.localScale = new Vector3(12.0f, 0.4f, 12.0f);

            Physics.SyncTransforms();

            _clip = AudioClip.Create("test-rain", 8000, 1, 8000, false);
            _host = new GameObject("Player");
            _host.SetActive(false);
            _host.transform.position = new Vector3(0.0f, 0.1f, 0.0f);
            _rain = _host.AddComponent<RainEffect>();
            _rain.RainSound = _clip;
            _host.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_roof != null) Object.DestroyImmediate(_roof);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_clip != null) Object.DestroyImmediate(_clip);
            CurrentWeather.Clear();
        }

        private static Bounds Box(Vector3 centre, Vector3 size) => new Bounds(centre, size);

        /// <summary>
        /// THE WETNESS GLITCH: rain edits the shared ground materials in place, and
        /// stopping the rain by any route but waiting for it to dry left them dark
        /// and glossy in the ASSET -- which the next scene builder then saved, so the
        /// whole game looked different in dry weather. However the rain ends, the
        /// materials must come back exactly as they were.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingTheRainLeavesTheSharedGroundMaterialsDry()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color dryColour = new Color(0.8f, 0.7f, 0.6f, 1.0f);
            material.SetColor("_BaseColor", dryColour);
            material.SetFloat("_Smoothness", 0.1f);

            // Its OWN rain, wired while still inactive: the first Update captures what "dry"
            // looks like, and the fixture's rain may already have had its first frame.
            var host = new GameObject("WetRain");
            host.SetActive(false);
            host.transform.position = new Vector3(0.0f, 0.1f, 0.0f);
            RainEffect rain = host.AddComponent<RainEffect>();
            rain.RainSound = _clip;
            rain.WetGroundMaterials = new[] { material };
            host.SetActive(true);

            // Let the storm build first: at the start of the ramp the rain is too thin to hold a soak.
            rain.SetRain(1.0f);
            float waited = 0.0f;
            while (rain.Intensity < 1.0f && waited < 8.0f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            rain.Soak(1.0f);
            waited = 0.0f;
            while (Mathf.Approximately(material.GetFloat("_Smoothness"), 0.1f) && waited < 5.0f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(Mathf.Abs(material.GetFloat("_Smoothness") - 0.1f), 1e-3f, "setup: the rain never wetted the material");

            Object.DestroyImmediate(host);

            Assert.AreEqual(0.1f, material.GetFloat("_Smoothness"), 1e-5f, "the ground stayed glossy after the rain was destroyed");
            Assert.AreEqual(dryColour.r, material.GetColor("_BaseColor").r, 1e-5f, "the ground stayed dark after the rain was destroyed");
            Object.DestroyImmediate(material);
        }

        [UnityTest]
        public IEnumerator NoRainFallsUnderARoofAndItLandsOnTopOfIt()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.5f);

            Assert.Greater(_rain.LiveDropCount, 50, "no rain at all -- setup is broken");

            int under = _rain.CountLiveDropsIn(Box(new Vector3(0.0f, 3.0f, 0.0f), new Vector3(10.0f, 5.5f, 10.0f)));
            int onTheRoof = _rain.CountLiveDropsIn(Box(new Vector3(0.0f, 12.5f, 0.0f), new Vector3(10.0f, 12.5f, 10.0f)));
            int outside = _rain.CountLiveDropsIn(Box(new Vector3(14.0f, 9.0f, 0.0f), new Vector3(8.0f, 18.0f, 10.0f)));

            TestContext.WriteLine($"drops: {under} under the roof, {onTheRoof} above it, {outside} out in the open");
            Assert.AreEqual(0, under, "rain fell under the roof");
            Assert.Greater(onTheRoof, 2, "the rain over the roof should be falling onto it");
            Assert.Greater(outside, 5, "the rain beside the roof should still be falling to the ground");
        }

        [UnityTest]
        public IEnumerator DropsOverARoofStopAtItsSurfaceNotBelowIt()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.5f);

            // Any drop that is over the roof must be above the roof's top.
            int belowTop = _rain.CountLiveDropsIn(Box(new Vector3(0.0f, 3.0f, 0.0f), new Vector3(11.0f, RoofTop, 11.0f)));
            Assert.AreEqual(0, belowTop, "a drop over the roof was still falling below the roof's top surface");
        }

        [UnityTest]
        public IEnumerator TheCoverMapKnowsWhoIsUnderTheRoof()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(1.0f, CurrentWeather.Shelter(new Vector3(0.0f, 0.1f, 0.0f)), 0.01f, "under the roof");
            Assert.AreEqual(0.0f, CurrentWeather.Shelter(new Vector3(12.0f, 0.1f, 0.0f)), 0.01f, "well outside it");
            Assert.AreEqual(0.0f, CurrentWeather.Shelter(new Vector3(0.0f, RoofTop, 0.0f)), 0.01f, "standing ON the roof");
            Assert.Greater(CurrentWeather.RainOn(new Vector3(12.0f, 0.1f, 0.0f)), 0.0f, "rain does fall in the open");
            Assert.AreEqual(0.0f, CurrentWeather.RainOn(new Vector3(0.0f, 0.1f, 0.0f)), 0.01f, "and not under the roof");
        }

        [UnityTest]
        public IEnumerator ACharacterStandingInTheColumnIsNotMistakenForARoof()
        {
            // A pedestrian's capsule 2m over an open patch of ground.
            var person = new GameObject("Pedestrian");
            person.transform.position = new Vector3(31.0f, 1.0f, 1.0f);   // dead centre of a 2m cell, so the cell's ray meets it
            person.AddComponent<CapsuleCollider>().height = 2.0f;
            person.AddComponent<TestDamageable>();
            Physics.SyncTransforms();

            _host.transform.position = new Vector3(31.0f, 0.1f, 5.0f);
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(0.0f, _rain.Cover.Shelter(new Vector3(31.0f, 0.1f, 1.0f)), 0.01f,
                "a person's own body counted as a roof over their own head");
            Assert.AreEqual(0.0f, _rain.Cover.SurfaceHeightAt(31.0f, 1.0f), 0.05f,
                "rain in that column should fall past the person to the ground");
            Object.DestroyImmediate(person);
        }

        [UnityTest]
        public IEnumerator UnderTheRoofTheRainSoundIsQuieterAndMuffledAndOutOfItIsNot()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.6f);

            var source = _rain.GetComponent<AudioSource>();
            var filter = _rain.GetComponent<AudioLowPassFilter>();
            Assert.Greater(_rain.SoundShelter, 0.99f);
            Assert.Less(filter.cutoffFrequency, 2500.0f, "rain under a roof should be muffled");
            Assert.AreEqual(Weather.RainVolume(1.0f) * Weather.ShelteredVolume(1.0f), source.volume, 0.02f,
                "rain under a roof should be quieter");

            _host.transform.position = new Vector3(20.0f, 0.1f, 0.0f);
            yield return new WaitForSeconds(2.2f);

            Assert.Less(_rain.SoundShelter, 0.01f);
            Assert.AreEqual(22000.0f, filter.cutoffFrequency, 100.0f, "out in the open the rain is not filtered");
            Assert.AreEqual(Weather.RainVolume(1.0f), source.volume, 0.02f, "and full volume");
        }

        [UnityTest]
        public IEnumerator SteppingOutFromUnderTheRoofFadesTheMuffleInsteadOfSnappingIt()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.0f);
            Assert.Greater(_rain.SoundShelter, 0.99f);

            _host.transform.position = new Vector3(20.0f, 0.1f, 0.0f);
            yield return null;
            yield return null;

            Assert.Greater(_rain.SoundShelter, 0.5f, "the muffle should ease out over a third of a second, not vanish in a frame");
        }

        [UnityTest]
        public IEnumerator TheCityGetsWetQuicklyAndDriesSlowlyAndWetnessIsPublished()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.5f);
            float wetAfterRain = _rain.Wetness;
            Assert.Greater(wetAfterRain, 0.02f, "the city did not start getting wet");
            Assert.AreEqual(_rain.Wetness, CurrentWeather.Wetness, 0.01f, "wetness is published to the rest of the game");
            Assert.AreEqual(_rain.Intensity, CurrentWeather.Intensity, 0.01f);

            _rain.Soak(1.0f);
            _rain.SetRain(0.0f);
            yield return new WaitForSeconds(3.5f);   // long enough for the rain to fully stop

            Assert.AreEqual(0.0f, _rain.Intensity, 1e-4f, "the rain never fully stopped");
            Assert.Greater(_rain.Wetness, 0.9f, "the city dried out in seconds -- it should stay wet for minutes");
        }

        [UnityTest]
        public IEnumerator DryDriesEverythingAtOnce()
        {
            _rain.Soak(1.0f);
            _rain.SetRain(0.6f);
            yield return null;

            _rain.Dry();
            yield return null;

            Assert.AreEqual(0.0f, _rain.Wetness);
            Assert.AreEqual(0.0f, _rain.Intensity);
            Assert.IsFalse(_rain.IsActive);
        }

        [UnityTest]
        public IEnumerator RingsFormOnWetGroundAndNotOnDryGround()
        {
            // Dry ground: a downpour makes splashes but no rings.
            _host.transform.position = new Vector3(60.0f, 0.1f, 0.0f);
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(_rain.LiveSplashCount, 0, "drops landing should splash");
            Assert.AreEqual(0, _rain.LiveRippleCount, "dry ground grew rings");

            _rain.Soak(1.0f);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(_rain.LiveRippleCount, 0, "soaked ground grew no rings");
        }

        [UnityTest]
        public IEnumerator ADrizzleIsThinnerThanADownpour()
        {
            _host.transform.position = new Vector3(60.0f, 0.1f, 0.0f);
            _rain.SetRain(Weather.LightRain);
            yield return new WaitForSeconds(2.5f);
            int drizzle = _rain.LiveDropCount;

            _rain.SetRain(Weather.HeavyRain);
            yield return new WaitForSeconds(4.0f);
            int downpour = _rain.LiveDropCount;

            TestContext.WriteLine($"live drops: {drizzle} in a drizzle, {downpour} in a downpour");
            Assert.Greater(downpour, drizzle * 2, "a downpour should be far denser than a drizzle");
        }

        [UnityTest]
        public IEnumerator AStrikeShowsABoltNowAndItsThunderArrivesAtTheSpeedOfSound()
        {
            _host.transform.position = new Vector3(60.0f, 0.1f, 0.0f);
            _rain.StrikeLightning(343.0f);   // exactly one second of sound

            Assert.IsTrue(_rain.BoltVisible, "the bolt should be there the moment it strikes");
            Assert.AreEqual(1, _rain.PendingThunderCount, "thunder should be on its way");
            Assert.AreEqual(0, _rain.ThunderPlayCount, "thunder can't have arrived yet");

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(_rain.BoltVisible, "the bolt is a flash, not a lamp");
            Assert.AreEqual(0, _rain.ThunderPlayCount, "half a second in, a kilometre-third-away thunder has not arrived");

            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(1, _rain.ThunderPlayCount, "the thunder never played");
            Assert.AreEqual(0, _rain.PendingThunderCount);
        }

        [UnityTest]
        public IEnumerator ADistantStrikeIsHeardLaterThanANearOne()
        {
            _host.transform.position = new Vector3(60.0f, 0.1f, 0.0f);
            _rain.StrikeLightning(150.0f);     // 0.44 s
            _rain.StrikeLightning(1200.0f);    // 3.5 s

            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(1, _rain.ThunderPlayCount, "the near one is heard first");
            Assert.AreEqual(1, _rain.PendingThunderCount, "the far one is still on its way");

            yield return new WaitForSeconds(3.0f);
            Assert.AreEqual(2, _rain.ThunderPlayCount);
        }

        [UnityTest]
        public IEnumerator DestroyingTheRainLeavesADryWorldForEverythingElse()
        {
            _rain.SetRain(1.0f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(CurrentWeather.IsRaining);

            Object.DestroyImmediate(_host);
            _host = null;

            Assert.IsFalse(CurrentWeather.IsRaining);
            Assert.AreEqual(0.0f, CurrentWeather.Wetness);
            Assert.IsNull(CurrentWeather.Cover, "a dead rain system left its cover map behind");
        }

        [UnityTest]
        public IEnumerator PlainSetActiveStillMeansAFullDownpour()
        {
            _rain.SetActive(true);
            yield return null;
            Assert.IsTrue(_rain.IsActive);
            Assert.AreEqual(Weather.HeavyRain, _rain.TargetIntensity);

            _rain.SetActive(false);
            yield return null;
            Assert.IsFalse(_rain.IsActive);
        }
    }
}
