using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the jeans: the owner's cut of Meshy's jeans, fitted to this body (SourceArt/BlackJeans/BlackJeans.glb, black_jeans_fit.py) and
    /// skinned in Blender by black_jeans_build.py into Models/BlackJeans.json, one pair in three colours, each its own 1024 map: the Black Jeans
    /// (BlackJeans.jpg), the Blue, Bred and Maroon Jeans (owner, 2026-10-25; Meshy's retextures of the same figure baked onto the cut by
    /// SourceArt/BlackJeans/jeans_colour_bake.py). To refit them, move them in Blender, export over BlackJeans.glb and run the build again.
    /// </summary>
    public static class JeansBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/BlackJeans.prefab";
        public const string BluePrefabPath = "Assets/CityX/Wearables/BlueJeans.prefab";
        public const string BredPrefabPath = "Assets/CityX/Wearables/BredJeans.prefab";
        public const string MaroonPrefabPath = "Assets/CityX/Wearables/MaroonJeans.prefab";
        private const string SourcePath = "Assets/CityX/Wearables/Models/BlackJeans.json";
        private const string MeshPath = "Assets/CityX/Wearables/BlackJeansMesh.asset";

        // Lying on the ground: shrunk and tipped, as the old jeans were.
        private const float GroundScale = 0.5f;
        private const float GroundPitchDegrees = -70.0f;
        private const float GroundLift = 0.12f;

        /// <summary>Denim: matte.</summary>
        private const float FabricSmoothness = 0.1f;

        /// <summary>
        /// The legs, which the jeans cover all round down below the ankle joints: never drawn, so a stride never brings their skin through
        /// (hidden only where covered, a calf behind a bent knee came through). The ankles and the feet stay.
        /// </summary>
        private static readonly HumanBodyBones[] AlwaysHidden =
        {
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        };

        /// <summary>The body hidden only where the jeans cover it (BodyUnderGarment): the hips, up to the waistband.</summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered = { HumanBodyBones.Hips };

        /// <summary>
        /// The skin this far down from the waistband's top is always drawn: hidden where covered, its cut edge showed above the band as a row
        /// of dark teeth (the jeans' inside seen through it). The band stands at least 1 cm off it.
        /// </summary>
        private const float WaistbandShown = 0.04f;

        /// <summary>
        /// The skin below this height is always drawn: the ankles, which the hems hang over standing. Hidden, a stride lifting a hem showed a
        /// gap down to the foot's jagged cut edge.
        /// </summary>
        private const float AnkleShown = 0.15f;

        [MenuItem("CityX/Rebuild Jeans")]
        public static void BuildAll()
        {
            Build();
            BuildBlue();
            BuildBred();
            BuildMaroon();
        }

        /// <summary>The Black Jeans (the pair's own mesh is made here; the other colours reuse it).</summary>
        public static GameObject Build() => Save("BlackJeans", PrefabPath, ItemRegistry.BlackJeans, bakeMesh: true);

        public static GameObject BuildBlue() => Save("BlueJeans", BluePrefabPath, ItemRegistry.BlueJeans, bakeMesh: false);

        public static GameObject BuildBred() => Save("BredJeans", BredPrefabPath, ItemRegistry.BredJeans, bakeMesh: false);

        public static GameObject BuildMaroon() => Save("MaroonJeans", MaroonPrefabPath, ItemRegistry.MaroonJeans, bakeMesh: false);

        private static GameObject Save(string name, string prefabPath, string itemId, bool bakeMesh)
        {
            Mesh mesh = bakeMesh ? BlenderGarmentMesh.Bake(SourcePath, "BlackJeansMesh", MeshPath) : AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                return null;
            }
            Material material = BlenderGarmentMesh.TexturedMaterial($"Assets/CityX/Wearables/{name}.mat", $"Assets/CityX/Wearables/{name}.jpg",
                FabricSmoothness);
            float waistband = mesh.bounds.max.y - WaistbandShown;
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out int bodyTriangles, centre => centre.y > waistband || centre.y < AnkleShown);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<Jeans>(
                mesh, material, prefabPath, name, GroundScale, GroundPitchDegrees, GroundLift,
                jeans =>
                {
                    jeans.SetItemId(itemId);
                    jeans.HiddenBodyTriangles = hidden;
                    jeans.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: {name} written to {prefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
            }
            return saved;
        }
    }
}
