using System.Collections;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The rain in the REAL city, real scene, real streamed chunks -- not a box
    /// on a plane. The city's own highway runs over the spawn point on an
    /// elevated deck 14 metres up, which is exactly the kind of overhang rain
    /// has to respect: this measures that no drop falls under it and rain does
    /// fall in the open beside it.
    ///
    /// When there is a real graphics device (run with -Graphics) it also
    /// photographs what it measured, to CityX/Logs/CityShots/rain-*.bmp -- a
    /// green measurement is not proof rain LOOKS right, which this project has
    /// learned more than once. Under -nographics the photographs are skipped
    /// and the measurements still run.
    /// </summary>
    public sealed class RainRealCityPlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1.0f;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static bool CanPhotograph => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        private void Teleport(PlayerVitals player, DevConsoleView console, Vector3 position)
        {
            console.Streamer.StreamAround(position, 900);
            Physics.SyncTransforms();
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static float GroundAt(float x, float z)
        {
            return (float)CurrentWorld.Instance.Terrain.ContinuousHeightAt(x, z);
        }

        private static void Photograph(Camera camera, Vector3 position, Vector3 euler, string name)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(directory);
                WriteBitmap(Path.Combine(directory, name + ".bmp"), image);
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>A bottom-up 24-bit BMP -- the test assembly has no PNG encoder to hand, and this needs none.</summary>
        private static void WriteBitmap(string path, Texture2D image)
        {
            int width = image.width, height = image.height;
            Color32[] pixels = image.GetPixels32();
            int rowBytes = (width * 3 + 3) & ~3;
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)'B'); writer.Write((byte)'M');
                writer.Write(54 + rowBytes * height); writer.Write(0); writer.Write(54);
                writer.Write(40); writer.Write(width); writer.Write(height);
                writer.Write((short)1); writer.Write((short)24); writer.Write(0);
                writer.Write(rowBytes * height); writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
                var row = new byte[rowBytes];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color32 c = pixels[y * width + x];
                        row[x * 3] = c.b; row[x * 3 + 1] = c.g; row[x * 3 + 2] = c.r;
                    }
                    writer.Write(row);
                }
            }
        }

        [UnityTest]
        public IEnumerator NoRainFallsUnderTheHighwayDeckInTheRealCityAndItDoesInTheOpenBesideIt()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            SceneManager.SetActiveScene(_scene);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var player = Object.FindFirstObjectByType<PlayerVitals>();
            RainEffect rain = console.Rain;
            Assert.IsNotNull(rain, "no rain in the real scene");

            // The city's highway (chunk column x=0, world x 0..24) is a deck 14m up.
            // Stand in the arterial that crosses under it, a few metres short of it,
            // so the deck edge is inside the 40m rain box. (The deck sweeps a few metres
            // sideways along its length, so only the middle of it, x 10..14, is under it
            // at every point; the sliver beside that is legitimately open sky.)
            float x = -8.0f, z = 12.0f;
            float ground = GroundAt(x, z);
            Teleport(player, console, new Vector3(x, ground + 0.1f, z));
            yield return null;

            console.Submit("/weather rain");
            yield return new WaitForSeconds(2.0f);

            float deckGround = GroundAt(11.0f, 12.0f);
            Vector3 underDeck = new Vector3(11.0f, deckGround + 1.0f, 12.0f);
            float shelter = CurrentWeather.Shelter(underDeck);
            TestContext.WriteLine($"shelter under the deck: {shelter:0.00}; live drops {rain.LiveDropCount}");
            Assert.Greater(rain.LiveDropCount, 100, "no rain in the real city at all");
            Assert.Greater(shelter, 0.9f, "the cover map did not see the highway deck over the street");
            Assert.AreEqual(0.0f, CurrentWeather.RainOn(underDeck), 0.1f, "rain fell on the ground under the deck");

            int under = rain.CountLiveDropsIn(new Bounds(new Vector3(11.0f, deckGround + 6.5f, 12.0f), new Vector3(2.0f, 11.0f, 14.0f)));
            int open = rain.CountLiveDropsIn(new Bounds(new Vector3(-20.0f, ground + 8.0f, 12.0f), new Vector3(12.0f, 16.0f, 20.0f)));
            TestContext.WriteLine($"drops under the deck: {under}; in the open beside it: {open}");
            Assert.AreEqual(0, under, "rain fell under the highway deck");
            Assert.Greater(open, 10, "rain should fall in the open street beside the deck");

            if (CanPhotograph)
            {
                Camera camera = Camera.main;
                var follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
                if (follow != null) follow.enabled = false;
                Vector3 eye = new Vector3(x, ground + 2.0f, z);

                // NO WaitForEndOfFrame anywhere below. In this batch harness the
                // simulation stops ticking after the first end-of-frame wait (the
                // frame count and the clock both freeze, and every later yield
                // returns at once), which silently turns "rain for another second"
                // into no time at all. Camera.Render() needs no such wait, so every
                // wait here is an ordinary frame or timed wait.

                // A storm in full swing, not one still easing in: the ramp is 3s.
                float waited = 0.0f;
                while ((rain.StormStrength < 1.0f || rain.Intensity < 1.0f) && waited < 10.0f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                // Soaked, and given a second to grow rings, before the street is photographed.
                rain.Soak(1.0f);
                yield return new WaitForSeconds(1.5f);
                TestContext.WriteLine($"in the air {rain.LiveDropCount}, splashes {rain.LiveSplashCount}, rings alive {rain.LiveRippleCount}, rings started {rain.RipplesEmitted}");
                Assert.Greater(rain.RipplesEmitted, 0, "soaked ground in the real city grew no rings");

                Photograph(camera, eye, new Vector3(4.0f, 90.0f, 0.0f), "rain-under-deck");
                Photograph(camera, eye, new Vector3(-2.0f, 270.0f, 0.0f), "rain-street-open");
                Photograph(camera, eye, new Vector3(-14.0f, 270.0f, 0.0f), "rain-overcast");
                Photograph(camera, new Vector3(x, ground + 0.5f, z), new Vector3(9.0f, 270.0f, 0.0f), "rain-street-ripples");
                // Looking down at the wet road just ahead, where rings and splashes are
                // seen from above rather than at a grazing angle.
                Photograph(camera, new Vector3(x - 1.0f, ground + 1.7f, z + 5.0f), new Vector3(45.0f, 270.0f, 0.0f), "rain-ripples-down");

                // Fire a strike and photograph THAT instant. The strike is made and the
                // component's own LateUpdate -- the thing that lights the sky -- is run
                // by hand before the photograph, so what is caught is exactly the frame
                // a strike starts on (a real frame here can outlast the bolt's 0.18s,
                // so waiting frames for the flash would miss it). LateUpdate is
                // idempotent -- it re-applies the sky fresh each call -- so running it
                // twice in one frame is harmless.
                // Photographed from high enough to see over the neighbouring roofs,
                // pitched up toward where the bolt comes down (450m out, due -x).
                var cycleNow = Object.FindFirstObjectByType<DayNightCycle>();
                rain.StrikeLightning(450.0f, 270.0f);
                typeof(RainEffect).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(rain, null);
                Assert.Greater(cycleNow.Flash, 0.5f, "a strike should light the sky");
                Assert.IsTrue(rain.BoltVisible, "and draw a bolt");
                Photograph(camera, new Vector3(x, ground + 70.0f, z), new Vector3(-8.0f, 270.0f, 0.0f), "rain-lightning");
            }
#else
            yield break;
#endif
        }
    }
}
