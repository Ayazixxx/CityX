using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the skelli jeans and hooded cloak prefabs from the real
    /// skinned meshes in skelli_jeans.glb/hooded_cloak.glb, now that
    /// com.unity.cloud.gltfast is installed and both files import as real
    /// geometry (previously they sat on Unity's inert DefaultImporter, the
    /// same gap the bottle's own procedural workaround was flagged against).
    ///
    /// THE IMPORTED MESH'S OWN BINDPOSES ARE DISCARDED, on purpose -- see
    /// BakeMesh. Only its vertices, triangles and bone WEIGHTS survive; the
    /// bindposes are recomputed from scratch against this rig's own rest
    /// geometry, exactly the way wearables/skelli_jeans.gd's and
    /// hooded_cloak.gd's own _rebind() discard the source Skin's binds and
    /// rebuild them from FusedBody.bone_origins() instead of trusting
    /// whatever the export tool encoded.
    /// </summary>
    public static class SkinnedGarmentBuilder
    {
        private const string JeansGlbPath = "Assets/CityX/Wearables/Models/skelli_jeans.glb";
        private const string CloakGlbPath = "Assets/CityX/Wearables/Models/hooded_cloak.glb";
        private const string JeansTexturePath = "Assets/CityX/Wearables/Models/skelli_jeans_texture_0.png";
        private const string JeansMeshPath = "Assets/CityX/Wearables/SkelliJeansMesh.asset";
        private const string CloakMeshPath = "Assets/CityX/Wearables/HoodedCloakMesh.asset";
        private const string JeansPrefabPath = "Assets/CityX/Wearables/SkelliJeans.prefab";
        private const string CloakPrefabPath = "Assets/CityX/Wearables/HoodedCloak.prefab";

        // Lying on the ground pre-pickup, shown tipped and shrunk so a
        // person-sized garment reads as a dropped item rather than half an
        // invisible person -- matches the ground truth's own numbers.
        private const float JeansGroundScale = 0.5f;
        private const float JeansGroundPitchDegrees = -70.0f;
        private const float JeansGroundLift = 0.12f;

        private const float CloakGroundScale = 0.45f;
        private const float CloakGroundPitchDegrees = -78.0f;
        private const float CloakGroundLift = 0.15f;

        private static readonly Color CloakColour = new Color(0.30f, 0.26f, 0.22f);

        private const string JeansMaterialPath = "Assets/CityX/Wearables/SkelliJeansFabric.mat";

        [MenuItem("CityX/Rebuild Skelli Jeans")]
        public static GameObject BuildJeans()
        {
            return Build<SkelliJeans>(JeansGlbPath, JeansMeshPath, JeansPrefabPath, "SkelliJeans",
                BuildJeansMaterial(), JeansGroundScale, JeansGroundPitchDegrees, JeansGroundLift);
        }

        /// <summary>
        /// A plain URP Lit material carrying the imported denim texture,
        /// built fresh rather than reusing glTFast's own imported Shader
        /// Graph material.
        ///
        /// THE TEXTURE'S OWN DEFAULT IMPORT COMPRESSION (DXT1) CORRUPTS IT
        /// INTO BLOTCHY, WRONG-COLOURED PATCHES -- confirmed by decoding
        /// Unity's own imported pixels (genuinely grayscale denim, exactly
        /// matching the source PNG) and then rendering the SAME mesh and
        /// UVs with the SAME texture forced to uncompressed RGBA32, which
        /// came out correctly. DXT1 packs each 4x4 pixel block from two
        /// colour endpoints; this texture is mostly flat pure white (the
        /// cut-away body regions -- see this file's class comment) against
        /// small, high-contrast black denim islands, and Unity's own
        /// compressor produces visibly wrong per-block colours on that
        /// pattern. Forcing the Standalone platform override to
        /// uncompressed RGBA32 avoids it -- a few extra megabytes for one
        /// wearable texture is a fair trade for it not looking corrupted.
        /// </summary>
        private static Material BuildJeansMaterial()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(JeansTexturePath);
            if (importer != null)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
                settings.overridden = true;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Texture baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(JeansTexturePath);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "SkelliJeansFabric" };
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0.0f);
            // The waistband and both cuffs are intentionally open --
            // build_skelli_jeans.py exports its own source material
            // double-sided specifically so those openings don't read as
            // holes (skelli_jeans.gd's own comment says so outright), and
            // so does whatever a bent knee exposes on the inner thigh.
            // Left at Unity's own default (back-face culled), every one of
            // those became a literal see-through gap -- matches
            // BuildCloak's own _Cull=0 fix for the identical reason.
            material.SetFloat("_Cull", 0.0f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(JeansMaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(JeansMaterialPath);
            }
            AssetDatabase.CreateAsset(material, JeansMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        [MenuItem("CityX/Rebuild Hooded Cloak")]
        public static GameObject BuildCloak()
        {
            Material cloakMaterial = CharacterSkin.Ensure("HoodedCloakFabric", CloakColour);
            cloakMaterial.SetFloat("_Smoothness", 0.05f);
            // An open hood and open sleeves mean the inside faces the
            // camera whenever you can see into them; single-sided they
            // would read as holes -- matches the ground truth's own
            // CULL_DISABLED.
            cloakMaterial.SetFloat("_Cull", 0.0f);
            return Build<HoodedCloak>(CloakGlbPath, CloakMeshPath, CloakPrefabPath, "HoodedCloak",
                cloakMaterial, CloakGroundScale, CloakGroundPitchDegrees, CloakGroundLift);
        }

        private static GameObject Build<T>(string glbPath, string meshPath, string prefabPath,
            string rootName, Material overrideMaterial,
            float groundScale, float groundPitchDegrees, float groundLift) where T : SkinnedGarment
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (source == null)
            {
                Debug.LogError($"CityX: {glbPath} did not load as a GameObject -- " +
                                "check that com.unity.cloud.gltfast is installed and the file reimported.");
                return null;
            }

            var sourceRenderer = source.GetComponentInChildren<SkinnedMeshRenderer>();
            if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
            {
                Debug.LogError($"CityX: {glbPath} has no SkinnedMeshRenderer.");
                return null;
            }

            Mesh bakedMesh = BakeMesh(sourceRenderer, meshPath);
            if (bakedMesh == null)
            {
                return null;
            }

            Material usedMaterial = overrideMaterial != null ? overrideMaterial : sourceRenderer.sharedMaterial;

            var root = new GameObject(rootName);
            try
            {
                var garment = root.AddComponent<T>();
                garment.GarmentMesh = bakedMesh;
                garment.GarmentMaterial = usedMaterial;

                // A non-skinned preview, shown lying on the ground pre-pickup
                // and hidden by SkinnedGarment.OnEquip -- its raw, unposed
                // mesh sitting where the live skin also renders would
                // otherwise z-fight with it.
                var preview = new GameObject("Preview");
                preview.transform.SetParent(root.transform, false);
                preview.transform.localScale = Vector3.one * groundScale;
                preview.transform.localEulerAngles = new Vector3(groundPitchDegrees, 0.0f, 0.0f);
                preview.transform.localPosition = new Vector3(0.0f, groundLift, 0.0f);
                var filter = preview.AddComponent<MeshFilter>();
                filter.sharedMesh = bakedMesh;
                var previewRenderer = preview.AddComponent<MeshRenderer>();
                previewRenderer.sharedMaterial = usedMaterial;
                garment.GroundPreview = preview;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SkinnedGarment.InteractRange;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {rootName} written to {prefabPath} -- " +
                          $"{bakedMesh.vertexCount} vertices, {sourceRenderer.bones.Length} bones");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Copies the imported mesh's vertices/triangles/weights, then
        /// replaces its bindposes with ones computed against a FRESH,
        /// unposed instance of this rig's own skeleton.
        ///
        /// THE BINDPOSE HAS TO BE COMPUTED AT REST, NOT AT RUNTIME. A live
        /// player's Animator is almost never sitting in its rest pose --
        /// it is walking, idling, mid-swing -- so reading
        /// Animator.GetBoneTransform() at equip time and using THAT as a
        /// bindpose would bake in whatever pose happened to be playing at
        /// that instant, and the garment would subtly warp relative to the
        /// body in every other pose. FusedBodyBuilder.Build hits the exact
        /// same requirement for the body's own mesh, and solves it the
        /// same way: bind against a rig that has just been built and never
        /// posed.
        ///
        /// BUT NOT THE SAME REST. FusedBodyBuilder generates the body's own
        /// mesh FRESH from BodyRig.Bones() (T-pose), so binding it against
        /// that same T-pose is self-consistent by construction. This mesh
        /// is not generated -- it is imported verbatim from hooded_cloak.glb
        /// / skelli_jeans.glb, authored against fused_body.gd's own
        /// arms-down rest, which is what BodyRig.RestBones() reconstructs.
        /// Binding arms-down vertex data against T-pose bone positions was
        /// the actual cause of the cloak's sleeves visibly tearing away
        /// from the arms: see RestBones's own doc comment for the numbers.
        /// </summary>
        private static Mesh BakeMesh(SkinnedMeshRenderer source, string path)
        {
            GameObject skeletonRoot = BodyRigBuilder.BuildSkeleton(
                "GarmentBindSkeleton", out Dictionary<string, Transform> named, BodyRig.RestBones());
            try
            {
                var bindPoses = new Matrix4x4[source.bones.Length];
                for (int i = 0; i < source.bones.Length; i++)
                {
                    string boneName = source.bones[i] != null ? source.bones[i].name : null;
                    if (boneName == null || !named.TryGetValue(boneName, out Transform bone))
                    {
                        Debug.LogError($"CityX: no bone '{boneName}' on this rig -- " +
                                        "the garment will not bind correctly at that vertex.");
                        bindPoses[i] = Matrix4x4.identity;
                        continue;
                    }
                    // Maps from the mesh's space into the bone's, so the
                    // bone's own transform undoes it exactly at rest --
                    // the same formula FusedBodyBuilder.Build uses for the
                    // body's own SkinnedMeshRenderer.
                    bindPoses[i] = bone.worldToLocalMatrix * skeletonRoot.transform.localToWorldMatrix;
                }

                Mesh baked = Object.Instantiate(source.sharedMesh);
                baked.name = source.sharedMesh.name;
                baked.bindposes = bindPoses;

                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(baked, path);
                AssetDatabase.SaveAssets();
                return baked;
            }
            finally
            {
                Object.DestroyImmediate(skeletonRoot);
            }
        }

        // --- visual verification ---

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

        // Matches CharacterCapture.cs's own Rest/Stride poses exactly, so a
        // wearable is judged in the same two shapes the bare body already
        // is -- arms-down rest is where a bad bindpose shows as a static
        // gap, and a mid-stride bend is where it shows as tearing.
        private static readonly Turn[] RestPose =
        {
            new Turn("arm_l", 0.0f, 0.0f, 78.0f),
            new Turn("forearm_l", 0.0f, 0.0f, 12.0f),
            new Turn("arm_r", 0.0f, 0.0f, -78.0f),
            new Turn("forearm_r", 0.0f, 0.0f, -12.0f),
        };

        private static readonly Turn[] StridePose =
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
        /// The rebuilt cloak and jeans, worn, at rest and mid-stride, close
        /// up on a dark background -- the same framing and pose set
        /// CharacterCapture.Capture already uses for the bare body, so this
        /// is judged the same way. Self-contained rather than calling into
        /// CharacterCapture directly: that lives in the Character/Editor
        /// assembly, which cannot reference Wearables or Items without
        /// inverting this project's own dependency direction (Wearables may
        /// depend on Character, never the reverse).
        ///
        /// Equips through the real SkinnedGarment.OnEquip, not a hand-rolled
        /// stand-in -- what gets rendered is exactly what the game itself
        /// builds when a player presses the equip button.
        /// </summary>
        [MenuItem("CityX/Capture Wearable Screenshots")]
        public static void CaptureWearables()
        {
            var bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (bodyPrefab == null)
            {
                Debug.LogError("CityX: no player prefab at Assets/CityX/Character/Player.prefab.");
                return;
            }

            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Shoot(bodyPrefab, CloakPrefabPath, outputDirectory, "cloak-front", 0.0f, RestPose);
            Shoot(bodyPrefab, CloakPrefabPath, outputDirectory, "cloak-stride", 215.0f, StridePose);
            Shoot(bodyPrefab, JeansPrefabPath, outputDirectory, "jeans-front", 0.0f, RestPose);
            Shoot(bodyPrefab, JeansPrefabPath, outputDirectory, "jeans-stride", 215.0f, StridePose);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Shoot(GameObject bodyPrefab, string garmentPrefabPath, string directory,
            string name, float yaw, IEnumerable<Turn> pose)
        {
            var root = new GameObject("WearableCaptureRig");
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;

            var previousAmbient = RenderSettings.ambientMode;
            var previousSky = RenderSettings.ambientSkyColor;
            bool previousFog = RenderSettings.fog;

            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientSkyColor = new Color(0.34f, 0.36f, 0.40f);

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.0f;
                sun.transform.rotation = Quaternion.Euler(35.0f, 150.0f + yaw, 0.0f);

                var body = (GameObject)PrefabUtility.InstantiatePrefab(bodyPrefab);
                body.transform.SetParent(root.transform);
                body.transform.position = Vector3.zero;
                body.transform.rotation = Quaternion.Euler(0.0f, yaw, 0.0f);

                foreach (Turn turn in pose)
                {
                    Transform bone = FindDeep(body.transform, turn.Bone);
                    if (bone == null)
                    {
                        Debug.LogWarning($"CityX: no bone called '{turn.Bone}' -- that part of the pose did nothing.");
                        continue;
                    }
                    bone.localRotation = Quaternion.Euler(turn.Euler);
                }

                GameObject garmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(garmentPrefabPath);
                if (garmentPrefab == null)
                {
                    Debug.LogWarning($"CityX: no garment prefab at {garmentPrefabPath} -- " +
                                      "run its own Rebuild menu item first.");
                }
                else
                {
                    var garmentInstance = (GameObject)PrefabUtility.InstantiatePrefab(garmentPrefab);
                    garmentInstance.transform.SetParent(body.transform, false);
                    var garment = garmentInstance.GetComponent<SkinnedGarment>();
                    garment.OnEquip(body, ItemInstanceData.None);
                }

                // The renderer only re-skins when it thinks something
                // changed, and outside play mode nothing tells it the bones
                // moved.
                foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
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
                cameraObject.transform.position = new Vector3(0.0f, 1.35f, -5.2f);
                cameraObject.transform.rotation = Quaternion.identity;

                target = new RenderTexture(700, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(700, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 700, 900), 0, 0);
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
    }
}
