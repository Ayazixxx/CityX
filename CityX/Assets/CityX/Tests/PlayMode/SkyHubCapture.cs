using System.Collections;
using CityX.Character;
using CityX.View;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the sky hub in the real city (docs/SKY_HUB.md), to Logs/CityShots/skyhub-*.png: from the street at the spawn looking up, from far off
    /// across the city, the stair's foot on its landing from the street and from above, a look along the stair low down over the city and high up, its
    /// top where it comes up onto the island, and on the island. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SkyHubCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SkyHubCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheHub()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            CityScale saved = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city
            Time.captureFramerate = 60;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.IsNotNull(streamer, "no streamer in the real scene");
            for (int i = 0; i < 10; i++) yield return null;
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            SkyHubPlan hub = streamer.World.Plan.SkyHub;
            Assert.IsTrue(hub.Found, "this world has no hub");
            Assert.IsNotNull(streamer.SkyHub, "no hub in the scene -- rebuild the scene");
            Assert.IsNotNull(streamer.SkyHub.Built, "the hub was not built");
            Debug.Log($"CityX sky hub: middle ({hub.CentreX:F1}, {hub.CentreZ:F1}), foot {hub.FootHeight:F1}, start {hub.StartAngle:F3}, way {hub.Direction}");

            Camera scene = Camera.main;
            if (scene != null) scene.enabled = false;
            var camera = new GameObject("HubCam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 3000.0f;
            camera.allowHDR = true;
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var viewer = new GameObject("HubViewer");
            streamer.Viewer = viewer.transform;

            Vector3 centre = new Vector3((float)hub.CentreX, (float)hub.FootHeight, (float)hub.CentreZ);
            Vec2 footPlace = hub.PlaceAt(0.0);
            Vector3 foot = new Vector3((float)footPlace.X, (float)hub.FootHeight, (float)footPlace.Y);
            Vector3 tangent = new Vector3((float)hub.FootTangent.X, 0.0f, (float)hub.FootTangent.Y);
            Vector3 inward = new Vector3((float)hub.FootInward.X, 0.0f, (float)hub.FootInward.Y);
            Vector3 spawn = streamer.World.Plan.TryFindSpawn(out double sx, out double sz) ? new Vector3((float)sx, (float)hub.FootHeight + 1.6f, (float)sz) : foot;
            Vector3 islandTop = centre + Vector3.up * (float)SkyHubPlan.IslandHeight;

            Vector3 StairAt(double s, double across = 0.0)
            {
                Vec2 p = hub.PlaceAt(s, across);
                return new Vector3((float)p.X, (float)(hub.HeightAt(s)), (float)p.Y);
            }

            // The city round the foot first, so it is there behind everything from the ground.
            yield return Streamed(streamer, viewer, foot + Vector3.up * 2.0f, 2500);
            Shoot(camera, "skyhub-1-foot-from-the-street", foot - inward * 34.0f - tangent * 34.0f + Vector3.up * 2.2f, foot + tangent * 4.0f + Vector3.up * 6.0f);
            Shoot(camera, "skyhub-2-foot-from-above", foot - tangent * 34.0f + Vector3.up * 40.0f, foot + tangent * 8.0f);
            Shoot(camera, "skyhub-3-foot-looking-up-the-stair", foot - tangent * 22.0f + Vector3.up * 2.2f, StairAt(60.0));
            Shoot(camera, "skyhub-4-up-from-the-spawn", spawn, centre + Vector3.up * 1100.0f);
            Shoot(camera, "skyhub-5-under-the-island", foot - inward * 40.0f + Vector3.up * 2.0f, islandTop - Vector3.up * 200.0f);

            Vector3 outside = (foot - centre).normalized;
            Shoot(camera, "skyhub-6-whole-from-afar", centre + outside * 2100.0f + Vector3.up * 900.0f, centre + Vector3.up * 800.0f);
            Shoot(camera, "skyhub-7-whole-from-high-up", centre + outside * 1400.0f + new Vector3(outside.z, 0.0f, -outside.x) * 700.0f + Vector3.up * 2300.0f, centre + Vector3.up * 1000.0f);

            // On the stair, low over the city, then near the top.
            Vector3 low = StairAt(330.0);
            yield return Streamed(streamer, viewer, low, 600);
            Shoot(camera, "skyhub-8-on-the-stair-low", low + Vector3.up * 2.4f - TangentAt(hub, 330.0) * 14.0f, StairAt(370.0) + Vector3.up * 2.0f);
            Shoot(camera, "skyhub-9-on-the-stair-looking-down", low + Vector3.up * 3.5f, StairAt(150.0, 60.0));

            Vector3 high = StairAt(SkyHubPlan.StairLength - 40.0);
            yield return Streamed(streamer, viewer, high, 300);
            Shoot(camera, "skyhub-10-the-top-of-the-stair", high + Vector3.up * 2.4f, StairAt(SkyHubPlan.StairLength) + Vector3.up * 1.0f);
            Vec2 topPlace = hub.PlaceAt(SkyHubPlan.StairLength + 15.0, -SkyHubPlan.StairWidth * 0.5);
            Vector3 stepOff = new Vector3((float)topPlace.X, (float)hub.IslandTop + 2.2f, (float)topPlace.Y);
            Vector3 onward = TangentAt(hub, SkyHubPlan.StairLength);
            Shoot(camera, "skyhub-11-stepping-onto-the-island", StairAt(SkyHubPlan.StairLength - 14.0, 8.0) + Vector3.up * 2.4f, stepOff - onward * 4.0f - Vector3.up * 1.0f);
            Shoot(camera, "skyhub-12-across-the-island", islandTop + (stepOff - islandTop).normalized * 250.0f + Vector3.up * 2.2f, islandTop + Vector3.up * 2.0f);
            Shoot(camera, "skyhub-13-over-the-edge", islandTop - (stepOff - islandTop).normalized * 301.0f + Vector3.up * 3.0f, islandTop - (stepOff - islandTop).normalized * 700.0f - Vector3.up * 1200.0f);

            streamer.Viewer = null;
            CityScale.Active = saved;
            Object.Destroy(camera.gameObject);
            Object.Destroy(viewer);
            if (scene != null) scene.enabled = true;
#else
            yield break;
#endif
        }

        private static Vector3 TangentAt(in SkyHubPlan hub, double s)
        {
            Vec2 a = hub.PlaceAt(s), b = hub.PlaceAt(s + 1.0);
            return new Vector3((float)(b.X - a.X), 0.0f, (float)(b.Y - a.Y));
        }

        /// <summary>Moves the viewer and lets the city stream in round it, by the clock.</summary>
        private static IEnumerator Streamed(CityStreamer streamer, GameObject viewer, Vector3 at, int frames)
        {
            viewer.transform.position = at;
            float until = Time.realtimeSinceStartup + 40.0f;
            for (int i = 0; i < frames && Time.realtimeSinceStartup < until; i++)
            {
                yield return null;
            }
        }

        private static void Shoot(Camera camera, string name, Vector3 from, Vector3 at)
        {
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, from, at);
        }
    }
}
