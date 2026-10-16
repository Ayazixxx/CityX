using System.Collections;
using System.IO;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the footbridges (World/Footbridges) in the real city -- from the street under one, from on its deck, and a street with
    /// several -- to Logs/CityShots/bridge-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FootbridgeCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheFootbridges()
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
            var world = new CityWorld(streamer.WorldSeed);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var cameraObject = new GameObject("BridgeCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // The bridge nearest the spawn in the densest district, and the street with the most bridges.
            Footbridge best = default;
            bool found = false;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 40))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (b.SiteA != site.Id) continue;
                    if (!found || (site.Zone == ZoneKind.Core && world.Building(best.SiteA).Zone != ZoneKind.Core))
                    {
                        best = b;
                        found = true;
                    }
                }
            }
            Assert.IsTrue(found, "no footbridge near the spawn to render");
            Vector3 middle = (best.StartWorld + best.EndWorld) * 0.5f;
            Vector3 flat = new Vector3(best.EndWorld.x - best.StartWorld.x, 0.0f, best.EndWorld.z - best.StartWorld.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, flat);
            float ground = (float)world.Plan.GroundHeight(middle.x, middle.z);

            var viewer = new GameObject("BridgeViewer");
            viewer.transform.position = new Vector3(middle.x, ground, middle.z);
            streamer.Viewer = viewer.transform;
            streamer.Rebuild();
            streamer.StreamAround(viewer.transform.position, int.MaxValue);
            yield return new WaitForSecondsRealtime(3.0f);

            // From the street, looking up at it along the street (the street runs across the bridge).
            Vector3 eye = new Vector3(middle.x, ground + 2.3f, middle.z) + side * 22.0f;
            BuildingShapeCapture.Shoot(camera, directory, "bridge-street", eye, middle + Vector3.up * 0.5f);
            // On the deck, at a person's eye, looking across to the far doorway.
            BuildingShapeCapture.Shoot(camera, directory, "bridge-deck", best.StartWorld + flat * 1.0f + Vector3.up * 2.3f, best.EndWorld + Vector3.up * 1.5f);
            // From above and to one side, the whole bridge and its buildings.
            BuildingShapeCapture.Shoot(camera, directory, "bridge-above", middle + side * 30.0f + Vector3.up * 20.0f, middle);
            Debug.Log($"CityX footbridge capture: {best.SiteA}->{best.SiteB} ({world.Building(best.SiteA).Zone}), floors {best.FloorA}/{best.FloorB}, span {best.Span:F1} m");
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
