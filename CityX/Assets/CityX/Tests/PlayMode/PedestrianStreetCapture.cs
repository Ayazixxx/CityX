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
    /// Not a test: the pedestrian street nearest the spawn (World/PedestrianStreets), from over its mouth by day and at dusk and from a person's
    /// eye at its end, to Logs/CityShots/street-pedestrian-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PedestrianStreetCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderThePedestrianStreet()
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
            PlanWorld plan = world.Plan;
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
            }
            var skyline = Object.FindFirstObjectByType<SkylineStreamer>();
            if (skyline != null)
            {
                skyline.enabled = false;
            }

            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            EdgeGeometry g = null;
            double best = double.MaxValue;
            foreach (RoadEdge e in plan.Data.Edges)
            {
                if (!PedestrianStreets.Is(plan, e)) continue;
                EdgeGeometry eg = plan.Geometry.Edges[e.Id];
                if (eg == null || eg.Length - eg.TrimA - eg.TrimB < 24.0) continue;
                double d = ((e.A.P + e.B.P) * 0.5 - spawn).Length;
                if (d < best) { best = d; g = eg; }
            }
            Assert.IsNotNull(g, "no pedestrian street in the city");

            var cameraObject = new GameObject("StreetCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("StreetViewer");
            streamer.Viewer = viewer.transform;
            streamer.RadiusChunks = 4;
            streamer.Rebuild();

            Vec2 mid = g.A + g.Dir * (g.Length * 0.5);
            float paving = (float)(plan.GroundHeight(mid.X, mid.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight);
            var centre = new Vector3((float)mid.X, paving, (float)mid.Y);
            viewer.transform.position = centre;
            streamer.StreamAround(centre, int.MaxValue);
            yield return null;
            yield return null;

            Vec2 mouth = g.A + g.Dir * (g.TrimA - 6.0);
            var high = new Vector3((float)mouth.X, paving + 22.0f, (float)mouth.Y);
            Vec2 eyeAt = g.A + g.Dir * (g.TrimA + 2.0) + g.Dir.Perp * (g.HalfWidth - 1.5);
            var eye = new Vector3((float)eyeAt.X, paving + 2.3f, (float)eyeAt.Y);
            Vec2 farAt = g.A + g.Dir * (g.Length - g.TrimB);
            var farEnd = new Vector3((float)farAt.X, paving + 1.0f, (float)farAt.Y);
            foreach ((string when, float time) in new[] { ("day", SkyCycle.DaySeconds * 0.42f), ("dusk", SkyCycle.DaySeconds * 0.95f) })
            {
                if (cycle != null)
                {
                    cycle.TimeOfDay = time;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                    DynamicGI.UpdateEnvironment();
                }
                yield return null;
                BuildingShapeCapture.Shoot(camera, directory, $"street-pedestrian-{when}", high, centre);
                BuildingShapeCapture.Shoot(camera, directory, $"street-pedestrian-eye-{when}", eye, farEnd);
            }
            Debug.Log($"CityX pedestrian street capture: edge {g.Edge.Id}, {g.Length:F0} m long, {g.HalfWidth * 2.0:F1} m wide, {best:F0} m from the spawn");
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
