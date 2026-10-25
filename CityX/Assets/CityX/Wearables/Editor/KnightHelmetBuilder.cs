using CityX.Character;
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
    /// Its colour (owner, 2026-10-18: "use the textures from this on the knight helmet", Meshy's Silver Sentinel Helm, four 2048 maps) is baked in
    /// Blender onto this mesh's own unwrap as ONE 512 map, KnightHelmetSteel.png (a Meshy texture set is far too heavy for many players at once);
    /// what the trimmed mesh no longer has simply was not baked. Metal and gloss are the material's, not maps.
    /// </summary>
    public static class KnightHelmetBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/KnightHelmet.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/KnightHelmet.glb";
        private const string MeshPath = "Assets/CityX/Wearables/KnightHelmetMesh.asset";
        private const string TexturePath = "Assets/CityX/Wearables/KnightHelmetSteel.png";
        private const string MaterialPath = "Assets/CityX/Wearables/KnightHelmetSteel.mat";

        /// <summary>Polished steel: the colour map carries the wear and shading, the material the shine.</summary>
        public const float SteelMetallic = 0.6f;

        public const float SteelSmoothness = 0.6f;

        /// <summary>
        /// The owner's hand fit on the worn helmet in Play Mode (2026-10-18), baked into the mesh on top of the Blender placement: in the Head
        /// bone's space, a little lower and further back, the front tipped down and a touch smaller.
        /// </summary>
        private static readonly Vector3 FitPosition = new Vector3(0.0f, -0.05f, -0.04f);
        private static readonly Vector3 FitRotationEuler = new Vector3(5.57f, 0.0f, 0.0f);
        private const float FitScale = 0.975f;

        /// <summary>The helmet's own material (a textured item cannot share the flat-tinted helmet material): the baked colour map on lit steel.</summary>
        private static Material SteelMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(CityX.Core.LitShader.Find());
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", SteelMetallic);
            material.SetFloat("_Smoothness", SteelSmoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        [MenuItem("CityX/Rebuild Knight Helmet")]
        public static GameObject Build()
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            HeldModelBaker.Place(vertices, normals, new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f), Quaternion.Euler(FitRotationEuler), FitScale);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] += FitPosition;
            }
            Mesh mesh = HeldModelBaker.Save(MeshPath, "KnightHelmetMesh", vertices, normals, triangles, uv);

            var root = new GameObject("KnightHelmet");
            try
            {
                var model = new GameObject(KnightHelmet.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = SteelMaterial();

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
