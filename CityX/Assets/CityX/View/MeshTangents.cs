using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A mesh's tangents worked out from plain arrays, so it can run on a worker thread (Mesh.RecalculateTangents can only run on the main
    /// thread, and on a city chunk it cost about 5 ms there every time one was built or rebuilt: measured 2026-10-14 while an AK-47 sprayed a
    /// row of windows, one of the two halves of each 17 ms hitch). The classic per-triangle method (Lengyel): each triangle's direction of
    /// increasing U (and V) in space is added to its three corners, then each corner's sum is made square to its normal; w says which way the
    /// bitangent runs (the facade shader builds it as w * cross(normal, tangent)). On the flat faces a city is made of this is what Unity's
    /// own gives (MeshTangentsTests compares them).
    /// </summary>
    public static class MeshTangents
    {
        public static Vector4[] Compute(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[][] submeshTriangles)
        {
            int count = vertices.Length;
            var tangents = new Vector4[count];
            if (normals == null || uvs == null || normals.Length != count || uvs.Length != count)
            {
                for (int i = 0; i < count; i++)
                {
                    tangents[i] = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                }
                return tangents;
            }
            var along = new Vector3[count];
            var up = new Vector3[count];
            foreach (int[] triangles in submeshTriangles)
            {
                if (triangles == null)
                {
                    continue;
                }
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    Vector3 e1 = vertices[b] - vertices[a];
                    Vector3 e2 = vertices[c] - vertices[a];
                    Vector2 d1 = uvs[b] - uvs[a];
                    Vector2 d2 = uvs[c] - uvs[a];
                    float determinant = d1.x * d2.y - d2.x * d1.y;
                    if (Mathf.Abs(determinant) < 1e-12f)
                    {
                        continue;   // no area in UV: it says nothing about which way U runs
                    }
                    float r = 1.0f / determinant;
                    Vector3 sdir = (e1 * d2.y - e2 * d1.y) * r;
                    Vector3 tdir = (e2 * d1.x - e1 * d2.x) * r;
                    along[a] += sdir;
                    along[b] += sdir;
                    along[c] += sdir;
                    up[a] += tdir;
                    up[b] += tdir;
                    up[c] += tdir;
                }
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 n = normals[i];
                Vector3 t = along[i] - n * Vector3.Dot(n, along[i]);
                if (t.sqrMagnitude < 1e-12f)
                {
                    // Nothing to go on (no UVs across it): any direction square to the normal.
                    t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right);
                    if (t.sqrMagnitude < 1e-12f)
                    {
                        t = Vector3.right;
                    }
                }
                t.Normalize();
                float w = Vector3.Dot(Vector3.Cross(n, t), up[i]) < 0.0f ? -1.0f : 1.0f;
                tangents[i] = new Vector4(t.x, t.y, t.z, w);
            }
            return tangents;
        }
    }
}
