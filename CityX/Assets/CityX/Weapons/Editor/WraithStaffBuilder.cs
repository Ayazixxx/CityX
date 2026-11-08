using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// The Wraith Staff's prefab (owner, 2026-11-08, with Meshy's "Crimson Oracle Staff": "make the red orb the same color as the wraith's eyes
    /// and make it glow as well. also lower it to a reasonable face count"). SourceArt/WraithStaff/wraith_staff_build.py stands the model as the
    /// game's staffs stand (the grip at the origin, the shaft up +Y, the orb's middle StaffBuilder.OrbHeight up), cuts it to 6,000 triangles
    /// and bakes its paint onto one 512 map; its two parts come in by material name: the staff, painted, and the orb, in the Wraith's eyes'
    /// red and glow (WraithBuilder.EyeColour, EyeGlowColour).
    /// </summary>
    public static class WraithStaffBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/WraithStaff.prefab";
        public const string SourcePath = "Assets/CityX/Weapons/WraithStaff.glb";
        private const string ColourPath = "Assets/CityX/Weapons/WraithStaff.jpg";
        private const string BodyMaterialPath = StaffBuilder.Folder + "/WraithStaffBody.mat";
        private const string OrbMaterialPath = StaffBuilder.Folder + "/WraithStaffOrb.mat";

        /// <summary>
        /// The orb's own light (owner, 2026-11-08: "make the wraith staff orb have a bit of red light reflect off of it not too much just enough
        /// that it is noticeable in the dark"): a small red glow at the orb, reaching the hand, the arm and the ground near the holder.
        /// </summary>
        public static readonly Color OrbLightColour = new Color(1.0f, 0.12f, 0.1f);

        public const float OrbLightIntensity = 0.8f;

        public const float OrbLightRange = 3.0f;

        /// <summary>The model's two parts, by the material names the build script gives them.</summary>
        public const string BodyMaterialName = "WraithStaffBody";
        public const string OrbMaterialName = "WraithStaffOrb";

        [MenuItem("CityX/Rebuild Wraith Staff")]
        public static GameObject Build()
        {
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            MeshFilter filter = imported != null ? imported.GetComponentInChildren<MeshFilter>(true) : null;
            if (filter == null || !filter.TryGetComponent(out MeshRenderer importedRenderer))
            {
                Debug.LogError($"CityX: no model in {SourcePath} -- run SourceArt/WraithStaff/wraith_staff_build.py.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(StaffBuilder.Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Staffs");
            }
            Material body = BodyMaterial();
            Material orb = OrbMaterial();
            var root = new GameObject("WraithStaff");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                Matrix4x4 placed = imported.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                model.transform.localPosition = placed.GetColumn(3);
                model.transform.localRotation = placed.rotation;
                model.transform.localScale = placed.lossyScale;
                model.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                Material[] slots = importedRenderer.sharedMaterials;
                var materials = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    materials[i] = slots[i] != null && slots[i].name.StartsWith(OrbMaterialName) ? orb : body;
                }
                model.AddComponent<MeshRenderer>().sharedMaterials = materials;

                var tip = new GameObject(StaffItem.TipName);
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = new Vector3(0.0f, StaffBuilder.OrbHeight, 0.0f);
                Light glow = new GameObject("OrbLight").AddComponent<Light>();
                glow.transform.SetParent(tip.transform, false);
                glow.type = LightType.Point;
                glow.color = OrbLightColour;
                glow.intensity = OrbLightIntensity;
                glow.range = OrbLightRange;
                glow.shadows = LightShadows.None;

                root.AddComponent<WraithStaff>().Tip = tip.transform;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the Wraith Staff written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The staff's paint, baked from Meshy's onto its own map.</summary>
        private static Material BodyMaterial()
        {
            Material material = HeldModelBaker.LitMaterial(BodyMaterialPath, Color.white, 0.4f, 0.35f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ColourPath));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The orb: the Wraith's eyes' red, glowing as they do.</summary>
        private static Material OrbMaterial()
        {
            Material material = HeldModelBaker.LitMaterial(OrbMaterialPath, WraithBuilder.EyeColour, 0.0f, 0.7f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", WraithBuilder.EyeGlowColour);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // or the keyword is dropped when saved
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
