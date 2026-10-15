using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>Not a test: renders the real scene from street level at several times of day to Logs/CityShots/light-*.png. Explicit; needs -Graphics.</summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class LightingCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheLighting()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++) yield return null;
            yield return new WaitForSeconds(0.5f);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            cycle.Running = false;
            BuildingShapeCapture.UseTheScenesAir();

            var cameraObject = new GameObject("LightCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.farClipPlane = 800.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            float[] times = { SkyCycle.StartSeconds, SkyCycle.DaySeconds * 0.5f, SkyCycle.DaySeconds * 0.92f, SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.4f };
            string[] names = { "morning", "noon", "dusk", "night" };
            Vector3 eye = vitals.transform.position + new Vector3(0.0f, 2.2f, 0.0f);
            for (int i = 0; i < times.Length; i++)
            {
                cycle.TimeOfDay = times[i];
                cycle.Apply(SkyCycle.At(times[i]));
                UnityEngine.DynamicGI.UpdateEnvironment();
                yield return new WaitForSeconds(0.3f);
                for (int view = 0; view < 2; view++)
                {
                    camera.transform.position = eye;
                    camera.transform.rotation = Quaternion.Euler(8.0f, view * 150.0f + 20.0f, 0.0f);
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
                    File.WriteAllBytes(Path.Combine(directory, $"light-{names[i]}-{view}.png"), (byte[])enc.Invoke(null, new object[] { image }));
                    camera.targetTexture = null;
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    Object.Destroy(image);
                }
            }
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
