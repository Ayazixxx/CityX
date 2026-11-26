using System.Collections.Generic;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The body drawn without what the worn garments hide (BodyCover): two garments worn together (a shirt and jeans) both cut theirs, the same
    /// set cuts the same mesh in any order, and a mask made for another body cuts nothing. The cut keeps only the points it draws (a crowd of
    /// dressed bodies was bending all of their hidden skin, docs/CROWD_TEST.md), each as it was.
    /// </summary>
    public sealed class BodyCoverTests
    {
        private const int Triangles = 16;

        private static Mesh Body()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int t = 0; t < Triangles; t++)
            {
                vertices.Add(new Vector3(t, 0.0f, 0.0f));
                vertices.Add(new Vector3(t, 1.0f, 0.0f));
                vertices.Add(new Vector3(t + 1, 0.0f, 0.0f));
                triangles.AddRange(new[] { t * 3, t * 3 + 1, t * 3 + 2 });
            }
            var mesh = new Mesh { name = "TestBody" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private static byte[] Mask(params int[] hidden)
        {
            var mask = new byte[(Triangles + 7) / 8];
            foreach (int t in hidden)
            {
                mask[t / 8] |= (byte)(1 << (t % 8));
            }
            return mask;
        }

        [Test]
        public void TwoGarmentsWornTogetherBothHideWhatTheyCover()
        {
            Mesh body = Body();
            byte[] shirt = Mask(0, 1, 2), jeans = Mask(10, 11);
            Mesh both = BodyCover.Cut(body, new List<(byte[], int)> { (shirt, Triangles), (jeans, Triangles) });
            Assert.AreEqual((Triangles - 5) * 3, both.triangles.Length, "the body under one of the two is still drawn");
            Assert.AreSame(both, BodyCover.Cut(body, new List<(byte[], int)> { (jeans, Triangles), (shirt, Triangles) }),
                "the same two garments, put on the other way round, cut another mesh");
            Mesh shirtOnly = BodyCover.Cut(body, new List<(byte[], int)> { (shirt, Triangles) });
            Assert.AreEqual((Triangles - 3) * 3, shirtOnly.triangles.Length, "with the jeans off, their part of the body is not given back");
            Assert.AreSame(body, BodyCover.Cut(body, new List<(byte[], int)>()), "with nothing worn the body is not whole");
        }

        [Test]
        public void TheCutKeepsOnlyThePointsItDrawsEachAsItWas()
        {
            Mesh body = Body();
            var uvs = new List<Vector2>();
            var weights = new BoneWeight[body.vertexCount];
            for (int v = 0; v < body.vertexCount; v++)
            {
                uvs.Add(new Vector2(v * 0.01f, 0.5f));
                weights[v] = new BoneWeight { boneIndex0 = v % 3, weight0 = 0.75f, boneIndex1 = 3, weight1 = 0.25f };
            }
            body.SetUVs(0, uvs);
            body.boneWeights = weights;
            body.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity };
            body.RecalculateNormals();
            byte[] shirt = Mask(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
            Mesh cut = BodyCover.Cut(body, new List<(byte[], int)> { (shirt, Triangles) });

            Assert.AreEqual((Triangles - 10) * 3, cut.vertexCount, "the cut still bends the points no shown triangle uses");
            int[] before = body.triangles, after = cut.triangles;
            Vector3[] wasAt = body.vertices, nowAt = cut.vertices;
            Vector2[] wasUv = body.uv, nowUv = cut.uv;
            BoneWeight[] wasWeights = body.boneWeights, nowWeights = cut.boneWeights;
            for (int corner = 0; corner < after.Length; corner++)
            {
                int was = before[30 + corner], now = after[corner];   // the first ten triangles hidden: the rest in order
                Assert.AreEqual(wasAt[was], nowAt[now], "a shown point moved");
                Assert.AreEqual(wasUv[was], nowUv[now], "a shown point lost its texture place");
                Assert.AreEqual(wasWeights[was].boneIndex0, nowWeights[now].boneIndex0, "a shown point lost its bones");
                Assert.AreEqual(wasWeights[was].weight0, nowWeights[now].weight0, 1e-4f, "a shown point lost its bones");
            }
            Assert.AreEqual(body.bindposes.Length, cut.bindposes.Length, "the cut lost its bind poses");
        }

        [Test]
        public void AMaskMadeForAnotherBodyCutsNothing()
        {
            Mesh body = Body();
            byte[] stale = Mask(3), jeans = Mask(10);
            Mesh cut = BodyCover.Cut(body, new List<(byte[], int)> { (stale, Triangles + 4), (jeans, Triangles) });
            Assert.AreEqual((Triangles - 1) * 3, cut.triangles.Length, "a stale mask cut the body, or the good one did not");
        }
    }
}
