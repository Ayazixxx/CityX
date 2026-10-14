using System.Collections.Generic;
using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The chunk's tangents are worked out on a worker now (MeshTangents) instead of by Mesh.RecalculateTangents on the main thread (2026-10-14:
    /// half of each 17 ms stall when an AK sprayed a row of windows). The facade shader's normal map reads them, so they have to agree with
    /// Unity's own on the flat faces a city is made of: walls facing every way, floors, ceilings, with UVs running either way.
    /// </summary>
    public sealed class MeshTangentsTests
    {
        /// <summary>A box of quads, each face with its own UVs in metres (as the city's are), some of them mirrored.</summary>
        private static Mesh Box(out Vector3[] vertices, out Vector3[] normals, out Vector2[] uvs, out int[] triangles)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, bool mirror)
            {
                int start = v.Count;
                v.AddRange(new[] { a, b, c, d });
                n.AddRange(new[] { normal, normal, normal, normal });
                float w = Vector3.Distance(a, b), h = Vector3.Distance(b, c);
                uv.AddRange(mirror
                    ? new[] { new Vector2(w, 0), new Vector2(0, 0), new Vector2(0, h), new Vector2(w, h) }
                    : new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) });
                t.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Quad(new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 2, 0), new Vector3(0, 2, 0), Vector3.back, false);
            Quad(new Vector3(3, 0, 4), new Vector3(0, 0, 4), new Vector3(0, 2, 4), new Vector3(3, 2, 4), Vector3.forward, true);
            Quad(new Vector3(0, 0, 4), new Vector3(0, 0, 0), new Vector3(0, 2, 0), new Vector3(0, 2, 4), Vector3.left, false);
            Quad(new Vector3(3, 0, 0), new Vector3(3, 0, 4), new Vector3(3, 2, 4), new Vector3(3, 2, 0), Vector3.right, true);
            Quad(new Vector3(0, 2, 0), new Vector3(3, 2, 0), new Vector3(3, 2, 4), new Vector3(0, 2, 4), Vector3.up, false);
            Quad(new Vector3(0, 0, 4), new Vector3(3, 0, 4), new Vector3(3, 0, 0), new Vector3(0, 0, 0), Vector3.down, true);
            vertices = v.ToArray();
            normals = n.ToArray();
            uvs = uv.ToArray();
            triangles = t.ToArray();
            var mesh = new Mesh { vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            return mesh;
        }

        [Test]
        public void TheWorkersTangentsAgreeWithUnitysOnFlatFaces()
        {
            Mesh mesh = Box(out Vector3[] vertices, out Vector3[] normals, out Vector2[] uvs, out int[] triangles);
            try
            {
                mesh.RecalculateTangents();
                Vector4[] unity = mesh.tangents;
                Vector4[] ours = MeshTangents.Compute(vertices, normals, uvs, new[] { triangles });
                Assert.AreEqual(unity.Length, ours.Length);
                for (int i = 0; i < ours.Length; i++)
                {
                    float angle = Vector3.Angle(unity[i], ours[i]);
                    Assert.Less(angle, 1.0f, $"vertex {i}: tangent {((Vector3)ours[i]).ToString("0.00")}, Unity's {((Vector3)unity[i]).ToString("0.00")}");
                    Assert.AreEqual(unity[i].w, ours[i].w, $"vertex {i}: the bitangent runs the other way (a mirrored face's normal map would light inside out)");
                    Assert.AreEqual(0.0f, Vector3.Dot(ours[i], normals[i]), 1e-4f, $"vertex {i}: the tangent is not square to the normal");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AFaceWithNoUVsStillGetsATangentSquareToItsNormal()
        {
            var vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            var normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            var uvs = new[] { Vector2.zero, Vector2.zero, Vector2.zero };
            Vector4[] ours = MeshTangents.Compute(vertices, normals, uvs, new[] { new[] { 0, 2, 1 } });
            foreach (Vector4 tangent in ours)
            {
                Assert.AreEqual(1.0f, ((Vector3)tangent).magnitude, 1e-4f);
                Assert.AreEqual(0.0f, Vector3.Dot(tangent, Vector3.back), 1e-4f);
            }
        }
    }
}
