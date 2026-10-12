using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The CURRENT player body as an .obj for Blender, to fit clothing to (owner, 2026-10-11): the tube body with its fused shoulders and the
    /// head, in the rest pose the mesh is modelled in (arms down, BodyRig.RestBindMatrix), at real size in metres, feet on y = 0, facing +Z.
    /// Each part is its own object in the file. Unlike PlayerBodyReferenceExporter (the original Godot body), this reads Player.prefab itself.
    ///
    /// Blender: File > Import > Wavefront (.obj) with "Forward: Z, Up: Y"; Unity's left-handed X is mirrored by the import, which is why X is
    /// negated here, so the character's left stays its left.
    /// </summary>
    public static class PlayerModelExporter
    {
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string OutputPath = "../PlayerModel.obj";

        [MenuItem("CityX/Export Player Model for Blender (OBJ)")]
        public static void Export()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject player = Object.Instantiate(prefab);
            try
            {
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var obj = new StringBuilder();
                obj.AppendLine("# CityX player model: rest pose (arms down), metres, Y up, facing +Z.");
                int offset = 1;

                foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null || skin.sharedMesh.name != "FusedBody")
                    {
                        continue;   // the body only, not garments or hair
                    }
                    // The mesh's own vertices ARE the rest pose; placed by the renderer's transform.
                    offset = Write(obj, "Body", skin.sharedMesh, skin.transform.localToWorldMatrix, offset);
                }
                foreach (MeshFilter filter in player.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null && filter.name.ToLowerInvariant().Contains("head"))
                    {
                        offset = Write(obj, "Head", filter.sharedMesh, HeadAtRest(player, filter), offset);
                    }
                }
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputPath));
                File.WriteAllText(path, obj.ToString());
                Debug.Log($"CityX: player model written to {path} ({offset - 1} vertices)");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        /// <summary>
        /// The head is a child of the head bone, which in the prefab stands in the T-pose skeleton's place; at rest the head bone is where the
        /// rest bind puts it, so the head goes there.
        /// </summary>
        private static Matrix4x4 HeadAtRest(GameObject player, MeshFilter head)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform bone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (bone == null)
            {
                return head.transform.localToWorldMatrix;
            }
            Matrix4x4 headInBone = bone.worldToLocalMatrix * head.transform.localToWorldMatrix;
            Matrix4x4 boneAtRest = animator.transform.localToWorldMatrix * CityX.Character.BodyRig.RestBindMatrix(HumanBodyBones.Head);
            return boneAtRest * headInBone;
        }

        private static int Write(StringBuilder obj, string name, Mesh mesh, Matrix4x4 toWorld, int offset)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            obj.AppendLine($"o {name}");
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            foreach (Vector3 v in vertices)
            {
                Vector3 p = toWorld.MultiplyPoint3x4(v);
                obj.AppendLine(string.Format(inv, "v {0:0.#####} {1:0.#####} {2:0.#####}", -p.x, p.y, p.z));
            }
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            if (hasNormals)
            {
                foreach (Vector3 n in normals)
                {
                    Vector3 w = toWorld.MultiplyVector(n).normalized;
                    obj.AppendLine(string.Format(inv, "vn {0:0.####} {1:0.####} {2:0.####}", -w.x, w.y, w.z));
                }
            }
            int[] triangles = mesh.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                // X is mirrored, so the winding turns over too.
                int a = triangles[i] + offset, b = triangles[i + 2] + offset, c = triangles[i + 1] + offset;
                obj.AppendLine(hasNormals ? $"f {a}//{a} {b}//{b} {c}//{c}" : $"f {a} {b} {c}");
            }
            return offset + vertices.Length;
        }
    }
}
