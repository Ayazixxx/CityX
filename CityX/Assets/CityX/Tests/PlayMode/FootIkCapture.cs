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
    /// Not a test: renders the foot placement on the real rig to Logs/CityShots/footik-stairs.png (walking up stairs, side view: the top row with the
    /// placement on, the bottom row with it off) and footik-slope.png (standing and walking across a hillside, from the front, the same two rows).
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FootIkCapture
    {
        private const int Cell = 300;
        private const int Columns = 7;

        [UnityTest]
        public IEnumerator RenderStairsAndSlope()
        {
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(40, 30, 0); sun.intensity = 1.3f;
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.9f; camera.backgroundColor = new Color(0.55f, 0.65f, 0.8f); camera.clearFlags = CameraClearFlags.SolidColor;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Time.captureFramerate = 60;
            const float dt = 1.0f / 60.0f;

            // STAIRS, from the side.
            {
                var made = new System.Collections.Generic.List<GameObject>();
                GameObject Box(Vector3 c, Vector3 s)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.position = c; go.transform.localScale = s;
                    go.GetComponent<Renderer>().material.color = new Color(0.6f, 0.58f, 0.55f);
                    made.Add(go);
                    return go;
                }
                Box(new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
                for (int i = 0; i < 12; i++) Box(new Vector3(0.0f, 0.125f * (i + 1), 1.2f + 0.4f * i), new Vector3(6.0f, 0.25f * (i + 1), 0.4f));
                Physics.SyncTransforms();
                var sheet = new Texture2D(Cell * Columns, Cell * 2, TextureFormat.RGB24, false);
                for (int row = 0; row < 2; row++)
                {
                    var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
                    var locomotion = player.GetComponent<PlayerLocomotion>();
                    locomotion.FootIkEnabled = row == 0;
                    yield return null; yield return null;
                    int col = 0;
                    for (int i = 0; i < 160 && col < Columns; i++)
                    {
                        locomotion.Tick(new LocomotionInput { Move = Vector2.up }, dt);
                        yield return null;
                        if (i >= 40 && (i - 40) % 16 == 0)
                        {
                            Vector3 p = player.transform.position;
                            Texture2D cell = Render(camera, p + new Vector3(6.0f, 1.2f, 0.0f), p + Vector3.up * 1.2f);
                            sheet.SetPixels(col * Cell, (1 - row) * Cell, Cell, Cell, cell.GetPixels());
                            Object.Destroy(cell);
                            col++;
                        }
                    }
                    Object.Destroy(player);
                    yield return null;
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(directory, "footik-stairs.png"), Encode(sheet));
                Object.Destroy(sheet);
                foreach (GameObject go in made) Object.Destroy(go);
                yield return null;
            }

            // THE SLOPE, from the front: standing across it, then walking along it.
            {
                var slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slope.transform.position = Vector3.zero; slope.transform.localScale = new Vector3(40, 1, 40); slope.transform.rotation = Quaternion.Euler(0, 0, 22);
                slope.GetComponent<Renderer>().material.color = new Color(0.45f, 0.6f, 0.4f);
                Physics.SyncTransforms();
                var sheet = new Texture2D(Cell * Columns, Cell * 2, TextureFormat.RGB24, false);
                for (int row = 0; row < 2; row++)
                {
                    var player = Object.Instantiate(prefab, new Vector3(0, 0.8f, 0), Quaternion.identity);
                    var locomotion = player.GetComponent<PlayerLocomotion>();
                    locomotion.FootIkEnabled = row == 0;
                    yield return null; yield return null;
                    int col = 0;
                    for (int i = 0; i < 200 && col < Columns; i++)
                    {
                        locomotion.Tick(i < 90 ? default : new LocomotionInput { Move = Vector2.up }, dt);
                        yield return null;
                        if (i >= 60 && (i - 60) % 20 == 0)
                        {
                            Vector3 p = player.transform.position;
                            Texture2D cell = Render(camera, p + new Vector3(0.0f, 1.2f, -6.0f), p + Vector3.up * 1.2f);
                            sheet.SetPixels(col * Cell, (1 - row) * Cell, Cell, Cell, cell.GetPixels());
                            Object.Destroy(cell);
                            col++;
                        }
                    }
                    Object.Destroy(player);
                    yield return null;
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(directory, "footik-slope.png"), Encode(sheet));
                Object.Destroy(sheet);
                Object.Destroy(slope);
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
#else
            yield break;
#endif
        }

        private static byte[] Encode(Texture2D sheet)
        {
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            return (byte[])enc.Invoke(null, new object[] { sheet });
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
