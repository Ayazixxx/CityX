using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Fur Hooded Parkas: Meshy's parka remodelled onto this body in the owner's Blender (SourceArt/Parka/Parka.glb), skinned by
    /// parka_build.py into Models/FurHoodedParka.json. Every colour is that one mesh with its own 1024 map, named after its prefab
    /// (GreenFurHoodedParka.jpg); a new colour is a new map, an item and a line here. docs/FUR_HOODED_PARKA.md.
    /// </summary>
    public static class FurHoodedParkaBuilder
    {
        public const string GreenPrefabPath = "Assets/CityX/Wearables/GreenFurHoodedParka.prefab";
        public const string MaroonPrefabPath = "Assets/CityX/Wearables/MaroonFurHoodedParka.prefab";
        private const string SourcePath = "Assets/CityX/Wearables/Models/FurHoodedParka.json";
        private const string MeshPath = "Assets/CityX/Wearables/FurHoodedParkaMesh.asset";

        /// <summary>Matt quilted cloth and fur.</summary>
        private const float FabricSmoothness = 0.15f;

        [MenuItem("CityX/Rebuild Fur Hooded Parkas")]
        public static void BuildAll()
        {
            Build();
            BuildMaroon();
        }

        /// <summary>The Green Fur Hooded Parka (the coat's own mesh is made here; other colours reuse it).</summary>
        public static GameObject Build() => Save("GreenFurHoodedParka", GreenPrefabPath, ItemRegistry.GreenFurHoodedParka, bakeMesh: true);

        /// <summary>The Maroon Fur Hooded Parka (owner, 2026-11-21: "make the maroon colorway ... same blue rarity level").</summary>
        public static GameObject BuildMaroon() => Save("MaroonFurHoodedParka", MaroonPrefabPath, ItemRegistry.MaroonFurHoodedParka, bakeMesh: false);

        private static GameObject Save(string name, string prefabPath, string itemId, bool bakeMesh)
        {
            Mesh mesh = bakeMesh ? BlenderGarmentMesh.Bake(SourcePath, "FurHoodedParkaMesh", MeshPath) : AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.TexturedMaterial($"Assets/CityX/Wearables/{name}.mat", $"Assets/CityX/Wearables/{name}.jpg",
                FabricSmoothness);
            byte[] hidden = LongCoatBody.Hidden(mesh, out int bodyTriangles);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<FurHoodedParka>(
                mesh, material, prefabPath, name, LongCoatBody.GroundScale, LongCoatBody.GroundPitchDegrees, LongCoatBody.GroundLift,
                parka =>
                {
                    parka.SetItemId(itemId);
                    parka.HiddenBodyTriangles = hidden;
                    parka.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: {name} written to {prefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
