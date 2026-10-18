using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The light trailing from a swung blade (BladeTrail: the sword spin, Octaslash): additive white, sharing the sword wave's streak texture
    /// so every sword light reads as the same kind of light.
    /// </summary>
    public static class SwordSlashMaterialBuilder
    {
        private const string MaterialPath = "Assets/CityX/Character/SwordSpinTrail.mat";
        private const string TexturePath = "Assets/CityX/Weapons/SwordWaveStreaks.png";
        private const string ShaderName = "Legacy Shaders/Particles/Additive";

        private const float Intensity = 1.1f;

        public static Material Ensure()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = Shader.Find(ShaderName);
            var streaks = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (streaks != null)
            {
                material.SetTexture("_MainTex", streaks);
            }
            // This shader draws 2 x tint x texture, so the tint is half the wanted brightness.
            material.SetColor("_TintColor", new Color(Intensity * 0.5f, Intensity * 0.5f, Intensity * 0.5f, 1.0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
