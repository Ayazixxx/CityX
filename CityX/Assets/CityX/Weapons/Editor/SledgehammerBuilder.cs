using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The sledgehammer (owner, 2026-11-05: "make a sledgehammer (two handed) and its still a 3 hit combo but the third one he smashes it
    /// straight down vertically. make a low effort model i will replace later"): a stand-in drawn in code, a turned wooden handle and a steel
    /// head laid across its top, placed in the greatsword's frame so it shares the two-handed hold (the left hand set on the grip by hand IK, as
    /// on every two-handed weapon: owner, "use hand ik for the grip"). Fought as the greatsword, except its third light is the overhead smash
    /// (SwordStrikeKind.Smash2H).
    /// </summary>
    public static class SledgehammerBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/Sledgehammer.prefab";
        private const string Folder = "Assets/CityX/Weapons/Sledgehammer";

        /// <summary>The handle's ends below and above the root, metres: the right fist sits 0.10 below the root and the left 0.30 (SwordWeapon's hold).</summary>
        public const float HandleBottom = -0.42f, HandleTop = 0.98f;

        public const float HandleRadius = 0.024f;

        /// <summary>The head: its length across the handle, its half-thickness, and where its middle sits on the handle.</summary>
        public const float HeadLength = 0.40f, HeadRadius = 0.08f, HeadMiddle = 0.92f;

        /// <summary>How far the head's top stands above the root: the reach the hits sweep to.</summary>
        public const float Reach = HeadMiddle + HeadRadius;

        public static readonly Color WoodColour = new Color(0.50f, 0.34f, 0.18f);
        public static readonly Color SteelColour = new Color(0.30f, 0.31f, 0.33f);

        [MenuItem("CityX/Rebuild Sledgehammer")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Sledgehammer");
            }
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/SledgehammerWood.mat", WoodColour, 0.0f, 0.2f);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/SledgehammerSteel.mat", SteelColour, 0.6f, 0.4f);

            const float knob = HandleRadius + 0.004f;
            Mesh handle = HeldModelBaker.Turned($"{Folder}/SledgehammerHandle.asset", new[]
            {
                new Vector2(0.0f, HandleBottom), new Vector2(knob, HandleBottom), new Vector2(knob, HandleBottom + 0.03f),
                new Vector2(HandleRadius, HandleBottom + 0.05f), new Vector2(HandleRadius, HandleTop), new Vector2(0.0f, HandleTop),
            }, "SledgehammerHandle", 10);

            const float half = HeadLength * 0.5f, bevel = 0.015f;
            Mesh head = HeldModelBaker.Turned($"{Folder}/SledgehammerHead.asset", new[]
            {
                new Vector2(0.0f, -half), new Vector2(HeadRadius - bevel, -half), new Vector2(HeadRadius, -half + bevel),
                new Vector2(HeadRadius, half - bevel), new Vector2(HeadRadius - bevel, half), new Vector2(0.0f, half),
            }, "SledgehammerHead", 8);

            var root = new GameObject("Sledgehammer");
            try
            {
                HeldModelBaker.Part(root.transform, "Handle", handle, wood);
                // Turned along its length, then laid across the handle's top, its faces on -X and +X: -X is the side every swing leads with.
                Transform headPart = HeldModelBaker.Part(root.transform, "Head", head, steel);
                headPart.localPosition = new Vector3(0.0f, HeadMiddle, 0.0f);
                headPart.localRotation = Quaternion.Euler(0.0f, 0.0f, 90.0f);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, Reach * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.Sledgehammer;
                weapon.OneHanded = false;
                weapon.Hammer = true;
                weapon.BladeLength = Reach;

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
