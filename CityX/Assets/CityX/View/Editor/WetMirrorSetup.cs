using CityX.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts the wet streets' mirror (<see cref="WetMirrorFeature"/>, Shaders/CityXWetMirror.hlsl) on the PC and mobile renderers, with the
    /// material that writes down each pixel's distance. Run by CityMassingSceneBuilder.Build every rebuild.
    /// </summary>
    public static class WetMirrorSetup
    {
        public const string MaterialPath = "Assets/CityX/View/Materials/CityXMirrorMemory.mat";
        public const string FeatureName = GraphicsQuality.ReflectionsFeature;

        public static Material EnsureMaterial() => FullScreenPassSetup.EnsureMaterial("CityX/MirrorMemory", MaterialPath, "CityXMirrorMemory");

        public static void EnsureFeature()
        {
            Material material = EnsureMaterial();
            foreach (string path in FullScreenPassSetup.RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (material == null || data == null)
                {
                    Debug.LogError($"CityX: renderer {path} or the wet mirror's material is missing");
                    continue;
                }
                WetMirrorFeature feature = FullScreenPassSetup.EnsureFeature<WetMirrorFeature>(data, FeatureName);
                feature.SetActive(true);
                feature.Material = material;
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
