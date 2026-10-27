using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Core;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the character select screen with six characters saved, to Logs/CityShots/select-*.png -- five on the row, the back
    /// two dark figures, the bin in the card's corner -- and again after the right arrow. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CharacterSelectCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CharacterSelectCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";
        private const int Width = 1280, Height = 720;
        private Scene _title;

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        private Camera _menuCamera;

        private static Texture2D Render(Camera camera)
        {
            RenderTexture own = camera.targetTexture;
            RenderTexture target = own != null ? own : new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                RenderTexture.active = previous;
                if (own == null)
                {
                    camera.targetTexture = null;
                    target.Release();
                    Object.Destroy(target);
                }
            }
        }

        /// <summary>The scene's picture with the menu, drawn on its own, laid over it by its alpha.</summary>
        private void Shoot(Camera camera, string name)
        {
            Texture2D image = Render(camera);
            Texture2D menu = Render(_menuCamera);
            Save(menu, name + "-menu");   // the menu alone as well: its text does not survive the laying over cleanly
            Color[] under = image.GetPixels();
            Color[] over = menu.GetPixels();
            for (int i = 0; i < under.Length; i++)
            {
                // Drawn over clear black, the menu's colours already carry their own alpha (premultiplied).
                Color o = over[i];
                under[i] = new Color(o.r + under[i].r * (1.0f - o.a), o.g + under[i].g * (1.0f - o.a), o.b + under[i].b * (1.0f - o.a), 1.0f);
            }
            image.SetPixels(under);
            image.Apply();
            Object.Destroy(menu);
            Save(image, name);
            Object.Destroy(image);
        }

        private static void Save(Texture2D image, string name)
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(folder);
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
        }

        [UnityTest]
        public IEnumerator RenderTheCharacterSelectScreen()
        {
#if UNITY_EDITOR
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("needs a graphics device: run with -Graphics");
            }
            string[] names = { "Happy", "Ayaz", "Wraith Food", "Glock Guy", "Khaki", "Sixth" };
            Color[] skins =
            {
                new Color(0.95f, 0.85f, 0.1f), new Color(0.35f, 0.25f, 0.2f), new Color(0.78f, 0.62f, 0.53f),
                new Color(0.55f, 0.4f, 0.32f), new Color(0.86f, 0.76f, 0.68f), new Color(0.44f, 0.52f, 0.58f),
            };
            for (int i = 0; i < names.Length; i++)
            {
                CharacterSave save = CharacterSave.Create(names[i], skins[i]);
                save.Wearing = new List<string> { ItemRegistry.TShirt, i % 2 == 0 ? ItemRegistry.Khakis : ItemRegistry.GreyJeans, ItemRegistry.WhiteSneaker };
                LocalServer.Database.For(PlayerKey.Local).Add(save);
            }

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _title = SceneManager.GetSceneByPath(ScenePath);
            GameObject[] roots = _title.GetRootGameObjects();
            TitleScreen title = roots.SelectMany(root => root.GetComponentsInChildren<TitleScreen>(true)).First();
            Camera city = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));

            title.Show(TitleScreen.Panel.CharacterSelect);
            yield return null;
            yield return null;
            CharacterSelect select = roots.SelectMany(root => root.GetComponentsInChildren<CharacterSelect>(true)).First();
            Camera stage = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First(c => c.name == "SelectCamera");

            // The menu's own canvas (an overlay, which a camera's picture leaves out) drawn by a camera of its own on top of the city's stack, so
            // the picture has the card, the bin and the buttons.
            Canvas canvas = select.GetComponentInParent<Canvas>().rootCanvas;
            int uiLayer = LayerMask.NameToLayer("UI");
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = uiLayer;   // the menu is on Default, with the street: its camera must see the menu alone
            }
            city.cullingMask &= ~(1 << uiLayer);
            var uiObject = new GameObject("MenuShotCamera");
            _menuCamera = uiObject.AddComponent<Camera>();
            _menuCamera.clearFlags = CameraClearFlags.SolidColor;
            _menuCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            _menuCamera.cullingMask = 1 << uiLayer;
            _menuCamera.enabled = false;
            // Its picture's size from the start: the canvas lays itself out for its camera's target, and a batch run's "screen" is another shape.
            _menuCamera.targetTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _menuCamera;
            canvas.planeDistance = 1.0f;
            Time.captureFramerate = 60;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;   // the list fades in
            }

            Shoot(city, "select-1-five");
            select.Next();
            yield return null;
            Shoot(city, "select-2-after-right-arrow");
            Object.Destroy(uiObject);
            Debug.Log($"CityX select capture: {select.Characters.Count} characters, centre {select.SelectedIndex}");
#else
            yield break;
#endif
        }
    }
}
