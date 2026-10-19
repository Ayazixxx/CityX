using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the Knight Helmet from the owner's KnightxHelmet.glb (2026-10-18: "make this a helmet you can spawn and equip ingame called Knight
    /// Helmet"). The owner decimated it to about 7,400 triangles in Blender and fitted it there on a body standing at the origin, so the
    /// export's own placement (its node's scale and lift) is kept: the mesh is baked with BodyRig.HeadCentreY taken off, putting the head
    /// sphere's centre, which is the Head bone's pivot, at the origin. To refit it, move or scale it in Blender and export over the same file.
    ///
    /// No texture: the shared helmet material, tinted steel at runtime (KnightHelmet.Awake).
    /// </summary>
    public static class KnightHelmetBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/KnightHelmet.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/KnightHelmet.glb";
        private const string MeshPath = "Assets/CityX/Wearables/KnightHelmetMesh.asset";

        [MenuItem("CityX/Rebuild Knight Helmet")]
        public static GameObject Build()
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles);
            HeldModelBaker.Place(vertices, normals, new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f), Quaternion.identity, 1.0f);
            Mesh mesh = HeldModelBaker.Save(MeshPath, "KnightHelmetMesh", vertices, normals, triangles);

            var root = new GameObject("KnightHelmet");
            try
            {
                var model = new GameObject(KnightHelmet.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = CharacterSkin.EnsureSharedCategory("Helmet");

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = HeadWearable.InteractRange;

                root.AddComponent<KnightHelmet>();

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
