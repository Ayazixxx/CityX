using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the water groundwork (docs/WATER.md) in the real city to Logs/CityShots/water-*.png -- a knee-deep pool on the
    /// street with the player wading through it, by day and by night and from low along the water, and a flood with the player floating in
    /// it -- and logs what a frame costs with the water on screen and without. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.WaterCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WaterCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const int Width = 1280, Height = 720;
        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private string _directory;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            CurrentWater.Clear();
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
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                _camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
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

        /// <summary>The time to render a view, milliseconds, averaged over many renders each waited for (a pixel read back).</summary>
        private float RenderMs(int count)
        {
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                _camera.targetTexture = target;
                RenderTexture.active = target;
                _camera.Render();
                pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    _camera.Render();
                    pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                }
                return (float)clock.Elapsed.TotalMilliseconds / count;
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixel);
            }
        }

        private void SetTime(DayNightCycle cycle, float dayShare)
        {
            if (cycle != null)
            {
                cycle.TimeOfDay = SkyCycle.DaySeconds * dayShare;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
        }

        private static IEnumerator Drive(PlayerLocomotion body, LocomotionInput input, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                body.Tick(input, 1.0f / 60.0f);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RenderTheWater()
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
            if (cycle != null) cycle.Running = false;
            SetTime(cycle, 0.42f);
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            var body = vitals.GetComponent<PlayerLocomotion>();
            yield return Drive(body, default, 60);

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("WaterCam");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 50.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;

            WaterSplashes kit = WaterSplashes.Current;
            Assert.IsNotNull(kit, "no water kit in the city: rebuild the scene");
            Vector3 feet = body.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(body.transform.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 eye = feet - forward * 5.0f + side * 2.2f + Vector3.up * 2.6f;
            Vector3 look = feet + forward * 2.0f + Vector3.up * 0.6f;

            Render("water-0-dry", eye, look);

            WaterSurface pool = WaterSurface.Create(WaterBody.Square(feet + forward * 3.0f, PoolSize, 0.7f), kit.WaterMaterial, "CapturePool");
            yield return Drive(body, new LocomotionInput { Move = Vector2.up, YawDegrees = body.transform.eulerAngles.y }, 50);
            Render("water-1-wading-day", eye, look);

            // What the water costs a frame: the same view with the pool hidden and shown, in turn, the fastest of each (the least disturbed).
            _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            float dryMs = float.MaxValue, wetMs = float.MaxValue;
            for (int round = 0; round < 4; round++)
            {
                pool.gameObject.SetActive(false);
                dryMs = Mathf.Min(dryMs, RenderMs(30));
                pool.gameObject.SetActive(true);
                pool.Redraw();
                wetMs = Mathf.Min(wetMs, RenderMs(30));
            }
            Render("water-2-low-along", feet + side * 3.0f + Vector3.up * 0.9f + forward * 1.0f, body.transform.position + Vector3.up * 0.5f);
            Debug.Log($"CityX water capture: wading depth {body.WaterDepth:0.00} m, speed {body.CurrentSpeed:0.0} m/s, splashes {kit.RingsEmitted} rings");

            SetTime(cycle, 0.95f);
            yield return Drive(body, new LocomotionInput { Move = Vector2.up, YawDegrees = body.transform.eulerAngles.y }, 10);
            Render("water-3-wading-night", eye, look);
            SetTime(cycle, 0.42f);
            Object.Destroy(pool.gameObject);
            yield return null;

            // A flood, risen to 4 m, the player floating in it with Space held.
            var floodBody = new WaterBody(feet.x - 60.0f, feet.z - 60.0f, feet.x + 60.0f, feet.z + 60.0f, feet.y - 40.0f, feet.y + 4.0f);
            floodBody.Flow = new Vector2(forward.x, forward.z);
            WaterSurface flood = WaterSurface.Create(floodBody, kit.WaterMaterial, "CaptureFlood");
            yield return Drive(body, new LocomotionInput { JumpHeld = true }, 240);
            Vector3 at = body.transform.position;
            Render("water-4-flood-floating", at - forward * 6.0f + side * 3.0f + Vector3.up * 3.0f, at + Vector3.up * 1.2f);
            Render("water-5-flood-street", at + Vector3.up * 14.0f - forward * 18.0f, at + forward * 20.0f);
            Debug.Log($"CityX water capture: floating {body.IsFloating}, depth share {body.WaterDepthShare:0.00}; " +
                      $"a frame {dryMs:0.00} ms dry, {wetMs:0.00} ms with the pool on screen ({wetMs - dryMs:+0.00;-0.00} ms)");
            Object.Destroy(flood.gameObject);
#else
            yield break;
#endif
        }

        /// <summary>The /water pool's size across, metres.</summary>
        private const float PoolSize = 16.0f;
    }
}
