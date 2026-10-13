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
    /// on a plane. The city's bridges carry their decks over the dry trench -- an
    /// overhang of the kind rain
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

            // The nearest bridge to the spawn: its deck stands over the dry trench, and the ground under it is open to nothing but the
            // deck. Stand along the trench, clear of the deck's width but close enough that the deck is inside the 40 m rain box.
            CityWorld cityWorld = CurrentWorld.Instance;
            cityWorld.Plan.TryFindSpawn(out double spawnX, out double spawnZ);
            CityX.World.Plan.EdgeGeometry bridge = null;
            double bestDistance = double.MaxValue;
            foreach (CityX.World.Plan.EdgeGeometry g in cityWorld.Plan.Geometry.Edges)
            {
                // An ordinary bridge: an expressway's bridge has its elevated deck over it too (2026-10-12), sheltering the land round it.
                if (g == null || !g.Edge.Bridge || g.Elevated) continue;
                double dx = (g.A.X + g.B.X) * 0.5 - spawnX, dz = (g.A.Y + g.B.Y) * 0.5 - spawnZ;
                if (dx * dx + dz * dz < bestDistance)
                {
                    bestDistance = dx * dx + dz * dz;
                    bridge = g;
                }
            }
            Assert.IsNotNull(bridge, "no bridge in the real city to stand under");
            // The point of the deck over the river channel, the nearest to its middle (a long bridge's middle can be over the bank, or a building
            // beside it: seed 38461927's is a 204 m arterial).
            double overWater = 0.5;
            for (double step = 0.0; step <= 0.5; step += 0.01)
            {
                double ta = 0.5 + step, tb = 0.5 - step;
                if (cityWorld.Plan.Fields.IsWater(bridge.A.X + (bridge.B.X - bridge.A.X) * ta, bridge.A.Y + (bridge.B.Y - bridge.A.Y) * ta)) { overWater = ta; break; }
                if (cityWorld.Plan.Fields.IsWater(bridge.A.X + (bridge.B.X - bridge.A.X) * tb, bridge.A.Y + (bridge.B.Y - bridge.A.Y) * tb)) { overWater = tb; break; }
            }
            float deckX = (float)(bridge.A.X + (bridge.B.X - bridge.A.X) * overWater);
            float deckZ = (float)(bridge.A.Y + (bridge.B.Y - bridge.A.Y) * overWater);
            var along = new Vector3((float)bridge.Dir.Perp.X, 0.0f, (float)bridge.Dir.Perp.Y);   // along the trench, across the deck's width

            float x = deckX + along.x * 22.0f, z = deckZ + along.z * 22.0f;
            float ground = GroundAt(x, z);
            Teleport(player, console, new Vector3(x, ground + 0.1f, z));
            yield return null;

            console.Submit("/weather rain");
            yield return new WaitForSeconds(2.0f);

            float deckGround = GroundAt(deckX, deckZ);
            Vector3 underDeck = new Vector3(deckX, deckGround + 1.0f, deckZ);
            float shelter = CurrentWeather.Shelter(underDeck);
            TestContext.WriteLine($"shelter under the bridge: {shelter:0.00}; live drops {rain.LiveDropCount}");
            Assert.Greater(rain.LiveDropCount, 100, "no rain in the real city at all");
            Assert.Greater(shelter, 0.9f, "the cover map did not see the bridge deck over the trench");
            Assert.AreEqual(0.0f, CurrentWeather.RainOn(underDeck), 0.1f, "rain fell on the ground under the deck");

            int under = rain.CountLiveDropsIn(new Bounds(new Vector3(deckX, deckGround + 4.0f, deckZ), new Vector3(4.0f, 6.0f, 4.0f)));

            // The open beside it: a 14 m square of open sky inside the rain box round the player, well clear of this deck. Found, not assumed:
            // an elevated expressway (2026-10-12) can run along the same river and shelter the spot a fixed offset would land on.
            var acrossTrench = new Vector3((float)bridge.Dir.X, 0.0f, (float)bridge.Dir.Y);
            // The most open of the candidate squares (least shelter over its middle and corners): the cover map shades open ground a little
            // near tall buildings, so nothing reads as exactly zero.
            Vector3 openCentre = Vector3.zero;
            float bestShelter = float.MaxValue;
            float[] offsets = { 12.0f, 8.0f, 4.0f, 0.0f, -4.0f, -6.0f };
            float[] sideways = { 0.0f, 6.0f, -6.0f, 12.0f, -12.0f };
            foreach (float oa in offsets)
            {
                foreach (float oc in sideways)
                {
                    Vector3 c = new Vector3(x, 0.0f, z) + along * oa + acrossTrench * oc;
                    if ((new Vector3(c.x - deckX, 0.0f, c.z - deckZ)).magnitude < 15.0f || (new Vector3(c.x - x, 0.0f, c.z - z)).magnitude > 13.0f) continue;
                    float total = 0.0f;
                    for (int k = 0; k < 5; k++)
                    {
                        Vector3 p = c + (k == 0 ? Vector3.zero : new Vector3((k & 1) == 0 ? 6.0f : -6.0f, 0.0f, k < 3 ? 6.0f : -6.0f));
                        p.y = GroundAt(p.x, p.z) + 1.0f;
                        total += CurrentWeather.Shelter(p);
                    }
                    if (total < bestShelter)
                    {
                        bestShelter = total;
                        openCentre = c;
                    }
                }
            }
            TestContext.WriteLine($"the open square's shelter, summed over 5 points: {bestShelter:0.00}");
            Assert.Less(bestShelter, 2.5f, "no open sky anywhere beside the bridge to compare with");
            int open = rain.CountLiveDropsIn(new Bounds(
                new Vector3(openCentre.x, GroundAt(openCentre.x, openCentre.z) + 8.0f, openCentre.z), new Vector3(14.0f, 16.0f, 14.0f)));
            TestContext.WriteLine($"drops under the bridge: {under}; in the open beside it: {open}");
            Assert.AreEqual(0, under, "rain fell under the bridge deck");
            Assert.Greater(open, 10, "rain should fall in the open beside the deck");

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
