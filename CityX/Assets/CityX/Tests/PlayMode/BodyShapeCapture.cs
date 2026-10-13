using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player body in several poses from several sides, and close-ups of a hand, a foot, the hips and the neck, to
    /// Logs/CityShots/body-*.png. What to look at after any change to FusedBodyShape or FusedBodyBuilder: that the joints are smooth, the
    /// fingers are fingers, the feet stand flat and the head sits on its neck. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BodyShapeCapture
    {
        [UnityTest]
        public IEnumerator RenderTheBody()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, new Vector3(0, 1000, 0), Quaternion.identity);
            foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            foreach (Rigidbody body in player.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
            Animator animator = player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            var lightObject = new GameObject("Key");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(35, 150, 0);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.5f;
            fill.transform.rotation = Quaternion.Euler(20, -40, 0);
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.40f);
            yield return null;

            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            Vector3 origin = player.transform.position;

            var idle = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/Godot/godot_idle.anim");
            var run = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/Godot/godot_run.anim");
            Transform armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            foreach (string pose in new[] { "tpose", "idle", "run", "arms-up", "arms-forward" })
            {
                // Back to the skeleton's own T-pose, then the pose.
                animator.enabled = true;
                animator.Rebind();
                animator.enabled = false;
                switch (pose)
                {
                    case "idle": if (idle != null) idle.SampleAnimation(animator.gameObject, 0.99f); break;
                    case "run": if (run != null) run.SampleAnimation(animator.gameObject, run.length * 0.3f); break;
                    case "arms-up":
                        armL.localRotation = Quaternion.Euler(0, 0, -90);
                        armR.localRotation = Quaternion.Euler(0, 0, 90);
                        break;
                    case "arms-forward":
                        armL.localRotation = Quaternion.Euler(0, 90, 0);
                        armR.localRotation = Quaternion.Euler(0, -90, 0);
                        break;
                }
                // The shared posture over the sampled pose, as the game lays it over the Animator's (the scripts are off here).
                if (pose == "idle" || pose == "run")
                {
                    player.GetComponentInChildren<CityX.Character.BodyPosture>(true)?.Apply();
                }
                yield return null;
                Vector3 centre = origin + Vector3.up * 1.35f;
                camera.fieldOfView = 32;
                foreach ((string view, float yaw) in new[] { ("front", 180.0f), ("three-quarter", 140.0f), ("side", 90.0f), ("back", 0.0f) })
                {
                    Vector3 eye = centre + Quaternion.Euler(0, yaw, 0) * Vector3.forward * 6.2f + Vector3.up * 0.3f;
                    Shoot(camera, directory, $"body-{pose}-{view}", eye, centre);
                }
                if (pose == "idle")
                {
                    camera.fieldOfView = 28;
                    Vector3 handL = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    Shoot(camera, directory, "body-closeup-hand-front", handL + new Vector3(-0.35f, -0.15f, 1.0f), handL + Vector3.down * 0.12f);
                    Shoot(camera, directory, "body-closeup-hand-side", handL + new Vector3(-1.0f, -0.1f, 0.25f), handL + Vector3.down * 0.12f);
                    Shoot(camera, directory, "body-closeup-hand-back", handL + new Vector3(-0.3f, 0.0f, -1.0f), handL + Vector3.down * 0.12f);
                    Vector3 footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Shoot(camera, directory, "body-closeup-feet", footL + new Vector3(-0.9f, 0.45f, 1.1f), footL + new Vector3(0.17f, -0.05f, 0.05f));
                    Shoot(camera, directory, "body-closeup-hips", origin + new Vector3(-1.4f, 1.1f, 1.6f), origin + new Vector3(0.0f, 0.85f, 0.0f));
                    Shoot(camera, directory, "body-closeup-hips-back", origin + new Vector3(1.4f, 1.1f, -1.6f), origin + new Vector3(0.0f, 0.85f, 0.0f));
                    Shoot(camera, directory, "body-closeup-shoulders", origin + new Vector3(-1.5f, 2.3f, 1.7f), origin + new Vector3(0.0f, 1.95f, 0.0f));
                    Shoot(camera, directory, "body-closeup-neck-side", origin + new Vector3(2.2f, 2.1f, 0.0f), origin + new Vector3(0.0f, 1.95f, 0.0f));
                    Shoot(camera, directory, "body-closeup-elbow", origin + new Vector3(-1.2f, 1.6f, 1.3f), origin + new Vector3(-0.45f, 1.42f, 0.0f));
                }
            }
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
            Object.Destroy(fill.gameObject);
            Object.Destroy(player);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 eye, Vector3 lookAt)
        {
            camera.transform.position = eye;
            camera.transform.LookAt(lookAt);
            const int w = 1000, h = 800;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);
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
