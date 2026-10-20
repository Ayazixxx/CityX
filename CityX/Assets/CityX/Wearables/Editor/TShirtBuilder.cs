using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the T-Shirt: Meshy's tee cut off its mannequin, fitted, evened out at the front and back, reduced and laid out for designs by
    /// SourceArt/TShirt/tshirt_build.py into Models/TShirt.json. It is plain white cloth; a design is a texture drawn on the template that
    /// script writes (docs/TSHIRT.md).
    /// </summary>
    public static class TShirtBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/TShirt.prefab";
        private const string SourcePath = "Assets/CityX/Wearables/Models/TShirt.json";
        private const string MeshPath = "Assets/CityX/Wearables/TShirtMesh.asset";
        private const string MaterialPath = "Assets/CityX/Wearables/TShirt.mat";

        // Matches the other tops' ground pose: tipped so it reads as a dropped shirt.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        /// <summary>Cotton: white, a little off, and matte.</summary>
        private static readonly Color Cotton = new Color(0.92f, 0.92f, 0.9f);
        private const float FabricSmoothness = 0.1f;

        /// <summary>The body never drawn under the shirt: the torso it covers all round.</summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.Spine };

        /// <summary>
        /// The body hidden only where the shirt covers it (BodyUnderGarment): the hips down to the hem, the upper arms down to the cuffs (short
        /// of the elbow), the neck under the collar.
        /// </summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered =
        {
            HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.Neck,
        };

        [MenuItem("CityX/Rebuild T-Shirt")]
        public static GameObject Build()
        {
            Mesh mesh = BlenderGarmentMesh.Bake(SourcePath, "TShirtMesh", MeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.PlainMaterial(MaterialPath, Cotton, FabricSmoothness);
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out int bodyTriangles);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<TShirt>(
                mesh, material, PrefabPath, "TShirt", GroundScale, GroundPitchDegrees, GroundLift,
                shirt =>
                {
                    shirt.HiddenBodyTriangles = hidden;
                    shirt.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: TShirt written to {PrefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
