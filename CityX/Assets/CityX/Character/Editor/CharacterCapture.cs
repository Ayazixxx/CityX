using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Renders the player's body close up, on a dark background, at rest and
    /// deformed.
    ///
    /// THE FRAMING MATTERS AND GETTING IT WRONG COST REAL TIME. The body's
    /// surface defects were first judged from a wide street shot where the
    /// character was a few dozen pixels tall, and reported as invisible. They
    /// are not invisible -- close up and dark they are the first thing you see.
    /// A character is looked at from a few metres away in a dim city, so that
    /// is what this renders.
    ///
    /// AND IT POSES THE RIG BY HAND. It used to call AnimationClip.
    /// SampleAnimation, which does not pose a humanoid rig in batch mode -- so
    /// every "posed" shot came out at rest and the only thing being inspected
    /// was the bind pose. That is exactly how a rig that tears passes
    /// inspection: the seams do not exist until the skin is deformed. Rotating
    /// named bones directly is cruder than an animation and has the one
    /// property that matters here, which is that it actually happens.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    /// </summary>
    public static class CharacterCapture
    {
        private const int Width = 700;
        private const int Height = 900;
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";

        /// <summary>One bone turned to a fixed local rotation.</summary>
        private readonly struct Turn
        {
            public readonly string Bone;
            public readonly Vector3 Euler;

            public Turn(string bone, float x, float y, float z)
            {
                Bone = bone;
                Euler = new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// The rig's own rest: arms down at the sides. The bind pose is a
        /// T-pose because Unity's retargeting needs one, and nobody ever sees
        /// the character in it, so it is the wrong thing to judge the body by.
        /// </summary>
        private static readonly Turn[] Rest =
        {
            new Turn("arm_l", 0.0f, 0.0f, 78.0f),
            new Turn("forearm_l", 0.0f, 0.0f, 12.0f),
            new Turn("arm_r", 0.0f, 0.0f, -78.0f),
            new Turn("forearm_r", 0.0f, 0.0f, -12.0f),
        };

        /// <summary>
        /// Mid-stride, with every joint that carries geometry bent at once.
        /// This is the shot that shows whether the skin holds together.
        /// </summary>
        private static readonly Turn[] Stride =
        {
            new Turn("spine", 0.0f, 10.0f, 0.0f),
            new Turn("arm_l", -35.0f, 0.0f, 78.0f),
            new Turn("forearm_l", -40.0f, 0.0f, 12.0f),
            new Turn("arm_r", 35.0f, 0.0f, -78.0f),
            new Turn("forearm_r", -40.0f, 0.0f, -12.0f),
            new Turn("thigh_l", -35.0f, 0.0f, 0.0f),
            new Turn("shin_l", 25.0f, 0.0f, 0.0f),
            new Turn("thigh_r", 30.0f, 0.0f, 0.0f),
            new Turn("shin_r", 45.0f, 0.0f, 0.0f),
        };

        /// <summary>
        /// A right cross: the shoulder taken through nearly a right angle with
        /// the torso twisted under it. The shoulder is where the tearing was
        /// reported, so it gets the most extreme pose in the set.
        /// </summary>
        private static readonly Turn[] Punch =
        {
            new Turn("spine", 0.0f, -30.0f, 0.0f),
            new Turn("head", 0.0f, 25.0f, 0.0f),
            new Turn("arm_r", 0.0f, -95.0f, 0.0f),
            new Turn("arm_l", -20.0f, 0.0f, 70.0f),
            new Turn("forearm_l", -75.0f, 0.0f, 12.0f),
            new Turn("thigh_l", -12.0f, 0.0f, 0.0f),
            new Turn("thigh_r", 12.0f, 0.0f, 0.0f),
        };

        /// <summary>
        /// Both arms straight up. The one pose the shoulder blend cannot hide
        /// in: the skin has to travel the whole way from lying along the torso
        /// to lying against the neck.
        /// </summary>
        private static readonly Turn[] Reach =
        {
            new Turn("arm_l", 0.0f, 0.0f, -75.0f),
            new Turn("arm_r", 0.0f, 0.0f, 75.0f),
            new Turn("head", -12.0f, 0.0f, 0.0f),
        };

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

            Shoot(prefab, outputDirectory, "body-front", 0.0f, Rest);
            Shoot(prefab, outputDirectory, "body-back", 180.0f, Rest);
            Shoot(prefab, outputDirectory, "body-stride", 215.0f, Stride);
            Shoot(prefab, outputDirectory, "body-punch", 205.0f, Punch);
            Shoot(prefab, outputDirectory, "body-reach", 200.0f, Reach);
            Shoot(prefab, outputDirectory, "body-tpose", 0.0f, null);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// Turns the named bones. A bone that is not there is reported rather
        /// than skipped: a typo would otherwise show up as a pose that quietly
        /// does less than it says.
        /// </summary>
        private static void Pose(GameObject instance, IEnumerable<Turn> turns)
        {
            if (turns == null)
            {
                return;
            }

            foreach (Turn turn in turns)
            {
                Transform bone = FindDeep(instance.transform, turn.Bone);
                if (bone == null)
                {
                    Debug.LogWarning($"CityX: no bone called '{turn.Bone}' -- that part of the " +
                                     "pose did nothing.");
                    continue;
                }
                bone.localRotation = Quaternion.Euler(turn.Euler);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            foreach (Transform child in root)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null)
                {
                    return hit;
                }
            }
            return null;
        }

        private static void Shoot(GameObject prefab, string directory, string name, float yaw,
            IEnumerable<Turn> pose)
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

                Pose(instance, pose);

                // The renderer only re-skins when it thinks something changed,
                // and outside play mode nothing tells it the bones moved.
                foreach (SkinnedMeshRenderer skin in
                    instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 32.0f;
                camera.nearClipPlane = 0.05f;

                // Framed on the whole 2.6m body from about five metres, which
                // is roughly where a third-person camera sits.
                cameraObject.transform.position = new Vector3(0.0f, 1.35f, -5.2f);
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
