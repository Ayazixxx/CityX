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

        /// <summary>Suede and wool fur: a little sheen, no more.</summary>
        private const float FabricSmoothness = 0.2f;

        [MenuItem("CityX/Rebuild Midnight Coat")]
        public static GameObject Build()
        {
            Mesh mesh = BlenderGarmentMesh.Bake(SourcePath, "MidnightCoatMesh", MeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.TexturedMaterial(MaterialPath, TexturePath, FabricSmoothness);
            byte[] hidden = LongCoatBody.Hidden(mesh, out int bodyTriangles);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<MidnightCoat>(
                mesh, material, PrefabPath, "MidnightCoat", LongCoatBody.GroundScale, LongCoatBody.GroundPitchDegrees, LongCoatBody.GroundLift,
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
