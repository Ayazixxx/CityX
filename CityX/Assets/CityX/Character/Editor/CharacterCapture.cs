using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.Core;
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

        /// <summary>
        /// The body wearing its own UV layout, as a checker.
        ///
        /// THE ONLY WAY TO SEE AN UNWRAP. Numbers can say no triangle spans two
        /// rows and few are degenerate, and the unwrap can still be unusable --
        /// squares stretched into ribbons down one limb and crushed on another
        /// means a brush changes size depending where you paint. A checker
        /// shows that instantly and nothing else does.
        /// </summary>
        [MenuItem("CityX/Capture UV Checker")]
        public static void CaptureChecker()
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

            Material checker = CheckerMaterial();
            Shoot(prefab, outputDirectory, "uv-front", 0.0f, Rest, null, 0.0f, checker);
            Shoot(prefab, outputDirectory, "uv-back", 180.0f, Rest, null, 0.0f, checker);
            Shoot(prefab, outputDirectory, "uv-tpose", 20.0f, null, null, 0.0f, checker);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// A checker drawn at the atlas's own resolution, so one square is one
        /// texel block and the row boundaries land where they really are.
        /// </summary>
        private static Material CheckerMaterial()
        {
            var texture = new Texture2D(PaintAtlas.Width, PaintAtlas.Height)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
            };

            for (int y = 0; y < PaintAtlas.Height; y++)
            {
                // Each atlas ROW gets its own hue, so a limb painting into the
                // wrong row is obvious rather than merely wrong-looking.
                int rowIndex = y / PaintAtlas.RowHeight;
                Color tint = Color.HSVToRGB(rowIndex / (float)PaintAtlas.PartCount, 0.55f, 1.0f);

                for (int x = 0; x < PaintAtlas.Width; x++)
                {
                    bool dark = ((x / 8) + (y / 8)) % 2 == 0;
                    texture.SetPixel(x, y, dark ? tint * 0.45f : tint);
                }
            }
            texture.Apply();

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            material.mainTexture = texture;
            return material;
        }

        [MenuItem("CityX/Capture Armpit Check")]
        public static void CaptureArmpitCheck()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            // BOTH shoulders, several poses/angles -- the first pass only
            // ever checked the left shoulder in the Rest pose from one
            // grazing angle. A gap that survives at a different yaw, on the
            // other side, or under a real (not hand-posed) clip would have
            // been invisible to that single shot.
            ArmpitShot(prefab, outputDirectory, "armpit-left-rest", 190.0f,
                new Vector3(0.30f, 1.75f, -1.6f), new Vector3(0.32f, 1.70f, 0.0f), Rest, null, 0.0f);
            ArmpitShot(prefab, outputDirectory, "armpit-right-rest", 10.0f,
                new Vector3(-0.30f, 1.75f, -1.6f), new Vector3(-0.32f, 1.70f, 0.0f), Rest, null, 0.0f);
            ArmpitShot(prefab, outputDirectory, "armpit-left-stride", 235.0f,
                new Vector3(0.30f, 1.60f, -1.6f), new Vector3(0.32f, 1.55f, 0.0f), Stride, null, 0.0f);
            ArmpitShot(prefab, outputDirectory, "armpit-right-punch", -25.0f,
                new Vector3(-0.30f, 1.85f, -1.6f), new Vector3(-0.32f, 1.80f, 0.0f), Punch, null, 0.0f);
            ArmpitShot(prefab, outputDirectory, "armpit-left-walk-clip", 235.0f,
                new Vector3(0.30f, 1.55f, -1.6f), new Vector3(0.32f, 1.50f, 0.0f), null,
                FindClip("walking"), 0.28f);
            ArmpitShot(prefab, outputDirectory, "armpit-right-sword-clip", -25.0f,
                new Vector3(-0.30f, 1.75f, -1.6f), new Vector3(-0.32f, 1.70f, 0.0f), null,
                FindClip("2hsw_attack"), 0.45f);

            // THE CHARGED JUMP POSE, NEWLY ADDED AND NEVER SWEPT -- the raw
            // retargeted clip crossed the arm tight across the torso far
            // enough to open a real hole here (confirmed with a flood-lit
            // render and DiagnoseFusedBodyMeshTopology: the base
            // mesh has zero boundary edges, so the hole was a Linear Blend
            // Skinning collapse under an extreme rotation, not a bad mesh).
            // PlayerRigBuilder.DampenChargedJumpPose pulls the whole
            // retargeted pose back toward true rest rather than the clip's
            // own mean -- see its own comment for why "own mean" was the
            // wrong axis to scale around for a clip that is mostly one
            // held pose. Checked against the SAME dampened clip the real
            // AnimatorController actually plays.
            //
            // RIGHT SIDE ONLY, deliberately -- this clip's own retargeted
            // pose is not left/right symmetric (this rig's left arm ends up
            // somewhere this camera's right-side framing does not reach,
            // several positions tried), and the fix itself has no left/
            // right-specific logic to go blind on: DampenChargedJumpPose
            // scales every muscle curve toward zero uniformly, confirmed by
            // the diagnostic sweep that found the pre-fix left and right
            // shoulder deltas were comparable (0.47 vs 0.55). The side that
            // actually surfaced the reported hole is the side worth
            // permanent coverage.
            AnimationClip chargedJump = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/CityX/Character/Animations/charged_jump_dampened.anim");
            ArmpitShot(prefab, outputDirectory, "armpit-right-charged-jump", -25.0f,
                new Vector3(-0.30f, 1.60f, -1.6f), new Vector3(-0.32f, 1.55f, 0.0f), null,
                chargedJump, 0.9f);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// DEFINITIVE, CAMERA-INDEPENDENT: a truly watertight mesh has every
        /// edge shared by EXACTLY two triangles. Anything shared by only one
        /// is a real boundary -- a hole -- in the BASE bind-pose mesh
        /// itself, independent of any pose, camera angle, or animation.
        /// Screenshot-based checks can miss a hole that happens to be
        /// occluded from every angle tried, or chase phantom holes that are
        /// really just deep, correctly-closed shadow (see this file's own
        /// history with the earlier, unlit ArmpitShot). Counting edges has
        /// neither failure mode.
        /// </summary>
        [MenuItem("CityX/Diagnose Fused Body Mesh Topology")]
        public static void DiagnoseFusedBodyMeshTopology()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            if (mesh == null)
            {
                Debug.LogError("CityX: no mesh at Assets/CityX/Character/FusedBody.asset");
                return;
            }

            Vector3[] verts = mesh.vertices;

            // DEDUPE BY POSITION FIRST. A textured character mesh splits a
            // vertex into several INDICES at every UV/normal seam -- two
            // triangles that are genuinely edge-adjacent in 3D space can
            // easily reference different indices for what is the same
            // point. Counting raw index pairs treats every one of those
            // seams as a false boundary; only position tells the truth.
            const float Quantum = 1e-4f;
            var canonical = new Dictionary<(int, int, int), int>();
            var canonicalOf = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var key = (Mathf.RoundToInt(verts[i].x / Quantum),
                    Mathf.RoundToInt(verts[i].y / Quantum),
                    Mathf.RoundToInt(verts[i].z / Quantum));
                if (!canonical.TryGetValue(key, out int id))
                {
                    id = canonical.Count;
                    canonical[key] = id;
                }
                canonicalOf[i] = id;
            }

            var edgeCounts = new Dictionary<(int, int), int>();

            void CountEdge(int a, int b)
            {
                a = canonicalOf[a];
                b = canonicalOf[b];
                var key = a < b ? (a, b) : (b, a);
                edgeCounts.TryGetValue(key, out int count);
                edgeCounts[key] = count + 1;
            }

            // A representative original-index position for each canonical
            // id, so a reported edge can be printed as a real coordinate.
            var positionOfCanonical = new Vector3[canonical.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                positionOfCanonical[canonicalOf[i]] = verts[i];
            }

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    CountEdge(tris[i], tris[i + 1]);
                    CountEdge(tris[i + 1], tris[i + 2]);
                    CountEdge(tris[i + 2], tris[i]);
                }
            }

            int boundaryEdges = 0;
            Vector3 boundsMin = Vector3.positiveInfinity;
            Vector3 boundsMax = Vector3.negativeInfinity;
            int reported = 0;
            foreach (var kvp in edgeCounts)
            {
                if (kvp.Value != 2)
                {
                    boundaryEdges++;
                    Vector3 a = positionOfCanonical[kvp.Key.Item1];
                    Vector3 b = positionOfCanonical[kvp.Key.Item2];
                    boundsMin = Vector3.Min(boundsMin, Vector3.Min(a, b));
                    boundsMax = Vector3.Max(boundsMax, Vector3.Max(a, b));
                    if (reported < 20)
                    {
                        Debug.Log($"CityX: boundary edge (sharedBy={kvp.Value}) at {a} -- {b}");
                        reported++;
                    }
                }
            }

            Debug.Log($"CityX: {mesh.vertexCount} raw vertices ({canonical.Count} distinct positions), " +
                $"{edgeCounts.Count} unique edges, {boundaryEdges} boundary edges " +
                "(should be 0 for a watertight mesh)");
            if (boundaryEdges > 0)
            {
                Debug.Log($"CityX: boundary edges span roughly {boundsMin} to {boundsMax}");
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }


        private static void ArmpitShot(GameObject prefab, string outputDirectory, string name, float yaw,
            Vector3 cameraPosition, Vector3 lookAt, IEnumerable<Turn> pose, AnimationClip clip, float clipFraction)
        {
            var root = new GameObject("ArmpitCheckRig");
            RenderTexture target = null;
            var previousAmbient = RenderSettings.ambientMode;
            var previousSky = RenderSettings.ambientSkyColor;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(20.0f, 20.0f + yaw, 0.0f);

                // A DELIBERATE FILL LIGHT, not the scene's leftover default --
                // this rig previously had NONE (unlike Shoot's own ambient
                // setup below), and a real, closed, deeply concave crease with
                // no fill light reaching it self-shadows to near-black,
                // visually identical to this background colour. Ruling that
                // out before trusting anything this tool renders as a hole.
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.40f);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root.transform);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);
                Pose(instance, pose);
                SampleClip(instance, clip, clipFraction);

                foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
                camera.fieldOfView = 28.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = cameraPosition;
                cameraObject.transform.LookAt(lookAt);

                target = new RenderTexture(700, 700, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(700, 700, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 700, 700), 0, 0);
                image.Apply();
                string path = Path.Combine(outputDirectory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbient;
                RenderSettings.ambientSkyColor = previousSky;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
        }

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
            Shoot(prefab, outputDirectory, "clip-sprint", 205.0f, null,
                FindClip("sprint"), 0.35f);
            Shoot(prefab, outputDirectory, "clip-idle", 0.0f, null,
                FindClip("idle"), 0.5f);
            Shoot(prefab, outputDirectory, "clip-jab", 210.0f, null,
                FindClip("boxing_right_jab"), 0.45f);

            // A SPREAD OF THE PORTED CLIPS, one per family, because the
            // failure mode of bringing fifty-three clips across at once is
            // that most of them work and two of them do something mad, and
            // spot-checking the same four locomotion clips would never see it.
            Shoot(prefab, outputDirectory, "clip-sword-idle", 200.0f, null,
                FindClip("2hsw_idle"), 0.5f);
            Shoot(prefab, outputDirectory, "clip-sword-attack", 210.0f, null,
                FindClip("2hsw_attack"), 0.45f);
            Shoot(prefab, outputDirectory, "clip-crouch-walk", 205.0f, null,
                FindClip("crouch_walk"), 0.3f);
            Shoot(prefab, outputDirectory, "clip-climb", 200.0f, null,
                FindClip("climbing_up_wall"), 0.4f);
            Shoot(prefab, outputDirectory, "clip-pistol-aim", 210.0f, null,
                FindClip("pistol_aim_in"), 0.9f);
            Shoot(prefab, outputDirectory, "clip-dance", 200.0f, null,
                FindClip("dance"), 0.35f);
            Shoot(prefab, outputDirectory, "clip-charged-jump", 200.0f, null,
                FindClip("charged_jump"), 0.9f);

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
                Transform bone = TransformSearch.FindDeep(instance.transform, turn.Bone);
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

            // AND RECENTRE ON THE HIPS. Resetting the animator's own transform
            // removes root motion, but several clips -- climbing a wall,
            // entering a car, the intro fall -- carry their travel in the HIPS
            // track instead, and those walked straight out of frame. Sliding
            // the whole rig back so the hips sit where they rest keeps the pose
            // exactly as the clip made it and puts it where the camera is
            // looking. It is a framing correction, not a change to the pose.
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                Vector3 rest = animator.transform.TransformPoint(
                    CityX.Character.BodyRig.PositionOf(HumanBodyBones.Hips));
                animator.transform.position += rest - hips.position;
            }
        }

        private static void Shoot(GameObject prefab, string directory, string name, float yaw,
            IEnumerable<Turn> pose, AnimationClip clip, float clipFraction,
            Material overrideMaterial = null)
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
                    if (overrideMaterial != null)
                    {
                        skin.sharedMaterial = overrideMaterial;
                    }
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
