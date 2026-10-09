using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts the city's air (Shaders/CityXHaze.shader) into the frame: a material for it and a Full Screen Pass renderer feature on the PC renderer that
    /// draws it before post-processing, reading the depth texture (which the PC pipeline asset already has on). Idempotent: run by
    /// CityMassingSceneBuilder.Build every rebuild; a renderer that already has the feature is left as it is apart from its settings.
    /// The haze's numbers are globals DayNightCycle sets each frame; with none set (any other scene) the pass leaves the image alone.
    /// </summary>
    public static class HazeSetup
    {
        public const string MaterialPath = "Assets/CityX/View/Materials/CityXHaze.mat";
        public static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        public const string FeatureName = "CityXHaze";

        public static Material EnsureMaterial()
        {
            Shader shader = Shader.Find("CityX/Haze");
            if (shader == null)
            {
                Debug.LogError("CityX haze: shader CityX/Haze not found");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CityXHaze" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void EnsureFeature()
        {
            Material material = EnsureMaterial();
            foreach (string path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (material == null || data == null)
                {
                    Debug.LogError($"CityX haze: renderer {path} or the material is missing");
                    continue;
                }
                EnsureFeature(data, material);
            }
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFeature(UniversalRendererData data, Material material)
        {
            FullScreenPassRendererFeature feature = null;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
            {
                if (f is FullScreenPassRendererFeature full && f.name == FeatureName)
                {
                    feature = full;
                }
            }
            if (feature == null)
            {
                // The way the renderer's own inspector adds a feature: a sub-asset, then its entry in both serialized lists (the list and the id map).
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var so = new SerializedObject(data);
                SerializedProperty list = so.FindProperty("m_RendererFeatures");
                SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }

            feature.SetActive(true);
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }
}
