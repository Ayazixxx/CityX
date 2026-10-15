using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders a Shoota in the real city to Logs/CityShots/shoota-*.png -- turning on the player and opening fire (the rifle
    /// shouldered, the flash, the tracer), seen from the side and from over the player's shoulder, and then hurt and running for cover.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ShootaCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ShootaCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private GameObject _shoota;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_shoota != null) Object.Destroy(_shoota);
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

        /// <summary>Waits for his next round (so the flash and the tracer are in the frame), at most a few seconds.</summary>
        private static IEnumerator NextShot(ShootaBrain shoota)
        {
            int before = shoota.RoundsFired;
            for (int i = 0; i < 300 && shoota.RoundsFired == before; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RenderTheShoota()
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
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            vitals.InfiniteHealth = true;
            vitals.GetComponent<PlayerInputReader>().enabled = false;

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("ShootaCam") { tag = "MainCamera" };
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 55.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            Transform body = vitals.transform;
            Vector3 forward = body.forward;
            Vector3 side = body.right;
            Vector3 spot = body.position + forward * 12.0f;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Shoota.prefab");
            _shoota = Object.Instantiate(prefab, spot + Vector3.up * 0.1f, Quaternion.LookRotation(-forward, Vector3.up));
            var shoota = _shoota.GetComponent<ShootaBrain>();
            shoota.Target = vitals;
            Vector3 middle = (body.position + spot) * 0.5f;

            Render("shoota-0-standing", spot + side * 5.0f + forward * 2.0f + Vector3.up * 2.2f, spot + Vector3.up * 1.4f);
            yield return NextShot(shoota);
            Render("shoota-1-firing-side", middle + side * 9.0f + Vector3.up * 2.4f, middle + Vector3.up * 1.4f);
            yield return NextShot(shoota);
            Render("shoota-2-firing-close", spot + side * 3.5f - forward * 2.5f + Vector3.up * 2.0f, spot + Vector3.up * 1.6f);
            yield return NextShot(shoota);
            Render("shoota-3-over-the-shoulder", body.position - forward * 4.5f + side * 1.2f + Vector3.up * 3.0f, spot + Vector3.up * 1.4f);

            ((IDamageable)_shoota.GetComponent<PlayerVitals>()).ApplyHit(10.0f, body.position, 0.0f);
            for (int i = 0; i < 60; i++) yield return null;
            Render("shoota-4-hurt-running", _shoota.transform.position + side * 6.0f + Vector3.up * 2.5f, _shoota.transform.position + Vector3.up * 1.2f);
            for (int i = 0; i < 120; i++) yield return null;
            Render("shoota-5-in-cover", _shoota.transform.position + side * 6.0f - forward * 3.0f + Vector3.up * 3.0f,
                _shoota.transform.position + Vector3.up * 1.0f);
            Debug.Log($"CityX shoota capture: {shoota.RoundsFired} rounds, {shoota.Hits} hits, state {shoota.State}, cover {shoota.HasCover} " +
                $"at {shoota.CoverSpot} (he is at {_shoota.transform.position}); the last cover search took {shoota.LastCoverSearchMs:0.00} ms, " +
                $"{shoota.CoverSearchSummary}");
#else
            yield break;
#endif
        }
    }
}
