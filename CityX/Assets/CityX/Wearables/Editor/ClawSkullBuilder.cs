using CityX.Character;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Claw Skull from Models/ClawSkull.glb (docs/CLAW_SKULL.md): the Crimson Claw's head as the owner fitted it in Blender on a body
    /// standing at the origin, so the export's own placement is kept and the head's centre taken off it, putting the head sphere's centre, the
    /// Head bone's pivot, at the origin. To refit it, move or scale it in Blender and export over the same file. It wears the Claw's own
    /// material, flat colour with no map, so it always matches the Claw and adds no texture.
    /// </summary>
    public static class ClawSkullBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/ClawSkull.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/ClawSkull.glb";
        public const string MaterialPath = "Assets/CityX/Character/Materials/CrimsonClawBody.mat";
        private const string MeshPath = "Assets/CityX/Wearables/ClawSkullMesh.asset";

        [MenuItem("CityX/Rebuild Claw Skull")]
        public static GameObject Build()
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            HeldModelBaker.Place(vertices, normals, new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f), Quaternion.identity, 1.0f);
            Mesh mesh = HeldModelBaker.Save(MeshPath, "ClawSkullMesh", vertices, normals, triangles, uv);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                throw new System.IO.FileNotFoundException($"CityX: no material at {MaterialPath}: build the Crimson Claw first");
            }

            var root = new GameObject("ClawSkull");
            try
            {
                var model = new GameObject(ClawSkull.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = material;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = HeadWearable.InteractRange;

                root.AddComponent<ClawSkull>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
