using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the sword spin in the real scene, from the side and from above, to Logs/CityShots/spin-*.png so the pose and the
    /// swoosh can be LOOKED AT (green tests are no proof that it looks right). Explicit, so the suite skips it; run it by name with
    /// -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordSpinCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordSpinCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private void Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int width = 1024, height = 768;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                // This assembly has no reference to the image-conversion module, so ask for EncodeToPNG by name (the editor always has it).
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(_directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RenderTheSpin()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            var melee = vitals.GetComponent<MeleeAttacker>();
            Inventory inventory = vitals.GetComponent<Inventory>();
            inventory.AddItem(ItemRegistry.Sword, 1);
            inventory.RemoveFromStorage(ItemRegistry.Sword, 1, out ItemInstanceData data);
            inventory.EquipItemId(ItemRegistry.Sword, vitals.gameObject, data);
            vitals.InfiniteHealth = true;
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.5f);

            var cameraObject = new GameObject("SpinCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600.0f;
            camera.allowHDR = true;
            Transform body = vitals.transform;

            // ---- charging on the ground: the sword drawn back ----
            Press(_keyboard.digit2Key);
            yield return Seconds(0.45f);
            {
                Vector3 centre = body.position + Vector3.up * 1.6f;
                Shoot(camera, "spin-charge-side", centre + new Vector3(0.0f, 0.6f, -7.5f), centre);
                Shoot(camera, "spin-charge-top", centre + new Vector3(0.0f, 8.0f, -3.5f), centre);
            }
            Release(_keyboard.digit2Key);
            yield return Seconds(1.6f);

            // ---- on the ground: tap-spin, shoot at moments through it ----
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            float[] groundTimes = { 0.12f, 0.28f, 0.45f, 0.62f, 0.85f };
            float last = 0.0f;
            for (int i = 0; i < groundTimes.Length; i++)
            {
                yield return Seconds(groundTimes[i] - last);
                last = groundTimes[i];
                Vector3 centre = body.position + Vector3.up * 1.6f;
                Shoot(camera, $"spin-ground-{i}-side", centre + new Vector3(0.0f, 0.6f, -7.5f), centre);
                Shoot(camera, $"spin-ground-{i}-top", centre + new Vector3(0.0f, 8.0f, -3.5f), centre);
            }
            yield return Seconds(1.0f);

            // ---- in the air ----
            float groundY = body.position.y;
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            while (body.position.y - groundY < 0.6f)
            {
                yield return null;
            }
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            float[] airTimes = { 0.10f, 0.25f, 0.45f, 0.65f, 0.85f, 1.2f };
            last = 0.0f;
            for (int i = 0; i < airTimes.Length; i++)
            {
                yield return Seconds(airTimes[i] - last);
                last = airTimes[i];
                Vector3 centre = body.position + Vector3.up * 1.6f;
                Shoot(camera, $"spin-air-{i}-side", centre + new Vector3(0.0f, 0.6f, -8.5f), centre);
                Shoot(camera, $"spin-air-{i}-top", centre + new Vector3(0.0f, 8.0f, -3.5f), centre);
            }
            Debug.Log($"CityX: spin capture wrote to {_directory}; kind {melee.CurrentSpinKind}, pose blend {locomotion.SwordSpinPoseBlend:0.00}");
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
