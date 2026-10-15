using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the real scene's Stats page (owner, 2026-10-14: every stat on it), with points to spend and a few spent, at the top of its
    /// list and scrolled to the bottom. Written to Logs/CityShots/stats-page-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StatsPageCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator RenderTheStatsPage()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            const string scenePath = "Assets/CityX/Scenes/CityMassing.unity";
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            Scene scene = SceneManager.GetSceneByPath(scenePath);

            var page = Object.FindFirstObjectByType<StatsPanelView>(FindObjectsInactive.Include);
            Assert.IsNotNull(page, "no Stats page in the scene");
            PlayerVitals vitals = page.Vitals;
            vitals.AddXp(PlayerVitals.XpPerLevelBase * 3);   // to level 3: 10 points
            vitals.SpendStatPoints(StatId.MaxHealth, 3);
            vitals.SpendStatPoints(StatId.Armor, 2);
            vitals.SpendStatPoints(StatId.BlockChance, 1);
            page.Open();
            Assert.IsTrue(page.IsOpen, "the page did not stay open on its first opening");

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var shotCamera = new GameObject("ShotCamera").AddComponent<Camera>();
            shotCamera.clearFlags = CameraClearFlags.SolidColor;
            shotCamera.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            shotCamera.targetTexture = target;
            shotCamera.transform.position = new Vector3(0.0f, 2000.0f, 0.0f);   // clear of the city: only the page in view
            Canvas canvas = page.Root.GetComponent<Canvas>();
            foreach (Transform part in page.Root.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = 5;   // UI
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = shotCamera;
            canvas.planeDistance = 1.0f;
            shotCamera.cullingMask = 1 << canvas.gameObject.layer;
            yield return null;
            yield return null;
            CityX.View.CityHaze.Clear();   // the city's haze is a full-screen pass: off, or it lies over a page shot through a camera
            Shoot(shotCamera, target, directory, "stats-page-top");

            var scroll = page.Root.GetComponentInChildren<ScrollRect>();
            scroll.verticalNormalizedPosition = 0.0f;
            yield return null;
            CityX.View.CityHaze.Clear();
            Shoot(shotCamera, target, directory, "stats-page-bottom");

            page.Close();
            Object.Destroy(shotCamera.gameObject);
            target.Release();
            Object.Destroy(target);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, RenderTexture target, string directory, string name)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
    }
}
