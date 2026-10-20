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

        /// <summary>The body never drawn under the shirt: the torso (but the skin inside the crew neck, InsideTheNeck).</summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.Spine };

        /// <summary>
        /// The crew neck's opening (tshirt_build.py's NECK_SIDE, NECK_FRONT, NECK_BACK: an oval round the neck's axis, bind pose, +z ahead), less
        /// NeckBand, about where the collar's band lies: the skin within it is drawn, all of it, along that clean line (the build keeps the band
        /// out of the body, so none of it comes through). Hidden there, the inside of the shirt showed through the neck; hidden only where the
        /// shirt covered it, it came through the band in jagged bits.
        /// </summary>
        private const float NeckSide = 0.28f;
        private const float NeckFront = 0.2f;
        private const float NeckBack = 0.2f;
        private const float NeckBand = 0.02f;
        private const float NeckFloor = 1.75f;

        private static bool InsideTheNeck(Vector3 centre, Vector3 normal)
        {
            float across = centre.x / (NeckSide - NeckBand);
            float ahead = centre.z / ((centre.z > 0.0f ? NeckFront : NeckBack) - NeckBand);
            return centre.y > NeckFloor && across * across + ahead * ahead < 1.0f;
        }

        /// <summary>
        /// The body hidden only where the shirt covers it (BodyUnderGarment): the hips down to the hem, the upper arms down to the cuffs (short
        /// of the elbow). Not the neck: the crew neck opens round it.
        /// </summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered =
        {
            HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
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
                new Bounds(), out int bodyTriangles, InsideTheNeck);
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
