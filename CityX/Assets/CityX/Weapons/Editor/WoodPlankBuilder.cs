using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The wood plank (docs/WOOD_PLANK.md; owner, 2026-11-23: "a wood plank weapon, just a wood plank like a 2x4 ... common rarity level, its like one
    /// of the lowest level 2 handed weapons"): a TWO-HANDED blunt weapon fought as the baseball bat is (the greatsword's swings, guard and hold),
    /// its board made in code (WoodPlankShape) and painted with the drawn cartoon wood (SourceArt/WoodPlank/plank_texture.py), the hands laid on
    /// its flat sides (WoodPlankGrip).
    /// </summary>
    public static class WoodPlankBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/WoodPlank.prefab";
        public const string TexturePath = "Assets/CityX/Weapons/WoodPlank.png";
        private const string MeshPath = "Assets/CityX/Weapons/WoodPlankMesh.asset";
        private const string MaterialPath = "Assets/CityX/Weapons/WoodPlank.mat";

        /// <summary>Bare sawn wood: no shine.</summary>
        private const float WoodSmoothness = 0.15f;

        [MenuItem("CityX/Rebuild Wood Plank")]
        public static GameObject Build()
        {
            Texture2D texture = ImportedTexture();
            WoodPlankShape.Build(out Vector3[] vertices, out Vector3[] normals, out Vector2[] uv, out int[] triangles);
            Mesh mesh = HeldModelBaker.Save(MeshPath, "WoodPlankMesh", vertices, normals, triangles, uv);
            Material wood = HeldModelBaker.LitMaterial(MaterialPath, Color.white, 0.0f, WoodSmoothness);
            wood.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(wood);

            var root = new GameObject("WoodPlank");
            try
            {
                HeldModelBaker.Part(root.transform, "Model", mesh, wood);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, WoodPlankShape.Top * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.WoodPlank;
                weapon.OneHanded = false;
                weapon.BladeLength = WoodPlankShape.Top;
                weapon.SupportPalmOffset = WoodPlankShape.SupportPalmOffset;
                root.AddComponent<WoodPlankGrip>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The drawn wood, tiling along the plank (U) and not between its two boards (V).</summary>
        private static Texture2D ImportedTexture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null && (importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp || importer.maxTextureSize != 1024))
            {
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
