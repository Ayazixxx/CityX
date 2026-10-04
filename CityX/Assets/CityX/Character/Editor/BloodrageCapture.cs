using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>Renders the Bloodrage aura round a stand-in body (the real player prefab) to Logs/CityShots/bloodrage-*.png. Needs a graphics device.</summary>
    public static class BloodrageCapture
    {
        [MenuItem("CityX/Capture Bloodrage Aura")]
        public static void Capture()
        {
            GameObject aura = BloodrageAuraBuilder.Build();
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);

            var rig = new GameObject("BloodrageCaptureRig");
            try
            {
                GameObject body = player != null ? (GameObject)PrefabUtility.InstantiatePrefab(player) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(rig.transform, false);
                body.transform.position = Vector3.zero;
                foreach (var behaviour in body.GetComponentsInChildren<MonoBehaviour>())
                {
                    behaviour.enabled = false;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(aura);
                instance.transform.SetParent(body.transform, false);
                foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    particles.Simulate(2.0f, true, true);
                }

                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.transform.SetParent(rig.transform, false);
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(40.0f, 30.0f, 0.0f);
                sun.intensity = 0.5f;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(rig.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
                camera.allowHDR = true;

                Shoot(camera, directory, "bloodrage-front", new Vector3(0.0f, 1.4f, 6.5f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                Shoot(camera, directory, "bloodrage-side", new Vector3(6.5f, 1.4f, 0.0f), Quaternion.Euler(0.0f, -90.0f, 0.0f));
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>The player body posed mid-stride in the ordinary sprint and in the Bloodrage run, from the side, to compare the head.</summary>
        [MenuItem("CityX/Capture Bloodrage Run Pose")]
        public static void CaptureRunPose()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var rig = new GameObject("BloodrageRunCaptureRig");
            try
            {
                GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(player);
                body.transform.SetParent(rig.transform, false);
                foreach (var behaviour in body.GetComponentsInChildren<MonoBehaviour>())
                {
                    behaviour.enabled = false;
                }
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.transform.SetParent(rig.transform, false);
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(40.0f, 130.0f, 0.0f);
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(rig.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
                foreach (string name in new[] { "sprint_stride", "bloodrage_run_stride" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/" + name + ".anim");
                    BloodrageHeadFit.Pose(body.GetComponentInChildren<Animator>(), clip, clip.length * 0.25f);
                    Shoot(camera, directory, "pose-" + name, new Vector3(6.0f, 1.3f, 0.0f), Quaternion.Euler(0.0f, -90.0f, 0.0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Quaternion rotation)
        {
            camera.transform.position = position;
            camera.transform.rotation = rotation;
            const int width = 1024, height = 768;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
