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
    /// Not a test: how far the city is seen (2026-10-15, the render distance) -- from 400 m over the spawn, from a jetpack's height, and down the
    /// longest street at a person's eye, by day and at sunset, with the game camera's own far plane -- to Logs/CityShots/horizon-*.png. Explicit;
    /// needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class HorizonCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheHorizon()
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
            }

            // The game camera's own reach.
            float far = 2000.0f;
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c.CompareTag("MainCamera"))
                {
                    far = c.farClipPlane;
                }
            }
            var cameraObject = new GameObject("HorizonCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = far;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // Stream the city round the spawn, as the game does, and wait until it stops growing.
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vector3((float)sx, (float)world.Plan.Fields.Height(sx, sz), (float)sz);
            var viewer = new GameObject("HorizonViewer");
            viewer.transform.position = spawn;
            streamer.Viewer = viewer.transform;
            streamer.Rebuild();
            streamer.StreamAround(spawn, int.MaxValue);
            float settled = 0.0f, waited = 0.0f;
            int last = -1;
            while (waited < 90.0f && settled < 2.0f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                waited += 0.25f;
                int loaded = streamer.LoadedChunkCount;
                settled = loaded == last ? settled + 0.25f : 0.0f;
                last = loaded;
            }
            yield return new WaitForSecondsRealtime(1.0f);

            // Down the longest street: of sixteen headings at a person's eye, the one that sees furthest.
            Vector3 eye = spawn + Vector3.up * 2.3f;
            Vector3 street = Vector3.forward;
            float longest = 0.0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0.0f, i * 22.5f, 0.0f) * Vector3.forward;
                float reach = Physics.Raycast(eye, d, out RaycastHit hit, 3000.0f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 3000.0f;
                if (reach > longest)
                {
                    longest = reach;
                    street = d;
                }
            }

            (string name, float time)[] times = { ("day", SkyCycle.DaySeconds * 0.42f), ("sunset", SkyCycle.DaySeconds * 0.93f) };
            foreach ((string when, float time) in times)
            {
                if (cycle != null)
                {
                    cycle.TimeOfDay = time;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                    DynamicGI.UpdateEnvironment();
                }
                yield return null;
                // Toward the sun at sunset (his shot), across it by day.
                Vector3 toSun = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.forward;
                Vector3 level = new Vector3(toSun.x, 0.0f, toSun.z).sqrMagnitude > 1e-4f ? new Vector3(toSun.x, 0.0f, toSun.z).normalized : Vector3.forward;
                Vector3 look = when == "day" ? Quaternion.Euler(0.0f, 70.0f, 0.0f) * level : level;
                BuildingShapeCapture.Shoot(camera, directory, $"horizon-high-{when}", spawn + Vector3.up * 400.0f, spawn + Vector3.up * 400.0f + look * 1000.0f + Vector3.down * 260.0f);
                BuildingShapeCapture.Shoot(camera, directory, $"horizon-jetpack-{when}", spawn + Vector3.up * 120.0f, spawn + Vector3.up * 120.0f + look * 1000.0f + Vector3.down * 120.0f);
                BuildingShapeCapture.Shoot(camera, directory, $"horizon-street-{when}", eye, eye + street * 100.0f + Vector3.up * 4.0f);
            }
            Debug.Log($"CityX horizon capture: {streamer.LoadedChunkCount} chunks loaded, camera far plane {far:F0} m, fog {RenderSettings.fogStartDistance:F0}-{RenderSettings.fogEndDistance:F0} m, " +
                $"longest street view {longest:F0} m");
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
