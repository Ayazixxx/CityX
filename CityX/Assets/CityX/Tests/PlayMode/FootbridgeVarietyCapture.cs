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
    /// Not a test: the footbridges' variety (2026-10-16, the owner's reference images: angled and turning bridges, stairs between towers, catwalks
    /// with post-and-rail railings). The bridge near the spawn of each shape, of each rail, the highest with flights and one with a stair down to
    /// the street, each from the street looking up and from beside it at its height, to Logs/CityShots/bridges-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FootbridgeVarietyCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RenderTheBridgesKinds()
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
            }
            RenderSettings.fog = false;
            var cameraObject = new GameObject("BridgeCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("BridgeViewer");
            streamer.Viewer = viewer.transform;

            // The bridges near the spawn, nearest first; a pick of each kind.
            var all = new List<Footbridge>();
            var seen = new HashSet<(int, int, int)>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 40))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (seen.Add((b.SiteA, b.SiteB, b.Index))) all.Add(b);
                }
            }
            var picks = new List<(string name, Footbridge bridge)>();
            foreach (BridgeShape shape in new[] { BridgeShape.Straight, BridgeShape.Angled, BridgeShape.DogLeg, BridgeShape.Curve })
            {
                foreach (Footbridge b in all) if (b.Shape == shape) { picks.Add(("bridges-shape-" + shape.ToString().ToLowerInvariant(), b)); break; }
            }
            foreach (BridgeRail rail in new[] { BridgeRail.Parapet, BridgeRail.Rails, BridgeRail.Pickets, BridgeRail.Mesh, BridgeRail.Pipe })
            {
                foreach (Footbridge b in all) if (b.Rail == rail && b.Shape == BridgeShape.Straight) { picks.Add(("bridges-rail-" + rail.ToString().ToLowerInvariant(), b)); break; }
            }
            foreach (Footbridge b in all) if (b.Path.HasStairs) { picks.Add(("bridges-stairs", b)); break; }
            foreach (Footbridge b in all) if (b.Stair != null && !b.Stair.Switchback) { picks.Add(("bridges-street-stair", b)); break; }
            foreach (Footbridge b in all) if (b.Stair != null && b.Stair.Switchback) { picks.Add(("bridges-stair-tower", b)); break; }
            // The corridors (2026-10-23): an enclosed bridge and a breezeway, and one of each turning or with a flight.
            foreach (BridgeKind kind in new[] { BridgeKind.Enclosed, BridgeKind.Breezeway })
            {
                string kindName = "bridges-kind-" + kind.ToString().ToLowerInvariant();
                foreach (Footbridge b in all) if (b.Kind == kind && b.Shape == BridgeShape.Straight && !b.Path.HasStairs) { picks.Add((kindName, b)); break; }
                foreach (Footbridge b in all) if (b.Kind == kind && b.Shape != BridgeShape.Straight) { picks.Add((kindName + "-turning", b)); break; }
                foreach (Footbridge b in all) if (b.Kind == kind && b.Path.HasStairs) { picks.Add((kindName + "-flight", b)); break; }
            }

            foreach ((string name, Footbridge b) in picks)
            {
                BridgePath path = b.Path;
                float length = path.Length;
                Vector2 midPlan = path.PlanAt(length * 0.5f, out Vector2 dir);
                var middle = new Vector3(midPlan.x, path.HeightAt(length * 0.5f), midPlan.y);
                var flat = new Vector3(dir.x, 0.0f, dir.y);
                Vector3 side = Vector3.Cross(Vector3.up, flat).normalized;
                float ground = (float)world.Plan.GroundHeight(middle.x, middle.z);
                viewer.transform.position = middle;
                streamer.Rebuild();
                streamer.StreamAround(middle, int.MaxValue);
                yield return new WaitForSecondsRealtime(2.0f);
                // From the street a little to one side, looking up at it; and from beside it at its own height, a little above.
                Vector3 street = new Vector3(middle.x, ground + 2.0f, middle.z) + side * 9.0f - flat * 3.0f;
                BuildingShapeCapture.Shoot(camera, directory, name + "-up", street, middle);
                BuildingShapeCapture.Shoot(camera, directory, name + "-side", middle + side * (length * 0.55f + 6.0f) + Vector3.up * 4.0f, middle);
                if (b.Stair != null)
                {
                    Vector3 foot = b.Stair.Points[b.Stair.Points.Length - 1];
                    Vector3 away = new Vector3(b.Stair.Out.x, 0.0f, b.Stair.Out.y);
                    Vector3 along = new Vector3(b.Stair.Along.x, 0.0f, b.Stair.Along.y);
                    BuildingShapeCapture.Shoot(camera, directory, name + "-foot", foot + away * 7.0f + along * 4.0f + Vector3.up * 2.2f, foot + Vector3.up * 3.0f - along * 4.0f);
                }
                if (b.Kind != BridgeKind.Open)
                {
                    // Inside, a step in from A's mouth at a person's eye, looking down the corridor; and its far mouth from inside B's room.
                    Vector2 inPlan = path.PlanAt(1.5f, out Vector2 inDir);
                    var inside = new Vector3(inPlan.x, path.HeightAt(1.5f) + 2.3f, inPlan.y);
                    BuildingShapeCapture.Shoot(camera, directory, name + "-inside", inside, inside + new Vector3(inDir.x, -0.12f, inDir.y) * 10.0f);
                    Vector2 endPlan = path.PlanAt(length - 0.1f, out Vector2 endDir);
                    var room = new Vector3(endPlan.x + endDir.x * 3.5f, path.HeightAt(length) + 2.3f, endPlan.y + endDir.y * 3.5f);
                    BuildingShapeCapture.Shoot(camera, directory, name + "-mouth", room, room - new Vector3(endDir.x, 0.25f, endDir.y) * 6.0f);
                }
                Debug.Log($"CityX bridge capture: {name} = {b.SiteA}->{b.SiteB} #{b.Index} {b.Kind} {b.Shape} {b.Rail} {b.Deck}, floors {b.FloorA}/{b.FloorB}, " +
                    $"length {length:F1} m, {path.Points.Length} points, flights {(path.HasStairs ? "yes" : "no")}, piers {path.Piers.Length}, " +
                    $"street stair {(b.Stair != null ? $"{b.Stair.Top - b.Stair.Bottom:F1} m" : "no")}, clearance {middle.y - ground:F1} m");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
