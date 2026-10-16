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
    /// Not a test: finds a building with each of the roofs added on 2026-10-15 (a saltbox house and shed, a flat roof with eaves on a house and
    /// on a mid-rise block, a skillion house and shed) and a tower with each kind of top (mast, spire, stepped crown, slanted spire, fins), renders
    /// each alone from the street and from above (the towers' tops close up too), to Logs/CityShots/roofkind-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoofKindsCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheNewRoofsAndTheTowerTops()
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
            foreach (BuildingSite site in TestSites.NearSpawn(world, 90))
            {
                RoofShape roof = RoofShape.For(site);
                TowerTop top = TowerTop.For(site);
                string kind = null;
                bool shed = site.Params.Mass == BuildingMass.Warehouse;
                if (top.Exists)
                {
                    kind = "top-" + top.Kind.ToString().ToLowerInvariant();
                }
                else if (roof.Kind == RoofKind.Saltbox)
                {
                    kind = shed ? "saltbox-shed" : "saltbox";
                }
                else if (roof.Kind == RoofKind.FlatEaves)
                {
                    kind = shed ? "flateaves-shed" : (site.Params.Mass == BuildingMass.MidRise ? "flateaves-midrise" : "flateaves");
                }
                else if (roof.Kind == RoofKind.Skillion)
                {
                    kind = shed ? "skillion-shed" : "skillion";
                }
                if (kind != null && !picks.ContainsKey(kind))
                {
                    picks[kind] = site;
                }
                if (picks.Count >= 12)
                {
                    break;
                }
            }

            var cameraObject = new GameObject("RoofKindCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.farClipPlane = 2500.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            RenderSettings.fog = false;
            RenderSettings.fogStartDistance = 4000.0f;
            RenderSettings.fogEndDistance = 5000.0f;
            if (cycle != null)
            {
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            foreach (KeyValuePair<string, BuildingSite> pick in picks)
            {
                BuildingSite site = pick.Value;
                Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                Vector3 side = Vector3.Cross(Vector3.up, front);
                GameObject alone = BuildingShapeCapture.BuildAlone(world, streamer, site.Id, new Vector3(0.0f, 3000.0f, 0.0f), out float top, out float size);
                Vector3 mid = new Vector3(0.0f, 3000.0f + top * 0.6f, 0.0f);
                BuildingShapeCapture.Shoot(camera, directory, $"roofkind-{pick.Key}-front", mid + (front + side * 0.6f).normalized * size * 1.8f + Vector3.up * size * 0.25f, mid);
                BuildingShapeCapture.Shoot(camera, directory, $"roofkind-{pick.Key}-above", mid + (-front + side * 0.8f).normalized * size * 1.3f + Vector3.up * size * 1.1f, mid);
                RoofShape roofShape = RoofShape.For(site);
                if (roofShape.Pitched)
                {
                    // The profile: from beyond one end of the ridge, a little above the eaves, looking along it, so a saltbox's tall front and long
                    // back slope (and a skillion's rise) show in silhouette.
                    Vector3 ridge = roofShape.AlongX ? side : front;
                    float along = roofShape.AlongX ? site.Params.Width : site.Params.Depth;
                    var eaves = new Vector3(0.0f, 3000.0f + site.Params.Height, 0.0f);
                    BuildingShapeCapture.Shoot(camera, directory, $"roofkind-{pick.Key}-profile", eaves + ridge * (along * 0.5f + 26.0f) + Vector3.up * 3.0f,
                        eaves + Vector3.up * 2.5f);
                }
                TowerTop tip = TowerTop.For(site);
                if (tip.Exists)
                {
                    // The top close up: from beside the roof, a little above it, looking at the middle of the top.
                    var roofTop = new Vector3(0.0f, 3000.0f + top, 0.0f);
                    float reach = Mathf.Max(60.0f, tip.Height * 1.6f);
                    BuildingShapeCapture.Shoot(camera, directory, $"roofkind-{pick.Key}-close", roofTop + (front + side * 0.7f).normalized * reach + Vector3.up * (tip.Height * 0.5f + 8.0f),
                        roofTop + Vector3.up * tip.Height * 0.45f);
                }
                Object.Destroy(alone);
                yield return null;
                Debug.Log($"CityX roof kinds capture: {pick.Key} = #{site.Id} {site.Zone} {site.Params.Mass} " +
                    $"{site.Params.Width:F0}x{site.Params.Depth:F0}x{site.Params.Height:F0} roof {RoofShape.For(site).Kind} top {tip.Kind} {tip.Height:F0} m");
            }
            Object.Destroy(cameraObject);
            Debug.Log($"CityX roof kinds capture: found {string.Join(", ", picks.Keys)}");
#else
            yield break;
#endif
        }
    }
}
