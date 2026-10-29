using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds Flashlight.prefab (docs/FLASHLIGHT.md): a black anodised aluminium torch turned in code -- a ribbed tail cap, a straight grip, a
    /// head flaring out to a bezel round a pale domed lens -- about a third of a metre long (the city is 1.3 times life size), its axis +Y, its
    /// origin where the fist closes on it. The light, its shaft and its glow are made at run time by FlashlightItem.
    /// </summary>
    public static class FlashlightBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Flashlight";
        public const string PrefabPath = "Assets/CityX/Weapons/Flashlight.prefab";

        private const int Sides = 16;

        private static readonly Color Anodised = new Color(0.06f, 0.06f, 0.065f);
        private static readonly Color Glass = new Color(0.86f, 0.86f, 0.8f);

        /// <summary>The body, tail to bezel: (radius, height) in metres.</summary>
        private static readonly Vector2[] BodyProfile =
        {
            new Vector2(0.0f, -0.155f), new Vector2(0.021f, -0.15f), new Vector2(0.023f, -0.142f), new Vector2(0.023f, -0.128f),
            new Vector2(0.021f, -0.124f), new Vector2(0.021f, 0.06f), new Vector2(0.03f, 0.1f), new Vector2(0.036f, 0.128f),
            new Vector2(0.036f, 0.162f), new Vector2(0.031f, 0.168f),
        };

        /// <summary>The lens: a shallow dome inside the bezel.</summary>
        private static readonly Vector2[] LensProfile =
        {
            new Vector2(0.031f, 0.166f), new Vector2(0.0f, 0.172f),
        };

        /// <summary>Where the light comes out: the middle of the lens, metres up the torch.</summary>
        public const float LensHeight = 0.172f;

        [MenuItem("CityX/Rebuild Flashlight")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Flashlight");
            }
            Material metal = HeldModelBaker.LitMaterial($"{Folder}/FlashlightBody.mat", Anodised, 0.6f, 0.55f);
            Material glass = HeldModelBaker.LitMaterial($"{Folder}/FlashlightLens.mat", Glass, 0.0f, 0.9f);
            var root = new GameObject("Flashlight");
            try
            {
                HeldModelBaker.Part(root.transform, "Body", HeldModelBaker.Turned($"{Folder}/FlashlightBody.asset", BodyProfile, "FlashlightBody", Sides), metal);
                HeldModelBaker.Part(root.transform, "LensGlass",
                    HeldModelBaker.Turned($"{Folder}/FlashlightLens.asset", LensProfile, "FlashlightLens", Sides), glass);
                var lens = new GameObject("Lens").transform;
                lens.SetParent(root.transform, false);
                lens.localPosition = new Vector3(0.0f, LensHeight, 0.0f);

                var collider = root.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                collider.radius = 0.03f;
                collider.height = 0.33f;

                root.AddComponent<FlashlightItem>().Lens = lens;
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
