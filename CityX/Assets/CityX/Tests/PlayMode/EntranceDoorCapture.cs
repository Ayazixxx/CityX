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
    /// Not a test: a real building's pair of front doors (BuildingInterior.HasDoubleEntrance) from the street, shut and then open, and a single
    /// door beside it for comparison, to Logs/CityShots/entrance-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class EntranceDoorCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheEntrances()
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
                DynamicGI.UpdateEnvironment();
            }

            var cameraObject = new GameObject("EntranceCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            int pairs = 0, singles = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                bool pair = BuildingInterior.HasDoubleEntrance(site);
                // A door at street level (no flight of entrance steps in the way of the view), on an ordinary building.
                if (!site.Exists || site.Shell || site.OnRoof || site.Params.IsSpecial || site.Sides >= 3
                    || BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs)
                {
                    continue;
                }
                if ((pair && pairs >= 2) || (!pair && singles >= 1))
                {
                    continue;
                }
                Vector3 door = TestSites.At(site, site.CentreX, site.BaseHeight, site.MaxZ);
                Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                streamer.Rebuild();
                streamer.StreamAround(door, int.MaxValue);
                yield return null;
                yield return null;
                string name = pair ? $"entrance-pair{pairs}" : "entrance-single";
                Vector3 eye = door + front * 7.0f + Vector3.up * 1.9f;
                BuildingShapeCapture.Shoot(camera, directory, name + "-shut", eye, door + Vector3.up * 1.6f);
                foreach (BuildingDoor leaf in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                {
                    if (Vector3.Distance(leaf.transform.position, door) < 3.0f)
                    {
                        leaf.SetOpen(true);
                    }
                }
                for (int f = 0; f < 90; f++) yield return null;
                BuildingShapeCapture.Shoot(camera, directory, name + "-open", eye, door + Vector3.up * 1.6f);
                Debug.Log($"CityX entrance capture: {name} = #{site.Id} {site.Zone} {site.Params.Width:F0} m front");
                if (pair) pairs++; else singles++;
                if (pairs >= 2 && singles >= 1)
                {
                    break;
                }
            }
            Object.Destroy(cameraObject);
            Assert.Greater(pairs, 0, "no pair of front doors near the spawn to render");
#else
            yield break;
#endif
        }
    }
}
