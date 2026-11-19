using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The bo staff (owner, 2026-11-19: "make the model just a basic metal pole nothing special. make it about the length of the spikesword or however
    /// long you think it should be based on the video"; docs/BO_STAFF.md): a plain steel pole, 2.2 m on this 2.6 m body (the pole in the owner's clip is
    /// as long as its fighter is tall), with a dark rubber cap on each end. In the greatsword's frame (StaffRules): the root a hand's breadth above the
    /// right fist, the length along +Y, both fists on its last half metre, so it shares the two-handed blades' tuned hold.
    /// </summary>
    public static class BoStaffBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/BoStaff.prefab";
        private const string Folder = "Assets/CityX/Weapons/BoStaff";

        /// <summary>How long each end's cap is, metres, and how much wider than the pole.</summary>
        public const float CapLength = 0.06f, CapSwell = 0.004f;

        public static readonly Color SteelColour = new Color(0.60f, 0.62f, 0.65f);
        public static readonly Color CapColour = new Color(0.10f, 0.10f, 0.11f);

        [MenuItem("CityX/Rebuild Bo Staff")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "BoStaff");
            }
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/BoStaffSteel.mat", SteelColour, 0.8f, 0.5f);
            Material rubber = HeldModelBaker.LitMaterial($"{Folder}/BoStaffCap.mat", CapColour, 0.0f, 0.2f);

            float pole = StaffRules.Radius, cap = StaffRules.Radius + CapSwell;
            Mesh shaft = HeldModelBaker.Turned($"{Folder}/BoStaffPole.asset", new[]
            {
                new Vector2(0.0f, StaffRules.Butt + CapLength), new Vector2(pole, StaffRules.Butt + CapLength),
                new Vector2(pole, StaffRules.Tip - CapLength), new Vector2(0.0f, StaffRules.Tip - CapLength),
            }, "BoStaffPole", 12);
            Mesh capMesh = HeldModelBaker.Turned($"{Folder}/BoStaffCap.asset", new[]
            {
                new Vector2(0.0f, 0.0f), new Vector2(cap * 0.8f, 0.0f), new Vector2(cap, 0.012f), new Vector2(cap, CapLength), new Vector2(0.0f, CapLength),
            }, "BoStaffCap", 12);

            var root = new GameObject("BoStaff");
            try
            {
                HeldModelBaker.Part(root.transform, "Pole", shaft, steel);
                Transform bottom = HeldModelBaker.Part(root.transform, "ButtCap", capMesh, rubber);
                bottom.localPosition = new Vector3(0.0f, StaffRules.Butt, 0.0f);
                Transform top = HeldModelBaker.Part(root.transform, "TipCap", capMesh, rubber);
                top.localPosition = new Vector3(0.0f, StaffRules.Tip, 0.0f);
                top.localRotation = Quaternion.Euler(180.0f, 0.0f, 0.0f);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, (StaffRules.Butt + StaffRules.Tip) * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.BoStaff;
                weapon.OneHanded = false;
                weapon.Staff = true;
                weapon.BladeLength = StaffRules.Tip;

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
