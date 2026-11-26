using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Squirrel Mask (docs/SQUIRREL_MASK.md). Models/SquirrelMask.glb is made in Blender from the owner's Meshy model
    /// (SourceArt/SquirrelMask/squirrel_mask_build.py) in the Head bone's frame, the head's centre at the origin: the brown Fur and the
    /// Black eyes and nose, each a flat colour with no map.
    /// </summary>
    public static class SquirrelMaskBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/SquirrelMask.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/SquirrelMask.glb";
        private const string MeshFolder = "Assets/CityX/Wearables/SquirrelMaskMeshes";
        private const string FurMaterialPath = "Assets/CityX/Wearables/SquirrelMaskFur.mat";
        private const string BlackMaterialPath = "Assets/CityX/Wearables/SquirrelMaskBlack.mat";

        /// <summary>The owner's brown (2026-11-25: "so that the fur is #874d00 brown").</summary>
        public static readonly Color FurBrown = new Color(0x87 / 255.0f, 0x4d / 255.0f, 0x00 / 255.0f);

        public static readonly Color Black = new Color(0.03f, 0.03f, 0.03f);

        private const float FurSmoothness = 0.1f;

        /// <summary>A little sheen, so the nose reads as a wet nose against the matte fur.</summary>
        private const float BlackSmoothness = 0.45f;

        private static void Part(Transform root, Transform model, string name, Material material) =>
            ModelParts.Add(root, model, name, material, MeshFolder, "SquirrelMask");

        [MenuItem("CityX/Rebuild Squirrel Mask")]
        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (model == null)
            {
                Debug.LogError($"CityX: no model at {SourcePath} -- run SourceArt/SquirrelMask/squirrel_mask_build.py in Blender.");
                return null;
            }
            System.IO.Directory.CreateDirectory(MeshFolder);

            var root = new GameObject("SquirrelMask");
            try
            {
                Part(root.transform, model.transform, SquirrelMask.FurName,
                    HeldModelBaker.LitMaterial(FurMaterialPath, FurBrown, 0.0f, FurSmoothness));
                Part(root.transform, model.transform, SquirrelMask.BlackName,
                    HeldModelBaker.LitMaterial(BlackMaterialPath, Black, 0.0f, BlackSmoothness));

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;

                root.AddComponent<SquirrelMask>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
