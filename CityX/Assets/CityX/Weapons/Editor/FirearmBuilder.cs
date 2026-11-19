using System.Collections.Generic;
using CityX.CharacterEditor;
using CityX.Core;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// BUILDS A FIREARM PREFAB FROM ITS BLENDER MODEL (2026-10-14: the Glock and the AK-47, CityX/SourceArt/Glock and /AK47). The model is a .glb
    /// of separate parts in the gun's own frame (x right, y up, z the muzzle, the origin at the web of the grip); each part becomes its own
    /// child here, its mesh baked to a small asset with no textures (flat-coloured URP materials, by the Blender material's name), its origin
    /// where it moves about, its rotation the identity -- exactly how the reload motions' props were exported, so a motion's part poses apply
    /// as they are. Then the muzzle flash at the profile's muzzle, the shot voices, the reload's voice, and the PistolWeapon with the profile.
    /// </summary>
    public static class FirearmBuilder
    {
        public const string ShotAudioPath = "Assets/CityX/Weapons/Audio/pistol_shot.mp3";
        public const string SparkMaterialPath = "Assets/CityX/Weapons/PistolSpark.mat";

        /// <summary>Independent voices for the gunshot, so overlapping shots never re-pitch each other (PistolWeapon.ShotPlayerPool).</summary>
        public const int ShotVoiceCount = 8;

        private static readonly Color SparkColour = new Color(1.0f, 0.85f, 0.4f);
        private static readonly Color MuzzleLightColour = new Color(1.0f, 0.75f, 0.35f);

        /// <summary>Rebuilds every firearm and logs each one's parts (where they sit, how big) -- the quick check after a model changes.</summary>
        [MenuItem("CityX/Rebuild Firearms")]
        public static void BuildAll()
        {
            foreach (GameObject prefab in new[] { PistolBuilder.Build(), AK47Builder.Build(), ShotgunBuilder.Build(), RPGBuilder.Build() })
            {
                if (prefab == null)
                {
                    continue;
                }
                var log = new System.Text.StringBuilder($"CityX firearm {prefab.name}:\n");
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    Bounds b = filter.sharedMesh.bounds;
                    log.AppendLine($"  {filter.name,-10} at {filter.transform.localPosition.ToString("0.000")} mesh centre {b.center.ToString("0.000")} size {b.size.ToString("0.000")} submeshes {filter.sharedMesh.subMeshCount}");
                }
                var weapon = prefab.GetComponent<PistolWeapon>();
                log.AppendLine($"  muzzle {weapon.MuzzleFlashObject.transform.localPosition.ToString("0.000")}, action {(weapon.ActionPart != null ? weapon.ActionPart.name : "none")}, " +
                               $"magazine {(weapon.MagazinePart != null ? weapon.MagazinePart.name : "none")}, trigger {(weapon.TriggerPart != null ? weapon.TriggerPart.name : "none")}");
                Debug.Log(log.ToString());
            }
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>A Blender material's look here: colour, metallic, smoothness.</summary>
        public struct Finish
        {
            public Color Colour;
            public float Metallic;
            public float Smoothness;

            public Finish(Color colour, float metallic, float smoothness)
            {
                Colour = colour;
                Metallic = metallic;
                Smoothness = smoothness;
            }
        }

        /// <summary>
        /// Builds and saves the prefab. <paramref name="gunNode"/> is the model's root object's name in Blender (its parts are its children);
        /// <paramref name="finishes"/> maps the Blender material names to looks; <paramref name="shotAudioPath"/> is its shot's recording (the pistol's unless it has one).
        /// </summary>
        public static GameObject Build(string glbPath, string prefabPath, string meshFolder, string rootName, string gunNode,
            FirearmProfile profile, IDictionary<string, Finish> finishes, string shotAudioPath = ShotAudioPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (model == null)
            {
                Debug.LogError($"CityX: no model at {glbPath} -- run its Blender build script (CityX/SourceArt).");
                return null;
            }
            Transform gun = TransformSearch.FindDeep(model.transform, gunNode) ?? model.transform;
            System.IO.Directory.CreateDirectory(meshFolder);

            var root = new GameObject(rootName);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);

                var parts = new Dictionary<string, Transform>();
                foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                    {
                        continue;
                    }
                    // The part's place in the gun's frame; its rotation and scale (if the import added any) baked into the vertices.
                    Matrix4x4 relative = gun.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3 origin = relative.GetColumn(3);
                    Matrix4x4 bake = Matrix4x4.Translate(-origin) * relative;
                    Mesh mesh = BakedMesh(filter.sharedMesh, bake, $"{meshFolder}/{filter.name}.asset", rootName + filter.name);

                    var part = new GameObject(filter.name);
                    part.transform.SetParent(visual.transform, false);
                    part.transform.localPosition = origin;
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    MeshRenderer source = filter.GetComponent<MeshRenderer>();
                    var materials = new Material[mesh.subMeshCount];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        string sourceName = source != null && i < source.sharedMaterials.Length && source.sharedMaterials[i] != null
                            ? source.sharedMaterials[i].name : "";
                        materials[i] = MaterialFor(sourceName, finishes);
                    }
                    renderer.sharedMaterials = materials;
                    parts[filter.name] = part.transform;
                }

                root.transform.localScale = Vector3.one * profile.WorldPickupScale;
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = PistolWeapon.InteractRange / profile.WorldPickupScale;

                GameObject muzzleFlash = BuildMuzzleFlash(root.transform, profile.MuzzleLocal, out ParticleSystem sparks);

                AudioSource shotPlayer = BuildAudioSource(root.transform, "ShotPlayer", shotAudioPath);
                var shotPool = new AudioSource[ShotVoiceCount];
                shotPool[0] = shotPlayer;
                for (int i = 1; i < ShotVoiceCount; i++)
                {
                    shotPool[i] = BuildAudioSource(root.transform, $"ShotPlayer{i + 1}", shotAudioPath);
                }
                AudioSource reloadPlayer = BuildAudioSource(root.transform, "ReloadPlayer", null);

                var weapon = root.AddComponent<PistolWeapon>();
                weapon.Profile = profile;
                weapon.VisualRoot = visual.transform;
                weapon.ActionPart = Find(parts, profile.ActionPartName);
                weapon.MagazinePart = Find(parts, profile.MagazinePartName);
                weapon.TriggerPart = Find(parts, profile.TriggerPartName);
                weapon.MuzzleFlashObject = muzzleFlash;
                weapon.MuzzleLight = muzzleFlash.GetComponent<Light>();
                weapon.MuzzleSparks = sparks;
                weapon.ShotPlayer = shotPlayer;
                weapon.ShotPlayerPool = shotPool;
                weapon.ReloadPlayer = reloadPlayer;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {profile.DisplayName} written to {prefabPath} ({parts.Count} parts)");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform Find(Dictionary<string, Transform> parts, string name)
        {
            if (!string.IsNullOrEmpty(name) && parts.TryGetValue(name, out Transform part))
            {
                return part;
            }
            Debug.LogWarning($"CityX: the model has no part named '{name}' -- it will not move.");
            return null;
        }

        private static Mesh BakedMesh(Mesh source, Matrix4x4 bake, string path, string name)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Matrix4x4 normalMatrix = bake.inverse.transpose;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = bake.MultiplyPoint3x4(vertices[i]);
                if (i < normals.Length)
                {
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                }
            }
            var mesh = new Mesh { name = name, vertices = vertices };
            if (normals.Length == vertices.Length)
            {
                mesh.normals = normals;
            }
            mesh.subMeshCount = source.subMeshCount;
            // A reflection in the bake (the import's mirror) would turn every face inside out: flip the winding back.
            bool mirrored = bake.determinant < 0.0f;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] triangles = source.GetTriangles(s);
                if (mirrored)
                {
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                    }
                }
                mesh.SetTriangles(triangles, s);
            }
            if (normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material MaterialFor(string sourceName, IDictionary<string, Finish> finishes)
        {
            // glTFast may suffix an imported material's name; match on the Blender name it starts with.
            foreach (KeyValuePair<string, Finish> pair in finishes)
            {
                if (sourceName.StartsWith(pair.Key))
                {
                    Material material = CharacterSkin.Ensure(pair.Key, pair.Value.Colour);
                    material.SetFloat("_Metallic", pair.Value.Metallic);
                    material.SetFloat("_Smoothness", pair.Value.Smoothness);
                    EditorUtility.SetDirty(material);
                    return material;
                }
            }
            Debug.LogWarning($"CityX: no finish for the model's material '{sourceName}' -- gunmetal used.");
            Material fallback = CharacterSkin.Ensure("PistolGunmetal", new Color(0.16f, 0.16f, 0.17f));
            fallback.SetFloat("_Metallic", 0.75f);
            fallback.SetFloat("_Smoothness", 0.65f);
            return fallback;
        }

        /// <summary>A point light and a short burst of sparks at the muzzle (pistol.tscn's MuzzleFlash/MuzzleSparks); off until a shot flips it on.</summary>
        public static GameObject BuildMuzzleFlash(Transform parent, Vector3 muzzleLocal, out ParticleSystem sparks)
        {
            var flash = new GameObject("MuzzleFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.localPosition = muzzleLocal;

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

            // A real asset, not a loose material: a loose one does not survive SaveAsPrefabAsset (it came back null: pink sparks).
            Material sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
            if (sparkMaterial == null)
            {
                sparkMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "PistolSpark" };
                AssetDatabase.CreateAsset(sparkMaterial, SparkMaterialPath);
            }
            sparkMaterial.SetColor("_BaseColor", SparkColour);
            renderer.sharedMaterial = sparkMaterial;

            sparks.Stop();
            return flash;
        }

        /// <summary>A 2D voice (the player's own gun plays at one flat volume wherever the camera is: an explicit report about distance fading).</summary>
        public static AudioSource BuildAudioSource(Transform parent, string name, string clipPath)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = string.IsNullOrEmpty(clipPath) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            source.playOnAwake = false;
            source.spatialBlend = 0.0f;
            if (!string.IsNullOrEmpty(clipPath) && source.clip == null)
            {
                Debug.LogWarning($"CityX: no audio clip at {clipPath} -- {name} will play silently.");
            }
            return source;
        }
    }
}
