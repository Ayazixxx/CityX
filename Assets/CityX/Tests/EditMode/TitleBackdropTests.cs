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

        [Test]
        public void TheLogoFlickersAndHidesThroughItsQuad()
        {
            _object = new GameObject("Skyline");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(_object.transform, false);
            var skyline = _object.AddComponent<TitleSkyline>();
            skyline.LogoRenderer = quad.GetComponent<Renderer>();

            skyline.SetLogoFlicker(0.55f);
            Assert.AreEqual(0.55f, skyline.LogoOpacity, 1e-5f);
            var block = new MaterialPropertyBlock();
            skyline.LogoRenderer.GetPropertyBlock(block);
            Assert.AreEqual(0.55f, block.GetColor("_BaseColor").a, 1e-4f, "the opacity should reach the renderer");

            skyline.SetLogoVisible(false);
            Assert.IsFalse(skyline.LogoVisible);
            skyline.SetLogoVisible(true);
            Assert.IsTrue(skyline.LogoVisible);
        }

        [Test]
        public void ThePanelsHideAndShowTheLogoQuadToo()
        {
            _object = new GameObject("Title", typeof(RectTransform));
            var title = _object.AddComponent<TitleScreen>();
            var skylineObject = new GameObject("Skyline");
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(skylineObject.transform, false);
            var skyline = skylineObject.AddComponent<TitleSkyline>();
            skyline.LogoRenderer = quad.GetComponent<Renderer>();
            title.Skyline = skyline;
            try
            {
                title.Show(TitleScreen.Panel.Settings);
                Assert.IsFalse(skyline.LogoVisible, "settings covers the logo's spot");
                title.Show(TitleScreen.Panel.Menu);
                Assert.IsTrue(skyline.LogoVisible);
            }
            finally
            {
                Object.DestroyImmediate(skylineObject);
            }
        }

        // --- the row of buildings ---

        [Test]
        public void TheRowIsWallsAndWindowsAndCoversTheWholeWidth()
        {
            TitleSkylineBaker.Baked row = TitleSkylineBaker.Bake(150.0f);
            try
            {
                Bounds bounds = row.Mesh.bounds;
                Assert.LessOrEqual(bounds.min.x, -150.0f, "the row should run off the left edge");
                Assert.GreaterOrEqual(bounds.max.x, 150.0f, "and off the right");
                Assert.Greater(row.BuildingCount, 8);
                Assert.AreEqual(TitleSkylineBaker.TallestHeight, bounds.max.y, 0.01f, "the centre tower is the tallest");

                Assert.AreEqual(5, row.Mesh.subMeshCount);
                Assert.AreEqual(0, row.Mesh.GetTriangles(0).Length, "no ground baked");
                Assert.Greater(row.Mesh.GetTriangles(2).Length, 0, "walls");
                Assert.Greater(row.Mesh.GetTriangles(4).Length, 0, "lit windows");
                // Walls and windows only: nothing inside a building at all.
                Assert.GreaterOrEqual(bounds.min.y, 0.0f);
            }
            finally
            {
                Object.DestroyImmediate(row.Mesh);
            }
        }

        [Test]
        public void TheRowIsTheSameEveryTime()
        {
            TitleSkylineBaker.Baked a = TitleSkylineBaker.Bake(120.0f);
            TitleSkylineBaker.Baked b = TitleSkylineBaker.Bake(120.0f);
            try
            {
                Assert.AreEqual(a.Mesh.vertexCount, b.Mesh.vertexCount);
                CollectionAssert.AreEqual(a.Heights, b.Heights);
            }
            finally
            {
                Object.DestroyImmediate(a.Mesh);
                Object.DestroyImmediate(b.Mesh);
            }
        }

        // --- the scene ---

        [Test]
        public void TheTitleSceneIsWiredToItsBackdrop()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/CityX/Scenes/Title.unity", OpenSceneMode.Additive);
            try
            {
                var title = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<TitleScreen>(true)).FirstOrDefault();
                Assert.IsNotNull(title, "no TitleScreen");
                Assert.IsNotNull(title.Skyline, "the logo is driven through the skyline");
                Assert.IsNotNull(title.Skyline.LogoRenderer);

                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
                Assert.IsTrue(all.Any(c => c is TitleRainLayer), "the rain layer");
                Assert.IsFalse(all.Any(c => c is CityStreamer), "the title is a baked row, not a streamed world");

                var filter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                    .FirstOrDefault(f => f.name == "Skyline");
                Assert.IsNotNull(filter);
                Assert.IsTrue(AssetDatabase.Contains(filter.sharedMesh), "the row is a saved mesh asset, not a runtime one");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
