using System.IO;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Renders a camera to a PNG under Logs/CityShots, for the Explicit *Capture tests that are looked at rather than asserted on. Shared, so a
    /// new capture does not carry its own copy of the render-and-save code (older captures still do: docs/CODE_STYLE.md, known debts).
    /// </summary>
    internal static class CaptureShots
    {
        public static string Folder
        {
            get
            {
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>Puts <paramref name="camera"/> at <paramref name="eye"/> looking at <paramref name="lookAt"/> and saves what it sees as name.png.</summary>
        public static void Shoot(Camera camera, string name, Vector3 eye, Vector3 lookAt, int width = 1024, int height = 768)
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(lookAt - eye, Vector3.up));
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
                // This assembly has no reference to the image-conversion module, so EncodeToPNG is asked for by name (the editor always has it).
                System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
                Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
