using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the body's weight (MovementWeight, docs/MOVEMENT.md), seen from the side a tenth of a second apart: a sprint starting from a stand, a
    /// sprint stopping, and a sprint reversing. Written to Logs/CityShots/movement-weight-sheet.png, a row each; the speed each row reaches is logged.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.MovementWeightCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class MovementWeightCapture
    {
        private const int Cell = 220;
        private const int Columns = 7;
        private const int FramesBetween = 6;
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheWeight()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            Vector2 forward = Vector2.up, none = Vector2.zero, back = Vector2.down;
            var rows = new (bool runUp, Vector2 move)[] { (false, forward), (true, none), (true, back) };
            var sheet = new Texture2D(Cell * Columns, Cell * rows.Length, TextureFormat.RGB24, false);
            for (int row = 0; row < rows.Length; row++)
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(row * 60.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 30; i++)
                {
                    locomotion.Tick(new LocomotionInput(), Step);
                    yield return null;
                }
                for (int i = 0; rows[row].runUp && i < 90; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = forward, Sprint = true }, Step);
                    yield return null;
                }
                string speeds = "";
                for (int column = 0; column < Columns; column++)
                {
                    Vector3 p = player.transform.position;
                    Texture2D cell = Render(camera, p + new Vector3(8.0f, 1.6f, 0.0f), p + Vector3.up * 1.2f);
                    sheet.SetPixels(column * Cell, (rows.Length - 1 - row) * Cell, Cell, Cell, cell.GetPixels());
                    Object.Destroy(cell);
                    speeds += $" {locomotion.CurrentSpeed:0.0}";
                    for (int i = 0; i < FramesBetween; i++)
                    {
                        locomotion.Tick(new LocomotionInput { Move = rows[row].move, Sprint = true }, Step);
                        yield return null;
                    }
                }
                Debug.Log($"CityX movement weight row {row}: speeds{speeds}");
                Object.Destroy(player);
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "movement-weight-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
            foreach (GameObject go in new[] { ground, sun.gameObject, camera.gameObject }) Object.Destroy(go);
#else
            yield break;
#endif
        }

        private static Texture2D Render(Camera camera, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            RenderTexture rt = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return image;
        }
    }
}
