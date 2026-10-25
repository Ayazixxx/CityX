using System.Collections.Generic;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The body drawn without what the worn garments hide (BodyCover): two garments worn together (a shirt and jeans) both cut theirs, the same
    /// set cuts the same mesh in any order, and a mask made for another body cuts nothing.
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
        public void AMaskMadeForAnotherBodyCutsNothing()
        {
            Mesh body = Body();
            byte[] stale = Mask(3), jeans = Mask(10);
            Mesh cut = BodyCover.Cut(body, new List<(byte[], int)> { (stale, Triangles + 4), (jeans, Triangles) });
            Assert.AreEqual((Triangles - 1) * 3, cut.triangles.Length, "a stale mask cut the body, or the good one did not");
        }
    }
}
