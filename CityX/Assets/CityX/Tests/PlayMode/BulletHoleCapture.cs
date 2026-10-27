using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders a real window near the spawn shot three times away from its middle, each hit marked with a small red dot, to
    /// Logs/CityShots/bullethole-*.png (each hole should sit on its dot); then bullet trails against the sky and a wall, as they were (added light)
    /// and as they are (laid over what is behind). Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.BulletHoleCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BulletHoleCapture : InputTestFixture
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

        /// <summary>A glazed window on a box building's street side near the spawn, second storey, with clear air in front of it.</summary>
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

        private static void Dot(Vector3 at, Vector3 outward)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(dot.GetComponent<Collider>());
            dot.transform.position = at + outward * 0.02f;
            dot.transform.localScale = Vector3.one * 0.025f;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = Color.red;
            dot.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        [UnityTest]
        public IEnumerator RenderBulletHolesAndTrails()
        {
#if UNITY_EDITOR
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
            var cameraObject = new GameObject("BulletHoleCam") { tag = "MainCamera" };   // the trails size themselves to the main camera
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 50.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            var breaker = Object.FindFirstObjectByType<GlassBreaker>();
            Assert.IsNotNull(breaker, "no glass breaker in the city");
            var world = new CityWorld(breaker.GetComponent<CityStreamer>().WorldSeed);
            Assert.IsTrue(FindWindow(world, out Vector3 glass, out Vector3 outward), "no window near the spawn to shoot");
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;

            // Three shots away from the middle: low left, high right, and off to one side at the middle's height.
            Vector3[] hits = { glass - side * 0.3f - Vector3.up * 0.45f, glass + side * 0.35f + Vector3.up * 0.4f, glass + side * 0.15f - Vector3.up * 0.1f };
            foreach (Vector3 hit in hits)
            {
                Assert.IsTrue(breaker.TakeBlow(hit, -outward, BlowStrength.Bullet), $"the shot at {hit} found no glass");
                Dot(hit, outward);
                yield return Frames(30);   // the chunk is redrawn with the new hole
            }
            Render("bullethole-1-close", glass + outward * 2.2f + side * 0.3f, glass);
            Render("bullethole-2-angle", glass + outward * 2.0f + side * 1.6f + Vector3.down * 0.5f, glass);

            // The trails: from a gun's height in the street up past the window into the sky, and one into the wall beside it.
            Vector3 muzzle = glass + outward * 6.0f + Vector3.down * 3.0f + side * 1.0f;
            Vector3 eye = muzzle + outward * 2.5f - side * 2.0f + Vector3.up * 0.6f;
            Vector3 look = glass + outward * 2.0f;
            Material trail = BulletTracer.EnsureMaterial();
            foreach (bool additive in new[] { true, false })
            {
                SeeThroughMaterial.Apply(trail, additive);
                BulletTracer skyward = BulletTracer.Spawn(muzzle, glass + outward * 1.0f + Vector3.up * 40.0f);
                BulletTracer wallward = BulletTracer.Spawn(muzzle, glass + side * 2.5f);
                Render(additive ? "bullettrail-1-was-added-light" : "bullettrail-2-now-laid-over", eye, look);
                Object.Destroy(skyward.gameObject);
                Object.Destroy(wallward.gameObject);
                yield return null;
            }
#else
            yield break;
#endif
        }
    }
}
