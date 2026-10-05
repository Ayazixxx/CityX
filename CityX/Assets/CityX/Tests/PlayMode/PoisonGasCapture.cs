using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Not a test: renders the poison gas (blown out, swelling, hanging, a poisoned dummy) to Logs/CityShots/gas-*.png. Explicit; needs -Graphics.</summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PoisonGasCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 1280, h = 720;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator RenderTheGas()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            var caster = vitals.GetComponent<PoisonGasCaster>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++) yield return null;
            yield return new WaitForSeconds(0.5f);

            var cameraObject = new GameObject("GasCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.farClipPlane = 600.0f;
            camera.allowHDR = true;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;

            Transform body = vitals.transform;
            Vector3 forward = body.forward;
            Vector3 chest = body.position + Vector3.up * 1.77f;
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.transform.position = body.position + forward * 5.0f + Vector3.up * 1.0f;
            dummy.AddComponent<SparringDummy>().MaxHealth = 1000.0f;
            Physics.SyncTransforms();

            Assert.IsTrue(caster.TryCast(chest, (dummy.transform.position - chest).normalized, vitals.GetComponentsInChildren<Collider>()));
            float[] times = { 0.4f, 1.2f, 2.6f, 5.0f };
            float last = 0.0f;
            for (int i = 0; i < times.Length; i++)
            {
                yield return new WaitForSeconds(times[i] - last);
                last = times[i];
                Vector3 look = dummy.transform.position;
                Shoot(camera, directory, $"gas-{i}-side", look + body.right * 7.0f + Vector3.up * 1.2f - forward * 1.0f, look);
                Shoot(camera, directory, $"gas-{i}-behind", chest - forward * 3.5f + Vector3.up * 1.0f, look);
            }
            Debug.Log($"CityX: gas capture: dummy poisoned = {dummy.GetComponent<PoisonEffect>() != null}");
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
