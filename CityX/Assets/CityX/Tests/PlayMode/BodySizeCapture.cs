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
    /// Not a test: renders Grow and Shrink in the real city to Logs/CityShots/size-*.png -- the player beside a passer-by, the magic swirling as
    /// they grow, the giant among the buildings and striding, the player's own view as a giant; then shrunk at the passer-by's feet and the
    /// player's own view as a tiny body.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.BodySizeCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BodySizeCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private GameObject _passerBy;
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
            if (_passerBy != null) Object.Destroy(_passerBy);
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

        private void Render(string name, Vector3 eye, Vector3 look, float near = 0.1f)
        {
            RenderFrom(name, eye, Quaternion.LookRotation(look - eye, Vector3.up), near);
        }

        private void RenderFrom(string name, Vector3 eye, Quaternion rotation, float near)
        {
            _camera.transform.SetPositionAndRotation(eye, rotation);
            _camera.nearClipPlane = near;
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

        /// <summary>What the player sees: the follow camera's own place and near plane (it keeps following; only its drawing is off).</summary>
        private void RenderThePlayersView(string name)
        {
            RenderFrom(name, _sceneCamera.transform.position, _sceneCamera.transform.rotation, _sceneCamera.nearClipPlane);
        }

        /// <summary>
        /// Where to stand to see <paramref name="target"/> from about <paramref name="wanted"/> metres with nothing in the way: the clearest of
        /// 24 directions round it, a little above it (the streets are narrow and bend; a fixed offset ended up inside a building).
        /// </summary>
        private static Vector3 ClearView(Vector3 target, float wanted, Transform ignore)
        {
            Vector3 best = target + Vector3.back * wanted;
            float bestFree = -1.0f;
            for (int i = 0; i < 24; i++)
            {
                float angle = i / 24.0f * Mathf.PI * 2.0f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0.18f, Mathf.Sin(angle)).normalized;
                float free = wanted;
                foreach (RaycastHit hit in Physics.RaycastAll(target, direction, wanted, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(ignore) && hit.distance < free)
                    {
                        free = hit.distance;
                    }
                }
                if (free > bestFree)
                {
                    bestFree = free;
                    best = target + direction * Mathf.Max(5.0f, free - 3.0f);
                }
            }
            return best;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RenderGrowAndShrink()
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
            var size = vitals.GetComponent<BodySizeAbility>();
            Assert.IsNotNull(size, "the scene's player has no Grow and Shrink -- rebuild the scene");

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("SizeCam");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 55.0f;
            _camera.farClipPlane = 2000.0f;
            _camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            Transform body = vitals.transform;
            Vector3 forward = body.forward;
            Vector3 side = body.right;
            var civilianPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Civilian.prefab");
            if (civilianPrefab != null)
            {
                _passerBy = Object.Instantiate(civilianPrefab, body.position + side * 1.6f + forward * 0.6f, Quaternion.LookRotation(-side));
                foreach (MonoBehaviour behaviour in _passerBy.GetComponents<MonoBehaviour>())
                {
                    if (behaviour.GetType().Name == "Civilian") behaviour.enabled = false;   // stands still for the pictures
                }
            }
            yield return Frames(10);
            Vector3 feet = body.position;
            Render("size-0-normal", feet - forward * 6.0f + side * 3.0f + Vector3.up * 2.2f, feet + Vector3.up * 1.4f);

            Assert.IsTrue(size.TryGrow());
            yield return Frames(Mathf.RoundToInt(BodySize.ChangeSeconds * 60.0f * 0.45f));
            Render("size-1-growing", feet - forward * 22.0f + side * 9.0f + Vector3.up * 6.0f, feet + Vector3.up * 6.0f);
            yield return Frames(Mathf.RoundToInt(BodySize.ChangeSeconds * 60.0f * 0.6f) + 10);
            Render("size-2-giant", ClearView(feet + Vector3.up * 13.0f, 60.0f, body), feet + Vector3.up * 13.0f);
            yield return null;
            RenderThePlayersView("size-3-giant-own-view");

            // A few strides: slow, heavy steps (StrideRate), the body moving 3.2 times as fast as a walk.
            float yaw = body.eulerAngles.y;
            for (int i = 0; i < 50; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = yaw }, 1.0f / 60.0f);
                yield return null;
            }
            Vector3 striding = body.position;
            Render("size-4-giant-striding", ClearView(striding + Vector3.up * 12.0f, 50.0f, body), striding + Vector3.up * 12.0f);
            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, 1.0f / 60.0f);
                yield return null;
            }

            // Straight from a giant to a tiny body, back where the passer-by stands.
            Assert.IsTrue(size.TryShrink());
            yield return Frames(Mathf.RoundToInt(BodySize.ChangeSeconds * 60.0f) + 10);
            body.position = feet + Vector3.up * 0.02f;
            Physics.SyncTransforms();
            yield return Frames(20);
            Vector3 tiny = body.position;
            Render("size-5-tiny", tiny - forward * 1.3f + side * 0.9f + Vector3.up * 0.5f, tiny + Vector3.up * 0.35f, 0.02f);
            yield return null;
            RenderThePlayersView("size-6-tiny-own-view");

            Debug.Log($"CityX size capture: giant scale reached, then {body.localScale.y:0.000}; the giant strode " +
                $"{Vector3.Distance(new Vector3(feet.x, 0, feet.z), new Vector3(striding.x, 0, striding.z)):0.0} m in 50 frames");
#else
            yield break;
#endif
        }
    }
}
