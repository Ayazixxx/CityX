using CityX.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts the light shafts (<see cref="LightShaftsFeature"/>, Shaders/CityXLightShafts.shader) on the PC and mobile renderers, after the city's
    /// air. Their numbers are the material's own; the air's thickness is the haze's. Run by CityMassingSceneBuilder.Build every rebuild.
    /// </summary>
    public static class LightShaftsSetup
    {
        public const string MaterialPath = "Assets/CityX/View/Materials/CityXLightShafts.mat";
        public const string FeatureName = GraphicsQuality.LightShaftsFeature;

        public static Material EnsureMaterial() => FullScreenPassSetup.EnsureMaterial("CityX/LightShafts", MaterialPath, "CityXLightShafts");

        public static void EnsureFeature()
        {
            Material material = EnsureMaterial();
            foreach (string path in FullScreenPassSetup.RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (material == null || data == null)
                {
                    Debug.LogError($"CityX: renderer {path} or the light shafts' material is missing");
                    continue;
                }
                LightShaftsFeature feature = FullScreenPassSetup.EnsureFeature<LightShaftsFeature>(data, FeatureName);
                feature.SetActive(true);
                feature.Material = material;
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
