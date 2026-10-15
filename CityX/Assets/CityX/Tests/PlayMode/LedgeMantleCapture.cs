using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>Not a test: renders the climb up to a ledge and the mantle over it (real rig) to Logs/CityShots/mantle-*.png. Explicit; needs -Graphics.</summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class LedgeMantleCapture
    {
        [UnityTest]
        public IEnumerator RenderTheMantle()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 2.0f, 2.0f); wall.transform.localScale = new Vector3(10, 4, 2);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0.3f), Quaternion.identity);
            var climbing = player.GetComponent<PlayerClimbing>();
            var vitals = player.GetComponent<PlayerVitals>();
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55;
            int shot = 0;
            float nextShotAtProgress = 0.0f;
            const float dt = 1.0f / 60.0f;
            for (int i = 0; i < 1200; i++)
            {
                if (climbing.IsClimbing)
                {
                    climbing.Tick(false, true, false, vitals, dt);
                    if (i % 40 == 0 && shot < 2) Shoot(camera, directory, $"mantle-climb-{shot++}", player.transform.position + new Vector3(5, 1.6f, -1.5f), player.transform.position + Vector3.up * 1.6f + Vector3.forward);
                }
                else if (climbing.IsToppingOut)
                {
                    climbing.TickMantle(dt);
                    if (climbing.MantleProgress >= nextShotAtProgress && shot < 8)
                    {
                        Shoot(camera, directory, $"mantle-{shot++}", player.transform.position + new Vector3(5, 1.2f, -0.5f), player.transform.position + Vector3.up * 1.3f + Vector3.forward);
                        nextShotAtProgress += 0.2f;
                    }
                }
                else if (i > 5) break;
                yield return null;
            }
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 1024, h = 768;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0); image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(image);
        }
    }
}
