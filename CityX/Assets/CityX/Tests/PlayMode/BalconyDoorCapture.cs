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
    /// Not a test: the doors onto the balconies (World/BalconyDoors) of real apartment blocks -- a sliding door, a pair and a single door, each
    /// from the air off its balcony and from inside the flat, shut and then open, and the back of a block with its doors -- to
    /// Logs/CityShots/balcony-door-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BalconyDoorCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheBalconyDoors()
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
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.45f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }
            BalconyDoorState.Shared.Clear();

            var cameraObject = new GameObject("BalconyDoorCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // One free-standing block per kind, with a door onto a floor that opens (on the second or third floor up).
            var picks = new Dictionary<BalconyDoorKind, (BuildingSite site, BalconyDoor door)>();
            var doors = new List<BalconyDoor>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 40))
            {
                if (site.Contacts != null && site.Contacts.Length > 0)
                {
                    continue;
                }
                BalconyDoors.For(site, WindowPlan.For(world, site), doors);
                foreach (BalconyDoor d in doors)
                {
                    if (d.Opens && (d.Row == 2 || d.Row == 4) && d.Kind == BalconyDoors.KindFor(site) && !picks.ContainsKey(d.Kind))
                    {
                        picks[d.Kind] = (site, d);
                    }
                }
                if (picks.Count == 3)
                {
                    break;
                }
            }

            bool backShot = false;
            foreach (KeyValuePair<BalconyDoorKind, (BuildingSite site, BalconyDoor door)> pick in picks)
            {
                BuildingSite site = pick.Value.site;
                BalconyDoor door = pick.Value.door;
                BalconyDoors.InWorld(site, door, out Vector3 corner, out Vector3 along, out Vector3 outward);
                Vector3 middle = corner + along * (door.Width * 0.5f);
                string name = $"balcony-door-{pick.Key.ToString().ToLowerInvariant()}";

                if (!backShot)
                {
                    // The back of the block on its own, high over the city: every flat's door on every balcony, a few of them open.
                    var all = new List<BalconyDoor>();
                    BalconyDoors.For(site, WindowPlan.For(world, site), all);
                    int opened = 0;
                    foreach (BalconyDoor d in all)
                    {
                        if (d.Opens && opened < 4 && d.Unit % 2 == 0)
                        {
                            BalconyDoorState.Shared.Set(site.Id, d.Unit, d.Row, true);
                            opened++;
                        }
                    }
                    var at = new Vector3(0.0f, 3000.0f, 0.0f);
                    GameObject alone = BuildingShapeCapture.BuildAlone(world, streamer, site.Id, at, out float top, out float size);
                    Vector3 mid = at + Vector3.up * (top * 0.45f);
                    BuildingShapeCapture.Shoot(camera, directory, "balcony-door-back", mid + (outward + along * 0.45f).normalized * size * 1.25f
                        + Vector3.up * size * 0.08f, mid);
                    Vector3 near = at + outward * (site.Params.Depth * 0.5f) + Vector3.up * (WindowGrid.RowsFor(site.Params.Height).Size * 2.0f + 1.4f);
                    BuildingShapeCapture.Shoot(camera, directory, "balcony-door-back-close", near + outward * 9.0f + along * 6.0f + Vector3.up * 3.0f,
                        near + along * 1.0f);
                    Object.Destroy(alone);
                    BalconyDoorState.Shared.Clear();
                    yield return null;
                    backShot = true;
                }

                float depth = ApartmentFront.For(site, WindowPlan.For(world, site)).Depth;
                foreach (bool open in new[] { false, true })
                {
                    BalconyDoorState.Shared.Set(site.Id, door.Unit, door.Row, open);
                    streamer.Rebuild();
                    streamer.StreamAround(middle, int.MaxValue);
                    yield return null;
                    yield return null;
                    string state = open ? "open" : "shut";
                    // Standing on the balcony by its parapet, a little to one side, as the player would; then from inside the flat, looking out.
                    camera.fieldOfView = 80.0f;
                    BuildingShapeCapture.Shoot(camera, directory, $"{name}-{state}-outside",
                        middle + outward * (depth - 0.35f) + along * 0.5f + Vector3.up * 2.0f, middle - along * 0.2f + Vector3.up * 1.2f);
                    camera.fieldOfView = 55.0f;
                    BuildingShapeCapture.Shoot(camera, directory, $"{name}-{state}-inside",
                        middle - outward * 4.2f - along * 1.0f + Vector3.up * 2.0f, middle + Vector3.up * 1.3f);
                }
                BalconyDoorState.Shared.Clear();
                Debug.Log($"CityX balcony door capture: {name} = building {site.Id} {site.Zone} flat {door.Unit} row {door.Row}, " +
                    $"{door.Width:F2} x {door.Height:F2} m");
            }
            Object.Destroy(cameraObject);
            Assert.That(picks.Count, Is.EqualTo(3), $"found only {string.Join(", ", picks.Keys)} to render");
#else
            yield break;
#endif
        }
    }
}
