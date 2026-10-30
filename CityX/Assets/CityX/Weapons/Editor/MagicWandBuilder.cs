using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// THE MAGIC WAND's stand-in model (docs/MAGIC_WAND.md), drawn in code until the owner's own, after Hogwarts Legacy's wands: a dark carved
    /// handle (a round pommel, a ring above it, the grip swelling a little under the fingers, a ring under the thumb), a thin bronze collar, and
    /// a lighter shaft tapering to a point. Turned shapes (HeldModelBaker). The grip is the origin and the shaft runs up +Y (StaffItem holds it
    /// there, out past the thumb); <see cref="StaffItem.Tip"/> is its point. 46 cm in all, a real wand's 35 at the game's 1.3x.
    /// </summary>
    public static class MagicWandBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/MagicWand.prefab";
        private const string Folder = "Assets/CityX/Weapons/MagicWand";
        private const int Sides = 12;

        /// <summary>The point, metres up the shaft from the grip: where specks and spells leave.</summary>
        public const float TipHeight = 0.362f;

        /// <summary>The handle turned: (radius, height) from the pommel up to the collar.</summary>
        private static readonly Vector2[] HandleProfile =
        {
            new Vector2(0.0f, -0.105f), new Vector2(0.008f, -0.104f), new Vector2(0.014f, -0.099f), new Vector2(0.016f, -0.091f),
            new Vector2(0.014f, -0.084f), new Vector2(0.011f, -0.08f), new Vector2(0.0115f, -0.074f), new Vector2(0.0135f, -0.07f),
            new Vector2(0.0115f, -0.066f), new Vector2(0.011f, -0.055f), new Vector2(0.0118f, -0.02f), new Vector2(0.0118f, 0.02f),
            new Vector2(0.0112f, 0.05f), new Vector2(0.0128f, 0.056f), new Vector2(0.0112f, 0.062f), new Vector2(0.011f, 0.068f),
        };

        private static readonly Vector2[] CollarProfile =
        {
            new Vector2(0.011f, 0.068f), new Vector2(0.0128f, 0.069f), new Vector2(0.0128f, 0.078f), new Vector2(0.0095f, 0.079f),
        };

        private static readonly Vector2[] ShaftProfile =
        {
            new Vector2(0.0095f, 0.078f), new Vector2(0.0088f, 0.12f), new Vector2(0.0072f, 0.22f), new Vector2(0.0055f, 0.31f),
            new Vector2(0.0042f, 0.345f), new Vector2(0.003f, 0.356f), new Vector2(0.0f, TipHeight),
        };

        public static readonly Color HandleColour = new Color(0.15f, 0.08f, 0.05f);

        public static readonly Color ShaftColour = new Color(0.36f, 0.22f, 0.12f);

        public static readonly Color CollarColour = new Color(0.62f, 0.45f, 0.24f);

        [MenuItem("CityX/Rebuild Magic Wand")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "MagicWand");
            }
            Material handleWood = HeldModelBaker.LitMaterial($"{Folder}/MagicWandHandle.mat", HandleColour, 0.0f, 0.35f);
            Material shaftWood = HeldModelBaker.LitMaterial($"{Folder}/MagicWandShaft.mat", ShaftColour, 0.0f, 0.3f);
            Material bronze = HeldModelBaker.LitMaterial($"{Folder}/MagicWandCollar.mat", CollarColour, 0.85f, 0.55f);

            var root = new GameObject("MagicWand");
            try
            {
                HeldModelBaker.Part(root.transform, "Handle", Turned(HandleProfile, "MagicWandHandle"), handleWood);
                HeldModelBaker.Part(root.transform, "Collar", Turned(CollarProfile, "MagicWandCollar"), bronze);
                HeldModelBaker.Part(root.transform, "Shaft", Turned(ShaftProfile, "MagicWandShaft"), shaftWood);

                var tip = new GameObject(StaffItem.TipName);
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = new Vector3(0.0f, TipHeight, 0.0f);

                MagicWand wand = root.AddComponent<MagicWand>();
                wand.Tip = tip.transform;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: MagicWand written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh Turned(Vector2[] profile, string name) => HeldModelBaker.Turned($"{Folder}/{name}.asset", profile, name, Sides);
    }
}
