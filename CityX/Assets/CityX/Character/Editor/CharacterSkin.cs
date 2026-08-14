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
