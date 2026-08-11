using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Renders the player's body close up, front and back, on a dark
    /// background.
    ///
    /// THE FRAMING MATTERS AND GETTING IT WRONG COST REAL TIME. The body's
    /// surface defects were first judged from a wide street shot where the
    /// character was a few dozen pixels tall, and reported as invisible. They
    /// are not invisible -- close up and dark they are the first thing you see.
    /// A character is looked at from a few metres away in a dim city, so that
    /// is what this renders.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    /// </summary>
    public static class CharacterCapture
    {
        private const int Width = 700;
        private const int Height = 900;
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";

        [MenuItem("CityX/Capture Character Screenshots")]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"CityX: no player prefab at {PrefabPath}.");
                return;
            }

            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Shoot(prefab, outputDirectory, "body-front", 0.0f, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-back", 180.0f, null, 0.0f);

            // POSED, NOT JUST AT REST. The seams that were reported are not
            // visible in the bind pose at all -- they open up when the skin is
            // DEFORMED, because that is when neighbouring vertices assigned to
            // different bones get pulled apart. Rendering only the rest pose is
            // how a torn rig passes inspection.
            AnimationClip punch = FindClip("boxing_right_jab");
            AnimationClip walk = FindClip("walking");
            Shoot(prefab, outputDirectory, "body-punch", 200.0f, punch, 0.45f);
            Shoot(prefab, outputDirectory, "body-walk", 210.0f, walk, 0.30f);
        }

        /// <summary>
        /// Pulls the animation clip out of a model file, skipping Unity's own
        /// __preview__ duplicates.
        /// </summary>
        private static AnimationClip FindClip(string modelName)
        {
            string path = $"Assets/CityX/Character/Animations/{modelName}.fbx";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            Debug.LogWarning($"CityX: no clip in {path} -- that pose will render at rest.");
            return null;
        }

        private static void Shoot(GameObject prefab, string directory, string name, float yaw,
            AnimationClip pose, float poseTime)
        {
            var root = new GameObject("CharacterCaptureRig");
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;

            var previousAmbient = RenderSettings.ambientMode;
            var previousSky = RenderSettings.ambientSkyColor;
            bool previousFog = RenderSettings.fog;

            try
            {
                RenderSettings.fog = false;
                // Roughly the city's own night fill, so the body is judged
                // under the light it will actually be seen in.
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.40f);

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.0f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f + yaw, 0.0f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root.transform);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);

                if (pose != null)
                {
                    // SampleAnimation poses the rig without entering play mode,
                    // which is the only way to see the skin deformed from a
                    // batch run.
                    pose.SampleAnimation(instance, poseTime * pose.length);
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 32.0f;
                camera.nearClipPlane = 0.05f;

                // Framed on the whole body from about four metres, which is
                // roughly where a third-person camera sits.
                cameraObject.transform.position = new Vector3(0.0f, 1.35f, -4.2f);
                cameraObject.transform.rotation = Quaternion.identity;

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 8,
                };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                string path = Path.Combine(directory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);

                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderSettings.ambientMode = previousAmbient;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.fog = previousFog;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(root);
            }
        }
    }
}
