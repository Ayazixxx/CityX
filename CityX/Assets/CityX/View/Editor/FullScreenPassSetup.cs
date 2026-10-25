using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts a full-screen pass (a material drawn over the whole frame: the city's air, the cel look's lines) onto the PC and mobile renderers as a
    /// URP Full Screen Pass renderer feature, with its material as an asset. Idempotent: a renderer that already has the feature keeps it, its
    /// settings set again.
    /// </summary>
    public static class FullScreenPassSetup
    {
        public static readonly string[] RendererPaths = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };

        /// <summary>The pass's material at <paramref name="path"/>, made from <paramref name="shaderName"/> if there is none yet.</summary>
        public static Material EnsureMaterial(string shaderName, string path, string name)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"CityX: shader {shaderName} not found");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The feature <paramref name="featureName"/> on every renderer, drawing <paramref name="material"/> at <paramref name="point"/>.</summary>
        public static void Ensure(string featureName, Material material, FullScreenPassRendererFeature.InjectionPoint point,
            ScriptableRenderPassInput needs)
        {
            foreach (string path in RendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (material == null || data == null)
                {
                    Debug.LogError($"CityX: renderer {path} or the material for {featureName} is missing");
                    continue;
                }
                Ensure(data, featureName, material, point, needs);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Ensure(UniversalRendererData data, string featureName, Material material,
            FullScreenPassRendererFeature.InjectionPoint point, ScriptableRenderPassInput needs)
        {
            FullScreenPassRendererFeature feature = null;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
            {
                if (f is FullScreenPassRendererFeature full && f.name == featureName)
                {
                    feature = full;
                }
            }
            if (feature == null)
            {
                // The way the renderer's own inspector adds a feature: a sub-asset, then its entry in both serialized lists (the list and the id map).
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = featureName;
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
            feature.injectionPoint = point;
            feature.fetchColorBuffer = true;
            feature.requirements = needs;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }
}
