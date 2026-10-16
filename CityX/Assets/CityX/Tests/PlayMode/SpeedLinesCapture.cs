using System.Collections;
using System.IO;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the flying speed lines at full speed and at three quarters, against a plain sky over a grey ground, to
    /// Logs/CityShots/speed-lines-*.png (owner, 2026-10-15: "make the lines ... a little more subtle"). Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SpeedLinesCapture
    {
        [UnityTest]
        public IEnumerator RenderTheSpeedLines()
        {
            CaptureGuard.NeedsGraphics();
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45.0f, 30.0f, 0.0f);
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.6f, 0.8f);
            camera.fieldOfView = 70.0f;
            cameraObject.transform.position = new Vector3(0.0f, 40.0f, 0.0f);
            cameraObject.transform.rotation = Quaternion.Euler(8.0f, 0.0f, 0.0f);
            JetpackSpeedLines lines = JetpackSpeedLines.Attach(cameraObject.transform);
            try
            {
                // Against the sky, where they should all but vanish, and against the dark of a street canyon, where they should read as air going past.
                var sky = new Color(0.45f, 0.6f, 0.8f);
                var dark = new Color(0.10f, 0.11f, 0.13f);
                foreach ((string label, float fraction, Color background) in new[] { ("full", 1.0f, sky), ("three-quarters", 0.75f, sky), ("full-dark", 1.0f, dark) })
                {
                    camera.backgroundColor = background;
                    Vector3 velocity = cameraObject.transform.forward * Mathf.Lerp(20.0f, 60.0f, fraction);
                    for (int frame = 0; frame < 90; frame++)
                    {
                        lines.Set(fraction, velocity);
                        cameraObject.transform.position += velocity / 60.0f;
                        yield return null;
                    }
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
                    File.WriteAllBytes(Path.Combine(directory, $"speed-lines-{label}.png"), (byte[])enc.Invoke(null, new object[] { image }));
                    camera.targetTexture = null;
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(rt);
                    Object.Destroy(image);
                    Debug.Log($"CityX speed lines ({label}): {lines.LiveCount} streaks alive");
                }
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(ground);
                Object.Destroy(sun.gameObject);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }
    }
}
