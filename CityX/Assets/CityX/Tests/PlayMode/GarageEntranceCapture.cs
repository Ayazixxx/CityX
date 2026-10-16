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
    /// Not a test: the way into a car park (owner, 2026-10-16: "there was physically no way for the car to drive into it, the road leads into a
    /// wall"). The garage near the spawn dug deepest into its hill and the one with the biggest climb from a level road, each from the road in front
    /// at a driver's eye, from above and to one side, and from inside looking out, to Logs/CityShots/garage-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GarageEntranceCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheWaysIntoTheGarages()
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
            RenderSettings.fog = false;
            var cameraObject = new GameObject("GarageCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("GarageViewer");
            streamer.Viewer = viewer.transform;

            // The garage dug deepest into its hill, and the entrance with the biggest climb from its road.
            BuildingSite dug = BuildingSite.None, climb = BuildingSite.None;
            int climbEntrance = 0;
            float bestDug = float.MinValue, bestClimb = float.MinValue;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 30))
            {
                if (!s.IsParkingGarage) continue;
                GarageEntrance[] two = GarageEntrances.For(world.Plan, s);
                if (s.BaseHeight - two[0].DeckY > bestDug)
                {
                    bestDug = s.BaseHeight - two[0].DeckY;
                    dug = s;
                }
                for (int i = 0; i < 2; i++)
                {
                    // As the drive-in test picks: the biggest climb from a road that is not tilted more than 6 degrees along the way in.
                    if (!two[i].HasDriveway || Mathf.Abs(two[i].DeckY - two[i].RoadY) <= bestClimb) continue;
                    float m = (two[i].X0 + two[i].X1) * 0.5f, f = s.Params.Depth * 0.5f;
                    s.LocalToWorld(m, f + two[i].KerbOut + 2.0, out double nx, out double nz);
                    s.LocalToWorld(m, f + two[i].KerbOut + 7.0, out double fx, out double fz);
                    float tilt = Mathf.Atan2((float)System.Math.Abs(world.Plan.GroundHeight(nx, nz) - world.Plan.GroundHeight(fx, fz)), 5.0f) * Mathf.Rad2Deg;
                    if (tilt > 6.0f) continue;
                    bestClimb = Mathf.Abs(two[i].DeckY - two[i].RoadY);
                    climb = s;
                    climbEntrance = i;
                }
            }
            Assert.IsTrue(dug.Exists, "no garage near the spawn");

            foreach ((BuildingSite site, int which, string name) in new[] { (dug, 0, "garage-dug"), (dug, 1, "garage-dug-other"), (climb, climbEntrance, "garage-climb") })
            {
                if (!site.Exists) continue;
                GarageEntrance e = GarageEntrances.For(world.Plan, site)[which];
                float front = site.Params.Depth * 0.5f, mid = (e.X0 + e.X1) * 0.5f;
                Vector3 At(float localX, float outFromWall, float y)
                {
                    site.LocalToWorld(localX, front + outFromWall, out double x, out double z);
                    return new Vector3((float)x, y, (float)z);
                }
                Vector3 opening = At(mid, 0.0f, e.StreetY);
                viewer.transform.position = opening;
                streamer.Rebuild();
                streamer.StreamAround(opening, int.MaxValue);
                yield return new WaitForSecondsRealtime(2.0f);
                Vector3 road = At(mid, e.KerbOut + 6.0f, e.RoadY);
                BuildingShapeCapture.Shoot(camera, directory, name + "-street", road + Vector3.up * 2.0f, opening + Vector3.up * 1.2f);
                BuildingShapeCapture.Shoot(camera, directory, name + "-above", At(mid + 14.0f, e.KerbOut + 14.0f, e.DeckY + 16.0f), At(mid, -2.0f, e.DeckY));
                BuildingShapeCapture.Shoot(camera, directory, name + "-inside", At(mid, -12.0f, e.DeckY + 2.0f), road + Vector3.up * 1.0f);
                Debug.Log($"CityX garage capture: {name} = #{site.Id} {site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0} m, plan floor {site.BaseHeight:F2}, " +
                    $"lowest deck {e.DeckY:F2}; opening {which} at {mid:F1}: kerb {e.KerbOut:F1} m out, road {e.RoadY:F2}, at the wall {e.StreetY:F2}, ramp ends {e.RampEnd:F1} m in");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
