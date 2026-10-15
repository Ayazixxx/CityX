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
    /// Not a test: finds apartment blocks of each kind near the spawn (solid parapets, railings, a walkway, a tall one, white parapets) and renders
    /// each alone from its back (the balconies) and its front (the walkway), and two of them in the streamed city from the street, each BEFORE
    /// (CityMeshBuilder.ApartmentFrontsOn off) and AFTER, to Logs/CityShots/apt-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ApartmentFrontCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheApartmentBlocks()
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
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.40f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }

            var picks = new Dictionary<string, BuildingSite>();
            foreach (BuildingSite site in TestSites.NearSpawn(world, 60))
            {
                if (picks.Count >= 6)
                {
                    break;
                }
                ApartmentFront front = ApartmentFront.For(site, WindowPlan.For(world, site));
                if (!front.Any || (site.Contacts != null && site.Contacts.Length > 0) || HasVolumesAfter(world, site))
                {
                    continue;
                }
                int rows = WindowGrid.RowsFor(site.Params.Height).Count;
                if (front.Balconies && !front.Railing && rows >= 5 && rows <= 12 && !picks.ContainsKey("solid")) picks["solid"] = site;
                else if (front.Balconies && front.Railing && rows >= 4 && !picks.ContainsKey("railing")) picks["railing"] = site;
                else if (front.Walkway && rows >= 4 && !picks.ContainsKey("walkway")) picks["walkway"] = site;
                else if (front.Balconies && rows >= 14 && !picks.ContainsKey("tower")) picks["tower"] = site;
                else if (front.Balconies && front.Finish == ApartmentFront.ParapetFinish.White && !picks.ContainsKey("white")) picks["white"] = site;
                else if (front.Balconies && site.Zone == ZoneKind.Residential && !picks.ContainsKey("home")) picks["home"] = site;
            }

            var cameraObject = new GameObject("AptCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // In the city first: the street behind a block and the street in front of one, before and after.
            foreach (string kind in new[] { "solid", "walkway" })
            {
                if (!picks.TryGetValue(kind, out BuildingSite site))
                {
                    continue;
                }
                Vector3 centre = TestSites.At(site, site.CentreX, site.BaseHeight, site.CentreZ);
                Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                Vector3 face = kind == "walkway" ? front : -front;
                float half = site.Params.Depth * 0.5f;
                Vector3 eye = centre + face * (half + 11.0f) + side * (site.Params.Width * 0.45f) + Vector3.up * 1.8f;
                Vector3 look = centre + face * half + Vector3.up * (site.Params.Height * 0.45f) - side * (site.Params.Width * 0.1f);
                foreach (bool on in new[] { false, true })
                {
                    CityMeshBuilder.ApartmentFrontsOn = on;
                    streamer.Rebuild();
                    streamer.StreamAround(centre, int.MaxValue);
                    yield return null;
                    yield return null;
                    BuildingShapeCapture.Shoot(camera, directory, $"apt-{kind}-street-{(on ? "after" : "before")}", eye, look);
                }
            }

            // Then each alone, high above the city, from the back and the front, before and after.
            RenderSettings.fog = false;
            RenderSettings.fogStartDistance = 4000.0f;
            RenderSettings.fogEndDistance = 5000.0f;
            if (cycle != null)
            {
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                Vector3 front = CityMeshBuilder.WallNormal(pick.Value, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                foreach (bool on in new[] { false, true })
                {
                    CityMeshBuilder.ApartmentFrontsOn = on;
                    GameObject alone = BuildingShapeCapture.BuildAlone(world, streamer, pick.Value.Id, new Vector3(0.0f, 3000.0f, 0.0f), out float top, out float size);
                    Vector3 mid = new Vector3(0.0f, 3000.0f + top * 0.5f, 0.0f);
                    string state = on ? "after" : "before";
                    BuildingShapeCapture.Shoot(camera, directory, $"apt-{pick.Key}-back-{state}", mid + (-front + side * 0.55f).normalized * size * 1.7f + Vector3.up * size * 0.15f, mid);
                    BuildingShapeCapture.Shoot(camera, directory, $"apt-{pick.Key}-front-{state}", mid + (front + side * 0.6f).normalized * size * 1.7f + Vector3.up * size * 0.15f, mid);
                    // Close on the back: a few floors of balconies, as someone in the next block would see them.
                    Vector3 near = new Vector3(0.0f, 3000.0f + Mathf.Min(top * 0.5f, 12.0f), 0.0f) - front * (pick.Value.Params.Depth * 0.5f);
                    BuildingShapeCapture.Shoot(camera, directory, $"apt-{pick.Key}-close-{state}", near - front * 9.0f + side * 7.0f + Vector3.up * 2.0f, near + side * 1.0f);
                    Object.Destroy(alone);
                    yield return null;
                }
                Debug.Log($"CityX apartment capture: {pick.Key} = #{pick.Value.Id} {pick.Value.Zone} {pick.Value.Params.Mass} " +
                    $"{pick.Value.Params.Width:F0}x{pick.Value.Params.Depth:F0}x{pick.Value.Params.Height:F0}");
            }
            CityMeshBuilder.ApartmentFrontsOn = true;
            Object.Destroy(cameraObject);
            Debug.Log($"CityX apartment capture: found {string.Join(", ", picks.Keys)}");
#else
            yield break;
#endif
        }

        /// <summary>True when the next sites are volumes or wings of this one's building (a shaped building; the plain boxes read clearer here).</summary>
        private static bool HasVolumesAfter(CityWorld world, BuildingSite site)
        {
            if (site.OnRoof || site.Wing)
            {
                return true;
            }
            if (site.Id + 1 >= world.BuildingCount)
            {
                return false;
            }
            BuildingSite next = world.Building(site.Id + 1);
            return next.OnRoof || next.Wing;
        }
    }
}
