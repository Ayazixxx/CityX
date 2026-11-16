using System.IO;
using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the flamethrower prefab -- a small procedural tank-and-nozzle prop (matching JetpackBuilder/GrapplingHookBuilder's own reasoning:
    /// no glTF importer exists in this project for a hand-authored model, so nothing here waits on one) -- and the streams of fire every fire
    /// weapon pours: the flamethrower's at its nozzle, the flame staff's on a prefab of its own, a jetpack's exhaust (JetpackBuilder). Each is
    /// a FlameStreamLook, the fire spells' burning gas poured along the stream at run time (docs/FIRE.md, "The fire spells"); since 2026-11-15
    /// the flamethrower and the jetpack too (owner: "just try to improve all the fire"), which burned three layers of soft additive dots.
    /// </summary>
    public static class FlamethrowerBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Flamethrower.prefab";
        public const string StaffFlamePrefabPath = "Assets/CityX/Weapons/StaffFlame.prefab";
        private const string SmokeMaterialPath = "Assets/CityX/Weapons/FlamethrowerSmoke.mat";
        private const string ParticleTexturePath = "Assets/CityX/Weapons/Textures/FlamethrowerParticle.png";

        private static readonly Color MetalColour = new Color(0.10f, 0.10f, 0.11f);

        private const float BodyLength = 0.34f;
        private const float BodyRadius = 0.045f;
        private const float NozzleLength = 0.10f;
        private const float NozzleRadius = 0.022f;
        private const float GripHeight = 0.14f;

        /// <summary>The flamethrower's stream against the flame staff's: a little smaller, from a nozzle 2 cm across.</summary>
        public const float GunFlameScale = 0.75f;

        [MenuItem("CityX/Rebuild Flamethrower")]
        public static GameObject Build()
        {
            var root = new GameObject("Flamethrower");
            try
            {
                BuildProp(root.transform);

                Transform nozzle = new GameObject("Nozzle").transform;
                nozzle.SetParent(root.transform, false);
                nozzle.localPosition = new Vector3(0.0f, 0.0f, BodyLength + NozzleLength);
                nozzle.localRotation = Quaternion.identity;

                // The Wraith's smoke is drawn with this material (WraithBuilder): it outlived the flamethrower's own particle smoke.
                BuildSmokeMaterial(BuildParticleTexture());
                Light flameLight = BuildFlameLight(nozzle);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Flamethrower.InteractRange;

                var flamethrower = root.AddComponent<Flamethrower>();
                flamethrower.Flame = BuildStream(nozzle, Flamethrower.Range, GunFlameScale);
                flamethrower.FlameLight = flameLight;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: flamethrower written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A stream of fire on an object of its own under <paramref name="parent"/>, its +Z the parent's (FlameStreamLook), <paramref name="reach"/>
        /// metres long dry and <paramref name="scale"/> times the flame staff's size: the flamethrower's and a jetpack's (JetpackBuilder).
        /// </summary>
        internal static FlameStreamLook BuildStream(Transform parent, float reach, float scale)
        {
            var flame = new GameObject("Flame");
            flame.transform.SetParent(parent, false);
            var look = flame.AddComponent<FlameStreamLook>();
            look.Reach = reach;
            look.Scale = scale;
            return look;
        }

        /// <summary>
        /// The flame staff's fire (2026-10-28, docs/STAFFS.md): the flamethrower's flickering light on a prefab of its own (FlameStreamLook) that
        /// FlameStreamCaster puts at the staff's head, its +Z the way the fire goes.
        /// </summary>
        public static GameObject BuildStaffFlame()
        {
            var root = new GameObject("StaffFlame");
            try
            {
                var look = root.AddComponent<FlameStreamLook>();
                look.Light = BuildFlameLight(root.transform);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StaffFlamePrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the staff flame written to {StaffFlamePrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A tank body, a front nozzle taper and a grip -- just enough
        /// geometry to read as a hand-held fire weapon, since the flame
        /// itself is what this build is actually about. Scaled Unity
        /// primitives throughout, not hand-copied meshes -- JetpackBuilder's
        /// own doc comment already found a hand-copied CylinderMesh
        /// produces zero-bounds renderers in this project.
        /// </summary>
        private static void BuildProp(Transform parent)
        {
            Material metal = CharacterSkin.Ensure("FlamethrowerMetal", MetalColour);
            metal.SetFloat("_Metallic", 0.7f);
            metal.SetFloat("_Smoothness", 0.3f);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0.0f, 0.0f, BodyLength * 0.5f);
            body.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            // Unity's primitive cylinder is 2 units tall with radius 1.
            body.transform.localScale = new Vector3(BodyRadius * 2.0f, BodyLength * 0.5f, BodyRadius * 2.0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = metal;

            GameObject muzzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            muzzle.name = "NozzleTube";
            Object.DestroyImmediate(muzzle.GetComponent<Collider>());
            muzzle.transform.SetParent(parent, false);
            muzzle.transform.localPosition = new Vector3(0.0f, 0.0f, BodyLength + NozzleLength * 0.5f);
            muzzle.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            muzzle.transform.localScale = new Vector3(NozzleRadius * 2.0f, NozzleLength * 0.5f, NozzleRadius * 2.0f);
            muzzle.GetComponent<MeshRenderer>().sharedMaterial = metal;

            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            Object.DestroyImmediate(grip.GetComponent<Collider>());
            grip.transform.SetParent(parent, false);
            grip.transform.localPosition = new Vector3(0.0f, -GripHeight * 0.5f, BodyLength * 0.25f);
            grip.transform.localScale = new Vector3(0.03f, GripHeight, 0.035f);
            grip.GetComponent<MeshRenderer>().sharedMaterial = metal;
        }

        /// <summary>
        /// Plain alpha transparency, deliberately NOT additive -- smoke darkens and occludes in reality; blended additively it would brighten
        /// what is behind it instead, reading as more fire rather than as smoke. A soft round sprite on it (BuildParticleTexture): Unity's
        /// Particles/Unlit shader with no texture draws each particle as a hard-edged square.
        /// </summary>
        private static void BuildSmokeMaterial(Texture2D particleTexture)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.color = Color.white;
            material.SetTexture("_BaseMap", particleTexture);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f); // Alpha

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(SmokeMaterialPath);
            }
            AssetDatabase.CreateAsset(material, SmokeMaterialPath);
        }

        /// <summary>
        /// A soft round sprite, generated rather than sourced -- a radial alpha falloff from opaque white at the centre to fully transparent at
        /// the edge. Written to a real imported PNG asset rather than kept as an in-memory Texture2D: a loose, unimported texture does not
        /// survive AssetDatabase.CreateAsset on the material that references it.
        /// </summary>
        private static Texture2D BuildParticleTexture()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float alpha = Mathf.Clamp01(1.0f - dist);
                    alpha = Mathf.Pow(alpha, 1.6f); // softer than linear -- a bright core, fading gently to nothing
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string directory = Path.GetDirectoryName(ParticleTexturePath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, directory));
            }
            File.WriteAllBytes(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, ParticleTexturePath), png);
            AssetDatabase.ImportAsset(ParticleTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ParticleTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);
        }

        /// <summary>
        /// Off at build time -- Flamethrower.TickFlicker turns it on (and
        /// makes it flicker) only while the trigger is actually held,
        /// matching MuzzleFlashObject's own "starts hidden" convention.
        /// </summary>
        private static Light BuildFlameLight(Transform parent)
        {
            var go = new GameObject("FlameLight");
            go.transform.SetParent(parent, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.55f, 0.2f);
            light.range = 5.0f;
            light.intensity = 0.0f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
