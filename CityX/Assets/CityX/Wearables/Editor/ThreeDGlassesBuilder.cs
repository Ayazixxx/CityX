using CityX.Character;
using CityX.Core;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the 3D Glasses (owner, 2026-11-19: "make it shaped like the black one but white ingame. make the blue and red part's transparent").
    /// Models/ThreeDGlasses.glb is made in Blender from flat outlines (SourceArt/ThreeDGlasses/glasses_model.py) in the Head bone's frame, the
    /// head's centre at the origin: the white Frame (the front and both arms) and the two lens sheets. The frame is white card, the lenses
    /// tinted film the face shows through, in the transparent queue (drawn after the cel look's black lines, which would trace their edges).
    /// </summary>
    public static class ThreeDGlassesBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/ThreeDGlasses.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/ThreeDGlasses.glb";
        private const string MeshFolder = "Assets/CityX/Wearables/ThreeDGlassesMeshes";
        private const string FrameMaterialPath = "Assets/CityX/Wearables/ThreeDGlassesWhite.mat";
        private const string RedMaterialPath = "Assets/CityX/Wearables/ThreeDGlassesRed.mat";
        private const string BlueMaterialPath = "Assets/CityX/Wearables/ThreeDGlassesBlue.mat";

        /// <summary>White card: a little off pure white so it still shades, matte.</summary>
        public static readonly Color FrameWhite = new Color(0.95f, 0.95f, 0.94f);

        private const float FrameSmoothness = 0.25f;

        /// <summary>The lenses' film: red and blue, more seen through than not.</summary>
        public static readonly Color RedFilm = new Color(1.0f, 0.1f, 0.1f, 0.4f);

        public static readonly Color BlueFilm = new Color(0.1f, 0.3f, 1.0f, 0.4f);

        [MenuItem("CityX/Rebuild 3D Glasses")]
        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (model == null)
            {
                Debug.LogError($"CityX: no model at {SourcePath} -- run SourceArt/ThreeDGlasses/glasses_model.py in Blender.");
                return null;
            }
            System.IO.Directory.CreateDirectory(MeshFolder);

            var root = new GameObject("ThreeDGlasses");
            try
            {
                Part(root.transform, model.transform, ThreeDGlasses.FrameName,
                    HeldModelBaker.LitMaterial(FrameMaterialPath, FrameWhite, 0.0f, FrameSmoothness));
                Part(root.transform, model.transform, ThreeDGlasses.RedLensName, Film(RedMaterialPath, RedFilm));
                Part(root.transform, model.transform, ThreeDGlasses.BlueLensName, Film(BlueMaterialPath, BlueFilm));

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;

                root.AddComponent<ThreeDGlasses>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>One of the model's parts, as it lies in the head's frame, under the root with its material.</summary>
        private static void Part(Transform root, Transform model, string name, Material material)
        {
            Transform source = TransformSearch.FindDeep(model, name);
            MeshFilter filter = source != null ? source.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogError($"CityX: the 3D Glasses model has no part named {name}.");
                return;
            }
            Matrix4x4 bake = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Mesh mesh = FirearmBuilder.BakedMesh(filter.sharedMesh, bake, $"{MeshFolder}/{name}.asset", "ThreeDGlasses" + name);
            var part = new GameObject(name);
            part.transform.SetParent(root, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent
                ? UnityEngine.Rendering.ShadowCastingMode.Off
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>A lens's tinted film: unlit, so it reads the same colour in any light, and seen through (SeeThroughMaterial).</summary>
        private static Material Film(string path, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            SeeThroughMaterial.Apply(material, additive: false);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
