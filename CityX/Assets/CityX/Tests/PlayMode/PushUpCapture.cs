using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders /pushups from the side on the real rig -- going down, two push-ups, getting up -- to Logs/CityShots/pushup-NN.png,
    /// to see the body lie on the floor with its hands and feet down (not sunk into it, not floating). Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class PushUpCapture
    {
        [UnityTest]
        public IEnumerator RenderThePushUps()
        {
#if UNITY_EDITOR
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(30.0f, 1.0f, 30.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50.0f, -40.0f, 0.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            var reader = player.GetComponent<PlayerInputReader>();
            reader.enabled = false;
            var body = player.GetComponent<PlayerLocomotion>();
            PushUpEmote emote = reader.PushUps;
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            cameraObject.transform.position = new Vector3(8.0f, 1.0f, 0.4f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    body.Tick(new LocomotionInput { YawDegrees = 0.0f }, 1.0f / 60.0f);
                    yield return null;
                }
                int shot = 0;
                float next = 0.0f;
                float total = emote.EnterSeconds + emote.RepSeconds * 2.0f + emote.ExitSeconds + 0.4f;
                for (float t = 0.0f; t < total; t += 1.0f / 60.0f)
                {
                    int phase = t < emote.EnterSeconds ? 1 : t < emote.EnterSeconds + emote.RepSeconds * 2.0f ? 2 : t < total - 0.4f ? 3 : 0;
                    body.Tick(new LocomotionInput { YawDegrees = 0.0f, PushUpPhase = phase }, 1.0f / 60.0f);
                    yield return null;
                    if (t >= next)
                    {
                        next += 0.3f;
                        Shoot(camera, Path.Combine(directory, $"pushup-{shot++:00}.png"));
                        Animator a = player.GetComponentInChildren<Animator>();
                        float Y(HumanBodyBones bone) => a.GetBoneTransform(bone) != null ? a.GetBoneTransform(bone).position.y : float.NaN;
                        Debug.Log($"CityX push-up shot {shot - 1:00} (phase {phase}): hands {Y(HumanBodyBones.LeftHand):0.00}/{Y(HumanBodyBones.RightHand):0.00}, " +
                                  $"feet {Y(HumanBodyBones.LeftFoot):0.00}/{Y(HumanBodyBones.RightFoot):0.00}, shoulders {Y(HumanBodyBones.LeftUpperArm):0.00}, hips {Y(HumanBodyBones.Hips):0.00}");
                    }
                }
                Debug.Log($"CityX push-ups: enter {emote.EnterSeconds:0.00} s, rep {emote.RepSeconds:0.00} s, exit {emote.ExitSeconds:0.00} s; {shot} shots");
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(player);
                Object.Destroy(ground);
                Object.Destroy(sun.gameObject);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string path)
        {
            const int w = 640, h = 400;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(path, (byte[])enc.Invoke(null, new object[] { image }));
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
        }
    }
}
