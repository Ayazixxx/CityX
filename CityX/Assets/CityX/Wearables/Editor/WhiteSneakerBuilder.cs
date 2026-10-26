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
    /// </summary>
    public static class WhiteSneakerBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/WhiteSneaker.prefab";
        public const string SourcePath = "Assets/CityX/Wearables/Models/WhiteSneaker.glb";
        private const string TexturePath = "Assets/CityX/Wearables/WhiteSneaker.jpg";
        private const string MaterialPath = "Assets/CityX/Wearables/WhiteSneaker.mat";
        public const string LeftMeshPath = "Assets/CityX/Wearables/WhiteSneakerLeftMesh.asset";
        public const string RightMeshPath = "Assets/CityX/Wearables/WhiteSneakerRightMesh.asset";

        /// <summary>Canvas and rubber: matte.</summary>
        private const float Smoothness = 0.2f;

        /// <summary>The feet themselves, inside the shoes: never drawn.</summary>
        private static readonly HumanBodyBones[] AlwaysHidden = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

        /// <summary>The ankles, hidden where the collars cover them (above them the jeans' hems or bare shins show).</summary>
        private static readonly HumanBodyBones[] HiddenWhereCovered = { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };

        [MenuItem("CityX/Rebuild White Sneaker")]
        public static GameObject Build()
        {
            HeldModelBaker.Load(SourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            var root = new GameObject("WhiteSneaker");
            var pair = new Mesh { name = "WhiteSneakerPair", vertices = vertices, normals = normals, triangles = triangles };
            try
            {
                Material material = BlenderGarmentMesh.TexturedMaterial(MaterialPath, TexturePath, Smoothness);
                // The left foot is on -x: each shoe the triangles wholly on its side.
                Transform left = Shoe(root.transform, "Left", LeftMeshPath, HumanBodyBones.LeftFoot, vertices, normals, triangles, uv,
                    p => p.x < 0.0f, material);
                Transform right = Shoe(root.transform, "Right", RightMeshPath, HumanBodyBones.RightFoot, vertices, normals, triangles, uv,
                    p => p.x >= 0.0f, material);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                var shoes = root.AddComponent<Shoes>();
                shoes.Setup(ItemRegistry.WhiteSneaker, left, right);
                pair.RecalculateBounds();
                shoes.HiddenBodyTriangles = BodyUnderGarment.HiddenTriangles(pair, AlwaysHidden, new HumanBodyBones[0], new Bounds(),
                    HiddenWhereCovered, new Bounds(), out shoes.HiddenFromBodyTriangles);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: WhiteSneaker written to {PrefabPath} -- {triangles.Length / 3} triangles for the pair");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(pair);
            }
        }

        /// <summary>
        /// One shoe: the triangles <paramref name="side"/> takes, baked from the body's frame into <paramref name="foot"/>'s as the body's skin
        /// is bound to it (BodyRig.RestBindMatrix), saved, and placed under the pair's root where that foot stands (so the pair lies on the
        /// ground as worn).
        /// </summary>
        private static Transform Shoe(Transform root, string name, string meshPath, HumanBodyBones foot, Vector3[] vertices,
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
            Mesh mesh = HeldModelBaker.Save(meshPath, "WhiteSneaker" + name + "Mesh", verts.ToArray(), norms.ToArray(), tris.ToArray(), uvs.ToArray());
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
