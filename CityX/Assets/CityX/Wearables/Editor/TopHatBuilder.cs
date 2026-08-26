using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the top hat -- brim + crown + a contrasting band, all
    /// procedural, matching top_hat.gd's own three-CylinderMesh build and
    /// exact dimensions (Godot's head_size cube matches this rig's own
    /// FusedBodyShape.HeadRadius exactly -- see TopHat.HeadPosition's own
    /// doc comment -- so nothing here needed rescaling for the port).
    ///
    /// UNIFORM CYLINDERS, NOT TAPERED. Unity's built-in primitive cylinder
    /// has no independent top/bottom radius the way Godot's CylinderMesh
    /// does. The crown's own taper there is a 0.29-to-0.27 difference,
    /// barely perceptible on a real hat -- approximated here as a single
    /// uniform 0.28 radius rather than hand-building a tapered mesh, which
    /// JetpackBuilder's own doc comment already found produces zero-bounds
    /// renderers in this project (a scaled built-in primitive, never a
    /// hand-copied one, is the standing convention every procedural prop
    /// here follows).
    /// </summary>
    public static class TopHatBuilder
    {
        private const string PrefabPath = "Assets/CityX/Wearables/TopHat.prefab";

        private const float BrimRadius = 0.4f;
        private const float BrimHeight = 0.04f;
        private const float CrownRadius = 0.28f;
        private const float CrownHeight = 0.32f;
        private const float BandRadius = 0.295f;
        private const float BandHeight = 0.06f;

        private static readonly Color HatColour = new Color(0.03f, 0.03f, 0.035f);
        private static readonly Color BandColour = new Color(0.09f, 0.02f, 0.02f);

        [MenuItem("CityX/Rebuild Top Hat")]
        public static GameObject Build()
        {
            var root = new GameObject("TopHat");
            try
            {
                Material hatMaterial = CharacterSkin.Ensure("TopHatFabric", HatColour);
                hatMaterial.SetFloat("_Smoothness", 0.4f);

                Material bandMaterial = CharacterSkin.Ensure("TopHatBand", BandColour);
                bandMaterial.SetFloat("_Smoothness", 0.5f);

                BuildPiece(root.transform, hatMaterial, "Brim", BrimRadius, BrimHeight, BrimHeight * 0.5f);
                BuildPiece(root.transform, hatMaterial, "Crown", CrownRadius, CrownHeight,
                    BrimHeight + CrownHeight * 0.5f);
                BuildPiece(root.transform, bandMaterial, "Band", BandRadius, BandHeight,
                    BrimHeight + BandHeight * 0.5f);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = TopHat.InteractRange;

                root.AddComponent<TopHat>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: top hat written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPiece(Transform parent, Material material, string name,
            float radius, float height, float centreY)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = name;
            Object.DestroyImmediate(piece.GetComponent<Collider>());
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = new Vector3(0.0f, centreY, 0.0f);
            // Unity's primitive cylinder is 2 units tall with radius 1.
            piece.transform.localScale = new Vector3(radius * 2.0f, height * 0.5f, radius * 2.0f);
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
