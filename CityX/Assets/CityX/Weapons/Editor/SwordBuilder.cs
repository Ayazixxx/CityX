using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the sword prefab -- a tapered three-segment blade, a
    /// crossguard, a wrapped grip and a pommel, all procedural (matching
    /// BottleBuilder/GrapplingHookBuilder/JetpackBuilder's own reasoning:
    /// no glTF importer exists in this project for a hand-authored sword
    /// mesh, and no sword model exists anywhere in this project's own
    /// assets or the user's Downloads folder to import instead -- checked
    /// directly, not assumed). No Godot source to port numbers from -- see
    /// SwordWeapon.cs's own doc comment.
    ///
    /// SCALED PRIMITIVES, NOT HAND-COPIED MESHES -- see JetpackBuilder's own
    /// class comment for why: a hand-copied Mesh produced zero-bounds,
    /// invisible renderers here before, and scaling Unity's own known-good
    /// primitive meshes sidesteps whatever that was entirely.
    ///
    /// BUILT WITH THE BLADE ALONG LOCAL +Y, hilt below it at the origin --
    /// the same "build in a natural upright orientation, then rotate once
    /// at the grip" split BottleWeapon/JetpackBuilder use, so
    /// SwordWeapon.GripHandRotationEuler is the only place that ever needs
    /// retuning if the in-hand angle looks wrong.
    /// </summary>
    public static class SwordBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Sword.prefab";

        private static readonly Color BladeColour = new Color(0.75f, 0.77f, 0.80f);
        private static readonly Color GuardColour = new Color(0.55f, 0.44f, 0.18f);
        private static readonly Color GripColour = new Color(0.10f, 0.08f, 0.06f);

        private const float BladeLength = 0.85f;
        private const float BaseWidth = 0.065f;
        private const float MidWidth = 0.05f;
        private const float TipWidth = 0.03f;
        private const float BladeThickness = 0.012f;

        private const float GuardWidth = 0.22f;
        private const float GuardHeight = 0.03f;
        private const float GuardThickness = 0.03f;

        private const float GripLength = 0.22f;
        private const float GripRadius = 0.018f;

        private const float PommelRadius = 0.035f;

        [MenuItem("CityX/Rebuild Sword")]
        public static GameObject Build()
        {
            var root = new GameObject("Sword");
            try
            {
                Material bladeMaterial = CharacterSkin.Ensure("SwordBlade", BladeColour);
                bladeMaterial.SetFloat("_Metallic", 0.9f);
                bladeMaterial.SetFloat("_Smoothness", 0.75f);

                Material guardMaterial = CharacterSkin.Ensure("SwordGuard", GuardColour);
                guardMaterial.SetFloat("_Metallic", 0.7f);
                guardMaterial.SetFloat("_Smoothness", 0.45f);

                Material gripMaterial = CharacterSkin.Ensure("SwordGrip", GripColour);
                gripMaterial.SetFloat("_Metallic", 0.0f);
                gripMaterial.SetFloat("_Smoothness", 0.15f);

                // Grip and pommel sit BELOW the origin, blade rises above it --
                // the origin is where the hand actually grips, matching how
                // BottleWeapon's own GripHandPosition is defined relative to
                // its visual root.
                BuildCylinderSegment(root.transform, gripMaterial, "Grip",
                    new Vector3(0.0f, -GripLength * 0.5f, 0.0f), GripRadius, GripLength);

                var pommel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pommel.name = "Pommel";
                Object.DestroyImmediate(pommel.GetComponent<Collider>());
                pommel.transform.SetParent(root.transform, false);
                pommel.transform.localPosition = new Vector3(0.0f, -GripLength, 0.0f);
                pommel.transform.localScale = Vector3.one * (PommelRadius * 2.0f);
                pommel.GetComponent<MeshRenderer>().sharedMaterial = guardMaterial;

                BuildBoxSegment(root.transform, guardMaterial, "Guard",
                    Vector3.zero, GuardWidth, GuardHeight, GuardThickness);

                // Three tapering segments give the blade a real point without
                // needing a hand-authored mesh -- a single uniform bar reads
                // as a ruler, not a blade.
                float baseLen = BladeLength * 0.45f;
                float midLen = BladeLength * 0.35f;
                float tipLen = BladeLength * 0.20f;

                float baseCentre = baseLen * 0.5f;
                BuildBoxSegment(root.transform, bladeMaterial, "BladeBase",
                    new Vector3(0.0f, baseCentre, 0.0f), BaseWidth, baseLen, BladeThickness);

                float midCentre = baseLen + midLen * 0.5f;
                BuildBoxSegment(root.transform, bladeMaterial, "BladeMid",
                    new Vector3(0.0f, midCentre, 0.0f), MidWidth, midLen, BladeThickness * 0.85f);

                float tipCentre = baseLen + midLen + tipLen * 0.5f;
                BuildBoxSegment(root.transform, bladeMaterial, "BladeTip",
                    new Vector3(0.0f, tipCentre, 0.0f), TipWidth, tipLen, BladeThickness * 0.7f);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                pickupTrigger.center = new Vector3(0.0f, BladeLength * 0.3f, 0.0f);

                root.AddComponent<SwordWeapon>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: sword written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildBoxSegment(Transform parent, Material material, string name,
            Vector3 localPosition, float width, float length, float thickness)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            Object.DestroyImmediate(segment.GetComponent<Collider>());
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;
            segment.transform.localScale = new Vector3(width, length, thickness);
            segment.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void BuildCylinderSegment(Transform parent, Material material, string name,
            Vector3 localPosition, float radius, float length)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name;
            Object.DestroyImmediate(segment.GetComponent<Collider>());
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = localPosition;
            // Unity's primitive cylinder is 2 units tall with radius 1,
            // and already runs along its own local Y -- no extra rotation
            // needed since the grip is meant to run along the sword's own
            // +Y just like the blade above it.
            segment.transform.localScale = new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f);
            segment.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
