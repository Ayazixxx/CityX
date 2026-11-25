using CityX.Core;
using CityX.WeaponsEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// A rigid wearable made of named parts, each with its own material (the 3D Glasses' frame and lenses, the Squirrel Mask's fur and black):
    /// each part baked as it lies in the imported model's frame and hung under the prefab's root.
    /// </summary>
    public static class ModelParts
    {
        /// <summary>
        /// The model's part <paramref name="name"/>, baked to <c>{meshFolder}/{name}.asset</c> and placed under <paramref name="root"/> with
        /// <paramref name="material"/>. A see-through material casts no shadow.
        /// </summary>
        public static void Add(Transform root, Transform model, string name, Material material, string meshFolder, string meshPrefix)
        {
            Transform source = TransformSearch.FindDeep(model, name);
            MeshFilter filter = source != null ? source.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError($"CityX: the {meshPrefix} model has no part named {name}.");
                return;
            }
            Matrix4x4 bake = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Mesh mesh = FirearmBuilder.BakedMesh(filter.sharedMesh, bake, $"{meshFolder}/{name}.asset", meshPrefix + name);
            var part = new GameObject(name);
            part.transform.SetParent(root, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
