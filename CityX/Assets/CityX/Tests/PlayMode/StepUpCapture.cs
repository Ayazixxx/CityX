using System.Collections;
using System.Collections.Generic;
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
    /// Not a test: renders the step up (PlayerLocomotion.StepUp.cs) -- a body running into a shattered shop window whose sill stands 0.55 m over
    /// the pavement under a 2.9 m opening, as in the owner's screenshot -- from the side (the near jamb left out, a cutaway) and from behind, to
    /// Logs/CityShots/step-up-side-*.png and step-up-behind-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StepUpCapture
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private void Box(Vector3 centre, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = centre;
            box.transform.localScale = size;
            _made.Add(box);
        }

        [UnityTest]
        public IEnumerator RenderTheStepUp()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(60.0f, 1.0f, 60.0f));      // the pavement
            Box(new Vector3(0.0f, 0.275f, 2.15f), new Vector3(3.0f, 0.55f, 0.3f));    // the wall under the window: the sill, 0.55 m up
            Box(new Vector3(-3.0f, 2.25f, 2.15f), new Vector3(3.0f, 4.5f, 0.3f));     // the far jamb (the near one is left out: a cutaway)
            Box(new Vector3(0.0f, 3.975f, 2.15f), new Vector3(3.0f, 1.05f, 0.3f));    // over the opening, 2.9 m above the sill
            Box(new Vector3(0.0f, 0.195f, 6.3f), new Vector3(9.0f, 0.39f, 8.0f));     // the shop's floor, 0.16 m under the sill
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50.0f, -60.0f, 0.0f);
            _made.Add(sun.gameObject);
            Physics.SyncTransforms();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, -2.5f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var body = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 20; i++)
            {
                body.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }

            var cameraObject = new GameObject("Cam");
            _made.Add(cameraObject);
            Camera side = cameraObject.AddComponent<Camera>();
            side.orthographic = true;
            side.orthographicSize = 1.9f;
            var behindObject = new GameObject("Behind");
            _made.Add(behindObject);
            Camera behind = behindObject.AddComponent<Camera>();
            behind.fieldOfView = 60.0f;

            var run = new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f, Sprint = true };
            int shot = 0;
            for (int frame = 0; frame < 90; frame++)
            {
                body.Tick(run, 1.0f / 60.0f);
                Vector3 p = player.transform.position;
                if (p.z > 0.4f && p.z < 3.4f && frame % 2 == 0 && shot < 14)
                {
                    Shoot(side, directory, $"step-up-side-{shot:00}", new Vector3(9.0f, 1.5f, 1.6f), new Vector3(0.0f, 1.5f, 1.6f));
                    Shoot(behind, directory, $"step-up-behind-{shot:00}", p + new Vector3(0.6f, 2.6f, -4.2f), p + new Vector3(0.0f, 1.3f, 2.0f));
                    Debug.Log($"CityX step-up capture {shot:00}: body at z {p.z:0.00}, y {p.y:0.00}, stepping {body.IsSteppingUp}, grounded {body.IsGrounded}");
                    shot++;
                }
                yield return null;
            }
            Assert.Greater(player.transform.position.z, 3.0f, $"the body did not get into the shop: {player.transform.position}");
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 960, h = 720;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
        }
    }
}
