using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Self Smite in the real city, by day and by night, to Logs/CityShots/selfsmite-*.png so it can be LOOKED AT -- the
    /// cloud forming overhead, the bolt coming down, the strike, the burst, the light on the street.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SelfSmiteCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SelfSmiteCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float Frame = 1.0f / 60.0f;
        private Scene _scene;
        private string _directory;
        private Camera _camera;
        private Camera _sceneCamera;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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

        private void Render(string name)
        {
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

        /// <summary>
        /// A shot <paramref name="moveSeconds"/> into the move: the camera is put in place a frame before (the bolt turns its ribbons to face the
        /// main camera as it builds them), then the frame is rendered.
        /// </summary>
        private IEnumerator ShotAt(SelfSmiteAbility smite, float moveSeconds, string name, Vector3 eye, Vector3 look)
        {
            while (smite.Seconds < moveSeconds - Frame && smite.Bolt != null)
            {
                yield return null;
            }
            _camera.transform.position = eye;
            _camera.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
            yield return null;
            Render(name);
            Debug.Log($"CityX self smite capture: {name} at {smite.Seconds:0.000} s (bolt {(smite.Bolt != null ? $"tip {smite.Bolt.TipHeight:0.0} m, shown {smite.Bolt.BoltShown}, burst {smite.Bolt.BlastShown}" : "gone")})");
        }

        [UnityTest]
        public IEnumerator RenderSelfSmite()
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

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            SelfSmiteAbility smite = vitals.GetComponent<SelfSmiteAbility>();
            Assert.IsNotNull(smite, "the scene's player has no Self Smite -- rebuild the scene");
            vitals.GetComponent<PlayerInputReader>().enabled = false;
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = vitals.transform.eulerAngles.y }, Frame);
                yield return null;
            }

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }

            // The capture camera stands in for the player's: tagged as the main camera (the bolt faces it), the scene's own put aside.
            _sceneCamera = Camera.main;
            if (_sceneCamera != null)
            {
                _sceneCamera.enabled = false;
            }
            var cameraObject = new GameObject("SelfSmiteCam") { tag = "MainCamera" };
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 60.0f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 2500.0f;
            _camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            Transform body = vitals.transform;
            Vector3 feet = body.position;
            Vector3 back = -body.forward;
            Vector3 side = body.right;
            Vector3 chest = feet + Vector3.up * 1.6f;
            Vector3 behind = chest + back * 6.0f + Vector3.up * 1.0f;
            Vector3 wide = feet + side * 16.0f + back * 4.0f + Vector3.up * 3.0f;
            Vector3 wideLook = feet + Vector3.up * 9.0f;

            // A dummy two and a half metres off, for scale and to be thrown.
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.transform.position = feet + body.forward * 2.5f + side * 1.0f + Vector3.up * 1.0f;
            dummy.AddComponent<SparringDummy>().Reset();
            Physics.SyncTransforms();

            // --- by day
            Assert.IsTrue(smite.TryCast(), "Self Smite was refused");
            yield return ShotAt(smite, 0.12f, "selfsmite-day-1-sky", feet + back * 10.0f + Vector3.up * 2.0f, feet + Vector3.up * 1500.0f + body.forward * 200.0f);
            yield return ShotAt(smite, 0.40f, "selfsmite-day-2-wide-falling", wide, feet + Vector3.up * 45.0f);
            yield return ShotAt(smite, 0.48f, "selfsmite-day-3-behind-coming", behind, chest);
            yield return ShotAt(smite, 0.52f, "selfsmite-day-4-behind-arriving", behind, chest);
            yield return ShotAt(smite, 0.56f, "selfsmite-day-5-behind-strike", behind, chest);
            yield return ShotAt(smite, 0.59f, "selfsmite-day-6-wide-strike", wide, wideLook);
            yield return ShotAt(smite, 0.66f, "selfsmite-day-7-behind-burst", behind, chest);
            yield return ShotAt(smite, 0.85f, "selfsmite-day-8-behind-after", behind, chest);
            yield return ShotAt(smite, 1.10f, "selfsmite-day-9-behind-recover", behind, chest);
            Debug.Log($"CityX self smite capture: the dummy took {dummy.GetComponent<SparringDummy>().TotalDamageTaken:0} damage");

            // --- by night: the light on the street
            for (int i = 0; i < 60 * 6 && smite.CooldownRemaining > 0.0f; i++)
            {
                yield return null;
            }
            if (cycle != null)
            {
                cycle.TimeOfDay = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            dummy.transform.position = feet + body.forward * 2.5f + side * 1.0f + Vector3.up * 1.0f;
            Physics.SyncTransforms();
            Assert.IsTrue(smite.TryCast(), "the second cast was refused");
            yield return ShotAt(smite, 0.50f, "selfsmite-night-1-behind-coming", behind, chest);
            yield return ShotAt(smite, 0.56f, "selfsmite-night-2-behind-strike", behind, chest);
            yield return ShotAt(smite, 0.60f, "selfsmite-night-3-wide-strike", wide, wideLook);
            yield return ShotAt(smite, 0.70f, "selfsmite-night-4-behind-burst", behind, chest);
            Object.Destroy(dummy);
            Debug.Log($"CityX self smite capture wrote to {_directory}");
#else
            yield break;
#endif
        }
    }
}
