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
    /// Not a test: a sky bridge's window breaking (World/BridgeGlass), seen from beside it over the street and from inside the corridor -- whole,
    /// one bay shot three times (its holes and cracks), a moment after a blow takes the next bay out (its pieces falling), and after -- to
    /// Logs/CityShots/bridgeglass-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BridgeGlassCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderABridgesGlassBreaking()
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

            var cameraObject = new GameObject("BridgeGlassCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var viewer = new GameObject("BridgeGlassViewer");
            streamer.Viewer = viewer.transform;

            // Two neighbouring bays in the middle of a level stretch of an enclosed bridge near the spawn.
            var panes = new List<BridgePane>();
            BridgePane shot = default, broken = default;
            bool found = false;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                foreach (Footbridge b in Footbridges.For(world, site))
                {
                    if (found || b.SiteA != site.Id || b.Kind != BridgeKind.Enclosed) continue;
                    panes.Clear();
                    BridgeGlass.Panes(b, panes);
                    for (int i = 1; i + 1 < panes.Count && !found; i++)
                    {
                        if (!panes[i].Level || !panes[i + 1].Level || panes[i + 1].Key.Column != panes[i].Key.Column + 1) continue;
                        shot = panes[i];
                        broken = panes[i + 1];
                        found = true;
                    }
                }
                if (found) break;
            }
            Assert.IsTrue(found, "no enclosed bridge near the spawn");

            Vector3 Point(in BridgePane pane, float u, float v) => Vector3.Lerp(pane.Start, pane.End, u) + Vector3.up * (pane.Height * v);
            Vector3 middle = Vector3.Lerp(Point(shot, 0.5f, 0.5f), Point(broken, 0.5f, 0.5f), 0.5f);
            Vector3 along = BridgeOutline.FlatOf(shot.End - shot.Start).normalized;
            viewer.transform.position = middle;
            streamer.Rebuild();
            streamer.StreamAround(middle, int.MaxValue);
            yield return new WaitForSecondsRealtime(1.5f);
            Vector3 outside = middle + shot.Normal * 4.5f + Vector3.up * 0.8f - along * 1.2f;
            Vector3 inside = middle - shot.Normal * (Footbridges.CorridorWidth - 0.4f) + along * 2.5f + Vector3.down * 0.2f;
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-whole", outside, middle);
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-whole-wide", middle + shot.Normal * 14.0f + Vector3.down * 3.0f, middle);

            breaker.TakeBlow(Point(shot, 0.3f, 0.6f), -shot.Normal, BlowStrength.Bullet);
            breaker.TakeBlow(Point(shot, 0.55f, 0.45f), -shot.Normal, BlowStrength.Bullet);
            breaker.TakeBlow(Point(shot, 0.7f, 0.7f), -shot.Normal, BlowStrength.Bullet);
            yield return new WaitForSecondsRealtime(1.0f);
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-shot", outside, middle);

            breaker.TakeBlow(Point(broken, 0.5f, 0.5f), -broken.Normal, BlowStrength.Heavy);
            for (int i = 0; i < 9; i++) yield return null;   // the pieces a few frames into their fall
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-broken", outside, middle);
            yield return new WaitForSecondsRealtime(2.5f);
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-after", outside, middle);
            BuildingShapeCapture.Shoot(camera, directory, "bridgeglass-inside", inside, middle);
            Debug.Log($"CityX bridge glass capture: bays {shot.Key} ({streamer.Damage.StateOf(shot.Key)}) and {broken.Key} ({streamer.Damage.StateOf(broken.Key)})");
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }
    }
}
