using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders snow in the real city (docs/SNOW.md) to Logs/CityShots/snow-*.png: a body's view of the street, over the roofs, a flurry's dusting, the blizzard, the prints a walk leaves, and night.
    /// Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SnowCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Step = 1.0f / 60.0f;
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheSnow()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var player = Object.FindFirstObjectByType<PlayerVitals>();
            var snow = Object.FindFirstObjectByType<SnowEffect>();
            var tracks = Object.FindFirstObjectByType<SnowTracks>();
            Assert.IsNotNull(snow, "no snow in the real scene -- rebuild it");
            Assert.IsNotNull(tracks, "no tracks in the real scene -- rebuild it");
            CityWorld world = CurrentWorld.Instance;
            world.Plan.TryFindSpawn(out double spawnX, out double spawnZ);
            var spawn = new Vector3((float)spawnX, (float)world.Terrain.ContinuousHeightAt(spawnX, spawnZ) + 0.2f, (float)spawnZ);
            console.Streamer.StreamAround(spawn, 900);
            Physics.SyncTransforms();
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = spawn;
            controller.enabled = true;
            Physics.SyncTransforms();
            player.GetComponent<PlayerInputReader>().enabled = false;
            yield return new WaitForSeconds(1.0f);

            console.Submit("/time noon");
            console.Cycle.Running = false;
            var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (follow != null) follow.enabled = false;
            Camera camera = Camera.main;
            Vector3 eye = spawn + new Vector3(0.0f, 3.0f, 0.0f);
            Vector3 ahead = eye + player.transform.forward * 40.0f + Vector3.down * 2.0f;
            Vector3 above = spawn + Vector3.up * 110.0f - player.transform.forward * 90.0f;

            // Clear, to compare.
            yield return Shoot(camera, "snow-0-clear", eye - player.transform.forward * 6.0f, ahead);

            // A flurry's dusting.
            console.Submit("/weather snow light");
            snow.Apply(new WeatherOrder { Snow = SnowRules.LightSnow, Cover = SnowRules.CoverCeiling(SnowRules.LightSnow) });
            yield return new WaitForSeconds(SnowEffect.MoodRampSeconds + 1.0f);
            yield return Shoot(camera, "snow-1-flurry-street", eye - player.transform.forward * 6.0f, ahead);
            yield return Shoot(camera, "snow-1-flurry-above", above, spawn);

            // Moderate, half buried.
            snow.Apply(new WeatherOrder { Snow = SnowRules.ModerateSnow, Cover = 0.8f });
            yield return new WaitForSeconds(SnowEffect.MoodRampSeconds + 1.0f);
            yield return Shoot(camera, "snow-2-moderate-street", eye - player.transform.forward * 6.0f, ahead);

            // The blizzard, over streets already buried.
            console.Submit("/weather deepsnow");
            yield return new WaitForSeconds(SnowEffect.MoodRampSeconds + 1.0f);
            yield return Shoot(camera, "snow-3-blizzard-street", eye - player.transform.forward * 6.0f, ahead);
            yield return Shoot(camera, "snow-3-blizzard-above", above, spawn);

            // Moderate again for the prints: a walk, and a look back along it.
            snow.Apply(new WeatherOrder { Snow = SnowRules.LightSnow, Cover = 1.0f });
            yield return new WaitForSeconds(SnowEffect.MoodRampSeconds);
            // Tracks are made near the camera (SnowTracks.TrackRange), and the last shot left it high over the city.
            camera.transform.position = spawn + Vector3.up * 3.0f;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            float yaw = player.transform.eulerAngles.y;
            Vector3 started = player.transform.position;
            for (float t = 0.0f; t < 5.0f; t += Step)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = yaw }, Step);
                yield return null;
            }
            Vector3 body = player.transform.position;
            Vector3 back = (started - body).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            TestContext.WriteLine($"walked {(body - started).magnitude:F1} m, prints made {tracks.PrintsMade}, lying {tracks.PrintsLying}");
            yield return Shoot(camera, "snow-4-prints", body - back * 1.0f + Vector3.up * 4.0f, body + back * 8.0f);
            yield return Shoot(camera, "snow-4-prints-near", body + back * 1.0f + side * 2.0f + Vector3.up * 2.2f, body + back * 3.0f);

            // Night.
            console.Submit("/time midnight");
            snow.Apply(new WeatherOrder { Snow = SnowRules.ModerateSnow, Cover = 1.0f });
            yield return new WaitForSeconds(2.0f);
            yield return Shoot(camera, "snow-5-night-street", eye - player.transform.forward * 6.0f, ahead);
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
