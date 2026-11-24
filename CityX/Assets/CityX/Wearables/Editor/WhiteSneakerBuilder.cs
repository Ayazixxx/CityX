using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the White Sneaker (owner, 2026-10-25: "add it as a wearable shoe and try to fit is the best you can. Item name : White Sneaker"):
    /// Models/WhiteSneaker.glb is the pair as the owner approved it in Blender (SourceArt/WhiteSneaker: Meshy's "Urban Soles" with the legs
    /// cut off, reduced, the tread sliced flat, one shoe mirrored for the other, then the owner's trim inside and the smoothing of the collar
    /// and the toe), standing on our body's feet in the game's frame. It is split by side and each shoe baked into its foot bone's space at
    /// rest, so worn it hangs on the bone with no offset (Shoes). To refit it, move a shoe in Blender and export over the same file.
    ///
    /// The BW Sneakers (owner, 2026-10-26) are the same pair in Meshy's black-and-white recolour, baked onto its layout by
    /// SourceArt/WhiteSneaker/sneaker_colour_bake.py: each colour its own map on the one pair of meshes.
    /// The Black Sneakers (owner, 2026-11-23: "make an all black version of the sneakers texture", "you can probably just make the white one
    /// darker") wear the white map darkened by SourceArt/WhiteSneaker/sneaker_black.py.
    /// </summary>
    public static class WhiteSneakerBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/WhiteSneaker.prefab";
        public const string BWPrefabPath = "Assets/CityX/Wearables/BWSneakers.prefab";
        public const string BlackPrefabPath = "Assets/CityX/Wearables/BlackSneakers.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/WhiteSneaker.glb";
        public const string LeftMeshPath = "Assets/CityX/Wearables/WhiteSneakerLeftMesh.asset";
        public const string RightMeshPath = "Assets/CityX/Wearables/WhiteSneakerRightMesh.asset";

        /// <summary>Canvas and rubber: matte.</summary>
        private const float Smoothness = 0.2f;

        /// <summary>How much bigger than the approved model the shoes are worn (owner, 2026-10-26: "make both shoes 1.1x bigger").</summary>
        public const float Size = 1.1f;

        /// <summary>
        /// How far forward of the approved model each shoe sits on its foot (owner, 2026-10-26: "slide it forward so the back of the heel it
        /// touching the inner wall of the shoe"; the tongue clipped into the front of the ankle): standing, the shin leans forward over the
        /// foot, so the heel stood 3 cm off the back of the ankle (the right ankle came out through its heel at 3).
        /// </summary>
        public const float Forward = 0.027f;

        /// <summary>The feet themselves, inside the shoes: never drawn.</summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

        /// <summary>The ankles, hidden where the collars cover them (above them the jeans' hems or bare shins show).</summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered = { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };

        [MenuItem("CityX/Rebuild Sneakers")]
        public static void BuildAll()
        {
            Build();
            BuildBW();
            BuildBlack();
        }

        public static GameObject Build() => Save("WhiteSneaker", PrefabPath, ItemRegistry.WhiteSneaker);

        public static GameObject BuildBW() => Save("BWSneakers", BWPrefabPath, ItemRegistry.BWSneakers);

        public static GameObject BuildBlack() => Save("BlackSneakers", BlackPrefabPath, ItemRegistry.BlackSneakers);

        /// <summary>
        /// The pair as the item <paramref name="itemId"/>, wearing Wearables/<paramref name="name"/>.jpg (the meshes are the same for each).
        /// </summary>
        private static GameObject Save(string name, string prefabPath, string itemId) =>
            SavePair(name, prefabPath, itemId, SourcePath, LeftMeshPath, RightMeshPath, "WhiteSneaker", Grown, Smoothness);

        /// <summary>
        /// A pair of shoes as the item <paramref name="itemId"/>: the model at <paramref name="sourcePath"/> (both shoes standing on the body's feet
        /// in the game's frame), each point put where <paramref name="placed"/> says, split by side, each shoe baked onto its foot (Shoes), wearing
        /// Wearables/<paramref name="name"/>.jpg. Shared by every pair (the sneakers, the Worn Boots).
        /// </summary>
        public static GameObject SavePair(string name, string prefabPath, string itemId, string sourcePath, string leftMeshPath, string rightMeshPath,
            string meshName, System.Func<Vector3, Vector3> placed, float smoothness)
        {
            HeldModelBaker.Load(sourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = placed(vertices[i]);
            }
            var root = new GameObject(name);
            var pair = new Mesh { name = meshName + "Pair", vertices = vertices, normals = normals, triangles = triangles };
            try
            {
                Material material = BlenderGarmentMesh.TexturedMaterial($"Assets/CityX/Wearables/{name}.mat", $"Assets/CityX/Wearables/{name}.jpg",
                    smoothness);
                // The left foot is on -x: each shoe the triangles wholly on its side.
                Transform left = Shoe(root.transform, "Left", leftMeshPath, meshName, HumanBodyBones.LeftFoot, vertices, normals, triangles, uv,
                    p => p.x < 0.0f, material);
                Transform right = Shoe(root.transform, "Right", rightMeshPath, meshName, HumanBodyBones.RightFoot, vertices, normals, triangles, uv,
                    p => p.x >= 0.0f, material);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                var shoes = root.AddComponent<Shoes>();
                shoes.Setup(itemId, left, right);
                pair.RecalculateBounds();
                shoes.HiddenBodyTriangles = BodyUnderGarment.HiddenTriangles(pair, AlwaysHidden, new HumanBodyBones[0], new Bounds(),
                    HiddenWhereCovered, new Bounds(), out shoes.HiddenFromBodyTriangles);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {name} written to {prefabPath} -- {triangles.Length / 3} triangles for the pair");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pair);
            }
        }

        /// <summary>
        /// A point of the approved pair at <see cref="Size"/>, grown from the ground under its own ankle so the sole stays on the floor, then
        /// moved <see cref="Forward"/>.
        /// </summary>
        public static Vector3 Grown(Vector3 point)
        {
            Vector3 ankle = BodyRig.RestBindMatrix(point.x < 0.0f ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot).GetColumn(3);
            var ground = new Vector3(ankle.x, 0.0f, ankle.z);
            return ground + (point - ground) * Size + Vector3.forward * Forward;
        }

        /// <summary>
        /// One shoe: the triangles <paramref name="side"/> takes, baked from the body's frame into <paramref name="foot"/>'s as the body's skin
        /// is bound to it (BodyRig.RestBindMatrix), saved, and placed under the pair's root where that foot stands (so the pair lies on the
        /// ground as worn).
        /// </summary>
        private static Transform Shoe(Transform root, string name, string meshPath, string meshName, HumanBodyBones foot, Vector3[] vertices,
            Vector3[] normals, int[] triangles, Vector2[] uv, System.Predicate<Vector3> side, Material material)
        {
            Matrix4x4 footInBody = BodyRig.RestBindMatrix(foot);
            Matrix4x4 toFoot = footInBody.inverse;
            var remap = new Dictionary<int, int>();
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (!side(vertices[triangles[t]]) || !side(vertices[triangles[t + 1]]) || !side(vertices[triangles[t + 2]]))
                {
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int i = triangles[t + k];
                    if (!remap.TryGetValue(i, out int j))
                    {
                        j = verts.Count;
                        remap[i] = j;
                        verts.Add(toFoot.MultiplyPoint3x4(vertices[i]));
                        norms.Add(toFoot.MultiplyVector(normals[i]).normalized);
                        uvs.Add(uv.Length > i ? uv[i] : Vector2.zero);
                    }
                    tris.Add(j);
                }
            }
            Mesh mesh = HeldModelBaker.Save(meshPath, meshName + name + "Mesh", verts.ToArray(), norms.ToArray(), tris.ToArray(), uvs.ToArray());
            var model = new GameObject(name);
            model.transform.SetParent(root, false);
            model.transform.localPosition = footInBody.GetColumn(3);
            model.transform.localRotation = footInBody.rotation;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = material;
            return model.transform;
        }
    }
}
