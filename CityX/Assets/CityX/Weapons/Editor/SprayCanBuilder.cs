using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds SprayCan.prefab (2026-10-15): a can of spray paint drawn in code -- a lathed metal can with a domed shoulder and a cap that shows
    /// the colour last painted with (SprayCanItem.Cap) -- a real can's size times the city's 1.3 (25 cm tall, 8.6 cm across). Two small meshes,
    /// no textures.
    /// </summary>
    public static class SprayCanBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/SprayCan.prefab";
        private const string BodyMeshPath = "Assets/CityX/Weapons/SprayCanBody.asset";
        private const string CapMeshPath = "Assets/CityX/Weapons/SprayCanCap.asset";
        private const string MetalMaterialPath = "Assets/CityX/Weapons/SprayCanMetal.mat";
        private const string CapMaterialPath = "Assets/CityX/Weapons/SprayCanCap.mat";

        /// <summary>The can's outline from the bottom up (radius, height), metres; its middle is put on the hand.</summary>
        private static readonly Vector2[] BodyProfile =
        {
            new Vector2(0.0f, 0.0f), new Vector2(0.040f, 0.0f), new Vector2(0.043f, 0.006f), new Vector2(0.043f, 0.200f),
            new Vector2(0.040f, 0.214f), new Vector2(0.031f, 0.226f), new Vector2(0.016f, 0.232f), new Vector2(0.0f, 0.233f),
        };

        private static readonly Vector2[] CapProfile =
        {
            new Vector2(0.0f, 0.229f), new Vector2(0.018f, 0.229f), new Vector2(0.018f, 0.246f), new Vector2(0.014f, 0.252f),
            new Vector2(0.0f, 0.253f),
        };

        private const int Sides = 20;

        /// <summary>The can is held by its middle.</summary>
        private const float GripHeight = 0.11f;

        [MenuItem("CityX/Rebuild Spray Can")]
        public static GameObject Build()
        {
            Mesh body = SaveMesh(Lathe(BodyProfile, "SprayCanBody"), BodyMeshPath);
            Mesh cap = SaveMesh(Lathe(CapProfile, "SprayCanCap"), CapMeshPath);
            Material metal = EnsureMaterial(MetalMaterialPath, new Color(0.74f, 0.76f, 0.79f), 0.85f, 0.62f);
            Material capMaterial = EnsureMaterial(CapMaterialPath, Color.white, 0.0f, 0.5f);

            var root = new GameObject("SprayCan");
            try
            {
                var bodyObject = new GameObject("Body");
                bodyObject.transform.SetParent(root.transform, false);
                bodyObject.transform.localPosition = Vector3.down * GripHeight;
                bodyObject.AddComponent<MeshFilter>().sharedMesh = body;
                bodyObject.AddComponent<MeshRenderer>().sharedMaterial = metal;

                var capObject = new GameObject("Cap");
                capObject.transform.SetParent(root.transform, false);
                capObject.transform.localPosition = Vector3.down * GripHeight;
                capObject.AddComponent<MeshFilter>().sharedMesh = cap;
                var capRenderer = capObject.AddComponent<MeshRenderer>();
                capRenderer.sharedMaterial = capMaterial;

                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = 0.043f;
                collider.height = 0.253f;
                collider.center = new Vector3(0.0f, 0.253f * 0.5f - GripHeight, 0.0f);

                var item = root.AddComponent<SprayCanItem>();
                item.Cap = capRenderer;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private const string GraffitiMaterialPath = "Assets/CityX/Weapons/GraffitiPaint.mat";

        /// <summary>
        /// What graffiti is drawn with (GraffitiNetwork.PaintMaterial -> GraffitiWall): URP's Lit shader, see-through, a little sheen -- lit, so
        /// the sun, the street lights, the room lights and the shadows fall on the paint as on the wall under it (owner: "make the light bounce
        /// off properly ingame as if it was apart of the wall texture"). An asset, so a build keeps that shader variant; each piece copies it
        /// and gives the copy its own paint.
        /// </summary>
        public static Material EnsureGraffitiMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GraffitiMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, GraffitiMaterialPath);
            }
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);   // alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            material.SetFloat("_Smoothness", 0.28f);
            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_ReceiveShadows", 1.0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A profile turned round the y axis: smooth sides, flat ends.</summary>
        private static Mesh Lathe(Vector2[] profile, string name)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int segment = 0; segment + 1 < profile.Length; segment++)
            {
                Vector2 a = profile[segment];
                Vector2 b = profile[segment + 1];
                Vector2 along = b - a;
                // The outward normal of this band in the profile's plane (radius out, height up).
                Vector2 n2 = new Vector2(along.y, -along.x).normalized;
                if (n2.x < 0.0f || (Mathf.Approximately(n2.x, 0.0f) && along.x < 0.0f))
                {
                    n2 = -n2;
                }
                int first = positions.Count;
                for (int s = 0; s <= Sides; s++)
                {
                    float angle = s / (float)Sides * Mathf.PI * 2.0f;
                    var spoke = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                    Vector3 normal = (spoke * n2.x + Vector3.up * n2.y).normalized;
                    positions.Add(spoke * a.x + Vector3.up * a.y);
                    normals.Add(normal);
                    positions.Add(spoke * b.x + Vector3.up * b.y);
                    normals.Add(normal);
                }
                for (int s = 0; s < Sides; s++)
                {
                    int v = first + s * 2;
                    // Outward-facing, clockwise seen from outside.
                    triangles.Add(v);
                    triangles.Add(v + 1);
                    triangles.Add(v + 3);
                    triangles.Add(v);
                    triangles.Add(v + 3);
                    triangles.Add(v + 2);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material EnsureMaterial(string path, Color colour, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
