using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the jeans: the owner's cut of Meshy's jeans, fitted to this body (SourceArt/BlackJeans/BlackJeans.glb, black_jeans_fit.py) and
    /// skinned in Blender by black_jeans_build.py into Models/BlackJeans.json, one pair in several colours, each its own 1024 map: the Black
    /// Jeans (BlackJeans.jpg), then the Blue, Bred and Maroon Jeans (owner, 2026-10-25) and the Khakis (2026-10-26), Meshy's retextures of the
    /// same figure baked onto the cut by SourceArt/BlackJeans/jeans_colour_bake.py. To refit them, move them in Blender, export over
    /// BlackJeans.glb and run the build again.
    /// </summary>
    public static class JeansBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/BlackJeans.prefab";
        public const string BluePrefabPath = "Assets/CityX/Wearables/BlueJeans.prefab";
        public const string BredPrefabPath = "Assets/CityX/Wearables/BredJeans.prefab";
        public const string MaroonPrefabPath = "Assets/CityX/Wearables/MaroonJeans.prefab";
        public const string KhakisPrefabPath = "Assets/CityX/Wearables/Khakis.prefab";
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
        /// The skin is always drawn below this much over the hems' lowest point, and hidden above it: the ankle skin left drawn higher (to 0.15 m)
        /// poked out past the hems in some strides (owner, 2026-10-25: "make it a tiny bit higher than the jeans lowest point so you cant see
        /// the under"). Hidden all the way down, a stride lifting a hem showed a gap down to the foot's jagged cut edge.
        /// </summary>
        private const float AnkleShownOverHem = 0.045f;   // 2 cm left a gap with the skin's jagged cut edge under a lifted hem

        [MenuItem("CityX/Rebuild Jeans")]
        public static void BuildAll()
        {
            Build();
            BuildBlue();
            BuildBred();
            BuildMaroon();
            BuildKhakis();
        }

        /// <summary>The Black Jeans (the pair's own mesh is made here; the other colours reuse it).</summary>
        public static GameObject Build() => Save("BlackJeans", PrefabPath, ItemRegistry.BlackJeans, bakeMesh: true);

        public static GameObject BuildBlue() => Save("BlueJeans", BluePrefabPath, ItemRegistry.BlueJeans, bakeMesh: false);

        public static GameObject BuildBred() => Save("BredJeans", BredPrefabPath, ItemRegistry.BredJeans, bakeMesh: false);

        public static GameObject BuildMaroon() => Save("MaroonJeans", MaroonPrefabPath, ItemRegistry.MaroonJeans, bakeMesh: false);

        public static GameObject BuildKhakis() => Save("Khakis", KhakisPrefabPath, ItemRegistry.Khakis, bakeMesh: false);

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
            float ankle = mesh.bounds.min.y + AnkleShownOverHem;
            byte[] hidden = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out int bodyTriangles, centre => centre.y > waistband || centre.y < ankle);
            // In shoes the ankles under the hems go too: the shoes cover them, and a jump pointing the feet brought their skin out through the
            // shoes (owner, 2026-10-26: "when pants and shoes are being worn you can make the feet underneath invisible").
            byte[] withShoes = BodyUnderGarment.HiddenTriangles(mesh, AlwaysHidden, new HumanBodyBones[0], new Bounds(), HiddenWhereCovered,
                new Bounds(), out _, centre => centre.y > waistband);
            GameObject saved = SkinnedGarmentBuilder.BuildPrefabFromMesh<Jeans>(
                mesh, material, prefabPath, name, GroundScale, GroundPitchDegrees, GroundLift,
                jeans =>
                {
                    jeans.SetItemId(itemId);
                    jeans.HiddenBodyTriangles = hidden;
                    jeans.HiddenBodyTrianglesWithShoes = withShoes;
                    jeans.HiddenFromBodyTriangles = bodyTriangles;
                });
            if (saved != null)
            {
                Debug.Log($"CityX: {name} written to {prefabPath} -- {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, " +
                    $"skin drawn below {ankle:F3} m");
            }
            return saved;
        }
    }
}
