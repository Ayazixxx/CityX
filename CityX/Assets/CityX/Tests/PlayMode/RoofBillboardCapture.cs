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
    /// Not a test: billboards on the roofs (World/RoofBillboards) in the real city -- from down the street by day, at dusk and at night (a powered
    /// building's floodlit board), close from across the street by day and at night, and from behind on the roof -- to
    /// Logs/CityShots/billboard-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RoofBillboardCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheBillboards()
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
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            Vector3 toSun = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.up;

            var cameraObject = new GameObject("BillboardCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // A board on a powered building (lit at night), floodlit, on a low roof; best facing the sun. Each pass asks less.
            BuildingSite site = default;
            bool found = false;
            for (int pass = 0; pass < 3 && !found; pass++)
            {
                foreach (BuildingSite s in TestSites.NearSpawn(world, 60))
                {
                    RoofBillboard b = RoofBillboards.For(s);
                    if (!b.Exists || !b.Floodlit || s.Params.Height > 30.0f || s.Contacts != null && s.Contacts.Length > 0) continue;
                    bool sunny = Vector3.Dot(CityMeshBuilder.WallNormal(s, WindowGrid.Wall.North), new Vector3(toSun.x, 0.0f, toSun.z).normalized) > 0.3f;
                    if ((PowerGrid.IsOn(s) || pass >= 2) && (sunny || pass >= 1))
                    {
                        site = s;
                        found = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(found, "no billboard near the spawn to render");
            RoofBillboard board = RoofBillboards.For(site);
            Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            Vector3 side = Vector3.Cross(Vector3.up, front);
            site.LocalToWorld(board.Offset, site.Params.Depth * 0.5f, out double bx, out double bz);
            var foot = new Vector3((float)bx, site.BaseHeight, (float)bz);
            float boardBottom = site.Top + board.Lift;
            Vector3 face = foot - front * RoofBillboards.SetBack + Vector3.up * (boardBottom - site.BaseHeight);   // the face's bottom middle
            Vector3 boardMiddle = face + Vector3.up * board.Height * 0.5f;

            // The streamer keeps the city round its viewer: the viewer goes to the board (it may be well away from the spawn), and the shots wait
            // for the colliders the cameras are placed by.
            var viewer = new GameObject("BillboardViewer");
            viewer.transform.position = foot;
            streamer.Viewer = viewer.transform;
            streamer.Rebuild();
            streamer.StreamAround(foot, int.MaxValue);
            float waited = 0.0f;
            while (waited < 20.0f && !Physics.Raycast(boardMiddle + front * 3.0f, -front, 5.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);

            // From the street: of the spots out in the road, along it either way and down a street opposite, where a person's eye sees most of the
            // board's face, the one that sees it most nearly face on (a street opposite, when there is one, over the road below it).
            Vector3 street = Vector3.zero;
            float best = 0.0f;
            foreach (float outward in new[] { 5.0f, 8.0f, 12.0f, 20.0f, 28.0f, 36.0f, 48.0f, 60.0f })
            {
                foreach (float along in new[] { 0.0f, 6.0f, -6.0f, 12.0f, -12.0f, 20.0f, -20.0f, 30.0f, -30.0f, 40.0f, -40.0f })
                {
                    if (!Eye(foot + front * outward + side * along, site.BaseHeight, out Vector3 eye) || !Sees(eye, face, front, side, board)) continue;
                    Vector3 look = (boardMiddle - eye).normalized;
                    float faceOn = -Vector3.Dot(look, front);
                    if (faceOn > best)
                    {
                        best = faceOn;
                        street = eye;
                    }
                }
            }
            bool clear = best > 0.0f;
            if (!clear)
            {
                street = foot + front * 8.0f + side * 30.0f + Vector3.up * 2.3f;
                Debug.Log("CityX billboard capture: no clear view from the street; shooting blind");
            }

            // Close, out over the street at the board's own height, as far as is clear.
            Vector3 away = (front + side * 0.35f).normalized;
            Vector3 start = boardMiddle + front * 1.2f;
            float room = Physics.Raycast(start, away, out RaycastHit h, 30.0f, ~0, QueryTriggerInteraction.Ignore) ? Mathf.Max(4.0f, h.distance - 1.0f) : 18.0f;
            Vector3 close = start + away * room + Vector3.up * 1.5f;

            (string name, float time)[] times =
            {
                ("day", SkyCycle.DaySeconds * 0.42f), ("dusk", SkyCycle.DaySeconds * 0.97f), ("night", SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.4f),
            };
            foreach ((string when, float time) in times)
            {
                if (cycle != null)
                {
                    cycle.TimeOfDay = time;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                    DynamicGI.UpdateEnvironment();
                }
                yield return null;
                BuildingShapeCapture.Shoot(camera, directory, $"billboard-street-{when}", street, boardMiddle + Vector3.up * 1.0f);
                if (when != "dusk")
                {
                    BuildingShapeCapture.Shoot(camera, directory, $"billboard-close-{when}", close, boardMiddle - Vector3.up * 1.0f);
                }
                if (when == "day")
                {
                    // From behind, on the roof: the steel, the legs.
                    BuildingShapeCapture.Shoot(camera, directory, "billboard-behind", boardMiddle - front * 7.0f + side * 4.0f - Vector3.up * 2.0f,
                        boardMiddle - Vector3.up * 2.5f);
                }
            }
            Debug.Log($"CityX billboard capture: building {site.Id} {site.Zone} {site.Params.Height:F0} m, board {board.Width:F1} x {board.Height:F1}, " +
                $"powered {PowerGrid.IsOn(site)}, street eye {Vector3.Distance(street, boardMiddle):F0} m off ({(clear ? "clear" : "blind")})");
            Object.Destroy(cameraObject);
            Object.Destroy(viewer);
#else
            yield break;
#endif
        }

        /// <summary>A person's eye over the street at <paramref name="spot"/>: on open ground near the building's foot, under the open sky.</summary>
        private static bool Eye(Vector3 spot, float baseHeight, out Vector3 eye)
        {
            eye = default;
            if (!Physics.Raycast(new Vector3(spot.x, baseHeight + 10.0f, spot.z), Vector3.down, out RaycastHit ground, 30.0f, ~0, QueryTriggerInteraction.Ignore)
                || ground.normal.y < 0.7f || Mathf.Abs(ground.point.y - baseHeight) > 5.0f)
            {
                return false;
            }
            eye = ground.point + Vector3.up * 2.3f;
            return !Physics.CheckSphere(eye, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                && !Physics.Raycast(eye, Vector3.up, 200.0f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Whether an eye sees most of a board's face (four of five points on it: its middle and four in from its corners).</summary>
        private static bool Sees(Vector3 eye, Vector3 faceBottom, Vector3 front, Vector3 side, in RoofBillboard board)
        {
            int seen = 0;
            foreach (Vector2 at in new[] { new Vector2(0.0f, 0.5f), new Vector2(-0.35f, 0.25f), new Vector2(0.35f, 0.25f), new Vector2(-0.35f, 0.8f), new Vector2(0.35f, 0.8f) })
            {
                Vector3 point = faceBottom + side * (at.x * board.Width) + Vector3.up * (at.y * board.Height) + front * 0.05f;
                Vector3 to = point - eye;
                if (Vector3.Dot(to, front) > -0.5f) continue;   // from behind the board's face
                float distance = to.magnitude;
                if (!Physics.Raycast(eye, to / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore) || hit.distance > distance - 0.3f)
                {
                    seen++;
                }
            }
            return seen >= 4;
        }
    }
}
