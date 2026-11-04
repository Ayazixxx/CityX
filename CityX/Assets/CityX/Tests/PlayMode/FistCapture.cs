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
    /// Not a test: renders every unarmed strike on the real rig as a contact sheet (a row per punch, five moments along it) to Logs/CityShots/fist-sheet.png,
    /// seen from the side and, in fist-sheet-front.png, from the front. The body walks up, then plants its feet and punches. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FistCapture
    {
        private const int Cell = 300;

        [UnityTest]
        public IEnumerator RenderThePunches()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(60, 1, 60);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var animator = player.GetComponentInChildren<Animator>();
            var driver = player.GetComponent<PlayerAnimationDriver>();
            var fists = player.GetComponent<FistPoseDriver>();
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 1.7f;
            const float dt = 1.0f / 60.0f;

            FistKind[] kinds = { FistKind.RightStraight, FistKind.LeftStraight, FistKind.Overhand, FistKind.Uppercut };
            float[] fractions = { 0.02f, 0.22f, -1.0f, 0.66f, 0.9f };   // -1 = the contact moment; the first is the stance the blow starts from
            foreach (bool front in new[] { false, true })
            {
                var sheet = new Texture2D(Cell * fractions.Length, Cell * kinds.Length, TextureFormat.RGB24, false);
                for (int row = 0; row < kinds.Length; row++)
                {
                    FistKind kind = kinds[row];
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false; player.transform.position = new Vector3(0.0f, 0.05f, -4.0f); controller.enabled = true; Physics.SyncTransforms();
                    // Walk a little so the legs have a stride, then throw the punch while still walking at the kind's pace.
                    for (int i = 0; i < 90; i++)
                    {
                        locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f }, dt);
                        animator.Update(dt);
                        yield return null;
                    }
                    Vector3 rowOrigin = player.transform.position + player.transform.forward * 0.6f;
                    fists.Cancel();
                    fists.enabled = false;
                    fists.Begin(kind);
                    fists.enabled = false;
                    float duration = FistStrikes.Seconds(kind);
                    int col = 0;
                    float elapsed = 0.0f;
                    while (col < fractions.Length)
                    {
                        float target = (fractions[col] < 0.0f ? FistStrikes.ContactFraction(kind) : fractions[col]) * duration;
                        while (elapsed < target)
                        {
                            locomotion.Tick(new LocomotionInput { Move = Vector2.zero, YawDegrees = 0.0f }, dt);
                            animator.Update(dt);
                            fists.Step(dt);
                            fists.enabled = false;
                            elapsed += dt;
                            yield return null;
                        }
                        Vector3 p = rowOrigin;   // a fixed frame per punch, so the body moving or bobbing shows
                        Vector3 eye = front ? p + new Vector3(FistStrikes.UsesRightHand(kind) ? 5.0f : -5.0f, 1.6f, 6.0f) : p + new Vector3(7, 1.4f, 0);
                        yield return null;
                        Texture2D cell = Render(camera, eye, p + Vector3.up * 1.3f);
                        sheet.SetPixels(col * Cell, (kinds.Length - 1 - row) * Cell, Cell, Cell, cell.GetPixels());
                        Object.Destroy(cell);
                        col++;
                    }
                    // Let the stance settle before the next one.
                    for (int i = 0; i < 90; i++)
                    {
                        fists.Step(dt);
                        fists.enabled = false;
                        yield return null;
                    }
                }
                sheet.Apply();
                var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(directory, front ? "fist-sheet-front.png" : "fist-sheet.png"), (byte[])enc.Invoke(null, new object[] { sheet }));
                Object.Destroy(sheet);
            }
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
