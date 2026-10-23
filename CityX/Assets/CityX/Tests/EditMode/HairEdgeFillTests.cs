using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A notch in a hairstyle's lower edge is filled with the hair itself (owner, 2026-10-23, of a gap on Hyde's left: "make the hair fill up
    /// that space make it blend well too"): the curl beside it is copied round into it, tucked just under the hair, and nothing of the
    /// original moves.
    /// </summary>
    public sealed class HairEdgeFillTests
    {
        private static Vector3 At(float turn, float height, float radius)
        {
            float t = turn * Mathf.Deg2Rad, h = height * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(h) * Mathf.Sin(t), Mathf.Sin(h), Mathf.Cos(h) * Mathf.Cos(t)) * radius;
        }

        private static float Turn(Vector3 p) => Mathf.Repeat(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg, 360.0f);

        private static float Height(Vector3 p) => Mathf.Asin(p.y / p.magnitude) * Mathf.Rad2Deg;

        /// <summary>A strip of hair one triangle pair per degree from 240.5 to 280.5 degrees round, -30 to 20 degrees up, 0.4 m out.</summary>
        private static void Strip(List<Vector3> points, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            for (float turn = 240.5f; turn < 280.0f; turn += 1.0f)
            {
                int i = points.Count;
                foreach (Vector3 p in new[] { At(turn, -30.0f, 0.4f), At(turn + 1.0f, -30.0f, 0.4f), At(turn + 1.0f, 20.0f, 0.4f), At(turn, 20.0f, 0.4f) })
                {
                    points.Add(p);
                    normals.Add(p.normalized);
                    uvs.Add(new Vector2(turn, 0.0f));
                }
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        [Test]
        public void TheCurlIsCopiedRoundIntoTheNotchJustUnderTheHairAndTheOriginalStays()
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Strip(points, normals, uvs, triangles);
            var before = new List<Vector3>(points);
            int originalTriangles = triangles.Count;
            int copied = HairEdgeFill.CopyCurl(points, normals, uvs, triangles, 0.35f, 250.0f, 260.0f, 25.0f, 8.0f);
            Assert.AreEqual(18, copied, "the nine whole degrees of the strip between 250 and 260 are copied, two triangles each");
            Assert.AreEqual(originalTriangles + copied * 3, triangles.Count);
            for (int i = 0; i < before.Count; i++) Assert.AreEqual(before[i], points[i], "the original hair moved");
            Assert.AreEqual(points.Count, normals.Count);
            Assert.AreEqual(points.Count, uvs.Count);
            for (int i = before.Count; i < points.Count; i++)
            {
                Assert.That(Turn(points[i]), Is.InRange(257.9f, 268.1f), "a copied point is not turned into the notch");
                Assert.That(points[i].magnitude, Is.InRange(0.4f * HairEdgeFill.DeepTuck - 1e-4f, 0.4f * HairEdgeFill.Tuck + 1e-4f), "a copied point is not tucked under the hair");
                if (Height(points[i]) < -0.1f) Assert.AreEqual(0.4f * HairEdgeFill.Tuck, points[i].magnitude, 1e-4f, "the curl's tip is not just under the hair");
                if (Height(points[i]) > 19.9f) Assert.Less(points[i].magnitude, 0.4f * HairEdgeFill.Tuck - 0.01f, "the curl's root is not deep under the hair");
                Assert.That(Vector3.Dot(normals[i], points[i].normalized), Is.GreaterThan(0.999f), "a copied normal did not turn with its point");
            }
        }

        [Test]
        public void HairAboveTheTopOrInsideTheHeadIsNotCopied()
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Strip(points, normals, uvs, triangles);
            Assert.AreEqual(0, HairEdgeFill.CopyCurl(points, normals, uvs, triangles, 0.35f, 250.0f, 260.0f, 10.0f, 8.0f), "the strip reaches 20 degrees up: above a 10-degree top");
            Assert.AreEqual(0, HairEdgeFill.CopyCurl(points, normals, uvs, triangles, 0.45f, 250.0f, 260.0f, 25.0f, 8.0f), "the strip lies inside a 0.45 m head");
        }
    }
}
