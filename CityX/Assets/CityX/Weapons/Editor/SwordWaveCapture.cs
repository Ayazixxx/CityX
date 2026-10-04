using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>Renders the sword wave prefab from the front and the side on a dark ground to Logs/CityShots/sword-wave-*.png (needs a graphics device).</summary>
    public static class SwordWaveCapture
    {
        [MenuItem("CityX/Capture Sword Wave")]
        public static void Capture()
        {
            GameObject prefab = SwordWaveBuilder.Build();
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var rig = new GameObject("WaveCaptureRig");
            try
            {
                GameObject wave = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                wave.transform.SetParent(rig.transform, false);
                wave.transform.position = Vector3.zero;
                Object.DestroyImmediate(wave.GetComponent<Rigidbody>());

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(rig.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
                camera.allowHDR = true;
                camera.nearClipPlane = 0.1f;

                Shoot(camera, directory, "sword-wave-front", new Vector3(0.0f, 0.3f, 7.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                Shoot(camera, directory, "sword-wave-side", new Vector3(6.0f, 0.2f, -0.5f), Quaternion.Euler(0.0f, -90.0f, 0.0f));
                Shoot(camera, directory, "sword-wave-quarter", new Vector3(4.5f, 1.0f, 5.0f), Quaternion.LookRotation(new Vector3(-4.5f, -0.8f, -5.0f)));
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Quaternion rotation)
        {
            camera.transform.position = position;
            camera.transform.rotation = rotation;
            const int width = 1024, height = 640;
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
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
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
