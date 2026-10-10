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
    /// Not a test: renders a powered building's rooms with every light on (inside, from the hall and the corner room) and from the street at
    /// night, and an unpowered one's dead fittings, to Logs/CityShots/roomlights-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoomLightsCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RenderTheRoomLights()
        {
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
            BuildingShapeCapture.UseTheScenesAir();

            // A powered building of each lighting pattern we can find near the spawn, and one without power.
            var picks = new Dictionary<string, BuildingSite>();
            foreach (BuildingSite s in TestSites.NearSpawn(world, 20))
            {
                if (s.Shell || s.Sides >= 3 || s.OnRoof || s.Params.Width < 12.0f) continue;
                List<RoomLightPlan> plans = RoomLighting.PlansFor(s);
                if (plans.Count == 0) continue;
                string kind = PowerGrid.IsOn(s) ? "on-" + plans[0].Pattern.ToString().ToLowerInvariant() : "dead";
                if (!picks.ContainsKey(kind)) picks[kind] = s;
                if (picks.Count >= 6) break;
            }
            Debug.Log($"CityX room lights capture: {string.Join(", ", picks.Keys)}");

            var cameraObject = new GameObject("RoomLightsCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70.0f;
            camera.nearClipPlane = 0.05f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                BuildingSite site = pick.Value;
                Vector3 centre = TestSites.At(site, site.CentreX, site.BaseHeight, site.CentreZ);
                streamer.StreamAround(centre, int.MaxValue);
                streamer.Rebuild();
                streamer.StreamAround(centre, int.MaxValue);
                List<RoomLightPlan> plans = RoomLighting.PlansFor(site);
                // Not every room: a lit building at night is some rooms on and some off. The ground and first floors, and every third floor above.
                foreach (RoomLightPlan plan in plans)
                {
                    if (plan.Key.Floor < 2 || plan.Key.Floor % 3 == 0) RoomLights.Set(plan.Key, true);
                }
                yield return null;
                yield return null;

                foreach (bool night in new[] { true, false })
                {
                    if (cycle != null)
                    {
                        cycle.Running = false;
                        cycle.TimeOfDay = night ? SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.4f : SkyCycle.DaySeconds * 0.45f;
                        cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                        DynamicGI.UpdateEnvironment();
                    }
                    yield return null;
                    string when = night ? "night" : "day";

                    // Inside the hall, from a back corner, looking at the front door and its switch.
                    RoomLightPlan hall = plans[0];
                    float eyeY = site.BaseHeight + 2.3f;
                    Vector3 from = World(site, -site.Params.Width * 0.3f, eyeY, -site.Params.Depth * 0.15f);
                    Vector3 to = World(site, hall.Switch.x, hall.Switch.y + 1.5f, hall.Switch.z);
                    Shot(camera, directory, $"roomlights-{pick.Key}-hall-{when}", from, to);

                    // Close on the switch.
                    Vector3 sw = World(site, hall.Switch.x, hall.Switch.y, hall.Switch.z);
                    Vector3 facing = Quaternion.Euler(0.0f, CityMeshBuilder.YawDegreesOf(site), 0.0f) * hall.SwitchFacing;
                    Shot(camera, directory, $"roomlights-{pick.Key}-switch-{when}", sw + facing * 1.6f + Vector3.up * 0.4f + Vector3.Cross(Vector3.up, facing) * 0.6f, sw);

                    // The corner room, if there is one.
                    if (plans.Count > 1 && plans[1].Key.Room == 1)
                    {
                        RoomLightPlan room = plans[1];
                        Vector3 rs = World(site, room.Switch.x, room.Switch.y, room.Switch.z);
                        Vector3 rf = Quaternion.Euler(0.0f, CityMeshBuilder.YawDegreesOf(site), 0.0f) * room.SwitchFacing;
                        Shot(camera, directory, $"roomlights-{pick.Key}-room-{when}", rs + rf * 0.6f + Vector3.up * 0.8f, rs + rf * 4.0f + Vector3.up * 2.5f);
                    }

                    // From the street in front of it.
                    Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                    Vector3 street = centre + front * (site.Params.Depth * 0.5f + 16.0f) + Vector3.up * 2.0f;
                    Shot(camera, directory, $"roomlights-{pick.Key}-street-{when}", street, centre + Vector3.up * 8.0f);

                    // Further back and higher, the whole building with its lit and dark floors.
                    Vector3 across = centre + front * (site.Params.Depth * 0.5f + 34.0f) + Vector3.up * 9.0f;
                    Shot(camera, directory, $"roomlights-{pick.Key}-facade-{when}", across, centre + Vector3.up * Mathf.Min(site.Params.Height * 0.45f, 30.0f));

                    // The front door from the pavement: a powered building's lamp over it at night.
                    Vector3 door = World(site, 0.0f, site.BaseHeight + 1.6f, site.FrontHalfDepth);
                    Vector3 side = Vector3.Cross(Vector3.up, front);
                    Shot(camera, directory, $"roomlights-{pick.Key}-door-{when}", door + front * 8.0f + side * 3.0f + Vector3.up * 0.8f, door);
                }
                RoomLights.Clear();
            }
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        /// <summary>One shot, with the room lights the camera would have chosen from where it stands (RoomLightDirector goes by the main camera).</summary>
        private static void Shot(Camera camera, string directory, string name, Vector3 from, Vector3 to)
        {
            RoomLightDirector.Direct(from);
            BuildingShapeCapture.Shoot(camera, directory, name, from, to);
        }

        private static Vector3 World(BuildingSite site, float x, float y, float z)
        {
            site.LocalToWorld(x, z, out double wx, out double wz);
            return new Vector3((float)wx, y, (float)wz);
        }
    }
}
