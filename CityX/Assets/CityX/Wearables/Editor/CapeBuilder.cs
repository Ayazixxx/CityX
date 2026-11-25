using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the capes (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily, make it tied
    /// around the front"): one prefab a colourway (CapeColourways), its outside, lining and ties each in the colourway's colour. The cape itself is
    /// made when it is worn (CapeShape, CapeCloth, CapeLook); the prefab holds its materials and the folded cape it lies as on the ground.
    /// </summary>
    public static class CapeBuilder
    {
        public const string Folder = "Assets/CityX/Wearables/Capes";
        private const string FoldedMeshPath = Folder + "/FoldedCapeMesh.asset";

        /// <summary>Cloth: barely any shine.</summary>
        private const float Smoothness = 0.12f;

        /// <summary>The folded cape on the ground: its size, metres.</summary>
        private static readonly Vector3 FoldedSize = new Vector3(0.46f, 0.06f, 0.34f);

        [MenuItem("CityX/Rebuild Capes")]
        public static void BuildAllFromMenu() => BuildAll();

        public static Inventory.ItemPrefabEntry[] BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Wearables", "Capes");
            }
            Mesh folded = SaveMesh(FoldedMesh());
            var entries = new List<Inventory.ItemPrefabEntry>();
            foreach (CapeColourways.Colourway colourway in CapeColourways.All)
            {
                GameObject prefab = Save(colourway, folded);
                if (prefab != null)
                {
                    entries.Add(new Inventory.ItemPrefabEntry { ItemId = colourway.ItemId, Prefab = prefab });
                }
            }
            AssetDatabase.SaveAssets();
            return entries.ToArray();
        }

        private static GameObject Save(CapeColourways.Colourway colourway, Mesh folded)
        {
            Material outside = BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_outside.mat", colourway.Outside, Smoothness);
            Material lining = BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_lining.mat", colourway.Lining, Smoothness);
            Material ties = BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_ties.mat", colourway.Ties, Smoothness);
            var root = new GameObject(colourway.ItemId);
            try
            {
                var preview = new GameObject("Folded");
                preview.transform.SetParent(root.transform, false);
                preview.AddComponent<MeshFilter>().sharedMesh = folded;
                preview.AddComponent<MeshRenderer>().sharedMaterials = new[] { outside, lining };
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                var cape = root.AddComponent<Cape>();
                cape.SetItemId(colourway.ItemId);
                cape.Outside = outside;
                cape.Lining = lining;
                cape.Ties = ties;
                cape.GroundPreview = preview;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CapeColourways.PrefabPath(colourway.ItemId));
                Debug.Log($"CityX: {colourway.DisplayName} written to {CapeColourways.PrefabPath(colourway.ItemId)}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A folded cape: a flat slab on the ground, its top the outside, its sides and underside the lining.</summary>
        private static Mesh FoldedMesh()
        {
            Vector3 h = FoldedSize * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var top = new List<int>();
            var rest = new List<int>();
            void Face(Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> into)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { normal, normal, normal, normal });
                into.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            float y0 = 0.0f, y1 = FoldedSize.y;
            Face(Vector3.up, new Vector3(-h.x, y1, -h.z), new Vector3(-h.x, y1, h.z), new Vector3(h.x, y1, h.z), new Vector3(h.x, y1, -h.z), top);
            Face(Vector3.down, new Vector3(-h.x, y0, h.z), new Vector3(-h.x, y0, -h.z), new Vector3(h.x, y0, -h.z), new Vector3(h.x, y0, h.z), rest);
            Face(Vector3.forward, new Vector3(-h.x, y0, h.z), new Vector3(h.x, y0, h.z), new Vector3(h.x, y1, h.z), new Vector3(-h.x, y1, h.z), rest);
            Face(Vector3.back, new Vector3(h.x, y0, -h.z), new Vector3(-h.x, y0, -h.z), new Vector3(-h.x, y1, -h.z), new Vector3(h.x, y1, -h.z), rest);
            Face(Vector3.right, new Vector3(h.x, y0, h.z), new Vector3(h.x, y0, -h.z), new Vector3(h.x, y1, -h.z), new Vector3(h.x, y1, h.z), rest);
            Face(Vector3.left, new Vector3(-h.x, y0, -h.z), new Vector3(-h.x, y0, h.z), new Vector3(-h.x, y1, h.z), new Vector3(-h.x, y1, -h.z), rest);
            var mesh = new Mesh { name = "FoldedCape" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(rest, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(FoldedMeshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, FoldedMeshPath);
                return mesh;
            }
            saved.Clear();
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            return saved;
        }
    }
}
