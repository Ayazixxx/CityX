using System.Collections;
using System.Collections.Generic;
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
    /// Not a test: the openings down to the basements, as a player standing in a lobby sees them (owner, 2026-10-16, with a screenshot of "a room that
    /// goes downward into the street"). Finds the buildings with a basement near the spawn at the game's own scale, one of each stair kind, builds
    /// each alone with no ground plane over the basement, and renders its lobby's opening from a third-person camera's height and from straight
    /// above, to Logs/CityShots/basement-*.png, logging the opening and the flight. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BasementStairCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheOpeningsDownToTheBasements()
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
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }
            RenderSettings.fog = false;

            CityScale saved = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city, the one the owner walks
            var world = new CityWorld(streamer.WorldSeed);
            var picks = new List<BuildingSite>();
            var kinds = new HashSet<StairKind>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                if (!site.Params.HasBasement || site.OnRoof || site.Shell)
                {
                    continue;
                }
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                if (stairs.Basement == null || kinds.Contains(stairs.Basement.Kind))
                {
                    continue;
                }
                kinds.Add(stairs.Basement.Kind);
                picks.Add(site);
                if (picks.Count >= 8)
                {
                    break;
                }
            }

            var cameraObject = new GameObject("BasementCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            var lamp = new GameObject("BasementLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 30.0f;
            lamp.intensity = 6.0f;

            foreach (BuildingSite site in picks)
            {
                BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
                StairPlan down = stairs.Basement;
                var at = new Vector3(0.0f, 3000.0f, 0.0f);
                GameObject alone = BuildingShapeCapture.BuildAlone(world, streamer, site.Id, at, out _, out _);
                // No ground over the basement: the helper lays a plane at the floor.
                foreach (Transform child in alone.transform)
                {
                    if (child.name == "Plane") Object.Destroy(child.gameObject);
                }
                yield return null;
                var axisX = new Vector3((float)site.AxisX, 0.0f, (float)site.AxisZ);
                var axisZ = new Vector3(-(float)site.AxisZ, 0.0f, (float)site.AxisX);
                Vector3 Local(Vector2 p, float y) => at + axisX * p.x + axisZ * p.y + Vector3.up * y;
                Rect hole = down.Hole;
                Vector3 middle = Local(hole.center, 0.0f);
                // From the lobby floor, a third-person camera's height and a few metres back from the opening's long side.
                Vector3 back = (hole.width >= hole.height ? -axisZ : -axisX) * (Mathf.Min(hole.width, hole.height) * 0.5f + 3.5f);
                lamp.transform.position = middle + Vector3.down * 1.5f;
                string name = $"basement-{down.Kind.ToString().ToLowerInvariant()}";
                BuildingShapeCapture.Shoot(camera, directory, name + "-lobby", middle + back + Vector3.up * 4.2f, middle + Vector3.down * 1.0f);
                BuildingShapeCapture.Shoot(camera, directory, name + "-above", middle + Vector3.up * 9.0f + back * 0.15f, middle + Vector3.down * 2.0f);
                float treads = 0.0f;
                foreach (Rect t in down.Treads) treads += t.width * t.height;
                Debug.Log($"CityX basement capture: {name} = #{site.Id} {site.Zone} {site.Params.Mass} {site.Params.Width:F1}x{site.Params.Depth:F1}x{site.Params.Height:F1}, " +
                    $"storey {stairs.Storey:F2}, flight {down.Kind} {down.Treads.Length} treads ({treads:F1} m2), risers {down.Risers} rise {down.Rise:F2}, " +
                    $"hole {hole.width:F2} x {hole.height:F2} = {hole.width * hole.height:F1} m2 at ({hole.center.x:F1}, {hole.center.y:F1}), bounds {down.Bounds}, " +
                    $"lobby flight {(stairs.Floors[0] != null ? stairs.Floors[0].Kind + " hole " + stairs.Floors[0].Hole : "none")}");
                Object.Destroy(alone);
                yield return null;
            }
            CityScale.Active = saved;
            Object.Destroy(cameraObject);
            Object.Destroy(lamp.gameObject);
            Debug.Log($"CityX basement capture: {picks.Count} kinds of basement stair found");
#else
            yield break;
#endif
        }
    }
}
