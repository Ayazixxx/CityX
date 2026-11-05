using System.Collections;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the C-menu in the real city (docs/HUD.md). The camera's spin round to the front, frame by frame from where the
    /// third-person camera stood (cmenu-spin-*.png), then the menu with its icons (cmenu.png) and with the Player fly-out open
    /// (cmenu-player.png). Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PlayerMenuCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RenderTheMenuAndItsSpin()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);
            for (int i = 0; i < 90; i++)
            {
                yield return null;   // the city streams in round the player and the third-person camera settles behind them
            }

            var menu = Object.FindFirstObjectByType<PlayerMenuView>();
            Assert.IsNotNull(menu, "no C-menu in the scene");
            Camera camera = Camera.main;
            var canvas = menu.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;   // drawn by the camera, so a render of it shows the menu over the city
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
            CityX.View.CityHaze.Clear();   // the city's haze is a full-screen pass: it would lie over a menu drawn by the camera
            string folder = BuildingShapeCapture.ShotsFolder;

            menu.Open();
            float seconds = PlayerMenuCamera.EaseSeconds;
            int shot = 0;
            for (float t = 0.0f; t <= seconds + 0.2f; t += 1.0f / 60.0f)
            {
                if (Mathf.Abs(t / 0.15f - Mathf.Round(t / 0.15f)) < 0.06f)
                {
                    Shoot(camera, folder, $"cmenu-spin-{shot++}");
                }
                yield return null;
            }
            Shoot(camera, folder, "cmenu");

            menu.Submenu.Show();
            yield return null;
            yield return null;
            Shoot(camera, folder, "cmenu-player");
            menu.Close();
#else
            yield break;
#endif
        }

        /// <summary>The camera's picture as it stands (BuildingShapeCapture.Shoot, pointed where it already looks).</summary>
        private static void Shoot(Camera camera, string folder, string name)
        {
            Transform eye = camera.transform;
            BuildingShapeCapture.Shoot(camera, folder, name, eye.position, eye.position + eye.forward);
        }
    }
}
