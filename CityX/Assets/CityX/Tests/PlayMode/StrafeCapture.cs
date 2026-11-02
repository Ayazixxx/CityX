using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: every side-step and diagonal the body plays while it faces the camera (locked on or aiming; docs/MOVEMENT.md), seen from the
    /// front at six moments of a stride, one row each: bare-handed right, bare-handed forward-right, one-handed sword, two-handed sword, a gun
    /// aimed. Written to Logs/CityShots/strafe-sheet.png. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.StrafeCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StrafeCapture
    {
        private const int Cell = 256;
        private const int Columns = 6;
        private const int FramesBetween = 6;
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheSideSteps()
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

            var rows = new (Vector2 move, LocomotionInput flags)[]
            {
                (Vector2.right, new LocomotionInput { LockedOn = true }),
                (new Vector2(0.7071f, 0.7071f), new LocomotionInput { LockedOn = true }),
                (Vector2.right, new LocomotionInput { LockedOn = true, Sword1HEquipped = true }),
                (Vector2.right, new LocomotionInput { LockedOn = true, SwordEquipped = true }),
                (Vector2.right, new LocomotionInput { Aiming = true, GunEquipped = true }),
            };
            var sheet = new Texture2D(Cell * Columns, Cell * rows.Length, TextureFormat.RGB24, false);
            for (int row = 0; row < rows.Length; row++)
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(row * 40.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                LocomotionInput input = rows[row].flags;
                input.Move = rows[row].move;
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(input, Step);
                    yield return null;
                }
                for (int column = 0; column < Columns; column++)
                {
                    for (int i = 0; i < FramesBetween; i++)
                    {
                        locomotion.Tick(input, Step);
                        yield return null;
                    }
                    Vector3 p = player.transform.position;
                    Texture2D cell = Render(camera, p + new Vector3(0.0f, 1.6f, 6.5f), p + Vector3.up * 1.2f);
                    sheet.SetPixels(column * Cell, (rows.Length - 1 - row) * Cell, Cell, Cell, cell.GetPixels());
                    Object.Destroy(cell);
                }
                Object.Destroy(player);
            }
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, "strafe-sheet.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
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
