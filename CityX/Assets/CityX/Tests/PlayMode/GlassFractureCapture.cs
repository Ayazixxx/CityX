using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders a real window near the spawn breaking, to Logs/CityShots/glass-*.png -- struck hard from inside, a few
    /// moments of it falling apart (the slivers going in, the big pieces hanging on the frame, then dropping), and the glass lying on the
    /// ground. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.GlassFractureCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GlassFractureCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            GlassFractureBurst.Clear();
            if (_camera != null) Object.Destroy(_camera.gameObject);
            if (_sceneCamera != null) _sceneCamera.enabled = true;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private void Render(string name, Vector3 eye, Vector3 look)
        {
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            const int width = 1280, height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                _camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>A glazed window on a rectangular building's street side near the spawn, second storey, with clear air in front of it.</summary>
        private static bool FindWindow(CityWorld world, out Vector3 onGlass, out Vector3 outward)
        {
            onGlass = default;
            outward = default;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 3))
            {
                if (site.Sides >= 3 || site.Shell)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                for (int column = 1; column + 1 < columns.Count; column++)
                {
                    if (rows.Count < 3 || !plan.IsGlazed(WindowGrid.Wall.North, column, 1))
                    {
                        continue;
                    }
                    Vector3 face = TestSites.At(site, (float)site.MinX + (column + 0.5f) * columns.Size, site.BaseHeight + 1.5f * rows.Size, (float)site.MaxZ);
                    Vector3 n = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                    // Clear air for 8 m in front of it, and the glass itself where a ray from the street meets it.
                    if (Physics.Raycast(face + n * 0.3f, n, 8.0f, ~0, QueryTriggerInteraction.Ignore)
                        || !Physics.Raycast(face + n * 2.0f, -n, out RaycastHit glass, 2.5f, ~0, QueryTriggerInteraction.Ignore)
                        || !GlassDamage.TryPaneAt(world, site, glass.point, out _))
                    {
                        continue;
                    }
                    onGlass = glass.point;
                    outward = n;
                    return true;
                }
            }
            return false;
        }

        [UnityTest]
        public IEnumerator RenderAWindowBreaking()
        {
#if UNITY_EDITOR
            // Rendering with no graphics device crashes Unity outright (a regex filter runs Explicit captures too): needs -Graphics.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("needs a graphics device: run with -Graphics");
            }
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            yield return Frames(30);

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("GlassCam");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 50.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            var breaker = Object.FindFirstObjectByType<GlassBreaker>();
            Assert.IsNotNull(breaker, "no glass breaker in the city");
            var world = new CityWorld(breaker.GetComponent<CityStreamer>().WorldSeed);
            Assert.IsTrue(FindWindow(world, out Vector3 glass, out Vector3 outward), "no window near the spawn to break");
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            Vector3 eye = glass + outward * 4.2f + side * 1.8f + Vector3.down * 0.4f;
            Vector3 look = glass + Vector3.down * 0.5f;
            Render("glass-0-whole", eye, look);

            int before = GlassFractureBurst.LiveShards;
            // Struck hard from inside, as by someone thrown through it: the glass comes out over the street, toward the camera.
            Assert.IsTrue(breaker.TakeBlow(glass, outward, BlowStrength.Heavy), "the blow broke nothing");
            int pieces = GlassFractureBurst.LiveShards - before;
            yield return Frames(3);
            Render("glass-1-struck", eye, look);
            yield return Frames(9);
            Render("glass-2-going", eye, look);
            yield return Frames(18);
            Render("glass-3-falling", eye, look);
            yield return Frames(60);
            Render("glass-4-down", glass + outward * 3.0f + side * 1.0f + Vector3.up * 0.8f, glass - outward * 1.5f + Vector3.down * 3.5f);

            Debug.Log($"CityX glass capture: the window at {glass} broke into {pieces} pieces");
#else
            yield break;
#endif
        }
    }
}
