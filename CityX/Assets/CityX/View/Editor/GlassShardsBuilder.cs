using CityX.View;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the glass shard burst -- real angular fragment meshes now,
    /// not a billboard particle sprite. Requested: "improve upon the
    /// window breaking mechanics. ideally it would be like garrys mod/half
    /// life where it turns to shards." The old version was a 16-particle
    /// Stretch/Billboard sphere burst that faded to nothing in 0.4s -- it
    /// read fine from a distance but was never real glass, just a sparkle.
    /// This builds N small MeshFilter+Rigidbody+MeshCollider prisms
    /// (GlassShardMesh) as children of one root carrying GlassShardBurst,
    /// which does the actual per-instance launching at spawn time.
    /// </summary>
    public static class GlassShardsBuilder
    {
        private const string PrefabPath = "Assets/CityX/View/GlassShards.prefab";
        private const string MaterialPath = "Assets/CityX/View/Materials/GlassShards.mat";
        private const string MeshPathFormat = "Assets/CityX/View/GlassShardMesh{0}.asset";

        private const int ShardCount = 12;
        private const float ShardSizeMin = 0.05f;
        private const float ShardSizeMax = 0.16f;
        private const float ShardThickness = 0.005f;   // real window-pane glass, ~5mm
        private const float ShardMass = 0.03f;
        private const float ShardBounce = 0.15f;        // glass barely bounces
        private const float ShardFriction = 0.4f;

        private static readonly Color Glass = new Color(0.85f, 0.92f, 0.90f, 1.0f);

        [MenuItem("CityX/Rebuild Glass Shards")]
        public static GameObject Build()
        {
            var root = new GameObject("GlassShards");
            try
            {
                root.AddComponent<GlassShardBurst>();

                Material material = BuildMaterial();
                var physicsMaterial = new PhysicsMaterial("GlassShardPhysics")
                {
                    bounciness = ShardBounce,
                    dynamicFriction = ShardFriction,
                    staticFriction = ShardFriction,
                };

                for (int i = 0; i < ShardCount; i++)
                {
                    BuildShard(root.transform, i, material, physicsMaterial);
                }

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: glass shards written to {PrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildShard(Transform parent, int index, Material material,
            PhysicsMaterial physicsMaterial)
        {
            var shard = new GameObject($"Shard{index}");
            shard.transform.SetParent(parent, false);

            // Spread across the pane's own rough footprint rather than every
            // shard bursting from one identical point, and a size that
            // varies per-shard the same way a real pane doesn't break into
            // uniform pieces.
            shard.transform.localPosition = new Vector3(
                Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0.0f);
            shard.transform.localRotation = Random.rotation;

            float size = Random.Range(ShardSizeMin, ShardSizeMax);
            Mesh mesh = EnsureMeshAsset(
                GlassShardMesh.Build(index % GlassShardMesh.ShapeCount, size, ShardThickness),
                string.Format(MeshPathFormat, index));

            var filter = shard.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = shard.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = shard.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            collider.sharedMaterial = physicsMaterial;

            var body = shard.AddComponent<Rigidbody>();
            body.mass = ShardMass;

            // CONTINUOUS, NOT DISCRETE. A shard is only ShardThickness (5mm)
            // deep, and by the time it lands it can be falling several
            // metres/second -- discrete detection only checks bounding
            // volumes at the START and END of a physics step, so a thin fast
            // mover can end a step already past a thin static collider
            // having never registered a contact. Confirmed directly: a
            // scratch PlayMode test dropped shards from 2m and found them
            // 22m below the ground plane after 2.5 real seconds with this
            // left at the default Discrete mode.
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private static Material BuildMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(MaterialPath);
            }

            var material = new Material(CityX.Core.LitShader.Find());
            material.SetColor("_BaseColor", Glass);
            material.SetFloat("_Smoothness", 0.85f);
            material.SetFloat("_Metallic", 0.05f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>
        /// Same reason BottleBuilder's own EnsureMeshAsset exists: a Mesh
        /// built with `new Mesh()` lives only in memory, so it has to be
        /// saved as a real asset before the prefab is, or the reference
        /// resolves to nothing the next time the project loads.
        /// </summary>
        private static Mesh EnsureMeshAsset(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
