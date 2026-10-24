using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the real scene's inventory (I), with a hat, jeans and a jetpack on and a few things in storage, so the equipped row -- helmet to
    /// secondary weapon, with the shoes, gloves and back slots the owner asked for (2026-10-15) -- can be looked at. Written to
    /// Logs/CityShots/inventory-slots.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class InventoryPanelCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        [UnityTest]
        public IEnumerator RenderTheInventory()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            const string scenePath = "Assets/CityX/Scenes/CityMassing.unity";
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            Scene scene = SceneManager.GetSceneByPath(scenePath);

            var view = Object.FindFirstObjectByType<InventoryPanelView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "no inventory in the scene");
            Inventory inventory = view.Inventory;
            foreach (string worn in new[] { ItemRegistry.KnightHelmet, ItemRegistry.HoodedCloak, ItemRegistry.Jetpack })
            {
                Assert.IsTrue(inventory.EquipItemId(worn, view.Player), $"could not put on the {worn}");
            }
            inventory.AddItem(ItemRegistry.Parachute, 1);
            inventory.AddItem(ItemRegistry.Pistol, 1);
            inventory.AddItem(ItemRegistry.GlassBottle, 3);
            view.Open();
            yield return null;

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var shotCamera = new GameObject("ShotCamera").AddComponent<Camera>();
            shotCamera.clearFlags = CameraClearFlags.SolidColor;
            shotCamera.backgroundColor = new Color(0.2f, 0.22f, 0.25f);
            shotCamera.targetTexture = target;
            shotCamera.transform.position = new Vector3(0.0f, 2000.0f, 0.0f);   // clear of the city: only the panel in view
            Canvas canvas = view.GetComponent<Canvas>();
            foreach (Transform part in view.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = 5;   // UI
            }
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = shotCamera;
            canvas.planeDistance = 1.0f;
            shotCamera.cullingMask = 1 << canvas.gameObject.layer;
            yield return null;
            yield return null;
            CityX.View.CityHaze.Clear();   // the city's haze is a full-screen pass: off, or it lies over a panel shot through a camera
            Shoot(shotCamera, target, directory, "inventory-slots");

            view.Close();
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
