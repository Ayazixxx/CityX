using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player body in the skeleton's own T-pose (what the character creator shows), front and three-quarter, to
    /// Logs/CityShots/tpose-*.png. The view where a skin blended across the shoulders stretched worst. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TPoseCapture
    {
        [UnityTest]
        public IEnumerator RenderTheTPose()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0, 1000, 0), Quaternion.identity);
            foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            foreach (Animator animator in player.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (Rigidbody body in player.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            var lightObject = new GameObject("Key");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightObject.transform.rotation = Quaternion.Euler(35, 150, 0);
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.45f);
            yield return null;

            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            camera.fieldOfView = 35;
            Vector3 centre = player.transform.position + Vector3.up * 1.4f;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            foreach ((string name, float yaw) in new[] { ("front", 180.0f), ("three-quarter", 145.0f), ("back", 0.0f) })
            {
                camera.transform.position = centre + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 7.5f;
                camera.transform.LookAt(centre);
                RenderTexture rt = RenderTexture.GetTemporary(900, 700, 24);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(900, 700, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 700), 0, 0);
                image.Apply();
                var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(directory, $"tpose-{name}.png"), (byte[])enc.Invoke(null, new object[] { image }));
                camera.targetTexture = null;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(image);
            }
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
            Object.Destroy(player);
#else
            yield break;
#endif
        }
    }
}
