using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The fire's two materials (docs/FIRE.md), on the CityX/Fire shader, as assets in Resources: FireSprites copies them at runtime and puts
    /// the noise on the copies. Assets so that a player build keeps the shader (a shader no asset references is left out of a build; the
    /// noise itself is made at runtime and is no asset). Rebuilt with the scene (CityMassingSceneBuilder).
    /// </summary>
    public static class FireMaterialsBuilder
    {
        public const string Folder = "Assets/CityX/Character/Resources";

        [MenuItem("CityX/Rebuild Fire Materials")]
        public static void Build()
        {
            Ensure(FireSprites.FlameMaterialName, 0.0f);
            Ensure(FireSprites.SmokeMaterialName, 1.0f);
            AssetDatabase.SaveAssets();
        }

        private static void Ensure(string name, float mode)
        {
            Shader shader = Shader.Find(FireSprites.ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"CityX: no {FireSprites.ShaderName} shader -- the fire will not draw.");
                return;
            }
            string path = $"{Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Mode", mode);
            EditorUtility.SetDirty(material);
        }
    }
}
