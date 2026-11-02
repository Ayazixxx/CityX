using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The holes drawn ahead of their rebuilt block (InstantBulletHoles): the real decal's size, on the glass, facing out, and let go block by block.</summary>
    public sealed class InstantBulletHolesTests
    {
        private InstantBulletHoles _holes;

        [SetUp]
        public void SetUp()
        {
            _holes = new InstantBulletHoles();
        }

        [TearDown]
        public void TearDown()
        {
            _holes.Dispose();
        }

        [Test]
        public void AHoleIsTheRealDecalsSizeAtTheHitAndFacesOut()
        {
            var point = new Vector3(10.0f, 5.0f, -3.0f);
            foreach (Vector3 outward in new[] { Vector3.forward, Vector3.right, new Vector3(1.0f, 0.0f, 1.0f).normalized })
            {
                _holes.Clear();
                _holes.Add(Vector2Int.zero, point, outward);
                Mesh mesh = _holes.Mesh;
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.Less(Vector3.Distance(mesh.bounds.center, point), 0.01f, "not at the hit");
                Vector3[] v = mesh.vertices;
                Assert.AreEqual(CityMeshBuilder.BulletHoleDecalWidth, Vector3.Distance(v[0], v[3]), 1e-3f, "not the real decal's width");
                Assert.AreEqual(CityMeshBuilder.BulletHoleDecalHeight, Vector3.Distance(v[0], v[1]), 1e-3f, "not the real decal's height");
                Assert.Greater(Vector3.Dot(v[0] - point, outward), 0.0f, "behind the glass");
                int[] t = mesh.triangles;
                // A triangle faces the way the cross of its edges points (a mesh in Unity winds clockwise seen from its front).
                Vector3 facing = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
                Assert.Greater(Vector3.Dot(facing, outward), 0.0f, "the hole faces into the building: it would not be drawn from the street");
            }
        }

        [Test]
        public void HolesAreLetGoBlockByBlock()
        {
            var one = new Vector2Int(1, 1);
            var two = new Vector2Int(2, 2);
            _holes.Add(one, Vector3.zero, Vector3.forward);
            _holes.Add(two, Vector3.right, Vector3.forward);
            _holes.Add(one, Vector3.up, Vector3.forward);
            Assert.AreEqual(3, _holes.Count);
            _holes.Forget(chunk => chunk == one);
            Assert.AreEqual(1, _holes.Count);
            Assert.AreEqual(4, _holes.Mesh.vertexCount, "the mesh still draws what was let go");
            _holes.Forget(chunk => false);
            Assert.AreEqual(1, _holes.Count, "a block still on its way lost its hole");
            _holes.Clear();
            Assert.AreEqual(0, _holes.Mesh.vertexCount);
        }
    }
}
