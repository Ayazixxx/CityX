using System.Collections;
using System.Linq;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// What is actually DRAWN behind the title screen's panels, in the real scene with
    /// real frames.
    ///
    /// The character list went blank -- Unity's stock camera blue where the city should be
    /// -- and nothing said so: the screenshot tool only ever drew the title's own camera,
    /// and the wiring looked right. So this renders the title camera the way the game does
    /// (its stack included) and compares pixels.
    /// </summary>
    public class TitleBackdropPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";
        private const int Width = 400;
        private const int Height = 250;

        private Scene _title;

        [UnitySetUp]
        public IEnumerator OpenTheTitle()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#endif
            yield return null;
            yield return null;
            _title = SceneManager.GetSceneByPath(ScenePath);
        }

        [UnityTearDown]
        public IEnumerator CloseTheTitle()
        {
            if (_title.IsValid() && _title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_title);
            }
        }

        private Camera CityCamera()
        {
            return _title.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .First(c => c.CompareTag("MainCamera"));
        }

        /// <summary>What the city camera draws, its stacked cameras included -- everything but the overlay canvas.</summary>
        private static Texture2D Shoot(Camera camera)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
            }
        }

        /// <summary>
        /// Writes a picture as a PNG -- through reflection, because the runtime assemblies do not
        /// reference the image-conversion module (BodyPaint's own note on the same thing) and a
        /// test has no business adding it. A picture that cannot be written is simply not saved.
        /// </summary>
        private static void SavePicture(string path, Texture2D image)
        {
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            System.Reflection.MethodInfo encode = conversion?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode?.Invoke(null, new object[] { image }) is byte[] bytes)
            {
                System.IO.File.WriteAllBytes(path, bytes);
            }
        }

        /// <summary>Mean per-channel difference (0..1) over a region given as fractions of the picture, from the bottom left.</summary>
        private static float MeanDifference(Texture2D a, Texture2D b, float x0, float y0, float x1, float y1)
        {
            int left = Mathf.RoundToInt(x0 * Width), right = Mathf.RoundToInt(x1 * Width);
            int bottom = Mathf.RoundToInt(y0 * Height), top = Mathf.RoundToInt(y1 * Height);
            float sum = 0.0f;
            int count = 0;
            for (int y = bottom; y < top; y++)
            {
                for (int x = left; x < right; x++)
                {
                    Color p = a.GetPixel(x, y);
                    Color q = b.GetPixel(x, y);
                    sum += Mathf.Abs(p.r - q.r) + Mathf.Abs(p.g - q.g) + Mathf.Abs(p.b - q.b);
                    count += 3;
                }
            }
            return sum / Mathf.Max(count, 1);
        }

        [UnityTest]
        public IEnumerator TheCityStaysBehindTheCharacterListWhichDrawsItsCharactersOverIt()
        {
            // Pixels need a GPU: a run started with -nographics has nothing to compare.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("no graphics device -- run with -Graphics to compare pixels");
            }

            var title = _title.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TitleScreen>(true))
                .First();
            Camera camera = CityCamera();

            title.Show(TitleScreen.Panel.Menu);
            yield return null;
            Texture2D menu = Shoot(camera);

            title.Show(TitleScreen.Panel.CharacterSelect);
            yield return null;
            Texture2D list = Shoot(camera);

            // Kept where the other screenshots go, so a failure can be looked at rather than argued with.
            string folder = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(folder);
            SavePicture(System.IO.Path.Combine(folder, "backdrop-menu.png"), menu);
            SavePicture(System.IO.Path.Combine(folder, "backdrop-list.png"), list);

            // The top corners and the far edges: nowhere a pedestal or a body reaches. If the
            // list's camera clears the picture, they are its background, not the city.
            Assert.Less(MeanDifference(menu, list, 0.0f, 0.75f, 0.2f, 1.0f), 0.01f,
                "the character list wiped the city out of the top left");
            Assert.Less(MeanDifference(menu, list, 0.8f, 0.75f, 1.0f, 1.0f), 0.01f,
                "the character list wiped the city out of the top right");
            Assert.Less(MeanDifference(menu, list, 0.0f, 0.0f, 0.15f, 0.3f), 0.01f,
                "the character list wiped the city out of the bottom left");

            // And it did draw something: the middle, where the chosen pedestal and its body stand.
            Assert.Greater(MeanDifference(menu, list, 0.44f, 0.35f, 0.56f, 0.75f), 0.03f,
                "the character list drew nothing over the city -- no pedestals, no characters");

            Object.Destroy(menu);
            Object.Destroy(list);
        }
    }
}
