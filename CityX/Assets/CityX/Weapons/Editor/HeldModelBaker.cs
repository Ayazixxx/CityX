using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Turns a model the owner brought in (a .glb of a held weapon, lying any way round) into a plain mesh asset in the frame every held weapon here
    /// is built in: the grip's reference point (a sword's guard) at the origin, the handle down -Y, the business end up +Y. The orientation is
    /// MEASURED from the model's own vertices (its long axis, which end is which), not typed in, so a model that comes in diagonal (the baseball
    /// bat lies at 45 degrees) or curved (the crowbar's claw) lands upright all the same.
    /// </summary>
    public static class HeldModelBaker
    {
        /// <summary>The glb's first mesh with the import's own transform (glTF's mirror, the node's scale) applied, in the glb's metres.</summary>
        public static void Load(string sourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
        {
            Load(sourcePath, out vertices, out normals, out triangles, out _);
        }

        /// <summary>The same, with the model's texture coordinates (empty when it has none).</summary>
        public static void Load(string sourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (model == null)
            {
                throw new System.IO.FileNotFoundException($"CityX: no model at {sourcePath}");
            }
            MeshFilter filter = model.GetComponentInChildren<MeshFilter>(true);
            Mesh source = filter.sharedMesh;
            Matrix4x4 bake = filter.transform.localToWorldMatrix;
            vertices = source.vertices;
            normals = source.normals;
            triangles = source.triangles;
            uv = source.uv;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = bake.MultiplyPoint3x4(vertices[i]);
                normals[i] = bake.MultiplyVector(normals[i]).normalized;
            }
        }

        /// <summary>The mean of the points (all of them, or those <paramref name="keep"/> accepts).</summary>
        public static Vector3 Centre(Vector3[] points, System.Func<Vector3, bool> keep = null)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 p in points)
            {
                if (keep != null && !keep(p))
                {
                    continue;
                }
                sum += p;
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// The direction the points spread most along -- their long axis -- as a unit vector (the main eigenvector of their covariance, by power
        /// iteration). Its sign is arbitrary. Only the points <paramref name="keep"/> accepts count, when given.
        /// </summary>
        public static Vector3 LongAxis(Vector3[] points, System.Func<Vector3, bool> keep = null)
        {
            Vector3 centre = Centre(points, keep);
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 p in points)
            {
                if (keep != null && !keep(p))
                {
                    continue;
                }
                Vector3 d = p - centre;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 axis = new Vector3(1.0f, 0.71f, 0.33f).normalized;
            for (int i = 0; i < 64; i++)
            {
                var next = new Vector3(
                    xx * axis.x + xy * axis.y + xz * axis.z,
                    xy * axis.x + yy * axis.y + yz * axis.z,
                    xz * axis.x + yz * axis.y + zz * axis.z);
                if (next.sqrMagnitude < 1e-12f)
                {
                    break;
                }
                axis = next.normalized;
            }
            return axis;
        }

        /// <summary>The least and greatest of the points along <paramref name="axis"/> (projected from the origin).</summary>
        public static void Extent(Vector3[] points, Vector3 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (Vector3 p in points)
            {
                float t = Vector3.Dot(p, axis);
                min = Mathf.Min(min, t);
                max = Mathf.Max(max, t);
            }
        }

        /// <summary>
        /// Moves <paramref name="origin"/> to the frame's origin, turns by <paramref name="rotation"/> and scales by <paramref name="scale"/>, in place.
        /// </summary>
        public static void Place(Vector3[] vertices, Vector3[] normals, Vector3 origin, Quaternion rotation, float scale)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = rotation * (vertices[i] - origin) * scale;
                normals[i] = (rotation * normals[i]).normalized;
            }
        }

        /// <summary>Writes the mesh asset at <paramref name="meshPath"/> (updated in place when it exists, so prefabs keep their reference).</summary>
        public static Mesh Save(string meshPath, string name, Vector3[] vertices, Vector3[] normals, int[] triangles, Vector2[] uv = null)
        {
            var mesh = new Mesh { name = name, vertices = vertices, normals = normals, triangles = triangles };
            if (uv != null)
            {
                mesh.uv = uv;
            }
            mesh.RecalculateBounds();
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, meshPath);
            return mesh;
        }

        /// <summary>A profile (radius, height) turned round the y axis in <paramref name="sides"/> steps: smooth round, a band per profile step.</summary>
        public static Mesh Lathe(Vector2[] profile, string name, int sides)
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
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2.0f;
                    var spoke = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
                    Vector3 normal = (spoke * n2.x + Vector3.up * n2.y).normalized;
                    positions.Add(spoke * a.x + Vector3.up * a.y);
                    normals.Add(normal);
                    positions.Add(spoke * b.x + Vector3.up * b.y);
                    normals.Add(normal);
                }
                for (int s = 0; s < sides; s++)
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

        /// <summary>A plain coloured material in the game's lit shader at <paramref name="path"/> (made once, its colour and finish kept up to date).</summary>
        public static Material LitMaterial(string path, Color colour, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(CityX.Core.LitShader.Find());
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
