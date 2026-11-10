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
    /// Not a test: the stairs up the outside to the roofs (World/RoofStairs, docs/LAYERED_CITY_PLAN.md L3) in the city as it is played, to
    /// Logs/CityShots/roofstairs-*.png: the first few near the spawn, each from the street, from along its wall, from the roof looking out
    /// through the opening in the parapet and from over its top looking down it; and the first one's street without the stairs and with
    /// them. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoofStairCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Picks = 4;

        private CityScale _scaleBefore;
        private Scene _scene;
        private CityStreamer _streamer;
        private GameObject _viewer;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RoofStairs.On = true;
            CityScale.Active = _scaleBefore;
            Footbridges.Reset();   // worked out for the played city's buildings
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator StandAt(Vector3 where)
        {
            _viewer.transform.position = where;
            _streamer.Rebuild();
            _streamer.StreamAround(where, int.MaxValue);
            yield return new WaitForSecondsRealtime(2.0f);
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator RenderTheRoofStairs()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            // The game's own city, not the classic one the tests run on: the pictures are of what is played.
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;
            Footbridges.Reset();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _streamer = Object.FindFirstObjectByType<CityStreamer>();
            var world = new CityWorld(_streamer.WorldSeed);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.45f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var cameraObject = new GameObject("RoofStairCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62.0f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            _viewer = new GameObject("RoofStairViewer");
            _streamer.Viewer = _viewer.transform;

            var picks = new List<(BuildingSite Site, RoofStair Stair)>();
            int inAll = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                RoofStair stair = RoofStairs.For(world, site);
                if (stair == null) continue;
                inAll++;
                // One of each kind of rail first, then whatever comes.
                if (picks.Count < Picks && !picks.Exists(p => p.Stair.Rail == stair.Rail)) picks.Add((site, stair));
            }
            Assert.Greater(picks.Count, 0, "no block near the spawn has a stair up to its roof");
            Debug.Log($"CityX roof stair capture: {inAll} stairs within 30 chunks of the spawn");

            for (int i = 0; i < picks.Count; i++)
            {
                (BuildingSite site, RoofStair roof) = picks[i];
                StreetStair s = roof.Stair;
                var outward = new Vector3(s.Out.x, 0.0f, s.Out.y);
                var along = new Vector3(s.Along.x, 0.0f, s.Along.y);
                Vector3 top = s.Points[1], foot = s.Points[s.Points.Length - 1];
                Vector3 flightMiddle = (s.Flights[0].From + s.Flights[0].To) * 0.5f;
                var middle = new Vector3(flightMiddle.x, (s.Top + s.Bottom) * 0.5f, flightMiddle.z);
                float rise = s.Top - s.Bottom;
                string name = $"roofstairs-{i + 1}-{roof.Rail}-{roof.Deck}".ToLowerInvariant();
                // From the ground out from the wall, as far back as there is open ground to stand on (the next block may be near).
                Vector3 street = new Vector3(middle.x, s.Bottom + 2.3f, middle.z) + outward * 6.0f + along * 5.0f;
                for (float back = Mathf.Max(14.0f, rise * 0.8f); back > 6.0f; back -= 1.0f)
                {
                    Vector3 stood = new Vector3(middle.x, s.Bottom + 2.3f, middle.z) + outward * back + along * 7.0f;
                    if (!world.TryBuildingAt(stood, out _, 1.0))
                    {
                        street = stood;
                        break;
                    }
                }

                if (i == 0)
                {
                    // The same street with no stairs up the outside anywhere, then with them.
                    RoofStairs.On = false;
                    Footbridges.Reset();
                    yield return StandAt(middle);
                    BuildingShapeCapture.Shoot(camera, directory, "roofstairs-street-without", street, middle);
                    RoofStairs.On = true;
                    Footbridges.Reset();
                    yield return StandAt(middle);
                    BuildingShapeCapture.Shoot(camera, directory, "roofstairs-street-with", street, middle);
                }
                else
                {
                    yield return StandAt(middle);
                }
                BuildingShapeCapture.Shoot(camera, directory, name + "-street", street, middle);
                BuildingShapeCapture.Shoot(camera, directory, name + "-along", new Vector3(foot.x, s.Bottom + 2.3f, foot.z) + along * 11.0f + outward * 1.5f,
                    middle - along * 2.0f);
                BuildingShapeCapture.Shoot(camera, directory, name + "-from-roof", s.Points[0] - outward * 4.0f + Vector3.up * 2.4f - along * 1.5f,
                    top + Vector3.up * 0.6f + along * 2.0f);
                BuildingShapeCapture.Shoot(camera, directory, name + "-down", top + Vector3.up * 3.2f - along * 2.5f + outward * 1.0f,
                    new Vector3(foot.x, s.Bottom, foot.z) + outward * 1.0f);
                Debug.Log($"CityX roof stair capture: building {site.Id} ({site.Zone}), wall {s.Wall}, {rise:F1} m up in {s.Flights.Length} flights, {roof.Rail} rails, {roof.Deck} deck");
            }
            Object.Destroy(cameraObject);
            Object.Destroy(_viewer);
#else
            yield break;
#endif
        }
    }
}
