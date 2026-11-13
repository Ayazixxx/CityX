using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Glacier Spikes' ice (docs/GLACIER_SPIKES.md): a material on the CityX/Ice shader, as an asset in Resources, which GlacierSpikesField copies
    /// at runtime. An asset so that a player build keeps the shader (a shader no asset references is left out of a build). Instanced: the
    /// crystals are drawn many to a call. Rebuilt with the scene (CityMassingSceneBuilder).
    /// </summary>
    public static class IceMaterialBuilder
    {
        public const string Folder = "Assets/CityX/Character/Resources";

        [MenuItem("CityX/Rebuild Ice Material")]
        public static void Build()
        {
            Shader shader = Shader.Find(GlacierSpikesField.ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"CityX: no {GlacierSpikesField.ShaderName} shader -- Glacier Spikes will not draw.");
                return;
            }
            string path = $"{Folder}/{GlacierSpikesField.MaterialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
    }
}
