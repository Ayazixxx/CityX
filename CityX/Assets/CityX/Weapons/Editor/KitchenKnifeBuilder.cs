using System.Collections.Generic;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// THE KITCHEN KNIFE (owner, 2026-10-29: "make a low effort kitchen knife model but make the mechanics good, when i get my meshy credits
    /// i will replace it with a good model"): a stand-in drawn in code -- a flat steel chef's blade, straight along its spine and curving up
    /// its belly to the point, thick at the spine and thin at the edge, on a turned dark wooden handle with a steel bolster. In the one-handed
    /// sword's frame (SwordWeapon): the root at the bolster, the blade up +Y, the edge on -X, where every one-handed key leads; the handle
    /// runs down -Y through the fist. A 20 cm blade and a 12 cm handle at the game's 1.3x. Fought as a knife (SwordWeapon.Knife, KnifeRules).
    /// </summary>
    public static class KitchenKnifeBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/KitchenKnife.prefab";
        private const string Folder = "Assets/CityX/Weapons/KitchenKnife";

        /// <summary>From the bolster to the point, metres.</summary>
        public const float BladeLength = 0.26f;

        /// <summary>The blade's outline, across (x, the edge toward -X) and along (y), from the spine's heel round the point to the edge's heel.</summary>
        private static readonly Vector2[] BladeOutline =
        {
            new Vector2(0.010f, 0.0f), new Vector2(0.010f, 0.17f), new Vector2(0.002f, BladeLength), new Vector2(-0.020f, 0.20f),
            new Vector2(-0.036f, 0.12f), new Vector2(-0.040f, 0.03f), new Vector2(-0.040f, 0.0f),
        };

        /// <summary>Half the blade's thickness at the spine, and at the edge.</summary>
        private const float SpineHalfThickness = 0.0022f;

        private const float EdgeHalfThickness = 0.0003f;

        /// <summary>The handle turned: (radius, height) from its butt up to the bolster.</summary>
        private static readonly Vector2[] HandleProfile =
        {
            new Vector2(0.0f, -0.172f), new Vector2(0.011f, -0.170f), new Vector2(0.0135f, -0.150f), new Vector2(0.0125f, -0.090f),
            new Vector2(0.0115f, -0.025f), new Vector2(0.0125f, -0.014f), new Vector2(0.0f, -0.012f),
        };

        private static readonly Vector2[] BolsterProfile =
        {
            new Vector2(0.0f, -0.015f), new Vector2(0.0135f, -0.015f), new Vector2(0.0145f, -0.006f), new Vector2(0.012f, 0.0f), new Vector2(0.0f, 0.002f),
        };

        public static readonly Color SteelColour = new Color(0.74f, 0.76f, 0.78f);

        public static readonly Color HandleColour = new Color(0.17f, 0.10f, 0.06f);

        [MenuItem("CityX/Rebuild Kitchen Knife")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "KitchenKnife");
            }
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/KitchenKnifeSteel.mat", SteelColour, 0.85f, 0.65f);
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/KitchenKnifeHandle.mat", HandleColour, 0.0f, 0.35f);
            Mesh blade = BladeMesh($"{Folder}/KitchenKnifeBlade.asset");
            Mesh handle = HeldModelBaker.Turned($"{Folder}/KitchenKnifeHandle.asset", HandleProfile, "KitchenKnifeHandle", 12);
            Mesh bolster = HeldModelBaker.Turned($"{Folder}/KitchenKnifeBolster.asset", BolsterProfile, "KitchenKnifeBolster", 12);

            var root = new GameObject("KitchenKnife");
            try
            {
                HeldModelBaker.Part(root.transform, "Blade", blade, steel);
                HeldModelBaker.Part(root.transform, "Bolster", bolster, steel);
                HeldModelBaker.Part(root.transform, "Handle", handle, wood);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.KitchenKnife;
                weapon.OneHanded = true;
                weapon.Knife = true;
                weapon.BladeLength = BladeLength;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The blade: its outline as a face on each side, thick at the spine and tapering to the edge (a wedge, so it catches the light
        /// as a blade does), joined round its rim. Each face and each rim band has its own vertices, so the edges stay sharp.
        /// </summary>
        private static Mesh BladeMesh(string meshPath)
        {
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            float spine = BladeOutline[0].x;
            float edge = BladeOutline[BladeOutline.Length - 1].x;
            float HalfThickness(float x) => Mathf.Lerp(EdgeHalfThickness, SpineHalfThickness, Mathf.InverseLerp(edge, spine, x));

            int n = BladeOutline.Length;
            foreach (float side in new[] { 1.0f, -1.0f })
            {
                int first = positions.Count;
                foreach (Vector2 p in BladeOutline)
                {
                    positions.Add(new Vector3(p.x, p.y, side * HalfThickness(p.x)));
                }
                // The outline is convex: a fan from its first point, wound to face out on this side.
                for (int i = 1; i + 1 < n; i++)
                {
                    if (side > 0.0f)
                    {
                        triangles.AddRange(new[] { first, first + i, first + i + 1 });
                    }
                    else
                    {
                        triangles.AddRange(new[] { first, first + i + 1, first + i });
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 a = BladeOutline[i];
                Vector2 b = BladeOutline[(i + 1) % n];
                int first = positions.Count;
                positions.Add(new Vector3(a.x, a.y, HalfThickness(a.x)));
                positions.Add(new Vector3(b.x, b.y, HalfThickness(b.x)));
                positions.Add(new Vector3(b.x, b.y, -HalfThickness(b.x)));
                positions.Add(new Vector3(a.x, a.y, -HalfThickness(a.x)));
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }

            var scratch = new Mesh { vertices = positions.ToArray(), triangles = triangles.ToArray() };
            scratch.RecalculateNormals();
            Mesh saved = HeldModelBaker.Save(meshPath, "KitchenKnifeBlade", scratch.vertices, scratch.normals, scratch.triangles);
            Object.DestroyImmediate(scratch);
            return saved;
        }
    }
}
