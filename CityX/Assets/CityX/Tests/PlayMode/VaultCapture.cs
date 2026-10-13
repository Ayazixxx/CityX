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
    /// <summary>Not a test: renders a running vault over a fence (real rig) to Logs/CityShots/vault-*.png. Explicit; needs -Graphics.</summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class VaultCapture
    {
        [UnityTest]
        public IEnumerator RenderTheVault()
        {
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 0.7f, 1.2f); wall.transform.localScale = new Vector3(4, 1.4f, 0.4f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0.2f), Quaternion.identity);
            var vaulting = player.GetComponent<PlayerVaulting>() ?? player.AddComponent<PlayerVaulting>();
            yield return null; yield return null;
            Assert.IsTrue(vaulting.TryStart(true, true, true, 5.0f, 1.0f / 60.0f));

            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.8f;
            int shot = 0;
            float nextShotAt = 0.0f;
            const float dt = 1.0f / 60.0f;
            for (int i = 0; i < 300 && vaulting.IsVaulting; i++)
            {
                vaulting.Tick(dt);
                if (vaulting.Progress >= nextShotAt && shot < 8)
                {
                    Shoot(camera, directory, $"vault-{shot++}", player.transform.position + new Vector3(9, 1.2f, 0.2f), player.transform.position + Vector3.up * 1.3f);
                    nextShotAt += 0.12f;
                }
                yield return null;
            }

            // THE WINDOW DIVE: the same wall with a window in it (sill 0.9, lintel 2.76, the glass gone), run at from outside.
            Object.Destroy(wall);
            var sillWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sillWall.transform.position = new Vector3(0, 0.45f, 1.2f); sillWall.transform.localScale = new Vector3(6, 0.9f, 0.4f);
            var lintelWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintelWall.transform.position = new Vector3(0, 2.76f + 1.5f, 1.2f); lintelWall.transform.localScale = new Vector3(6, 3.0f, 0.4f);
            Physics.SyncTransforms();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false; player.transform.position = new Vector3(0, 0.05f, 0.2f); player.transform.rotation = Quaternion.identity; cc.enabled = true;
            Physics.SyncTransforms();
            yield return null; yield return null;
            // The fence vault's own short cooldown has to run out first (TryStart counts it down).
            bool started = false;
            for (int i = 0; i < 60 && !started; i++)
            {
                started = vaulting.TryStart(true, true, true, 5.0f, 1.0f / 60.0f);
            }
            Assert.IsTrue(started, "the window was not vaultable");
            shot = 0;
            nextShotAt = 0.0f;
            for (int i = 0; i < 300 && vaulting.IsVaulting; i++)
            {
                vaulting.Tick(dt);
                if (vaulting.Progress >= nextShotAt && shot < 8)
                {
                    Shoot(camera, directory, $"vault-window-{shot++}", player.transform.position + new Vector3(9, 1.4f, 0.2f), player.transform.position + Vector3.up * 1.3f);
                    nextShotAt += 0.12f;
                }
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
