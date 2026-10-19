using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Midnight Coat: the owner's own fit of Meshy's shearling coat on this body (SourceArt/MidnightCoat/MidnightCoat.glb), skinned in
    /// Blender by coat_build.py into Models/MidnightCoat.json, with its one 512 map (MidnightCoat.jpg). To refit it, move it in Blender, export
    /// over the glb and run the script again.
    /// </summary>
    public static class MidnightCoatBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/MidnightCoat.prefab";
        private const string SourcePath = "Assets/CityX/Wearables/Models/MidnightCoat.json";
        private const string TexturePath = "Assets/CityX/Wearables/MidnightCoat.jpg";
        private const string MeshPath = "Assets/CityX/Wearables/MidnightCoatMesh.asset";
        private const string MaterialPath = "Assets/CityX/Wearables/MidnightCoat.mat";

        // Matches the other long coats' ground pose: tipped so it reads as a dropped coat.
        private const float GroundScale = 0.45f;
        private const float GroundPitchDegrees = -78.0f;
        private const float GroundLift = 0.15f;

        /// <summary>Suede and wool fur: a little sheen, no more.</summary>
        private const float FabricSmoothness = 0.2f;

        /// <summary>
        /// The body never drawn under the coat (owner, 2026-10-19: "make the torso and top half of arms up until the elbow invisible while
        /// wearing the midnight coat"): the torso and the upper arms, to the elbow.
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHidden =
        {
            HumanBodyBones.Spine, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
        };

        /// <summary>
        /// Behind the body, also the hips and the top of the thighs, down to the buttocks' crease (bind pose, -z behind; owner: "make the amount
        /// invisible a little more on the back, go lower because some the back is clipping through when doing swinging sword animations").
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHiddenBehind =
        {
            HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
        };
        private static readonly Bounds Behind = new Bounds(new Vector3(0.0f, 1.05f, -0.3f), new Vector3(0.8f, 0.6f, 0.6f));

        /// <summary>
        /// The coat's open front, bind pose (+z ahead), from the waist to the collar: the torso there is drawn wherever it shows through it
        /// (owner: "make a little bit of the top of the torso visible, any amount that is in view regularly", "and ... toward the bottom").
        /// </summary>
        private static readonly Bounds FrontOpening = new Bounds(new Vector3(0.0f, 1.5f, 0.25f), new Vector3(0.4f, 1.2f, 0.5f));

        /// <summary>
        /// The body hidden only where the coat covers it (BodyUnderGarment): the neck and forearms. Never the hips and legs: the skirt swings
        /// off them and the legs come out of its slit.
        /// </summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered =
        {
            HumanBodyBones.Neck, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
        };

        [MenuItem("CityX/Rebuild Midnight Coat")]
        public static GameObject Build()
        {
            Mesh mesh = BlenderGarmentMesh.Bake(SourcePath, "MidnightCoatMesh", MeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.TexturedMaterial(MaterialPath, TexturePath, FabricSmoothness);
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, AlwaysHiddenBehind, Behind, HiddenWhereCovered, FrontOpening,
                out int bodyTriangles);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<MidnightCoat>(
                mesh, material, PrefabPath, "MidnightCoat", GroundScale, GroundPitchDegrees, GroundLift,
                coat =>
                {
                    coat.HiddenBodyTriangles = hidden;
                    coat.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: MidnightCoat written to {PrefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
