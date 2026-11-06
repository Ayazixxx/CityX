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
            Shader shader = CityX.Core.LitShader.Find();
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;   // a skin made before the cel look was on URP's own Lit

            material.SetColor("_BaseColor", colour);
            Core.LitShader.MakeMatte(material);
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
                // Copied whole rather than pixel by pixel: a compressed atlas (the swordsman's) cannot take SetPixels32.
                EditorUtility.CopySerialized(atlas, savedAtlas);
                savedAtlas.name = $"{name}Atlas";
                EditorUtility.SetDirty(savedAtlas);
            }

            string materialPath = $"{Directory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = CityX.Core.LitShader.Find();
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;   // a skin made before the cel look was on URP's own Lit

            // White underneath, or the paint comes out tinted -- same
            // reasoning as CharacterIdentity.ApplyPaint's own copy of this.
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", savedAtlas);
            }
            material.mainTexture = savedAtlas;
            material.SetColor("_BaseColor", Color.white);
            Core.LitShader.MakeMatte(material);
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
