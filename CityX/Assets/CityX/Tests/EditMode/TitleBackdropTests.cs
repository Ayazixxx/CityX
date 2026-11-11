using System.Linq;
using CityX.UI;
using CityX.UIEditor;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The title screen's backdrop: a row of the game's own generated
    /// buildings, the logo over them, and the rain, eyes and lightning over
    /// that. Ported from title_screen/*.gd (the rain, eyes and flash are the
    /// Godot numbers) around buildings that come from this game's generator.
    /// </summary>
    public sealed class TitleBackdropTests
    {
        private GameObject _object;

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
            {
                Object.DestroyImmediate(_object);
            }
        }

        private T Layer<T>(float width = 1152.0f, float height = 648.0f) where T : Graphic
        {
            _object = new GameObject("Layer", typeof(RectTransform));
            var rect = (RectTransform)_object.transform;
            rect.sizeDelta = new Vector2(width, height);
            return _object.AddComponent<T>();
        }

        // --- the rain ---

        [Test]
        public void TheRainHasItsConfiguredNumberOfDrops()
        {
            var rain = Layer<TitleRainLayer>();
            rain.Reseed(1);
            rain.Step(1.0f / 60.0f);

            Assert.AreEqual(TitleRainLayer.DropCount, rain.Drops.Count);
            Assert.LessOrEqual(TitleRainLayer.DropCount, 150, "the rain is light: it must not bury the buildings");
        }

        [Test]
        public void TheRainStaysInsideAndAboveTheScreenAsItFalls()
        {
            var rain = Layer<TitleRainLayer>();
            rain.Reseed(7);
            for (int i = 0; i < 900; i++)
            {
                rain.Step(1.0f / 60.0f);
            }

            foreach (TitleRainLayer.Drop drop in rain.Drops)
            {
                Assert.GreaterOrEqual(drop.X, -20.0f - 3.0f, "a drop drifted far off the left edge");
                Assert.LessOrEqual(drop.X, 1152.0f);
                Assert.GreaterOrEqual(drop.Y, -20.0f);
                Assert.LessOrEqual(drop.Y, 648.0f + 16.0f, "a drop fell past the bottom without being recycled");
            }
        }

        [Test]
        public void SplashesAppearAtTheBottomAndFadeAway()
        {
            var rain = Layer<TitleRainLayer>();
            rain.Reseed(3);
            bool sawSplash = false;
            for (int i = 0; i < 1200; i++)
            {
                rain.Step(1.0f / 60.0f);
                foreach (TitleRainLayer.Splash splash in rain.Splashes)
                {
                    sawSplash = true;
                    Assert.GreaterOrEqual(splash.Y, 648.0f - 6.0f - 0.01f, "a splash forms on the ground line, at the bottom");
                    Assert.LessOrEqual(splash.Y, 648.0f + 0.01f);
                    Assert.LessOrEqual(splash.Radius, splash.MaxRadius + 0.5f);
                    Assert.Greater(splash.Opacity, 0.0f, "a spent splash should be removed, not kept");
                }
            }
            Assert.IsTrue(sawSplash, "20 seconds of rain never splashed");
            for (int i = 0; i < 200; i++)
            {
                rain.Step(1.0f / 60.0f);
            }
            Assert.Less(rain.Splashes.Count, 60, "splashes are piling up instead of fading");
        }

        [Test]
        public void TheSameSeedGivesTheSameRain()
        {
            var first = Layer<TitleRainLayer>();
            first.Reseed(11);
            for (int i = 0; i < 100; i++) first.Step(1.0f / 60.0f);
            float x = first.Drops[5].X, y = first.Drops[5].Y;
            Object.DestroyImmediate(_object);

            var second = Layer<TitleRainLayer>();
            second.Reseed(11);
            for (int i = 0; i < 100; i++) second.Step(1.0f / 60.0f);

            Assert.AreEqual(x, second.Drops[5].X, 1e-4f);
            Assert.AreEqual(y, second.Drops[5].Y, 1e-4f);
        }

        [Test]
        public void RainFallsTheSameDistanceWhateverTheFrameRate()
        {
            // The original stepped once per 60fps frame; here motion scales with time.
            // A seed that leaves the first drop well clear of the bottom, so nothing respawns mid-measure.
            int seed = 21;
            var fast = Layer<TitleRainLayer>();
            while (true)
            {
                fast.Reseed(seed);
                fast.Step(0.0f);
                if (fast.Drops[0].Y < 400.0f)
                {
                    break;
                }
                seed++;
            }
            float startY = fast.Drops[0].Y;
            for (int i = 0; i < 6; i++) fast.Step(1.0f / 60.0f);
            float fastY = fast.Drops[0].Y;
            Object.DestroyImmediate(_object);

            var slow = Layer<TitleRainLayer>();
            slow.Reseed(seed);
            slow.Step(0.0f);
            slow.Step(6.0f / 60.0f);

            Assert.AreEqual(fastY, slow.Drops[0].Y, 0.01f);
            Assert.Greater(fastY, startY, "rain falls DOWN the screen");
        }

        // --- the logo ---

        /// <summary>Held in the top left corner, whatever the screen's shape: its top left front corner lands where asked, at its share of the height.</summary>
        [Test]
        public void TheLogoIsHeldInTheTopLeftCornerAtItsShareOfTheHeight([Values(16.0f / 9.0f, 21.0f / 9.0f, 4.0f / 3.0f)] float aspect)
        {
            var bounds = new Bounds(new Vector3(0.3f, -0.1f, 0.05f), new Vector3(6.0f, 3.4f, 0.4f));
            const float fov = 42.0f, distance = 6.0f, share = 0.4f, yaw = 14.0f;
            var topLeft = new Vector2(0.03f, 0.96f);
            TitleLogo.Place(fov, aspect, distance, topLeft, share, yaw, bounds, out Vector3 position, out float scale);

            Vector3 corner = position + Quaternion.Euler(0.0f, yaw, 0.0f) * (new Vector3(bounds.min.x, bounds.max.y, bounds.min.z) * scale);
            float halfHeight = corner.z * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            var seen = new Vector2(0.5f + 0.5f * corner.x / (halfHeight * aspect), 0.5f + 0.5f * corner.y / halfHeight);
            Assert.AreEqual(topLeft.x, seen.x, 1e-4f, "the corner is not at the left");
            Assert.AreEqual(topLeft.y, seen.y, 1e-4f, "the corner is not at the top");
            Assert.AreEqual(share * 2.0f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), bounds.size.y * scale, 1e-4f, "not its share of the height");
        }

        /// <summary>The neon's glow and the light it throws on the letters follow the flicker together.</summary>
        [Test]
        public void TheNeonAndItsLightFlickerTogether()
        {
            _object = new GameObject("Logo");
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tube.transform.SetParent(_object.transform, false);
            var logo = _object.AddComponent<TitleLogo>();
            logo.Neon = tube.GetComponent<Renderer>();
            logo.Glow = new GameObject("Glow").AddComponent<Light>();
            logo.Glow.transform.SetParent(_object.transform, false);
            var flicker = new NeonFlicker(logo.Seed);
            var block = new MaterialPropertyBlock();
            bool sawDark = false;
            for (float time = 0.0f; time < 30.0f; time += 0.02f)
            {
                logo.Flicker(time);
                Assert.AreEqual(flicker.Brightness(time), logo.Brightness, 1e-5f);
                logo.Neon.GetPropertyBlock(block);
                Assert.AreEqual(logo.NeonColour.r * logo.Brightness, block.GetColor("_EmissionColor").r, 1e-3f, "the tube's glow is not the flicker's");
                Assert.AreEqual(logo.GlowIntensity * logo.Brightness, logo.Glow.intensity, 1e-3f, "the light on the letters is not the flicker's");
                sawDark |= logo.Brightness < 0.4f;
            }
            Assert.IsTrue(sawDark, "half a minute and the sign never faltered");
        }

        /// <summary>
        /// The logo is the menu's alone (every other panel is a box reaching into its corner); the city and the rooftop go only for the screens
        /// with their own stage and camera: the title's camera then draws nothing, so their stage lights cannot light the city.
        /// </summary>
        [Test]
        public void TheLogoIsTheMenusAndTheCityGoesOnlyForTheCharacterScreens()
        {
            _object = new GameObject("Title", typeof(RectTransform));
            var title = _object.AddComponent<TitleScreen>();
            var logoObject = new GameObject("Logo");
            logoObject.transform.SetParent(_object.transform, false);
            title.Logo = logoObject.AddComponent<TitleLogo>();
            var heroObject = new GameObject("Hero");
            heroObject.transform.SetParent(_object.transform, false);
            title.Hero = heroObject.AddComponent<TitleHero>();
            var cameraObject = new GameObject("TitleCamera");
            cameraObject.transform.SetParent(_object.transform, false);
            title.Backdrop = cameraObject.AddComponent<Camera>();
            title.BackdropMask = 0x5;

            foreach (TitleScreen.Panel panel in System.Enum.GetValues(typeof(TitleScreen.Panel)))
            {
                title.Show(panel);
                Assert.AreEqual(panel == TitleScreen.Panel.Menu, title.Logo.Shown, $"the logo on {panel}");
                bool ownStage = panel == TitleScreen.Panel.CharacterSelect || panel == TitleScreen.Panel.Creator;
                Assert.AreEqual(!ownStage, heroObject.activeSelf, $"the rooftop on {panel}");
                Assert.AreEqual(ownStage ? 0 : 0x5, title.Backdrop.cullingMask, $"the city on {panel}");
            }
            title.ShowIdlePrompt();
            Assert.IsTrue(title.Logo.Shown, "the logo is the first thing seen");
            Assert.IsTrue(heroObject.activeSelf);
            Assert.AreEqual(0x5, title.Backdrop.cullingMask);
        }

        // --- the scene ---

        /// <summary>
        /// The backdrop is the game's own city (owner, 2026-11-11), from its own streamer and skyline in its own materials, sky and grade; and
        /// nothing of the game's to touch it with (owner: "theres no need for shatterable windows or stuff like that").
        /// </summary>
        [Test]
        public void TheTitleSceneIsTheGamesOwnCityAndNothingToTouchIt()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/CityX/Scenes/Title.unity", OpenSceneMode.Additive);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
                var title = all.OfType<TitleScreen>().FirstOrDefault();
                Assert.IsNotNull(title, "no TitleScreen");
                Assert.IsNotNull(title.Logo, "the logo is not wired");
                Assert.IsNotNull(title.Hero, "the rooftop is not wired");
                Assert.IsNotNull(title.Backdrop, "the title's camera is not wired");
                Assert.AreNotEqual(0, title.BackdropMask, "the title's camera would draw nothing on the menu");
                Assert.IsTrue(all.Any(c => c is TitleRainLayer), "the rain layer");

                CityStreamer city = all.OfType<CityStreamer>().SingleOrDefault();
                Assert.IsNotNull(city, "the title has no city");
                foreach (System.Reflection.FieldInfo field in typeof(CityStreamer).GetFields())
                {
                    if (field.FieldType == typeof(Material))
                    {
                        Assert.IsNotNull(field.GetValue(city), $"the city's {field.Name} is not set: it would draw as the game's does not");
                    }
                }
                Assert.GreaterOrEqual(city.RadiusChunks, SkylineStreamer.ExcludeRadiusChunks, "a gap between the streamed city and the skyline");
                Assert.IsFalse(city.PlaceAtSpawn);
                SkylineStreamer skyline = all.OfType<SkylineStreamer>().SingleOrDefault();
                Assert.IsNotNull(skyline, "the title has no skyline past the streamed city");
                Assert.IsNotNull(skyline.StructureMaterial);

                TitleStage stage = all.OfType<TitleStage>().SingleOrDefault();
                Assert.IsNotNull(stage, "nothing puts the camera on a roof");
                Assert.AreSame(city, stage.City);
                Assert.AreSame(title.Backdrop, stage.View);
                Assert.AreSame(title.Hero.transform.parent, stage.Rooftop, "the character is not on the rooftop the stage places");

                Assert.IsNotNull(all.OfType<DayNightCycle>().Single().SkyDome, "not the game's sky");
                Assert.IsTrue(all.OfType<UnityEngine.Rendering.Volume>().Any(v => v.sharedProfile != null && v.sharedProfile.name == "CityGrade"),
                    "not the game's grade");

                foreach (System.Type touch in new[] { typeof(GlassBreaker), typeof(LightSwitchInteractor), typeof(BalconyDoorInteractor) })
                {
                    Assert.IsFalse(all.Any(c => c != null && touch.IsInstanceOfType(c)), $"the title has the game's {touch.Name}");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
