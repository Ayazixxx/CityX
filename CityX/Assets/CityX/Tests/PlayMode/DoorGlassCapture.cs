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
    /// Not a test: a balcony's glass door breaking (World/DoorGlass), seen from the balcony at a third-person camera's height -- whole, shot three
    /// times (its holes and cracks), and a moment after the fifth bullet brings one sheet down (its pieces falling) -- then a footbridge's door the
    /// same way, to Logs/CityShots/doorglass-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class DoorGlassCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderADoorsGlassBreaking()
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
            var breaker = Object.FindFirstObjectByType<GlassBreaker>();
            Assert.IsNotNull(breaker, "the scene's streamer has no window breaker");
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

            var cameraObject = new GameObject("DoorGlassCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("DoorGlassViewer");
            streamer.Viewer = viewer.transform;

            // A sliding door onto a balcony, and a footbridge's pair.
            var picks = new List<(BuildingSite site, BalconyDoor door, string name)>();
            var doors = new List<BalconyDoor>();
            bool sliding = false, bridge = false;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                if (site.Sides >= 3 || site.Shell) continue;
                WindowPlan plan = WindowPlan.For(world, site);
                doors.Clear();
                if (!sliding && ApartmentFront.For(site, plan).Balconies) BalconyDoors.For(site, plan, doors);
                if (!bridge) Footbridges.DoorsOf(world, site, plan, doors);
                foreach (BalconyDoor door in doors)
                {
                    bool isBridge = door.Unit >= Footbridges.DoorUnitBase;
                    if (!sliding && !isBridge && door.Kind == BalconyDoorKind.Sliding && door.Row >= 2)
                    {
                        picks.Add((site, door, "balcony"));
                        sliding = true;
                    }
                    else if (!bridge && isBridge && DoorGlass.Glazed(door))
                    {
                        picks.Add((site, door, "bridge"));
                        bridge = true;
                    }
                }
                if (sliding && bridge) break;
            }
            Assert.IsNotEmpty(picks, "no glazed door near the spawn");

            var panes = new DoorPane[2];
            foreach ((BuildingSite site, BalconyDoor door, string name) in picks)
            {
                BalconyDoors.InWorld(site, door, out Vector3 corner, out Vector3 along, out Vector3 outward);
                Vector3 middle = corner + along * (door.Width * 0.5f) + Vector3.up * 1.3f;
                viewer.transform.position = middle;
                streamer.Rebuild();
                streamer.StreamAround(middle, int.MaxValue);
                yield return new WaitForSecondsRealtime(1.5f);
                Vector3 eye = middle + outward * 3.3f + Vector3.up * 1.9f + along * 0.5f;
                BuildingShapeCapture.Shoot(camera, directory, $"doorglass-{name}-whole", eye, middle);

                DoorGlass.Panes(site, door, false, panes);
                DoorPane pane = panes[0];
                Vector3 Point(float u, float v)
                {
                    float pu = Mathf.Lerp(pane.U0, pane.U1, u), py = Mathf.Lerp(pane.Y0, pane.Y1, v);
                    site.LocalToWorld(pane.RunsInZ ? pane.Plane : pu, pane.RunsInZ ? pu : pane.Plane, out double wx, out double wz);
                    return new Vector3((float)wx, site.BaseHeight + py, (float)wz);
                }
                breaker.TakeBlow(Point(0.3f, 0.6f), -outward, BlowStrength.Bullet);
                breaker.TakeBlow(Point(0.55f, 0.45f), -outward, BlowStrength.Bullet);
                breaker.TakeBlow(Point(0.7f, 0.7f), -outward, BlowStrength.Bullet);
                yield return new WaitForSecondsRealtime(1.0f);
                BuildingShapeCapture.Shoot(camera, directory, $"doorglass-{name}-shot", eye, middle);
                BuildingShapeCapture.Shoot(camera, directory, $"doorglass-{name}-shot-close", middle + outward * 1.4f + Vector3.up * 0.3f, middle);

                breaker.TakeBlow(Point(0.4f, 0.3f), -outward, BlowStrength.Bullet);
                breaker.TakeBlow(Point(0.5f, 0.5f), -outward, BlowStrength.Bullet);
                for (int i = 0; i < 9; i++) yield return null;   // the pieces a few frames into their fall
                BuildingShapeCapture.Shoot(camera, directory, $"doorglass-{name}-broken", eye, middle);
                yield return new WaitForSecondsRealtime(2.5f);
                BuildingShapeCapture.Shoot(camera, directory, $"doorglass-{name}-after", eye, middle);
                Debug.Log($"CityX door glass capture: {name} = building {site.Id} ({site.Zone}), {door.Kind} door on the {door.Wall} wall, row {door.Row}; " +
                    $"sheet 0 now {streamer.Damage.StateOf(pane.Key)}");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
