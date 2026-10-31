using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The map in the real scene: a right-click sets a waypoint with a route along the streets, its beam stands in the world, the minimap
    /// shows it and hides while the full map is open, a right-click on the pin clears it, and reaching it clears it.
    /// </summary>
    public sealed class MapWaypointPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            Time.captureFramerate = 60;
            for (int i = 0; i < 60; i++) yield return null;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ARightClickSetsAWaypointWithARouteAndABeamAndThePinClearsIt()
        {
            yield return LoadScene();
            var view = Object.FindFirstObjectByType<MapPanelView>();
            Assert.IsNotNull(view, "no map in the scene");
            var waypoint = Object.FindFirstObjectByType<MapWaypoint>();
            var beacon = Object.FindFirstObjectByType<WaypointBeacon>();
            var mini = Object.FindFirstObjectByType<MiniMapView>(FindObjectsInactive.Include);
            Assert.IsNotNull(waypoint, "the map has no waypoint");
            Assert.IsNotNull(beacon, "the waypoint has no beacon");
            Assert.IsNotNull(mini, "there is no minimap");
            Assert.AreEqual(MapPanelView.Mode.Hidden, view.Current, "there is a map on screen before M is pressed");
            Assert.IsFalse(view.MiniMap.activeInHierarchy, "the minimap shows before M is pressed");
            Assert.IsFalse(view.IsOpen);

            view.Cycle();   // M: the full map
            yield return null;
            Assert.AreEqual(MapPanelView.Mode.Full, view.Current);
            Assert.IsTrue(view.IsOpen, "the first M did not open the full map");
            Assert.IsFalse(view.MiniMap.activeInHierarchy, "the minimap shows over the full map");

            Transform player = view.Canvas.Player;
            var target = new Vector2(player.position.x + 120.0f, player.position.z - 90.0f);
            view.Canvas.RightClickAt(view.Canvas.ScreenAt(target));
            yield return null;
            Assert.IsTrue(waypoint.Has, "a right-click on the map set no waypoint");
            Assert.AreEqual(target.x, waypoint.Point.x, 2.0f, "the waypoint is not where the map was clicked");
            Assert.AreEqual(target.y, waypoint.Point.y, 2.0f);
            Assert.Greater(waypoint.Route.Count, 2, "no route along the streets");
            Assert.GreaterOrEqual(waypoint.RouteMetres, Vector2.Distance(MapWaypoint.Flat(player.position), target) - 1.0f);
            yield return null;
            Assert.IsTrue(beacon.Showing, "no beam in the world over the waypoint");
            Assert.AreEqual(target.x, beacon.BeamBase.x, 0.5f);

            view.Cycle();   // M again: the minimap
            yield return null;
            Assert.AreEqual(MapPanelView.Mode.Mini, view.Current);
            Assert.IsFalse(view.IsOpen, "the second M did not close the full map");
            Assert.IsTrue(view.MiniMap.activeInHierarchy, "the second M did not turn it into the minimap");
            Assert.IsTrue(mini.WaypointLabel.enabled && mini.WaypointLabel.text.Contains("m"), "the minimap does not say how far the waypoint is");

            view.Cycle();   // M a third time: nothing
            yield return null;
            Assert.AreEqual(MapPanelView.Mode.Hidden, view.Current);
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(view.MiniMap.activeInHierarchy, "the third M did not take the map away");

            view.Open();
            yield return null;
            view.Canvas.RightClickAt(view.Canvas.ScreenAt(waypoint.Point));
            yield return null;
            Assert.IsFalse(waypoint.Has, "a right-click on the pin did not clear it");
            yield return null;
            Assert.IsFalse(beacon.Showing, "the beam stayed after the waypoint went");
            view.Close();
        }

        /// <summary>Owner, 2026-10-30: "is there a compass on the map? if not there probably should be". The full map shows a compass, N above it and S below.</summary>
        [UnityTest]
        public IEnumerator TheFullMapHasACompassWithNorthAtTheTop()
        {
            yield return LoadScene();
            var view = Object.FindFirstObjectByType<MapPanelView>();
            view.Open();
            yield return null;
            var rose = Object.FindFirstObjectByType<MapCompassView>();
            Assert.IsNotNull(rose, "the open full map shows no compass");
            Assert.IsTrue(rose.transform.IsChildOf(view.Root.transform), "the compass is not part of the full map");
            Transform n = rose.transform.Find("CompassN");
            Transform s = rose.transform.Find("CompassS");
            Assert.IsNotNull(n, "the compass has no N");
            Assert.IsNotNull(s, "the compass has no S");
            Assert.AreEqual("N", n.GetComponent<Text>().text);
            Assert.Greater(n.position.y, rose.transform.position.y, "the N is not above the compass");
            Assert.Less(s.position.y, rose.transform.position.y, "the S is not below the compass");
            Assert.IsFalse(rose.raycastTarget, "the compass would swallow drags and right-clicks meant for the map");
            view.Close();
            yield return null;
            Assert.IsFalse(rose.isActiveAndEnabled, "the compass stayed on screen with the map closed");
        }

        [UnityTest]
        public IEnumerator ReachingTheWaypointClearsIt()
        {
            yield return LoadScene();
            var waypoint = Object.FindFirstObjectByType<MapWaypoint>();
            Transform player = waypoint.Player;
            waypoint.Set(MapWaypoint.Flat(player.position) + new Vector2(MapWaypoint.ArriveMetres * 0.5f, 0.0f));
            yield return null;
            yield return null;
            Assert.IsFalse(waypoint.Has, "standing at the waypoint did not clear it");
        }

        /// <summary>Saves Logs/CityShots/map-full.png and map-mini.png with a waypoint set. Run with -Graphics and look.</summary>
        [UnityTest, Explicit]
        public IEnumerator MapCapture()
        {
            yield return LoadScene();
            var view = Object.FindFirstObjectByType<MapPanelView>();
            var waypoint = Object.FindFirstObjectByType<MapWaypoint>();
            Transform player = view.Canvas.Player;
            // Walk the city a little so there is explored map to see.
            var console = Object.FindFirstObjectByType<DevConsoleView>();
            for (int step = 0; step < 6; step++)
            {
                console.Streamer.StreamAround(player.position + new Vector3(step * 40.0f, 0.0f, -step * 30.0f), 6);
                yield return null;
            }
            waypoint.Set(MapWaypoint.Flat(player.position) + new Vector2(170.0f, -120.0f));
            for (int i = 0; i < 10; i++) yield return null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                yield break;
            }

            Canvas canvas = view.GetComponent<Canvas>();
            Camera camera = Camera.main;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.0f;

            Shoot(camera, "map-none");
            view.Cycle();   // the full map
            for (int i = 0; i < 3; i++) yield return null;
            Shoot(camera, "map-full");
            view.Cycle();   // the minimap
            for (int i = 0; i < 3; i++) yield return null;
            Shoot(camera, "map-mini");
            view.Cycle();
        }

        private static void Shoot(Camera camera, string name)
        {
            const int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24);
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
    }
}
