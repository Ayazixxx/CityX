using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Snow in a small scene: it eases in, lies and melts, stays out from under a roof, slows a body on it and takes its prints, and goes with the rain (docs/SNOW.md).</summary>
    public sealed class SnowPlayTests
    {
        private const float Step = 1.0f / 60.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private SnowEffect _snow;

        private static readonly int SnowId = Shader.PropertyToID("_CityXSnow");

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(300.0f, 1.0f, 300.0f);
            _made.Add(ground);
            Physics.SyncTransforms();
            var host = new GameObject("Snow");
            host.SetActive(false);
            _made.Add(host);
            _snow = host.AddComponent<SnowEffect>();
            _snow.FlakeMaterial = new Material(Shader.Find("CityX/Snowfall"));
            host.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            CurrentWeather.ClearSnow();
        }

        private GameObject Roof(Vector3 at)
        {
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = at;
            roof.transform.localScale = new Vector3(14.0f, 0.4f, 14.0f);
            Physics.SyncTransforms();
            _made.Add(roof);
            return roof;
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
#else
            return null;
#endif
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static IEnumerator Walk(GameObject body, float seconds)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f }, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ItEasesInLiesAndTellsTheShadersThenMeltsWhenItStops()
        {
            Assert.IsFalse(_snow.FlakesShowing, "snow on a clear day");
            _snow.Apply(new WeatherOrder { Snow = SnowRules.Blizzard, Cover = -1.0f });
            yield return null;
            Assert.Less(_snow.Intensity, 0.1f, "it came all at once");

            yield return Seconds(SnowEffect.IntensityRampSeconds + 0.5f);
            Assert.AreEqual(SnowRules.Blizzard, _snow.Intensity, 1e-3f, "it did not arrive");
            Assert.IsTrue(_snow.FlakesShowing, "nothing falling");
            Assert.AreEqual(_snow.Intensity, CurrentWeather.SnowFalling, 1e-3f, "not told to the rest of the game");
            Assert.Greater(_snow.Cover, 0.0f, "nothing lying");
            Assert.AreEqual(_snow.Cover, Shader.GetGlobalVector(SnowId).x, 1e-3f, "the shaders were not told");

            _snow.Apply(new WeatherOrder { Snow = 0.0f, Cover = 0.5f });
            yield return Seconds(SnowEffect.IntensityRampSeconds + 0.5f);
            Assert.IsFalse(_snow.FlakesShowing, "still falling after it stopped");
            float stopped = _snow.Cover;
            yield return Seconds(5.0f);
            Assert.Less(_snow.Cover, stopped, "it did not begin to melt");
            Assert.Greater(_snow.Cover, 0.0f, "it melted all at once");
        }

        [UnityTest]
        public IEnumerator UnderARoofNoneLiesAndTheBodyWalksFreely()
        {
            Roof(new Vector3(0.0f, 6.0f, 0.0f));
            _snow.Apply(new WeatherOrder { Snow = SnowRules.Blizzard, Cover = 1.0f });
            yield return Seconds(0.2f);
            Assert.AreEqual(0.0f, CurrentWeather.SnowUnderfoot(new Vector3(0.0f, 0.1f, 0.0f)), 1e-3f, "snow under the roof");
            Assert.Greater(CurrentWeather.SnowUnderfoot(new Vector3(30.0f, 0.1f, 0.0f)), 0.9f, "none in the open");
            Assert.Less(CurrentWeather.QuenchingOn(new Vector3(0.0f, 1.0f, 0.0f)), 1e-3f, "falling on a flame under a roof");
        }

        [UnityTest]
        public IEnumerator ABodyIsSlowerThroughDeepSnowThanOnTheBareGround()
        {
            GameObject body = Body(new Vector3(0.0f, 0.05f, 0.0f));
            yield return Walk(body, 0.5f);
            Vector3 from = body.transform.position;
            yield return Walk(body, 1.0f);
            float bare = (body.transform.position - from).magnitude;
            Assert.Greater(bare, 1.0f, "the test body did not walk");

            _snow.Apply(new WeatherOrder { Snow = 0.0f, Cover = 1.0f });
            yield return Walk(body, 1.0f);
            from = body.transform.position;
            yield return Walk(body, 1.0f);
            float deep = (body.transform.position - from).magnitude;
            Assert.AreEqual(SnowRules.PaceFloor, deep / bare, 0.05f, $"{bare:F2} m bare, {deep:F2} m in the snow");
        }

        [UnityTest]
        public IEnumerator AWalkingBodyLeavesPrintsInTheSnowAndNoneOnBareGround()
        {
            var tracksHost = new GameObject("Tracks");
            tracksHost.SetActive(false);
            _made.Add(tracksHost);
            var tracks = tracksHost.AddComponent<SnowTracks>();
            tracksHost.SetActive(true);
            GameObject body = Body(new Vector3(0.0f, 0.05f, 0.0f));
            Time.captureFramerate = 60;

            yield return Walk(body, 3.0f);
            Assert.AreEqual(0, tracks.PrintsMade, "prints on bare ground");

            _snow.Apply(new WeatherOrder { Snow = 0.0f, Cover = 1.0f });
            yield return Walk(body, 4.0f);
            Assert.Greater(tracks.PrintsMade, 3, "a body walked 4 seconds through deep snow and left no prints");
            Assert.AreEqual(tracks.PrintsMade, tracks.PrintsLying, "made prints are not lying on the ground");
            float strideSeconds = 4.0f / tracks.PrintsMade;
            Assert.Greater(strideSeconds, 0.15f, $"{tracks.PrintsMade} prints in 4 seconds: a foot is landing over and over");
        }

        [UnityTest]
        public IEnumerator AnOrderReachesTheSnow()
        {
            CurrentWeather.Order(new WeatherOrder { Snow = SnowRules.ModerateSnow, Cover = -1.0f });
            yield return null;
            Assert.AreEqual(SnowRules.ModerateSnow, _snow.TargetIntensity, 1e-4f, "the order did not reach the snow");
            CurrentWeather.Order(new WeatherOrder { Snow = 0.0f, Cover = 0.0f });
            yield return null;
            Assert.AreEqual(0.0f, _snow.TargetIntensity, 1e-4f);
            Assert.AreEqual(0.0f, _snow.Cover, 1e-3f);
        }

        [UnityTest]
        public IEnumerator TheRainFollowsTheBodyAFriendClaims()
        {
            var rainHost = new GameObject("Rain");
            _made.Add(rainHost);
            _snow.Rain = rainHost.AddComponent<RainEffect>();
            var body = new GameObject("Friend's body");
            _made.Add(body);
            LocalPlayerBody.Claim(body);
            yield return null;
            Assert.AreEqual(body.transform, _snow.Rain.transform.parent, "a friend's rain stayed on the host's switched-off body");
        }
    }
}
