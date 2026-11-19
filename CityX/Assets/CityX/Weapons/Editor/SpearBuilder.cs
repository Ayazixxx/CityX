using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The spear (owner, 2026-11-18: "research video games with spears and try to make good spear animations/mechanics"; docs/SPEAR.md): a
    /// stand-in drawn in code until the owner's model, an ash shaft with a steel butt cap and a flat leaf-shaped head, 2.4 m on this 2.6 m body
    /// (a real 1.85 m spear). In the sword's frame (the root a hand's breadth above the right fist, the length along +Y), gripped two thirds of
    /// the way back so the long end is ahead and the short end passes under the arm; its hold lays the shaft diagonally across the palm
    /// (SpearRules.GripTiltDegrees).
    /// </summary>
    public static class SpearBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/Spear.prefab";
        private const string Folder = "Assets/CityX/Weapons/Spear";

        /// <summary>The shaft's ends below and above the root, metres: the butt 0.75 m behind the right fist (which sits 0.10 below the root).</summary>
        public const float Butt = -0.65f, ShaftTop = 1.42f;

        public const float ShaftRadius = 0.021f;

        /// <summary>The head: from its socket on the shaft to its point, and its half-width at its widest (it is flattened to a third of that).</summary>
        public const float HeadFrom = 1.36f, Tip = 1.75f, HeadHalfWidth = 0.062f;

        public const float HeadFlatness = 0.3f;

        public static readonly Color WoodColour = new Color(0.55f, 0.40f, 0.22f);
        public static readonly Color SteelColour = new Color(0.52f, 0.54f, 0.57f);

        /// <summary>The shaft's direction in the right hand: out past the thumb, leaned toward the fingertips.</summary>
        public static Vector3 ShaftAxisInHand => Quaternion.AngleAxis(SpearRules.GripTiltDegrees, Vector3.up) * SwordWeapon.OneHandBladeAxisInHand;

        public static Quaternion HoldRotation => Quaternion.AngleAxis(SpearRules.GripTiltDegrees, Vector3.up) * SwordWeapon.DefaultOneHandGripRotation;

        public static Vector3 HoldPosition => SwordWeapon.FistPoint + ShaftAxisInHand * SwordWeapon.DefaultPrimaryGripDown;

        [MenuItem("CityX/Rebuild Spear")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Spear");
            }
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/SpearWood.mat", WoodColour, 0.0f, 0.25f);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/SpearSteel.mat", SteelColour, 0.75f, 0.55f);

            Mesh shaft = HeldModelBaker.Turned($"{Folder}/SpearShaft.asset", new[]
            {
                new Vector2(0.0f, Butt + 0.07f), new Vector2(ShaftRadius, Butt + 0.07f), new Vector2(ShaftRadius, ShaftTop), new Vector2(0.0f, ShaftTop),
            }, "SpearShaft", 10);
            // The steel: the butt's cap, and the head (a socket round the shaft's end, swelling into a leaf and drawn to a point).
            Mesh cap = HeldModelBaker.Turned($"{Folder}/SpearCap.asset", new[]
            {
                new Vector2(0.0f, Butt), new Vector2(ShaftRadius * 0.7f, Butt), new Vector2(ShaftRadius + 0.005f, Butt + 0.03f),
                new Vector2(ShaftRadius + 0.005f, Butt + 0.09f), new Vector2(0.0f, Butt + 0.09f),
            }, "SpearCap", 10);
            Mesh head = HeldModelBaker.Turned($"{Folder}/SpearHead.asset", new[]
            {
                new Vector2(0.0f, HeadFrom), new Vector2(ShaftRadius + 0.006f, HeadFrom), new Vector2(ShaftRadius + 0.004f, HeadFrom + 0.07f),
                new Vector2(HeadHalfWidth, HeadFrom + 0.15f), new Vector2(HeadHalfWidth * 0.8f, HeadFrom + 0.24f), new Vector2(0.0f, Tip),
            }, "SpearHead", 12);

            var root = new GameObject("Spear");
            try
            {
                HeldModelBaker.Part(root.transform, "Shaft", shaft, wood);
                HeldModelBaker.Part(root.transform, "Cap", cap, steel);
                // Flattened across Z: its two edges on -X and +X, the sides every sweep leads with.
                Transform headPart = HeldModelBaker.Part(root.transform, "Head", head, steel);
                headPart.localScale = new Vector3(1.0f, 1.0f, HeadFlatness);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, (Butt + Tip) * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.Spear;
                weapon.OneHanded = false;
                weapon.Spear = true;
                weapon.BladeLength = Tip;
                weapon.SetOwnHold(HoldPosition, HoldRotation.eulerAngles);

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
