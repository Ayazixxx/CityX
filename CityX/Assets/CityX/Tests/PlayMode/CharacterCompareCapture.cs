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
    /// Not tests: they render the Unity player body in the same poses, from the same views, with the same light, background and camera as the scratch scenes in the
    /// ORIGINAL Godot project (tests/scratch_render_player_compare.gd, scratch_render_clip_strip.gd, scratch_dump_player_joints.gd), so the two characters can be put
    /// side by side. Output is in Logs/CityShots. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CharacterCompareCapture
    {
        private const int Cell = 300;

        /// <summary>Every explicit capture builds its own stage; without this the next one finds the last one's character standing in the frame.</summary>
        [TearDown]
        public void ClearTheStage()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null || root.GetComponent<CharacterController>() != null || root.name.StartsWith("Cube") || root.name == "Player(Clone)" || root.name.StartsWith("Player"))
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void Stage(out GameObject player, out Camera camera)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(400, 1, 400);
            ground.GetComponent<Renderer>().material.color = new Color(0.42f, 0.40f, 0.38f);
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(40, -150, 0); sun.intensity = 1.2f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.55f, 0.70f);
#else
            player = null; camera = null;
#endif
        }

        private static Texture2D Shoot(Camera camera, Vector3 eye, Vector3 lookAt)
        {
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - eye, Vector3.up);
            RenderTexture rt = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); image.Apply();
            camera.targetTexture = null; RenderTexture.active = null; RenderTexture.ReleaseTemporary(rt);
            return image;
        }

        private static void Save(Texture2D sheet, string name)
        {
            sheet.Apply();
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var enc = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name), (byte[])enc.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
        }

        private static void PlaceAt(GameObject player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = position; player.transform.rotation = Quaternion.identity; controller.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>Where each joint of the Unity body is (relative to the player, in its own facing) in idle, walk and run, and the extreme leg swing over about a cycle and a half: the numbers to hold against tests/scratch_dump_player_joints.gd and scratch_dump_run_range.gd.</summary>
        [UnityTest]
        public IEnumerator DumpJoints()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out _);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var animator = player.GetComponentInChildren<Animator>();
            const float dt = 1.0f / 60.0f;
            (string, HumanBodyBones)[] joints =
            {
                ("hips", HumanBodyBones.Hips), ("spine", HumanBodyBones.Spine), ("neck", HumanBodyBones.Neck), ("head", HumanBodyBones.Head),
                ("arm_l", HumanBodyBones.LeftUpperArm), ("forearm_l", HumanBodyBones.LeftLowerArm), ("hand_l", HumanBodyBones.LeftHand),
                ("arm_r", HumanBodyBones.RightUpperArm), ("forearm_r", HumanBodyBones.RightLowerArm), ("hand_r", HumanBodyBones.RightHand),
                ("thigh_l", HumanBodyBones.LeftUpperLeg), ("shin_l", HumanBodyBones.LeftLowerLeg), ("foot_l", HumanBodyBones.LeftFoot),
                ("thigh_r", HumanBodyBones.RightUpperLeg), ("shin_r", HumanBodyBones.RightLowerLeg), ("foot_r", HumanBodyBones.RightFoot),
            };
            {
                // The baked idle clip applied straight to the body at the time the Godot dump uses (0.99 s), nothing else running: where the head ends up.
                var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/Godot/godot_idle.anim");
                locomotion.enabled = false;
                animator.enabled = false;
                idle.SampleAnimation(animator.gameObject, 0.99f);
                yield return null;
                Vector3 neck = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Neck).position);
                Vector3 head = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Head).position);
                Vector3 spine = player.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Spine).position);
                Debug.Log($"IDLEDIRECT neck ({neck.x:F3},{neck.y - 0.0f:F3},{neck.z:F3}) head centre ({head.x:F3},{head.y:F3},{head.z:F3}) spine ({spine.x:F3},{spine.y:F3},{spine.z:F3}) neck local {animator.GetBoneTransform(HumanBodyBones.Neck).localEulerAngles} head local {animator.GetBoneTransform(HumanBodyBones.Head).localEulerAngles} (Godot: neck pitch 19 deg, head 6 deg, head centre about (0.03, 2.22, +0.10))");
                locomotion.enabled = true;
                animator.enabled = true;
            }
            foreach (bool upperLayers in new[] { true, false })
            {
                for (int l = 1; l < animator.layerCount; l++) animator.SetLayerWeight(l, upperLayers ? 1.0f : 0.0f);
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(default, 1.0f / 60.0f);
                    yield return null;
                }
                Debug.Log($"IDLELAYERS upper layers {(upperLayers ? "ON" : "OFF")}: neck local {animator.GetBoneTransform(HumanBodyBones.Neck).localEulerAngles} head local {animator.GetBoneTransform(HumanBodyBones.Head).localEulerAngles} spine local {animator.GetBoneTransform(HumanBodyBones.Spine).localEulerAngles} upper arm L local {animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localEulerAngles}");
            }
            for (int l = 1; l < animator.layerCount; l++) animator.SetLayerWeight(l, 1.0f);
            foreach (string pose in new[] { "idle", "walk", "run" })
            {
                PlaceAt(player, new Vector3(0, 0.05f, -2.0f));
                yield return null;
                LocomotionInput input = default;
                if (pose == "walk") input = new LocomotionInput { Move = Vector2.up * 0.5f };
                else if (pose == "run") input = new LocomotionInput { Move = Vector2.up, Sprint = true };
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(input, dt);
                    yield return null;
                }
                float maxForward = -999.0f, maxBack = 999.0f, maxKnee = 0.0f;
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(input, dt);
                    yield return null;
                    foreach ((HumanBodyBones thigh, HumanBodyBones shin, HumanBodyBones foot) in new[]
                    {
                        (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
                        (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
                    })
                    {
                        Vector3 hip = player.transform.InverseTransformPoint(animator.GetBoneTransform(thigh).position);
                        Vector3 knee = player.transform.InverseTransformPoint(animator.GetBoneTransform(shin).position);
                        Vector3 ankle = player.transform.InverseTransformPoint(animator.GetBoneTransform(foot).position);
                        Vector3 thighDirection = (knee - hip).normalized;
                        float forward = Mathf.Atan2(thighDirection.z, -thighDirection.y) * Mathf.Rad2Deg;
                        maxForward = Mathf.Max(maxForward, forward);
                        maxBack = Mathf.Min(maxBack, forward);
                        maxKnee = Mathf.Max(maxKnee, Vector3.Angle(thighDirection, (ankle - knee).normalized));
                    }
                }
                var text = new System.Text.StringBuilder($"== {pose}: thigh forward max {maxForward:F1}, back max {maxBack:F1}, knee bend max {maxKnee:F1} (Godot: idle 17/6.6/34, walk 40/-17/68, run 66/-40/127)\n");
                foreach ((string name, HumanBodyBones bone) in joints)
                {
                    Vector3 p = player.transform.InverseTransformPoint(animator.GetBoneTransform(bone).position);
                    text.Append($"  {name} pos=({p.x:F3},{p.y:F3},{p.z:F3})\n");
                }
                Debug.Log(text.ToString());
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>One baked Godot clip applied straight to the body (no controller, no locomotion script), sampled at even times across its length, side-on: Logs/CityShots/unity-clip-strip.png, to hold against godot-clip-strip.png (tests/scratch_render_clip_strip.gd).</summary>
        [UnityTest]
        public IEnumerator RenderABakedClipStrip()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            player.GetComponent<PlayerLocomotion>().enabled = false;
            var animator = player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/Godot/godot_run.anim");
            Assert.IsNotNull(clip, "bake the Godot motion first");
            const int frames = 10;
            var sheet = new Texture2D(Cell * frames, Cell, TextureFormat.RGB24, false);
            Vector3 p = player.transform.position;
            for (int i = 0; i < frames; i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * i / frames);
                yield return null;
                Texture2D image = Shoot(camera, p + new Vector3(7.0f, 1.4f, 0), p + new Vector3(0, 1.1f, 0));
                sheet.SetPixels(i * Cell, 0, Cell, Cell, image.GetPixels());
                Object.Destroy(image);
            }
            Save(sheet, "unity-clip-strip.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>
        /// The upper body close up from the front-right, one baked clip sampled across its cycle (no controller): Logs/CityShots/unity-arms-closeup.png, to hold against
        /// godot-arms-closeup.png (tests/scratch_render_arms_closeup.gd). The camera is on the x-mirrored side, since the clips are baked mirrored (see GodotMotionBaker).
        /// Set <see cref="CloseupClip"/> to look at another clip.
        /// </summary>
        public static string CloseupClip = "run";

        [UnityTest]
        public IEnumerator RenderTheArmsCloseUp()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            player.GetComponent<PlayerLocomotion>().enabled = false;
            var animator = player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/CityX/Character/Animations/Godot/godot_{CloseupClip}.anim");
            Assert.IsNotNull(clip, "bake the Godot motion first");
            camera.fieldOfView = 24.0f;
            const int frames = 10;
            var sheet = new Texture2D(Cell * frames, Cell, TextureFormat.RGB24, false);
            Vector3 p = player.transform.position;
            for (int i = 0; i < frames; i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * i / frames);
                yield return null;
                Texture2D image = Shoot(camera, p + new Vector3(-2.6f, 1.9f, 3.0f), p + new Vector3(0, 1.5f, 0));
                sheet.SetPixels(i * Cell, 0, Cell, Cell, image.GetPixels());
                Object.Destroy(image);
            }
            Save(sheet, "unity-arms-closeup.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>A jab thrown through the real combat code, close up from the front (x-mirrored, like the other Godot comparisons), a frame every 0.045 s: Logs/CityShots/unity-jab-closeup.png against godot-jab-closeup.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheJabCloseUp()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var melee = player.GetComponent<MeleeAttacker>();
            camera.fieldOfView = 26.0f;
            const float dt = 1.0f / 60.0f;
            const int frames = 10;
            var sheet = new Texture2D(Cell * frames, Cell, TextureFormat.RGB24, false);
            PlaceAt(player, new Vector3(0, 0.05f, 0));
            for (int i = 0; i < 40; i++)
            {
                locomotion.Tick(default, dt);
                melee.Tick(dt);
                yield return null;
            }
            melee.TryAttack();
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < 3; i++)
                {
                    locomotion.Tick(default, dt);
                    melee.Tick(dt);
                    yield return null;
                }
                Vector3 p = player.transform.position;
                Texture2D image = Shoot(camera, p + new Vector3(-1.3f, 1.2f, 1.9f), p + new Vector3(0, 1.75f, 0.1f));
                sheet.SetPixels(f * Cell, 0, Cell, Cell, image.GetPixels());
                Object.Destroy(image);
            }
            Save(sheet, "unity-jab-closeup.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>For every bone: how far its own skin (vertices it owns most) reaches from the bone's segment, posed against at rest, to find skin that swells.</summary>
        private static string SwellReport(SkinnedMeshRenderer skin, Mesh baked)
        {
            skin.BakeMesh(baked);
            Mesh shared = skin.sharedMesh;
            BoneWeight[] weights = shared.boneWeights;
            Vector3[] posed = baked.vertices;
            Vector3[] rest = shared.vertices;
            Transform[] bones = skin.bones;
            var sb = new System.Text.StringBuilder();
            var posedMax = new float[bones.Length];
            var restMax = new float[bones.Length];
            var worstT = new float[bones.Length]; var worstW = new float[bones.Length]; var worstB1 = new string[bones.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                int b = weights[i].boneIndex0;
                Transform bone = bones[b];
                Transform child = bone.childCount > 0 ? bone.GetChild(0) : null;
                if (child == null) continue;
                // Rest segment from the bind pose, posed segment from the live bones (both in the renderer's space).
                Vector3 pa = skin.transform.InverseTransformPoint(bone.position), pb = skin.transform.InverseTransformPoint(child.position);
                Matrix4x4 bind = shared.bindposes[b].inverse;
                Vector3 ra = bind.MultiplyPoint3x4(Vector3.zero);
                Vector3 rb = bind.MultiplyPoint3x4(bone.InverseTransformPoint(child.position));
                { float dd = DistanceToSegment(posed[i], pa, pb); if (dd > posedMax[b]) { Vector3 abv = pb - pa; worstT[b] = Mathf.Clamp01(Vector3.Dot(posed[i] - pa, abv) / Mathf.Max(abv.sqrMagnitude, 1e-8f)); worstW[b] = weights[i].weight0; worstB1[b] = bones[weights[i].boneIndex1].name + ":" + weights[i].weight1.ToString("F2"); } }
                posedMax[b] = Mathf.Max(posedMax[b], DistanceToSegment(posed[i], pa, pb));
                restMax[b] = Mathf.Max(restMax[b], DistanceToSegment(rest[i], ra, rb));
            }
            for (int b = 0; b < bones.Length; b++)
            {
                if (restMax[b] > 0.0f) sb.Append($"  {bones[b].name}: reaches {posedMax[b]:F3} m from its bone, at rest {restMax[b]:F3} m (x{posedMax[b] / restMax[b]:F2})\n");
            }
            return sb.ToString();
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>The wall climb seen the way the third-person camera sees it (from behind and below): Logs/CityShots/unity-climb-behind.png, a frame every 0.2 s.</summary>
        [UnityTest]
        public IEnumerator RenderTheClimbFromBehind()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 20.0f, 52.0f); wall.transform.localScale = new Vector3(20, 40, 2);
            Physics.SyncTransforms();
            PlaceAt(player, new Vector3(0, 0.05f, 50.3f));
            var climbing = player.GetComponent<PlayerClimbing>();
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            camera.fieldOfView = 55.0f;
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            const float dt = 1.0f / 60.0f;
            const int frames = 10;
            var sheet = new Texture2D(Cell * frames, Cell, TextureFormat.RGB24, false);
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < 12; i++)
                {
                    climbing.Tick(false, true, false, vitals, dt);
                    yield return null;
                }
                Vector3 p = player.transform.position;
                Texture2D image = Shoot(camera, p + new Vector3(1.0f, 0.9f, -2.4f), p + new Vector3(0, 1.2f, 0.6f));
                sheet.SetPixels(f * Cell, 0, Cell, Cell, image.GetPixels());
                Object.Destroy(image);
            }
            Debug.Log("SWELLCLIMB\n" + SwellReport(player.GetComponentInChildren<SkinnedMeshRenderer>(), new Mesh()));
            Save(sheet, "unity-climb-behind.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>The idle from behind and to the side, the way the third-person camera sees it: Logs/CityShots/unity-idle-back.png against godot-idle-back.png (tests/scratch_render_idle_back.gd).</summary>
        [UnityTest]
        public IEnumerator RenderTheIdleFromBehind()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            player.GetComponent<PlayerLocomotion>().enabled = false;
            var animator = player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/CityX/Character/Animations/Godot/godot_idle.anim");
            Assert.IsNotNull(clip, "bake the Godot motion first");
            const int frames = 10;
            var sheet = new Texture2D(Cell * frames, Cell, TextureFormat.RGB24, false);
            Vector3 p = player.transform.position;
            for (int i = 0; i < frames; i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * i / frames);
                yield return null;
                Texture2D image = Shoot(camera, p + new Vector3(-2.2f, 1.9f, -3.4f), p + new Vector3(0, 1.3f, 0));
                sheet.SetPixels(i * Cell, 0, Cell, Cell, image.GetPixels());
                Object.Destroy(image);
            }
            Save(sheet, "unity-idle-back.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>A side-on strip of one stride, every few frames, driven the way the game drives it (locomotion script, controller, everything). Logs/CityShots/unity-stride-strip.png: walk on top, run below.</summary>
        [UnityTest]
        public IEnumerator RenderAStrideStrip()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            const float dt = 1.0f / 60.0f;
            const int columns = 10;
            var sheet = new Texture2D(Cell * columns, Cell * 2, TextureFormat.RGB24, false);
            for (int row = 0; row < 2; row++)
            {
                PlaceAt(player, new Vector3(0, 0.05f, -20.0f * (row + 1)));
                yield return null;
                var input = new LocomotionInput { Move = Vector2.up * (row == 0 ? 0.5f : 1.0f), Sprint = row == 1 };
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(input, dt);
                    yield return null;
                }
                for (int col = 0; col < columns; col++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        locomotion.Tick(input, dt);
                        yield return null;
                    }
                    Vector3 p = player.transform.position;
                    yield return null;
                    Texture2D image = Shoot(camera, p + new Vector3(7.0f, 1.4f, 0), p + new Vector3(0, 1.1f, 0));
                    sheet.SetPixels(col * Cell, (1 - row) * Cell, Cell, Cell, image.GetPixels());
                    Object.Destroy(image);
                }
            }
            Save(sheet, "unity-stride-strip.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RenderTheBodyLikeGodotDid()
        {
#if UNITY_EDITOR
            Stage(out GameObject player, out Camera camera);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var melee = player.GetComponent<MeleeAttacker>();
            const float dt = 1.0f / 60.0f;
            string[] poses = { "idle", "walk", "run", "guard", "jab" };
            var sheet = new Texture2D(Cell * 3, Cell * poses.Length, TextureFormat.RGB24, false);
            for (int row = 0; row < poses.Length; row++)
            {
                PlaceAt(player, new Vector3(0, 0.05f, -2.0f));
                yield return null;
                int frames = poses[row] == "jab" ? 8 : 40;
                for (int i = 0; i < frames; i++)
                {
                    LocomotionInput input = default;
                    if (poses[row] == "walk") input = new LocomotionInput { Move = Vector2.up * 0.5f };
                    else if (poses[row] == "run") input = new LocomotionInput { Move = Vector2.up, Sprint = true };
                    locomotion.Tick(input, dt);
                    if ((poses[row] == "guard" || poses[row] == "jab") && i == 0) melee.TryAttack();
                    melee.Tick(dt);
                    yield return null;
                }
                Vector3 p = player.transform.position;
                for (int col = 0; col < 3; col++)
                {
                    Vector3 eye = col == 0 ? p + new Vector3(0, 1.4f, 7.0f) : col == 1 ? p + new Vector3(7.0f, 1.4f, 0) : p + new Vector3(4.6f, 1.8f, 5.2f);
                    yield return null;
                    Texture2D image = Shoot(camera, eye, p + new Vector3(0, 1.1f, 0));
                    sheet.SetPixels(col * Cell, (poses.Length - 1 - row) * Cell, Cell, Cell, image.GetPixels());
                    Object.Destroy(image);
                }
            }
            Save(sheet, "unity-character-sheet.png");
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }
    }
}
