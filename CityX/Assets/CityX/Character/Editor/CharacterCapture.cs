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

            Shoot(prefab, outputDirectory, "body-front", 0.0f, Rest, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-back", 180.0f, Rest, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-stride", 215.0f, Stride, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-punch", 205.0f, Punch, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-reach", 200.0f, Reach, null, 0.0f);
            Shoot(prefab, outputDirectory, "body-tpose", 0.0f, null, null, 0.0f);

            // THE REAL CLIPS, RETARGETED. The hand poses above prove the skin
            // survives being bent; only these prove the Mixamo animations
            // actually arrive on this skeleton looking like themselves, which
            // is the whole bet of retargeting a human rig onto a figure with
            // arms a third longer than a person's.
            Shoot(prefab, outputDirectory, "clip-walk", 200.0f, null,
                FindClip("walking"), 0.28f);
            Shoot(prefab, outputDirectory, "clip-walk-late", 200.0f, null,
                FindClip("walking"), 0.72f);
            Shoot(prefab, outputDirectory, "clip-sprint", 205.0f, null,
                FindClip("sprint"), 0.35f);
            Shoot(prefab, outputDirectory, "clip-idle", 0.0f, null,
                FindClip("idle"), 0.5f);
            Shoot(prefab, outputDirectory, "clip-jab", 210.0f, null,
                FindClip("boxing_right_jab"), 0.45f);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
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
            Debug.LogWarning($"CityX: no clip in {path} -- that shot will render at rest.");
            return null;
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

        /// <summary>
        /// Poses the rig from a real clip, through the retargeting.
        ///
        /// AnimationMode, NOT AnimationClip.SampleAnimation. The latter is
        /// what the Animation window's scrubber does NOT use, and on a
        /// humanoid rig in batch mode it silently leaves the body at rest --
        /// which is how a set of "posed" captures came out identical to the
        /// bind pose and nobody noticed. This is the API the scrubber actually
        /// uses, and it goes through the avatar, so what comes out is the clip
        /// as the game will play it rather than raw bone tracks.
        /// </summary>
        private static void SampleClip(GameObject instance, AnimationClip clip, float fraction)
        {
            if (clip == null)
            {
                return;
            }

            // THE ANIMATOR'S OWN GAMEOBJECT, not the character root. Sampling a
            // humanoid clip against an object that merely CONTAINS the animator
            // does nothing at all and reports nothing -- the body comes back in
            // its bind pose and the shot looks like a character standing still.
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("CityX: no Animator to sample against.");
                return;
            }

            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            var rotation = new Quaternion[bones.Length];
            var position = new Vector3[bones.Length];

            // Sampling applies the clip's root motion, which walks the
            // character out of frame. The game runs with applyRootMotion off --
            // PlayerLocomotion owns position -- so putting the root back is
            // what makes the shot match what is played.
            Vector3 rootPosition = animator.transform.localPosition;
            Quaternion rootRotation = animator.transform.localRotation;

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(
                    animator.gameObject, clip, fraction * clip.length);
                AnimationMode.EndSampling();

                // SNAPSHOT WHILE IT IS STILL POSED. StopAnimationMode PUTS THE
                // POSE BACK -- that is the whole point of it, it is a
                // preview mode that does not dirty the scene -- so a capture
                // taken after stopping would be of the rest pose again, which
                // is the same failure this method exists to fix.
                for (int i = 0; i < bones.Length; i++)
                {
                    rotation[i] = bones[i].localRotation;
                    position[i] = bones[i].localPosition;
                }
            }
            finally
            {
                // Left on, it makes every later scene edit part of a recording
                // session, and the next capture would sample against a rig
                // already being driven.
                AnimationMode.StopAnimationMode();
            }

            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localRotation = rotation[i];
                bones[i].localPosition = position[i];
            }

            animator.transform.localPosition = rootPosition;
            animator.transform.localRotation = rootRotation;
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
            IEnumerable<Turn> pose, AnimationClip clip, float clipFraction)
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
                SampleClip(instance, clip, clipFraction);

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
