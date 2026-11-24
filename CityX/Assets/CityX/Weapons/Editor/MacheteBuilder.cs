using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The machete (docs/MACHETE.md; owner, 2026-11-23, with Meshy's "Machete": "put this machete in the game as a one handed sword, texture it so the
    /// blade is silver and the handle is black with those 3 silver dots, also lower the face count to a reasonable amount and get rid of the square
    /// grid pattern on the handle just make it smooth"): a ONE-HANDED sword with an edge, fought exactly as the combat sword is.
    ///
    /// The model is made in Blender (SourceArt/Machete/machete_build.py) already in the sword frame: the guard at the root, the handle down -Y, the
    /// blade up +Y, its edge toward -X. It comes in three parts, each in its own plain colour: the silver blade, the smooth black handle and its six
    /// silver rivets.
    /// </summary>
    public static class MacheteBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/Machete.prefab";
        public const string SourcePath = "Assets/CityX/Weapons/Machete.glb";
        private const string Folder = "Assets/CityX/Weapons/Machete";

        public static readonly string[] Parts = { "Blade", "Handle", "Rivets" };

        public static readonly Color Steel = new Color(0.78f, 0.79f, 0.82f);
        public static readonly Color Grip = new Color(0.05f, 0.05f, 0.05f);

        private const float SteelMetallic = 0.35f;
        private const float SteelSmoothness = 0.6f;
        private const float GripSmoothness = 0.3f;

        [MenuItem("CityX/Rebuild Machete")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Machete");
            }
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/MacheteSteel.mat", Steel, SteelMetallic, SteelSmoothness);
            Material grip = HeldModelBaker.LitMaterial($"{Folder}/MacheteGrip.mat", Grip, 0.0f, GripSmoothness);
            var root = new GameObject("Machete");
            try
            {
                float tip = 0.0f;
                foreach (string part in Parts)
                {
                    HeldModelBaker.LoadPart(SourcePath, part, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
                    Mesh mesh = HeldModelBaker.Save($"{Folder}/Machete{part}.asset", $"Machete{part}", vertices, normals, triangles, uv);
                    HeldModelBaker.Part(root.transform, part, mesh, part == "Handle" ? grip : steel);
                    if (part == "Blade")
                    {
                        tip = mesh.bounds.max.y;
                    }
                }

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, tip * 0.5f, 0.0f);

                SwordWeapon sword = root.AddComponent<SwordWeapon>();
                sword.ItemId = ItemRegistry.Machete;
                sword.OneHanded = true;
                sword.BladeLength = tip;

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
