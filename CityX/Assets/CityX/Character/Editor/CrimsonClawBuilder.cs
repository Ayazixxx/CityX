using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the Crimson Claw monster prefab from the imported
    /// npc/crimson_claw_monster.glb (copied in as
    /// Assets/CityX/NPC/Models/CrimsonClaw.glb, and its 4 audio clips already
    /// present at Assets/CityX/NPC/Audio -- both reused wholesale from the
    /// Godot project, matching [[port_reuse_godot_assets]]).
    ///
    /// The .glb carries no extra body to cut away -- it ships exactly 3 named mesh nodes ("Body",
    /// "LeftClaw", "RightClaw", a crude offline region-based split done
    /// once in Blender, not reproduced here), found by name via
    /// FindMeshFilterNamed the same way the source .gd's own
    /// _find_mesh_instance_named does, and used AS-IS with no vertex
    /// surgery.
    ///
    /// All the visual construction (facing flip, claw pivots, collision
    /// box, audio sources) happens HERE, once,
    /// at build time -- CrimsonClaw.cs itself is behaviour-only and just
    /// holds references to what this method wires up, the same split every
    /// other prefab in this project already keeps (compare JetpackBuilder
    /// building geometry while Jetpack.cs only ever reads fields). This is a deliberate difference from the source
    /// .gd, which builds its whole model procedurally at runtime in
    /// _ready() -- there is no Editor/runtime split in Godot to mirror.
    /// </summary>
    public static class CrimsonClawBuilder
    {
        private const string GlbPath = "Assets/CityX/NPC/Models/CrimsonClaw.glb";
        private const string AudioDirectory = "Assets/CityX/NPC/Audio";
        private const string PrefabPath = "Assets/CityX/Character/CrimsonClaw.prefab";
        private const string MaterialPath = "Assets/CityX/Character/Materials/CrimsonClawBody.mat";

        private static readonly Color BodyColor = new Color(0.94f, 0.94f, 0.92f);

        /// <summary>
        /// The eyes: what is seen deep inside its head through the two slits across its face (SourceArt/CrimsonClaw/claw_patch.py gives it
        /// this material name), glowing red (owner, 2026-11-07: "make it glow red from within the head only visible through the eyes"). Only
        /// the eyes give off light; nothing lights the rest of it. Dark red when the glow has gone out.
        /// </summary>
        private const string EyesSourceMaterial = "CrimsonClawEyes";
        private const string EyesMaterialPath = "Assets/CityX/Character/Materials/CrimsonClawEyes.mat";
        private static readonly Color EyesColor = new Color(0.16f, 0.01f, 0.01f);

        private const float AudioMinDistance = 3.0f;
        private const float AudioMaxDistance = 20.0f;

        [MenuItem("CityX/Rebuild Crimson Claw")]
        public static GameObject Build()
        {
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (imported == null)
            {
                Debug.LogError($"CityX: {GlbPath} missing -- no Crimson Claw built.");
                return null;
            }

            var root = new GameObject("CrimsonClaw");
            try
            {
                Material bodyMaterial = BuildBodyMaterial();

                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * CrimsonClaw.ModelScale;

                // Bug report: "the crimson claw is backwards its
                // chasing me with its backside facing me." The source .gd's
                // own class comment says the raw mesh faces +Z in GODOT's
                // own imported coordinate space, needing a 180-degree
                // correction there -- but that fact is specific to Godot's
                // own glTF importer, not something that carries over to
                // glTFast's own independent (and differently handendess-
                // converted) import of the exact same .glb into Unity.
                // Confirmed directly: applying Godot's own 180-degree
                // correction here left the model facing exactly backwards
                // in Unity, so glTFast's import already lands the mesh
                // facing Unity's own +Z-forward convention with NO
                // correction needed. Kept as its own named child (rather
                // than removed outright) purely so the hierarchy still
                // reads the same way and nothing downstream has to change
                // if a future mesh re-export ever needs a real correction
                // again.
                var facingFlip = new GameObject("FacingFlip");
                facingFlip.transform.SetParent(model.transform, false);
                facingFlip.transform.localRotation = Quaternion.identity;

                Transform bodyVisual = BuildBodyPiece(imported, facingFlip.transform, bodyMaterial, BuildEyesMaterial(), out int eyesSlot);
                Transform leftClawPivot = BuildClawPivot(imported, facingFlip.transform, "LeftClaw", bodyMaterial);
                Transform rightClawPivot = BuildClawPivot(imported, facingFlip.transform, "RightClaw", bodyMaterial);

                // NO EYES -- Requested: "remove the crimson claws glowing
                // eyes we are going to redo those completely." Used to build
                // a pair of emissive spheres + point lights here (see git
                // history if the old approach is ever wanted for reference);
                // removed outright rather than left disabled, since a full
                // redo was asked for, not a tweak.

                // A simple box approximation of the model's own head+claws
                // footprint (unscaled bbox roughly 1.8 x 1.3 x 2.0), matching
                // the source's own comment on why this is a close-enough,
                // not pixel-matched, hitbox -- sits on the root itself
                // (unscaled), not under Model, so it stays a fixed size
                // regardless of ModelScale.
                BoxCollider collision = root.AddComponent<BoxCollider>();
                collision.size = new Vector3(1.8f, 1.3f, 2.0f) * CrimsonClaw.ModelScale;

                CrimsonClaw crimsonClaw = root.AddComponent<CrimsonClaw>();
                crimsonClaw.LeftClawPivot = leftClawPivot;
                crimsonClaw.RightClawPivot = rightClawPivot;
                crimsonClaw.BodyVisual = bodyVisual;
                crimsonClaw.EyesMaterialSlot = eyesSlot;
                crimsonClaw.MainCollision = collision;

                WireAudio(root, crimsonClaw);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: Crimson Claw written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The head and body, the skin on every part of the mesh but the eyes (<paramref name="eyesSlot"/>: theirs, -1 if it has none).</summary>
        private static Transform BuildBodyPiece(GameObject imported, Transform parent, Material skin, Material eyes, out int eyesSlot)
        {
            eyesSlot = -1;
            MeshFilter source = FindMeshFilterNamed(imported.transform, "Body");
            if (source == null)
            {
                Debug.LogError($"CityX: {GlbPath} has no 'Body' mesh -- Crimson Claw will have no torso.");
                return null;
            }
            var visual = new GameObject("Body");
            visual.transform.SetParent(parent, false);
            visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            Material[] imports = source.GetComponent<MeshRenderer>().sharedMaterials;
            var materials = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                bool isEyes = i < imports.Length && imports[i] != null && imports[i].name.StartsWith(EyesSourceMaterial);
                materials[i] = isEyes ? eyes : skin;
                if (isEyes)
                {
                    eyesSlot = i;
                }
            }
            visual.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return visual.transform;
        }

        private static Material BuildEyesMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(EyesMaterialPath);
            if (material == null)
            {
                material = new Material(CityX.Core.LitShader.Find()) { name = "CrimsonClawEyes" };
                AssetDatabase.CreateAsset(material, EyesMaterialPath);
            }
            material.SetColor("_BaseColor", EyesColor);
            CityX.Core.LitShader.MakeMatte(material);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", CrimsonClaw.EyesGlow);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // with none, the keyword is dropped as the material is saved
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// A pivot at the named claw's wrist (<see cref="WristOf"/>) with the claw mesh as its child, offset so it renders in exactly its
        /// original spot at rest: turning the pivot swings the claw about its wrist. The pivots used to be fixed points from the Godot port,
        /// which in this import sat behind the body and on the other claw's side, so every swing flung the claw round a two-metre loop.
        /// </summary>
        private static Transform BuildClawPivot(GameObject imported, Transform parent, string meshName, Material material)
        {
            var pivot = new GameObject(meshName + "Pivot");
            pivot.transform.SetParent(parent, false);

            MeshFilter source = FindMeshFilterNamed(imported.transform, meshName);
            if (source == null)
            {
                Debug.LogWarning($"CityX: {GlbPath} has no '{meshName}' mesh -- that claw will be invisible.");
                return pivot.transform;
            }
            Vector3 pivotPosition = WristOf(source.sharedMesh);
            pivot.transform.localPosition = pivotPosition;
            var visual = new GameObject(meshName);
            visual.transform.SetParent(pivot.transform, false);
            visual.transform.localPosition = -pivotPosition;
            visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            return pivot.transform;
        }

        /// <summary>A claw's wrist: the middle of its back end (the model faces +Z, its talons reach forward from the wrist).</summary>
        public static Vector3 WristOf(Mesh claw)
        {
            Bounds bounds = claw.bounds;
            return new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
        }

        private static Material BuildBodyMaterial()
        {
            Shader shader = CityX.Core.LitShader.Find();
            var material = new Material(shader) { name = "CrimsonClawBody" };
            material.SetColor("_BaseColor", BodyColor);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Metallic", 0.0f);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(MaterialPath);
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Three persistent, reused players (not disposable one-shots) -- matches the source's own _build_audio_players, one dedicated player per sound role so an attack bark can't cut off an in-flight growl.</summary>
        private static void WireAudio(GameObject root, CrimsonClaw crimsonClaw)
        {
            crimsonClaw.GrowlAudio = BuildPositionalAudioSource(root, "GrowlAudio");
            crimsonClaw.AttackAudio = BuildPositionalAudioSource(root, "AttackAudio");
            crimsonClaw.DeathAudio = BuildPositionalAudioSource(root, "DeathAudio");

            crimsonClaw.AttackSound = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDirectory + "/claw_attack.mp3");
            crimsonClaw.DeathSound = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDirectory + "/claw_death.mp3");
            crimsonClaw.NoticeSounds = new[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDirectory + "/claw_noise_1.mp3"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDirectory + "/claw_noise_2.mp3"),
            };

            if (crimsonClaw.AttackSound == null || crimsonClaw.DeathSound == null ||
                crimsonClaw.NoticeSounds[0] == null || crimsonClaw.NoticeSounds[1] == null)
            {
                Debug.LogWarning($"CityX: one or more Crimson Claw audio clips missing under {AudioDirectory} -- it may play some sounds silently.");
            }
        }

        private static AudioSource BuildPositionalAudioSource(GameObject root, string name)
        {
            var audioObject = new GameObject(name);
            audioObject.transform.SetParent(root.transform, false);
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = AudioMinDistance;
            source.maxDistance = AudioMaxDistance;
            return source;
        }

        /// <summary>Depth-first search for the first MeshFilter under `root` whose own GameObject name matches `targetName` -- glTFast preserves the .glb's own node names, the same way Godot's importer does, so this can look each piece up directly instead of guessing by order.</summary>
        private static MeshFilter FindMeshFilterNamed(Transform root, string targetName)
        {
            if (root.name == targetName)
            {
                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    return filter;
                }
            }
            foreach (Transform child in root)
            {
                MeshFilter found = FindMeshFilterNamed(child, targetName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
