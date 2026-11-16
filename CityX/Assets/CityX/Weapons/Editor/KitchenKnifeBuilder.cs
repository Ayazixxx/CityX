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
    ///
    /// THE BUTTER KNIFE (2026-10-30, found on Old Guy's kitchen counter, docs/OLD_GUYS_HOUSE.md) is drawn the same way: a short blade with a
    /// rounded tip and a blunt edge, all steel. A dull knife (SwordWeapon.Dull): the knife's moves with softer blows, and no edge.
    /// </summary>
    public static class KitchenKnifeBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/KitchenKnife.prefab";
        public const string ButterKnifePrefabPath = "Assets/CityX/Weapons/ButterKnife.prefab";
        private const string Folder = "Assets/CityX/Weapons/KitchenKnife";

        /// <summary>From the bolster to the point, metres.</summary>
        public const float BladeLength = 0.26f;

        public const float ButterBladeLength = 0.175f;

        public static readonly Color SteelColour = new Color(0.74f, 0.76f, 0.78f);

        public static readonly Color HandleColour = new Color(0.17f, 0.10f, 0.06f);

        /// <summary>One knife's drawing: its blade's outline and thickness, its handle and bolster turned, and what it is.</summary>
        private sealed class KnifeDrawing
        {
            public string Name;
            public string ItemId;
            public string PrefabPath;
            public float BladeLength;

            /// <summary>The blade's outline, across (x, the edge toward -X) and along (y), from the spine's heel round the point to the edge's heel.</summary>
            public Vector2[] BladeOutline;

            /// <summary>Half the blade's thickness at the spine, and at the edge.</summary>
            public float SpineHalfThickness;

            public float EdgeHalfThickness;

            /// <summary>The handle turned: (radius, height) from its butt up to the bolster.</summary>
            public Vector2[] HandleProfile;

            public Vector2[] BolsterProfile;

            public bool SteelHandle;
            public bool Dull;

            /// <summary>How much bigger than drawn the knife is made (about the bolster, where it meets the hand's hold).</summary>
            public float ModelScale = 1.0f;

            /// <summary>Its own hold in the right hand, if it has one: the hand bone's space, and degrees.</summary>
            public bool OwnHold;
            public Vector3 HoldPosition;
            public Vector3 HoldEuler;
        }

        /// <summary>
        /// The kitchen knife's size and hold, set by the owner by hand in the editor (2026-11-15: "the knife model needs to be scaled up and
        /// adjust", then the Transform's numbers: "set this as the hold for the kitchen knife").
        /// </summary>
        public const float KitchenModelScale = 2.11f;

        public static readonly Vector3 KitchenHoldPosition = new Vector3(0.109f, -0.04f, 0.17f);

        public static readonly Vector3 KitchenHoldEuler = new Vector3(-85.6f, -5.15f, -178.1f);

        private static readonly KnifeDrawing Kitchen = new KnifeDrawing
        {
            Name = "KitchenKnife",
            ItemId = ItemRegistry.KitchenKnife,
            PrefabPath = PrefabPath,
            BladeLength = BladeLength,
            BladeOutline = new[]
            {
                new Vector2(0.010f, 0.0f), new Vector2(0.010f, 0.17f), new Vector2(0.002f, BladeLength), new Vector2(-0.020f, 0.20f),
                new Vector2(-0.036f, 0.12f), new Vector2(-0.040f, 0.03f), new Vector2(-0.040f, 0.0f),
            },
            SpineHalfThickness = 0.0022f,
            EdgeHalfThickness = 0.0003f,
            HandleProfile = new[]
            {
                new Vector2(0.0f, -0.172f), new Vector2(0.011f, -0.170f), new Vector2(0.0135f, -0.150f), new Vector2(0.0125f, -0.090f),
                new Vector2(0.0115f, -0.025f), new Vector2(0.0125f, -0.014f), new Vector2(0.0f, -0.012f),
            },
            BolsterProfile = new[]
            {
                new Vector2(0.0f, -0.015f), new Vector2(0.0135f, -0.015f), new Vector2(0.0145f, -0.006f), new Vector2(0.012f, 0.0f), new Vector2(0.0f, 0.002f),
            },
            ModelScale = KitchenModelScale,
            OwnHold = true,
            HoldPosition = KitchenHoldPosition,
            HoldEuler = KitchenHoldEuler,
        };

        private static readonly KnifeDrawing Butter = new KnifeDrawing
        {
            Name = "ButterKnife",
            ItemId = ItemRegistry.ButterKnife,
            PrefabPath = ButterKnifePrefabPath,
            BladeLength = ButterBladeLength,
            BladeOutline = new[]
            {
                new Vector2(0.008f, 0.0f), new Vector2(0.008f, 0.135f), new Vector2(0.004f, 0.160f), new Vector2(-0.005f, ButterBladeLength),
                new Vector2(-0.015f, 0.168f), new Vector2(-0.020f, 0.150f), new Vector2(-0.020f, 0.0f),
            },
            SpineHalfThickness = 0.0018f,
            EdgeHalfThickness = 0.0009f,
            HandleProfile = new[]
            {
                new Vector2(0.0f, -0.160f), new Vector2(0.008f, -0.158f), new Vector2(0.0105f, -0.140f), new Vector2(0.0095f, -0.060f),
                new Vector2(0.0065f, -0.012f), new Vector2(0.0f, -0.010f),
            },
            BolsterProfile = new[]
            {
                new Vector2(0.0f, -0.012f), new Vector2(0.0065f, -0.012f), new Vector2(0.0055f, 0.0f), new Vector2(0.0f, 0.001f),
            },
            SteelHandle = true,
            Dull = true,
        };

        [MenuItem("CityX/Rebuild Kitchen Knife")]
        public static GameObject Build() => BuildKnife(Kitchen);

        [MenuItem("CityX/Rebuild Butter Knife")]
        public static GameObject BuildButterKnife() => BuildKnife(Butter);

        private static GameObject BuildKnife(KnifeDrawing knife)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "KitchenKnife");
            }
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/KitchenKnifeSteel.mat", SteelColour, 0.85f, 0.65f);
            Material grip = knife.SteelHandle ? steel : HeldModelBaker.LitMaterial($"{Folder}/KitchenKnifeHandle.mat", HandleColour, 0.0f, 0.35f);
            Mesh blade = BladeMesh($"{Folder}/{knife.Name}Blade.asset", knife);
            Mesh handle = HeldModelBaker.Turned($"{Folder}/{knife.Name}Handle.asset", knife.HandleProfile, knife.Name + "Handle", 12);
            Mesh bolster = HeldModelBaker.Turned($"{Folder}/{knife.Name}Bolster.asset", knife.BolsterProfile, knife.Name + "Bolster", 12);

            var root = new GameObject(knife.Name);
            try
            {
                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                model.localScale = Vector3.one * knife.ModelScale;
                HeldModelBaker.Part(model, "Blade", blade, steel);
                HeldModelBaker.Part(model, "Bolster", bolster, steel);
                HeldModelBaker.Part(model, "Handle", handle, grip);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = knife.ItemId;
                weapon.OneHanded = true;
                weapon.Knife = true;
                weapon.Dull = knife.Dull;
                weapon.BladeLength = knife.BladeLength * knife.ModelScale;
                if (knife.OwnHold)
                {
                    weapon.SetOwnHold(knife.HoldPosition, knife.HoldEuler);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, knife.PrefabPath);
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
        private static Mesh BladeMesh(string meshPath, KnifeDrawing knife)
        {
            Vector2[] outline = knife.BladeOutline;
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            float spine = outline[0].x;
            float edge = outline[outline.Length - 1].x;
            float HalfThickness(float x) => Mathf.Lerp(knife.EdgeHalfThickness, knife.SpineHalfThickness, Mathf.InverseLerp(edge, spine, x));

            int n = outline.Length;
            foreach (float side in new[] { 1.0f, -1.0f })
            {
                int first = positions.Count;
                foreach (Vector2 p in outline)
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
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % n];
                int first = positions.Count;
                positions.Add(new Vector3(a.x, a.y, HalfThickness(a.x)));
                positions.Add(new Vector3(b.x, b.y, HalfThickness(b.x)));
                positions.Add(new Vector3(b.x, b.y, -HalfThickness(b.x)));
                positions.Add(new Vector3(a.x, a.y, -HalfThickness(a.x)));
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }

            var scratch = new Mesh { vertices = positions.ToArray(), triangles = triangles.ToArray() };
            scratch.RecalculateNormals();
            Mesh saved = HeldModelBaker.Save(meshPath, knife.Name + "Blade", scratch.vertices, scratch.normals, scratch.triangles);
            Object.DestroyImmediate(scratch);
            return saved;
        }
    }
}
