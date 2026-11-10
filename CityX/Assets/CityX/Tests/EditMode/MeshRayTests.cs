using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The ray that finds a body being painted as it idles (MeshRay; owner, 2026-11-10: "make it so you can draw on your character when its not tposed").</summary>
    public sealed class MeshRayTests
    {
        // Two squares' worth of triangle facing -Z (the side a camera at -Z sees), at z 1 and z 2.
        private static readonly Vector3[] Vertices =
        {
            new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 1.0f, 1.0f), new Vector3(1.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, 2.0f), new Vector3(0.0f, 1.0f, 2.0f), new Vector3(1.0f, 0.0f, 2.0f),
        };

        private static readonly int[] FarThenNear = { 3, 4, 5, 0, 1, 2 };

        [Test]
        public void ItFindsTheNearestFaceAndWhereOnItTheRayLanded()
        {
            bool hit = MeshRay.Nearest(Vertices, FarThenNear, new Vector3(0.25f, 0.5f, -3.0f), Vector3.forward, out float distance, out int triangle, out float u, out float v);
            Assert.IsTrue(hit);
            Assert.AreEqual(1, triangle, "not the nearer of the two");
            Assert.AreEqual(4.0f, distance, 1e-4f);
            Assert.AreEqual(0.5f, u, 1e-4f, "b is the vertex up");
            Assert.AreEqual(0.25f, v, 1e-4f, "c is the vertex across");
        }

        [Test]
        public void ItAgreesWithPhysicsAboutWhichSideIsTheFront()
        {
            var mesh = new Mesh { vertices = new[] { Vertices[0], Vertices[1], Vertices[2] }, triangles = new[] { 0, 1, 2 } };
            var holder = new GameObject("face");
            try
            {
                var collider = holder.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                Physics.SyncTransforms();
                var fromFront = new Ray(new Vector3(0.25f, 0.25f, -3.0f), Vector3.forward);
                var fromBehind = new Ray(new Vector3(0.25f, 0.25f, 5.0f), Vector3.back);
                Assert.AreEqual(collider.Raycast(fromFront, out _, 50.0f), MeshRay.Nearest(mesh.vertices, mesh.triangles, fromFront.origin, fromFront.direction, out _, out _, out _, out _));
                Assert.AreEqual(collider.Raycast(fromBehind, out _, 50.0f), MeshRay.Nearest(mesh.vertices, mesh.triangles, fromBehind.origin, fromBehind.direction, out _, out _, out _, out _));
                Assert.IsTrue(MeshRay.Nearest(mesh.vertices, mesh.triangles, fromFront.origin, fromFront.direction, out _, out _, out _, out _), "the front was missed");
            }
            finally
            {
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TheBoxesFindWhatLookingAtEveryTriangleFinds()
        {
            // A wavy sheet of far more triangles than one box holds, with rays from several sides.
            const int Side = 40;
            var vertices = new Vector3[(Side + 1) * (Side + 1)];
            for (int y = 0; y <= Side; y++)
            {
                for (int x = 0; x <= Side; x++)
                {
                    vertices[y * (Side + 1) + x] = new Vector3(x * 0.1f, y * 0.1f, Mathf.Sin(x * 0.7f) * 0.3f);
                }
            }
            var triangles = new int[Side * Side * 6];
            for (int y = 0, at = 0; y < Side; y++)
            {
                for (int x = 0; x < Side; x++, at += 6)
                {
                    int corner = y * (Side + 1) + x;
                    triangles[at] = corner;
                    triangles[at + 1] = corner + Side + 1;
                    triangles[at + 2] = corner + 1;
                    triangles[at + 3] = corner + 1;
                    triangles[at + 4] = corner + Side + 1;
                    triangles[at + 5] = corner + Side + 2;
                }
            }
            float[] boxes = MeshRay.NewBoxes(triangles.Length);
            MeshRay.FitBoxes(vertices, triangles, boxes);
            var random = new System.Random(7);
            int hits = 0;
            for (int i = 0; i < 300; i++)
            {
                var origin = new Vector3((float)random.NextDouble() * 5.0f - 0.5f, (float)random.NextDouble() * 5.0f - 0.5f, -3.0f);
                Vector3 direction = i % 5 == 0 ? Vector3.forward : new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f, 2.0f).normalized;
                bool every = MeshRay.Nearest(vertices, triangles, origin, direction, out float distance, out int triangle, out _, out _);
                bool boxed = MeshRay.Nearest(vertices, triangles, boxes, origin, direction, out float boxedDistance, out int boxedTriangle, out _, out _);
                Assert.AreEqual(every, boxed, $"ray {i}");
                if (every)
                {
                    hits++;
                    Assert.AreEqual(triangle, boxedTriangle, $"ray {i}");
                    Assert.AreEqual(distance, boxedDistance, 1e-4f, $"ray {i}");
                }
            }
            Assert.Greater(hits, 100, "the rays hardly touched the sheet, so this shows nothing");
        }

        [Test]
        public void ItMissesBesideTheFaceAndBehindTheRay()
        {
            Assert.IsFalse(MeshRay.Nearest(Vertices, FarThenNear, new Vector3(0.9f, 0.9f, -3.0f), Vector3.forward, out _, out _, out _, out _), "past the slanted edge");
            Assert.IsFalse(MeshRay.Nearest(Vertices, FarThenNear, new Vector3(0.25f, 0.5f, 3.0f), Vector3.forward, out _, out _, out _, out _), "behind where the ray starts");
        }
    }
}
