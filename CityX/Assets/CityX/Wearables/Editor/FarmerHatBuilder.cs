using CityX.Character;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Farmer Hat (owner, 2026-10-26: "separate the white playermodel from the hat. then lower face count to a reasonable amount and
    /// add it as a wearable hat ingame called "Farmer Hat""). Models/FarmerHat.glb is Meshy's straw hat cut off its figure and reduced
    /// (SourceArt/FarmerHat/farmer_hat_build.py), standing where that figure wore it scaled onto our body at the origin; the mesh is baked with
    /// BodyRig.HeadCentreY taken off, putting the head's centre, the Head bone's pivot, at the origin. Its colour is one 1024 map baked from
    /// Meshy's. To refit it, move or scale it in SourceArt/FarmerHat/FarmerHat.blend and export over the same file.
    /// </summary>
    public static class FarmerHatBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/FarmerHat.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/FarmerHat.glb";
        private const string MeshPath = "Assets/CityX/Wearables/FarmerHatMesh.asset";
        private const string TexturePath = "Assets/CityX/Wearables/FarmerHat.jpg";
        private const string MaterialPath = "Assets/CityX/Wearables/FarmerHat.mat";

        /// <summary>Dry straw: matte.</summary>
        private const float Smoothness = 0.1f;

        [MenuItem("CityX/Rebuild Farmer Hat")]
        public static GameObject Build()
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            HeldModelBaker.Place(vertices, normals, new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f), Quaternion.identity, 1.0f);
            Mesh mesh = HeldModelBaker.Save(MeshPath, "FarmerHatMesh", vertices, normals, triangles, uv);

            var root = new GameObject("FarmerHat");
            try
            {
                var model = new GameObject(FarmerHat.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = BlenderGarmentMesh.TexturedMaterial(MaterialPath, TexturePath, Smoothness);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;

                root.AddComponent<FarmerHat>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: FarmerHat written to {PrefabPath} -- {triangles.Length / 3} triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
