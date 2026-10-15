using CityX.CharacterEditor;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The metal crowbar (owner, 2026-10-15: "put this crowbar in as a 1 handed melee weapon and make it metallic", crowbar.glb, "rename the file
    /// to metalcrowbar"): a ONE-HANDED blunt weapon, fought exactly like the one-handed combat sword (SwordWeapon.OneHanded: the same swings,
    /// guard, parry and roll), held at its straight end and swung claw first.
    ///
    /// The glb lies on a diagonal with its claw curled at one end, so it is MEASURED (HeldModelBaker): the claw end is the one holding most of the
    /// vertices, the shaft runs along the long axis of the straight half, and the hand closes <see cref="GripFraction"/> of the way up from the
    /// straight end. Scaled to <see cref="Length"/> (a 0.77 m wrecking bar at the game's 1.3x) and baked upright in the sword frame: the root
    /// SwordWeapon.OneHandGripDown above the hand, the shaft up +Y, the claw curling toward -X, the swing's leading side, where a blade's edge is.
    /// No texture: one steel material, metallic and a little worn.
    /// </summary>
    public static class MetalCrowbarBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/MetalCrowbar.prefab";
        public const string SourcePath = "Assets/CityX/Weapons/metalcrowbar.glb";
        private const string MeshPath = "Assets/CityX/Weapons/MetalCrowbarMesh.asset";

        /// <summary>The crowbar end to end on the 2.6 m body, metres.</summary>
        public const float Length = 1.0f;

        /// <summary>The middle of the hand, up from the straight end, as a share of the length.</summary>
        public const float GripFraction = 0.14f;

        /// <summary>The share of the length at either end whose vertices are counted to tell the claw (dense) from the straight end.</summary>
        private const float EndShare = 0.15f;

        /// <summary>Dark forged steel: the metallic sheen comes from the material, not the colour.</summary>
        public static readonly Color SteelColour = new Color(0.36f, 0.37f, 0.40f);

        public const float SteelMetallic = 0.9f;

        public const float SteelSmoothness = 0.5f;

        [MenuItem("CityX/Rebuild Metal Crowbar")]
        public static GameObject Build()
        {
            Mesh mesh = BakedMesh(out float reach);
            var root = new GameObject("MetalCrowbar");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;

                Material steel = CharacterSkin.Ensure("MetalCrowbarSteel", SteelColour);
                steel.SetFloat("_Metallic", SteelMetallic);
                steel.SetFloat("_Smoothness", SteelSmoothness);
                steel.SetFloat("_Cull", 0.0f); // the glb is double-sided
                model.AddComponent<MeshRenderer>().sharedMaterial = steel;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, reach * 0.5f, 0.0f);

                SwordWeapon weapon = root.AddComponent<SwordWeapon>();
                weapon.ItemId = ItemRegistry.MetalCrowbar;
                weapon.OneHanded = true;
                weapon.BladeLength = reach;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The model measured, turned upright, scaled and saved; <paramref name="reach"/> is how far its far end stands above the root.</summary>
        public static Mesh BakedMesh(out float reach)
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles);

            // The long axis, pointed at the claw: the end holding more of the vertices (the claw's curl and split tip are most of the model).
            Vector3 axis = HeldModelBaker.LongAxis(vertices);
            HeldModelBaker.Extent(vertices, axis, out float min, out float max);
            float span = max - min;
            int nearMin = 0, nearMax = 0;
            foreach (Vector3 v in vertices)
            {
                float t = Vector3.Dot(v, axis);
                if (t < min + span * EndShare) nearMin++;
                if (t > max - span * EndShare) nearMax++;
            }
            if (nearMin > nearMax)
            {
                axis = -axis;
                HeldModelBaker.Extent(vertices, axis, out min, out max);
            }

            // The shaft: the long axis of the straight half (the claw's curl leans the whole model's axis off it).
            float half = min + span * 0.5f;
            System.Func<Vector3, bool> straightHalf = v => Vector3.Dot(v, axis) < half;
            Vector3 shaft = HeldModelBaker.LongAxis(vertices, straightHalf);
            if (Vector3.Dot(shaft, axis) < 0.0f)
            {
                shaft = -shaft;
            }
            Vector3 shaftCentre = HeldModelBaker.Centre(vertices, straightHalf);
            HeldModelBaker.Extent(vertices, shaft, out float lo, out float hi);
            float length = hi - lo;
            float scale = Length / length;

            // The hand on the shaft line, GripFraction up from the straight end; the root SwordWeapon.OneHandGripDown above it.
            float gripT = lo + length * GripFraction;
            Vector3 grip = shaftCentre + shaft * (gripT - Vector3.Dot(shaftCentre, shaft));
            Vector3 origin = grip + shaft * (SwordWeapon.OneHandGripDown / scale);

            // Upright, the claw curling toward -X: a sword's edge leads there (SwordWeapon's one-handed hold puts it along the fingers).
            Quaternion upright = Quaternion.FromToRotation(shaft, Vector3.up);
            Vector3 claw = HeldModelBaker.Centre(vertices, v => Vector3.Dot(v, axis) > max - span * EndShare);
            if ((upright * (claw - origin)).x > 0.0f)
            {
                upright = Quaternion.AngleAxis(180.0f, Vector3.up) * upright;
            }
            HeldModelBaker.Place(vertices, normals, origin, upright, scale);

            reach = float.MinValue;
            foreach (Vector3 v in vertices)
            {
                reach = Mathf.Max(reach, v.y);
            }
            return HeldModelBaker.Save(MeshPath, "MetalCrowbarMesh", vertices, normals, triangles);
        }
    }
}
