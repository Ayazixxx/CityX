using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Materials for character bodies.
    ///
    /// WHY THIS HAS TO EXIST. The Mixamo models are imported with
    /// materialImportMode set to None, because importing their placeholder
    /// materials just litters the project. The consequence is that every
    /// skinned mesh comes out of the importer with NO material, and a renderer
    /// with no material draws in URP's missing-shader magenta. Nothing errors;
    /// the character simply arrives bright pink.
    ///
    /// So the skin is assigned deliberately, which also gives civilians a way
    /// to look like somebody other than the player.
    /// </summary>
    public static class CharacterSkin
    {
        private const string Directory = "Assets/CityX/Character/Materials";

        public static readonly Color PlayerColour = new Color(0.72f, 0.60f, 0.52f);

        /// <summary>
        /// Pale and desaturated, so a crowd reads as background rather than
        /// competing with the player for attention. The Godot build made its
        /// civilians plain white for the same reason.
        /// </summary>
        public static readonly Color CivilianColour = new Color(0.78f, 0.78f, 0.80f);

        public static Material Ensure(string name, Color colour)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = $"{Directory}/{name}.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit shader not found -- characters will be magenta.");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            // RESET UNCONDITIONALLY, NOT ONLY ON CREATE. /celshade swaps this
            // same shared asset to CityX/ToonLit at runtime, and a Material
            // asset's shader -- unlike scene object state -- does not revert
            // when Play mode exits, so a session that ends mid-toggle leaves
            // the toon shader baked onto disk. Rebuilding the skin has to be
            // able to put it back on Lit, or the very first /celshade after
            // that looks like it does nothing (it's toggling ON a material
            // that was already stuck ON).
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                material.shader = litShader;
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A material carrying a real painted atlas rather than a flat colour
        /// -- OldGuyBuilder's own equivalent of what CharacterIdentity.Apply
        /// does for a live player body (see its own ApplyPaint), baked at
        /// build time instead of applied at runtime, since a design-baked NPC
        /// has no CharacterIdentity driving it.
        ///
        /// THE TEXTURE IS SAVED AS A REAL ASSET, NOT LEFT LOOSE -- the same
        /// lesson PistolSpark's own material learned: a loose in-memory
        /// Texture2D does not reliably survive PrefabUtility.SaveAsPrefabAsset,
        /// so this creates (or updates in place, so re-running the build after
        /// a repaint picks up the change) a real .asset file for it first.
        /// </summary>
        public static Material EnsurePainted(string name, Texture2D atlas)
        {
            System.IO.Directory.CreateDirectory(Directory);

            string texturePath = $"{Directory}/{name}Atlas.asset";
            Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (savedAtlas == null)
            {
                AssetDatabase.CreateAsset(atlas, texturePath);
                savedAtlas = atlas;
            }
            else if (savedAtlas != atlas)
            {
                savedAtlas.SetPixels32(atlas.GetPixels32());
                savedAtlas.Apply();
                EditorUtility.SetDirty(savedAtlas);
            }

            string materialPath = $"{Directory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit shader not found -- characters will be magenta.");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                material.shader = litShader;
            }

            // White underneath, or the paint comes out tinted -- same
            // reasoning as CharacterIdentity.ApplyPaint's own copy of this.
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", savedAtlas);
            }
            material.mainTexture = savedAtlas;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// One material shared by every wearable in a category (e.g. every
        /// helmet), instead of one unique Material asset per item --
        /// EXPLICIT ASK: preparing the equip pipeline for "100s if not
        /// 1000s" of clothing items worn by many players in the same area
        /// at once. Every item in a category referencing this SAME asset is
        /// what lets URP's SRP Batcher amortise per-object rendering cost
        /// across many characters regardless of catalog size; per-item
        /// colour still works because each renderer carries its own tint
        /// via a MaterialPropertyBlock (see CityX.Wearables.WearableTint,
        /// applied at runtime rather than here -- this method only compiles
        /// in the editor) rather than its own unique Material.
        ///
        /// ONLY FOR FLAT-COLOUR PROCEDURAL WEARABLES. A genuinely textured
        /// item -- SkelliJeansFabric's own denim photo, for one -- still
        /// needs its own material carrying its own texture; forcing a real
        /// texture map onto a shared flat-tint material would just lose it.
        /// This is for the common case a large catalog will mostly be
        /// instead: simple recoloured primitives or cut meshes, the same
        /// shape TopHat and FarmerHat already are.
        /// </summary>
        public static Material EnsureSharedCategory(string category)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = $"{Directory}/Wearable{category}Shared.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit shader not found -- wearables will be magenta.");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                // Lets Unity batch many characters' own renderers that all
                // reference this exact asset, even though each one still
                // carries its own per-renderer tint below.
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Paints every renderer under <paramref name="root"/>. Applies to the
        /// whole hierarchy rather than one named mesh, because the Mixamo body
        /// arrives as two skinned meshes and which is which is the exporter's
        /// business, not ours.
        /// </summary>
        public static void Apply(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length == 0
                    ? 1
                    : renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
