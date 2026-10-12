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
    /// <summary>
    /// Not a test: renders the wall climb on the real rig as a contact sheet to Logs/CityShots/climb-sheet.png (side view, a frame every 0.12 s while climbing up, then a
    /// few climbing down) and climb-sheet-front.png (from in front of the wall, three-quarter). The camera follows the body. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ClimbCapture
    {
        private const int Cell = 260;
        private const int Columns = 8;

        [UnityTest]
        public IEnumerator RenderTheClimb()
        {
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 20.0f, 2.0f); wall.transform.localScale = new Vector3(20, 40, 2);   // near face z = 1
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(35, 25, 0); sun.intensity = 1.4f;
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0.3f), Quaternion.identity);
            var climbing = player.GetComponent<PlayerClimbing>();
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            var animator = player.GetComponentInChildren<Animator>();
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            Time.captureFramerate = 60;   // every frame is exactly 1/60 s, so the Animator and the limb IK see the same time the climb is driven by
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.7f;
            const float dt = 1.0f / 60.0f;

            foreach (bool front in new[] { false, true })
            {
                var sheet = new Texture2D(Cell * Columns, Cell * 2, TextureFormat.RGB24, false);
                int col = 0;
                float sinceShot = 0.0f;
                for (int i = 0; i < 700 && col < Columns * 2; i++)
                {
                    bool up = i < 330;
                    climbing.Tick(false, up, !up, vitals, dt);
                    sinceShot += dt;
                    yield return null;
                    if (i > 40 && sinceShot >= 0.12f)
                    {
                        sinceShot = 0.0f;
                        Vector3 p = player.transform.position;
                        Vector3 eye = front ? p + new Vector3(3.5f, 1.4f, -4.5f) : p + new Vector3(7.0f, 1.3f, 0.3f);
                        Texture2D cell = Render(camera, eye, p + Vector3.up * 1.3f);
                        int row = col / Columns, c = col % Columns;
                        sheet.SetPixels(c * Cell, (1 - row) * Cell, Cell, Cell, cell.GetPixels());
                        Object.Destroy(cell);
                        col++;
                    }
                }
                sheet.Apply();
                var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(directory, front ? "climb-sheet-front.png" : "climb-sheet.png"), (byte[])enc.Invoke(null, new object[] { sheet }));
                Object.Destroy(sheet);
                // Back to the ground for the second pass.
                if (!front)
                {
                    for (int i = 0; i < 700 && climbing.IsClimbing; i++)
                    {
                        climbing.Tick(false, false, true, vitals, dt);
                        yield return null;
                    }
                    var cc = player.GetComponent<CharacterController>();
                    cc.enabled = false; player.transform.position = new Vector3(0, 0.05f, 0.3f); player.transform.rotation = Quaternion.identity; cc.enabled = true;
                    Physics.SyncTransforms();
                    yield return null;
                    Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
                }
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        private static Texture2D Render(Camera camera, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            RenderTexture rt = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); image.Apply();
            camera.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
            return image;
        }
    }
}
