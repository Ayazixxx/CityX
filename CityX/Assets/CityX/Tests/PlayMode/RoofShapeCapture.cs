using System.Collections;
using System.Collections.Generic;
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
    /// Not a test: finds a building with each kind of pitched roof near the spawn (gable, hip, skillion, a warehouse's) and renders each alone from
    /// the street and from above, then a residential street from the air, BEFORE (RoofShape.PitchedRoofsOn off) and AFTER, to
    /// Logs/CityShots/roof-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoofShapeCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderThePitchedRoofs()
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
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.40f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }

            var picks = new Dictionary<string, BuildingSite>();
            BuildingSite district = default;
            bool haveDistrict = false;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 60))
            {
                RoofShape roof = RoofShape.For(site);
                if (!roof.Pitched)
                {
                    continue;
                }
                string kind = site.Params.Mass == BuildingMass.Warehouse ? "shed" : roof.Kind.ToString().ToLowerInvariant();
                if (roof.Covering == RoofCovering.Tile && kind != "shed")
                {
                    kind += "-tile";
                }
                if (!picks.ContainsKey(kind))
                {
                    picks[kind] = site;
                }
                if (!haveDistrict && site.Zone == ZoneKind.Residential)
                {
                    district = site;
                    haveDistrict = true;
                }
                if (picks.Count >= 7 && haveDistrict)
                {
                    break;
                }
            }

            var cameraObject = new GameObject("RoofCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // A residential district from the air, before and after: the roofscape.
            if (haveDistrict)
            {
                Vector3 centre = TestSites.At(district, district.CentreX, district.BaseHeight, district.CentreZ);
                foreach (bool on in new[] { false, true })
                {
                    RoofShape.PitchedRoofsOn = on;
                    streamer.Rebuild();
                    streamer.StreamAround(centre, int.MaxValue);
                    yield return null;
                    yield return null;
                    string state = on ? "after" : "before";
                    BuildingShapeCapture.Shoot(camera, directory, $"roof-district-{state}", centre + new Vector3(-70.0f, 55.0f, -70.0f), centre);
                    BuildingShapeCapture.Shoot(camera, directory, $"roof-district-low-{state}", centre + new Vector3(45.0f, 14.0f, -40.0f),
                        centre + Vector3.up * 8.0f);
                }
            }

            RenderSettings.fog = false;
            RenderSettings.fogStartDistance = 4000.0f;
            RenderSettings.fogEndDistance = 5000.0f;
            if (cycle != null)
            {
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                Vector3 front = CityMeshBuilder.WallNormal(pick.Value, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                foreach (bool on in new[] { false, true })
                {
                    RoofShape.PitchedRoofsOn = on;
                    GameObject alone = BuildingShapeCapture.BuildAlone(world, streamer, pick.Value.Id, new Vector3(0.0f, 3000.0f, 0.0f), out float top, out float size);
                    Vector3 mid = new Vector3(0.0f, 3000.0f + top * 0.6f, 0.0f);
                    string state = on ? "after" : "before";
                    BuildingShapeCapture.Shoot(camera, directory, $"roof-{pick.Key}-front-{state}", mid + (front + side * 0.6f).normalized * size * 1.8f + Vector3.up * size * 0.25f, mid);
                    BuildingShapeCapture.Shoot(camera, directory, $"roof-{pick.Key}-above-{state}", mid + (-front + side * 0.8f).normalized * size * 1.3f + Vector3.up * size * 1.1f, mid);
                    Object.Destroy(alone);
                    yield return null;
                }
                Debug.Log($"CityX roof capture: {pick.Key} = #{pick.Value.Id} {pick.Value.Zone} {pick.Value.Params.Mass} " +
                    $"{pick.Value.Params.Width:F0}x{pick.Value.Params.Depth:F0}x{pick.Value.Params.Height:F0} {RoofShape.For(pick.Value).Pitch:F0} deg");
            }
            RoofShape.PitchedRoofsOn = true;
            Object.Destroy(cameraObject);
            Debug.Log($"CityX roof capture: found {string.Join(", ", picks.Keys)}");
#else
            yield break;
#endif
        }
    }
}
