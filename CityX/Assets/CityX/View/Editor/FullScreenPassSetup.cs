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
            FullScreenPassRendererFeature feature = EnsureFeature<FullScreenPassRendererFeature>(data, featureName);
            feature.SetActive(true);
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.injectionPoint = point;
            feature.fetchColorBuffer = true;
            feature.requirements = needs;
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        /// <summary>
        /// The renderer feature of type <typeparamref name="T"/> called <paramref name="featureName"/> on <paramref name="data"/>, made when there
        /// is none; one of another type by that name (an effect that moved to a feature of its own) is taken off first.
        /// </summary>
        public static T EnsureFeature<T>(UniversalRendererData data, string featureName) where T : ScriptableRendererFeature
        {
            T found = null;
            foreach (ScriptableRendererFeature f in data.rendererFeatures.ToArray())
            {
                if (f == null || f.name != featureName)
                {
                    continue;
                }
                if (f is T typed)
                {
                    found = typed;
                }
                else
                {
                    Remove(data, f);
                }
            }
            if (found != null)
            {
                return found;
            }

            // The way the renderer's own inspector adds a feature: a sub-asset, then its entry in both serialized lists (the list and the id map).
            var feature = ScriptableObject.CreateInstance<T>();
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
            return feature;
        }

        /// <summary><paramref name="feature"/> off the renderer: out of both serialized lists, and its sub-asset deleted.</summary>
        private static void Remove(UniversalRendererData data, ScriptableRendererFeature feature)
        {
            var so = new SerializedObject(data);
            SerializedProperty list = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.objectReferenceValue == feature)
                {
                    entry.objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(i);
                    map.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedProperties();
            AssetDatabase.RemoveObjectFromAsset(feature);
            Object.DestroyImmediate(feature, true);
        }
    }
}
