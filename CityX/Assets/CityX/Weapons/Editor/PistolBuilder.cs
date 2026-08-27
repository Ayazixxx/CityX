using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the pistol prefab. Ported from weapons/pistol.tscn/pistol.gd.
    ///
    /// THE REAL Beretta_M9.obj MODEL, NOT A PROCEDURAL BUILD -- the one
    /// weapon in this project that isn't (see BottleBuilder/
    /// GrapplingHookBuilder's own reasoning for why every other weapon is).
    /// Godot's own _build_visual() re-centers the imported mesh on its own
    /// bounding-box middle and normalizes its scale so the largest dimension
    /// matches TargetLengthMeters, rather than trusting the raw OBJ's units --
    /// ported the same way here rather than assuming Unity's own OBJ importer
    /// already got the scale right.
    ///
    /// THE IMPORTED MATERIAL IS OVERRIDDEN OUTRIGHT, matching pistol.gd's own
    /// GUNMETAL_COLOR override -- confirmed there via a direct dump that the
    /// source .mtl's real material is pure black (Kd 0 0 0, no texture maps
    /// to fall back on), which reads as a solid black silhouette against this
    /// game's own dark night lighting. No reason to assume Unity's importer
    /// resolves that .mtl any better; overriding is the same fix either way.
    /// </summary>
    public static class PistolBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Pistol.prefab";
        private const string ModelPath = "Assets/CityX/Weapons/Models/Beretta_M9.obj";
        private const string ShotAudioPath = "Assets/CityX/Weapons/Audio/pistol_shot.mp3";
        private const string ReloadAudioPath = "Assets/CityX/Weapons/Audio/beretta_reload.mp3";
        private const string SparkMaterialPath = "Assets/CityX/Weapons/PistolSpark.mat";

        // Real Beretta M9 overall length (~21.7cm).
        private const float TargetLengthMeters = 0.22f;

        /// <summary>
        /// How much bigger the pistol reads lying in the world than its
        /// true-to-life NormalizeScale size -- reported too small to
        /// notice on the ground. ONLY affects the world/pickup state: the
        /// root's own scale gets overwritten outright by OnEquip's
        /// GripHandPosition/GripHandRotationEuler/GripScale block the
        /// instant it's picked up, so this never touches how it looks in
        /// hand. The pickup trigger's radius is divided back down by the
        /// same factor below so InteractRange still means what it says in
        /// real metres, rather than silently doubling the pickup range too.
        /// </summary>
        private const float WorldPickupScale = 2.0f;

        private static readonly Color GunmetalColour = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color SparkColour = new Color(1.0f, 0.85f, 0.4f);
        private static readonly Color MuzzleLightColour = new Color(1.0f, 0.75f, 0.35f);

        [MenuItem("CityX/Rebuild Pistol")]
        public static GameObject Build()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: no model at {ModelPath} -- check the OBJ imported.");
                return null;
            }

            var root = new GameObject("Pistol");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                NormalizeScale(visual);

                Material gunmetal = CharacterSkin.Ensure("PistolGunmetal", GunmetalColour);
                gunmetal.SetFloat("_Metallic", 0.75f);
                gunmetal.SetFloat("_Smoothness", 0.65f);
                foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = gunmetal;
                }

                root.transform.localScale = Vector3.one * WorldPickupScale;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = PistolWeapon.InteractRange / WorldPickupScale;

                GameObject muzzleFlash = BuildMuzzleFlash(root.transform, out ParticleSystem sparks);

                var shotPlayer = BuildAudioSource(root.transform, "ShotPlayer", ShotAudioPath);
                var reloadPlayer = BuildAudioSource(root.transform, "ReloadPlayer", ReloadAudioPath);

                var pistol = root.AddComponent<PistolWeapon>();
                pistol.VisualRoot = visual.transform;
                pistol.MuzzleFlashObject = muzzleFlash;
                pistol.MuzzleLight = muzzleFlash.GetComponent<Light>();
                pistol.MuzzleSparks = sparks;
                pistol.ShotPlayer = shotPlayer;
                pistol.ReloadPlayer = reloadPlayer;
                pistol.BloodPrefab = BloodSplatterBuilder.Build();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: pistol written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Re-centers on the mesh's own bounding-box middle and scales so
        /// its largest dimension matches TargetLengthMeters -- matches
        /// pistol.gd's own _build_visual exactly, for the same reason: the
        /// raw imported mesh's actual units aren't something to assume.
        /// </summary>
        private static void NormalizeScale(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float largestDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float normalizeScale = largestDimension > 0.0001f ? TargetLengthMeters / largestDimension : 1.0f;

            Vector3 centreLocal = visual.transform.InverseTransformPoint(bounds.center);
            visual.transform.localScale = Vector3.one * normalizeScale;
            visual.transform.localPosition = -centreLocal * normalizeScale;
        }

        /// <summary>
        /// An OmniLight3D-equivalent point light plus a short particle burst
        /// of sparks -- pistol.tscn's own MuzzleFlash/MuzzleSparks numbers.
        /// Starts inactive; PistolWeapon.Fire flips it on for one frame.
        /// </summary>
        private static GameObject BuildMuzzleFlash(Transform parent, out ParticleSystem sparks)
        {
            var flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.localPosition = new Vector3(0.0f, 0.0f, TargetLengthMeters * 0.5f);

            Light light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = MuzzleLightColour;
            light.intensity = 6.0f;
            light.range = 1.5f;
            light.shadows = LightShadows.None;

            var sparksObject = new GameObject("MuzzleSparks");
            sparksObject.transform.SetParent(flash.transform, false);
            sparks = sparksObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = sparks.main;
            main.duration = 0.12f;
            main.loop = false;
            main.startLifetime = 0.12f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f, 6.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = SparkColour;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0.0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, 10) });

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12.0f;
            shape.radius = 0.01f;

            ParticleSystem.ForceOverLifetimeModule force = sparks.forceOverLifetime;
            force.enabled = true;
            force.y = -2.0f;

            var renderer = sparksObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;

            // SAVED AS A REAL ASSET, NOT LEFT LOOSE -- a bare in-memory
            // `new Material(...)` assigned straight to a renderer does not
            // reliably survive PrefabUtility.SaveAsPrefabAsset; loading the
            // saved Pistol.prefab back found sharedMaterial null (confirmed
            // directly), which URP renders as the reported bright pink/
            // purple "missing shader" fallback, same as CharacterSkin's own
            // doc comment describes for the identical symptom on a body
            // with no material at all. BloodSplatterBuilder/GlassShatter
            // Builder/GlassShardsBuilder all give their own particle
            // material real asset backing via AssetDatabase.CreateAsset
            // for exactly this reason; this one just never had.
            Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
            if (sparkMaterial == null)
            {
                sparkMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                {
                    name = "PistolSpark",
                };
                AssetDatabase.CreateAsset(sparkMaterial, SparkMaterialPath);
            }
            sparkMaterial.SetColor("_BaseColor", SparkColour);
            renderer.sharedMaterial = sparkMaterial;

            sparks.Stop();
            return flash;
        }

        /// <summary>
        /// Renders the pistol equipped on the real player prefab, close on
        /// the hand -- the only way to actually judge GripHandPosition/
        /// GripHandRotationEuler, which are a starting guess (see their own
        /// doc comment) rather than a live-tuned number.
        /// </summary>
        [MenuItem("CityX/Capture Pistol Grip")]
        public static void CaptureGrip()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null || pistolPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and Pistol.prefab built first.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PistolGripRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.Euler(0.0f, 200.0f, 0.0f);

                // Arms down, matching CharacterCapture's own Rest pose --
                // a pistol held at the hip, not mid-swing.
                PoseArmsDown(player.transform);

                var pistolInstance = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab);
                var pistol = pistolInstance.GetComponent<PistolWeapon>();
                pistol.OnEquip(player, ItemInstanceData.None);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // Framed FROM the pistol's own live world position, not a
                // guessed fixed coordinate -- a hardcoded camera position
                // landed on empty air the first time this ran, framing legs
                // instead of the gun. A generous distance straight in front
                // of the character, not hugging the grip, so a wrong grip
                // pose is still clearly visible rather than clipped into.
                Vector3 gripPosition = pistolInstance.transform.position;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position =
                    player.transform.position + Vector3.up * 1.0f - player.transform.forward * 1.6f;
                cameraObject.transform.LookAt(gripPosition);

                target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "pistol-grip.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Same idea as CaptureGrip, but posed against the REAL pistol_aim_in
        /// clip rather than a plain arms-down rest -- "the pistol is upside
        /// down as you are shooting it" is a claim about that specific pose,
        /// not the idle hip-carry one CaptureGrip already covers, and the
        /// wrist rotation an actual aiming clip drives can differ enough
        /// from a manual arms-down pose that a fixed GripHandRotationEuler
        /// looking right in one says nothing about the other.
        /// </summary>
        [MenuItem("CityX/Capture Pistol Aiming")]
        public static void CaptureAiming()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (playerPrefab == null || pistolPrefab == null)
            {
                Debug.LogError("CityX: need both Player.prefab and Pistol.prefab built first.");
                return;
            }

            AnimationClip aimClip = FindClip("pistol_aim_in");
            if (aimClip == null)
            {
                Debug.LogError("CityX: no pistol_aim_in clip found.");
                return;
            }

            string outputDirectory = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(outputDirectory);

            var root = new GameObject("PistolAimRig");
            RenderTexture target = null;
            try
            {
                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f, 0.0f);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.transform.SetParent(root.transform);
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.Euler(0.0f, 210.0f, 0.0f);

                SampleClip(player, aimClip, 0.9f);

                var pistolInstance = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab);
                var pistol = pistolInstance.GetComponent<PistolWeapon>();
                pistol.OnEquip(player, ItemInstanceData.None);

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.updateWhenOffscreen = true;
                }

                // Framed FROM THE GUN'S OWN LIVE WORLD POSITION, not the
                // player root's -- a clip's own hip track can carry real
                // root-adjacent travel (see CharacterCapture.SampleClip's
                // own "recentre on hips" comment, deliberately not repeated
                // in this file's own copy), which left the body's actual
                // mesh well away from player.transform.position the first
                // time this ran and framed a close-up of the neck instead
                // of the gun. The gun's own transform is parented to the
                // live hand bone, so it is exactly where the posed body
                // actually put it, independent of whatever the root did.
                Vector3 gripPosition = pistolInstance.transform.position;

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 35.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = gripPosition + new Vector3(0.3f, 0.15f, -0.9f);
                cameraObject.transform.LookAt(gripPosition);

                target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outputDirectory, "pistol-aiming.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                Object.DestroyImmediate(root);
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Pulls the animation clip out of a model file, skipping Unity's own
        /// __preview__ duplicates -- same shape CharacterCapture's own
        /// FindClip already uses.
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
            return null;
        }

        /// <summary>
        /// Poses <paramref name="instance"/> against <paramref name="clip"/>
        /// at a given fraction through it -- copied from CharacterCapture's
        /// own SampleClip rather than shared, since that one is private to
        /// its own file and this is the only other place that needs it.
        /// </summary>
        private static void SampleClip(GameObject instance, AnimationClip clip, float fraction)
        {
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            Transform[] bones = animator.GetComponentsInChildren<Transform>();
            var rotation = new Quaternion[bones.Length];
            var position = new Vector3[bones.Length];
            Vector3 rootPosition = animator.transform.localPosition;
            Quaternion rootRotation = animator.transform.localRotation;

            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, fraction * clip.length);
                AnimationMode.EndSampling();

                for (int i = 0; i < bones.Length; i++)
                {
                    rotation[i] = bones[i].localRotation;
                    position[i] = bones[i].localPosition;
                }
            }
            finally
            {
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

        private static void PoseArmsDown(Transform root)
        {
            SetLocalRotation(root, "arm_l", new Vector3(0.0f, 0.0f, 78.0f));
            SetLocalRotation(root, "forearm_l", new Vector3(0.0f, 0.0f, 12.0f));
            SetLocalRotation(root, "arm_r", new Vector3(0.0f, 0.0f, -78.0f));
            SetLocalRotation(root, "forearm_r", new Vector3(0.0f, 0.0f, -12.0f));
        }

        private static void SetLocalRotation(Transform root, string boneName, Vector3 euler)
        {
            Transform bone = FindDeep(root, boneName);
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(euler);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static AudioSource BuildAudioSource(Transform parent, string name, string clipPath)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            source.playOnAwake = false;
            source.spatialBlend = 1.0f;
            if (source.clip == null)
            {
                Debug.LogWarning($"CityX: no audio clip at {clipPath} -- {name} will play silently.");
            }
            return source;
        }
    }
}
