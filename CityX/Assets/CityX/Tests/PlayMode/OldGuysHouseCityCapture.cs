using System.Collections;
using System.IO;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Old Guy's house where it stands in the real city (docs/OLD_GUYS_HOUSE.md) to Logs/CityShots/house-city-*.png: as the player
    /// sees it on landing (the game's camera behind them), from across its street, and from above with the blocks round it. Explicit; run by name with
    /// -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.OldGuysHouseCityCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class OldGuysHouseCityCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private CityScale _scaleBefore;

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city: the classic one's blocks are too small for the house
        }

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
        }

        [UnityTest]
        public IEnumerator RenderTheHouseInTheCity()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            PlanWorld plan = new CityWorld(streamer.WorldSeed).Plan;
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }
            OldGuysHouseSite house = streamer.TutorialHouse;
            Assert.IsNotNull(house, "the scene has no Old Guy's house");
            house.Build();

            plan.TryFindSpawn(out double sx, out double sz);
            float ground = (float)plan.GroundHeight(sx, sz);
            var spawn = new Vector3((float)sx, ground, (float)sz);
            Vector3 middle = house.transform.TransformPoint(new Vector3(10.0f, 6.0f, 8.0f));
            streamer.StreamAround((spawn + middle) * 0.5f, int.MaxValue);
            for (int i = 0; i < 20; i++) yield return null;

            var cameraObject = new GameObject("HouseCityCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // On landing: the game's camera (FOV 50, 5 m back, a little over the head) behind the player on the driveway, looking at the house; and
            // turned round, the street the player lands beside.
            camera.fieldOfView = 50.0f;
            Vector3 landing = house.transform.TransformPoint(CityX.World.OldGuysHouse.Landing);
            BuildingShapeCapture.Shoot(camera, directory, "house-city-landing", house.transform.TransformPoint(CityX.World.OldGuysHouse.Landing + new Vector3(-2.0f, 3.3f, -5.0f)), house.transform.TransformPoint(new Vector3(12.0f, 4.0f, 2.0f)));
            BuildingShapeCapture.Shoot(camera, directory, "house-city-landing-back", landing + Vector3.up * 3.3f + house.transform.forward * 2.0f, landing - house.transform.forward * 20.0f + Vector3.up * 2.0f);
            // From across the street, at the front's middle.
            camera.fieldOfView = 60.0f;
            Vector3 across = house.transform.TransformPoint(new Vector3(11.5f, 2.5f, -22.0f));
            BuildingShapeCapture.Shoot(camera, directory, "house-city-street", across, house.transform.TransformPoint(new Vector3(11.5f, 5.0f, 6.0f)));
            // From high over the house (towers stand round it), the blocks round it and the spawn junction in view.
            Vector3 high = house.transform.TransformPoint(new Vector3(11.5f, 85.0f, -30.0f));
            BuildingShapeCapture.Shoot(camera, directory, "house-city-above", high, (spawn + house.transform.TransformPoint(new Vector3(11.5f, 0.0f, 14.0f))) * 0.5f);
            Debug.Log($"CityX house in the city: spawn {spawn}, house at {house.transform.position}, turned {house.transform.eulerAngles.y:F0}, footing {house.FootingDepth:F2} m");
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
