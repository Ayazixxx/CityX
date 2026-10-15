using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Tests
{
    /// <summary>
    /// A chunk's main mesh is laid out on a worker and copied into its Mesh in a few blocks (ChunkMeshData, 2026-10-14), where the classic
    /// Mesh API built it on the main thread in 10-20 ms for a block of towers. It has to be the same mesh: a real chunk is built both ways --
    /// the classic way exactly as CityStreamer did it until then -- and every vertex, index, submesh and bound is compared, for a block of
    /// towers (32-bit indices), its distant version and a street (16-bit). And the point of it: the main thread's part is a fraction of what
    /// it was.
    /// </summary>
    public sealed class ChunkMeshDataTests
    {
        private const int Seed = 61928374;

        private CityScale _scaleBefore;
        private readonly List<Mesh> _meshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own districts, where the blocks of towers are
        }

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
            foreach (Mesh mesh in _meshes)
            {
                Object.DestroyImmediate(mesh);
            }
            _meshes.Clear();
        }

        // ---- the chunks ----------------------------------------------------------------------------------------------------------------

        /// <summary>A real chunk's five main buffers -- ground, pavement, structures, dark glass, lit glass -- built as CityStreamer.GenerateChunk builds them.</summary>
        private static MeshBuffer[] Buffers(CityWorld world, Vector2Int chunk, bool far)
        {
            var b = new MeshBuffer[5];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = new MeshBuffer();
            }
            var origin = new Vector3(chunk.x * CityLayout.ChunkSize, 0.0f, chunk.y * CityLayout.ChunkSize);
            CityMeshBuilder.BuildChunk(world, chunk.x, chunk.y, origin, b[0], b[1], b[2], b[4], b[3],
                far ? null : new MeshBuffer(), null, far ? null : new List<CityMeshBuilder.CrackedOpening>(), far ? null : new MeshBuffer(),
                new MeshBuffer(), far ? null : new MeshBuffer(), far ? null : new MeshBuffer(),
                includeSurface: true, far: far, roomGlows: far ? null : new List<CityMeshBuilder.RoomGlow>());
            return b;
        }

        /// <summary>The groups CityStreamer draws them in: ground, pavement and walls under one material, then each glass.</summary>
        private static MeshBuffer[][] Groups(MeshBuffer[] b)
        {
            return new[] { new[] { b[0], b[1], b[2] }, new[] { b[3] }, new[] { b[4] } };
        }

        /// <summary>Round the spawn: the chunk that builds the tallest building, and a chunk that builds none (a street, a park).</summary>
        private static void PickChunks(CityWorld world, out Vector2Int towers, out Vector2Int street)
        {
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz), "the world has no spawn point");
            int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
            towers = new Vector2Int(cx0, cy0);
            street = new Vector2Int(int.MinValue, int.MinValue);
            float tallest = -1.0f;
            for (int dy = -6; dy <= 6; dy++)
            {
                for (int dx = -6; dx <= 6; dx++)
                {
                    List<int> owned = world.BuildingsOwnedBy(cx0 + dx, cy0 + dy);
                    if (owned.Count == 0 && street.x == int.MinValue)
                    {
                        street = new Vector2Int(cx0 + dx, cy0 + dy);
                    }
                    foreach (int id in owned)
                    {
                        float height = (float)world.Building(id).Params.Height;
                        if (height > tallest)
                        {
                            tallest = height;
                            towers = new Vector2Int(cx0 + dx, cy0 + dy);
                        }
                    }
                }
            }
            Assert.AreNotEqual(int.MinValue, street.x, "every chunk round the spawn builds a building: no street chunk to compare");
        }

        // ---- the two ways ----------------------------------------------------------------------------------------------------------------

        /// <summary>What the worker made until 2026-10-14: the five buffers combined into flat arrays, ground, pavement and walls as one submesh.</summary>
        private sealed class ClassicArrays
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public Color32[] Colors;
            public int[][] SubmeshTriangles;
            public Vector4[] Tangents;
        }

        private static ClassicArrays Classic(MeshBuffer[] buffers)
        {
            int vertexCount = 0;
            foreach (MeshBuffer buffer in buffers)
            {
                vertexCount += buffer.Vertices.Count;
            }
            var a = new ClassicArrays
            {
                Vertices = new Vector3[vertexCount], Normals = new Vector3[vertexCount], Uvs = new Vector2[vertexCount],
                Colors = new Color32[vertexCount], SubmeshTriangles = new int[buffers.Length][],
            };
            int written = 0;
            for (int b = 0; b < buffers.Length; b++)
            {
                MeshBuffer buffer = buffers[b];
                buffer.Vertices.CopyTo(a.Vertices, written);
                buffer.Normals.CopyTo(a.Normals, written);
                buffer.Uvs.CopyTo(a.Uvs, written);
                buffer.Colors.CopyTo(a.Colors, written);
                var shifted = new int[buffer.Triangles.Count];
                for (int i = 0; i < shifted.Length; i++)
                {
                    shifted[i] = buffer.Triangles[i] + written;
                }
                a.SubmeshTriangles[b] = shifted;
                written += buffer.Vertices.Count;
            }
            int[][] s = a.SubmeshTriangles;
            var everything = new int[s[0].Length + s[1].Length + s[2].Length];
            s[0].CopyTo(everything, 0);
            s[1].CopyTo(everything, s[0].Length);
            s[2].CopyTo(everything, s[0].Length + s[1].Length);
            s[0] = everything;
            s[1] = new int[0];
            s[2] = new int[0];
            a.Tangents = MeshTangents.Compute(a.Vertices, a.Normals, a.Uvs, s);
            return a;
        }

        /// <summary>The main thread's part until 2026-10-14 (CityStreamer.AssembleChunk): the classic Mesh API.</summary>
        private Mesh ClassicMesh(ClassicArrays a)
        {
            var mesh = new Mesh();
            _meshes.Add(mesh);
            mesh.indexFormat = a.Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(a.Vertices);
            mesh.SetNormals(a.Normals);
            mesh.SetUVs(0, a.Uvs);
            mesh.SetColors(a.Colors);
            int used = 0;
            foreach (int[] triangles in a.SubmeshTriangles)
            {
                if (triangles.Length > 0) used++;
            }
            mesh.subMeshCount = Mathf.Max(1, used);
            int slot = 0;
            foreach (int[] triangles in a.SubmeshTriangles)
            {
                if (triangles.Length == 0) continue;
                mesh.SetTriangles(triangles, slot);
                slot++;
            }
            mesh.SetTangents(a.Tangents);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh OurMesh(ChunkMeshData data)
        {
            var mesh = new Mesh();
            _meshes.Add(mesh);
            data.ApplyTo(mesh);
            return mesh;
        }

        // ---- comparing ----------------------------------------------------------------------------------------------------------------

        private static readonly VertexAttribute[] Attributes =
            { VertexAttribute.Position, VertexAttribute.Normal, VertexAttribute.Tangent, VertexAttribute.Color, VertexAttribute.TexCoord0 };

        private static void AssertSameMesh(Mesh classic, Mesh ours, string what)
        {
            Assert.AreEqual(classic.vertexCount, ours.vertexCount, $"{what}: vertex count");
            Assert.AreEqual(classic.indexFormat, ours.indexFormat, $"{what}: index format");
            Assert.AreEqual(classic.subMeshCount, ours.subMeshCount, $"{what}: submesh count");
            Assert.AreEqual(classic.vertexAttributeCount, ours.vertexAttributeCount, $"{what}: vertex attributes");
            foreach (VertexAttribute attribute in Attributes)
            {
                Assert.IsTrue(ours.HasVertexAttribute(attribute), $"{what}: no {attribute}");
                Assert.AreEqual(classic.GetVertexAttributeFormat(attribute), ours.GetVertexAttributeFormat(attribute), $"{what}: {attribute} format");
                Assert.AreEqual(classic.GetVertexAttributeDimension(attribute), ours.GetVertexAttributeDimension(attribute), $"{what}: {attribute} dimension");
            }

            Vector3[] p0 = classic.vertices, p1 = ours.vertices;
            Vector3[] n0 = classic.normals, n1 = ours.normals;
            Vector4[] t0 = classic.tangents, t1 = ours.tangents;
            Vector2[] u0 = classic.uv, u1 = ours.uv;
            Color32[] c0 = classic.colors32, c1 = ours.colors32;
            for (int i = 0; i < p0.Length; i++)
            {
                if (!p0[i].Equals(p1[i])) Assert.Fail($"{what}: vertex {i} is at {p1[i]}, was {p0[i]}");
                if (!n0[i].Equals(n1[i])) Assert.Fail($"{what}: vertex {i}'s normal is {n1[i]}, was {n0[i]}");
                if (!t0[i].Equals(t1[i])) Assert.Fail($"{what}: vertex {i}'s tangent is {t1[i]}, was {t0[i]}");
                if (!u0[i].Equals(u1[i])) Assert.Fail($"{what}: vertex {i}'s UV is {u1[i]}, was {u0[i]}");
                if (c0[i].r != c1[i].r || c0[i].g != c1[i].g || c0[i].b != c1[i].b || c0[i].a != c1[i].a)
                {
                    Assert.Fail($"{what}: vertex {i}'s colour is {c1[i]}, was {c0[i]}");
                }
            }

            for (int s = 0; s < classic.subMeshCount; s++)
            {
                SubMeshDescriptor a = classic.GetSubMesh(s), b = ours.GetSubMesh(s);
                Assert.AreEqual(a.topology, b.topology, $"{what}: submesh {s} topology");
                Assert.AreEqual(a.indexStart, b.indexStart, $"{what}: submesh {s} index start");
                Assert.AreEqual(a.indexCount, b.indexCount, $"{what}: submesh {s} index count");
                Assert.AreEqual(a.baseVertex, b.baseVertex, $"{what}: submesh {s} base vertex");
                Assert.AreEqual(a.firstVertex, b.firstVertex, $"{what}: submesh {s} first vertex");
                Assert.AreEqual(a.vertexCount, b.vertexCount, $"{what}: submesh {s} vertex count");
                AssertSameBounds(a.bounds, b.bounds, $"{what}: submesh {s} bounds");
                int[] i0 = classic.GetIndices(s), i1 = ours.GetIndices(s);
                Assert.AreEqual(i0.Length, i1.Length, $"{what}: submesh {s} indices");
                for (int i = 0; i < i0.Length; i++)
                {
                    if (i0[i] != i1[i]) Assert.Fail($"{what}: submesh {s} index {i} is {i1[i]}, was {i0[i]}");
                }
            }
            AssertSameBounds(classic.bounds, ours.bounds, $"{what}: bounds");
        }

        /// <summary>To a float's rounding: Unity keeps bounds as a centre and a half size, and works the whole mesh's out from the submeshes'.</summary>
        private static void AssertSameBounds(Bounds a, Bounds b, string what)
        {
            const float Rounding = 1e-3f;
            Assert.AreEqual(a.center.x, b.center.x, Rounding, $"{what}: centre {b.center} vs {a.center}");
            Assert.AreEqual(a.center.y, b.center.y, Rounding, $"{what}: centre {b.center} vs {a.center}");
            Assert.AreEqual(a.center.z, b.center.z, Rounding, $"{what}: centre {b.center} vs {a.center}");
            Assert.AreEqual(a.extents.x, b.extents.x, Rounding, $"{what}: extents {b.extents} vs {a.extents}");
            Assert.AreEqual(a.extents.y, b.extents.y, Rounding, $"{what}: extents {b.extents} vs {a.extents}");
            Assert.AreEqual(a.extents.z, b.extents.z, Rounding, $"{what}: extents {b.extents} vs {a.extents}");
        }

        // ---- the tests ----------------------------------------------------------------------------------------------------------------

        [Test]
        public void ABlockOfTowersIsTheSameMeshBothWays()
        {
            var world = new CityWorld(Seed);
            PickChunks(world, out Vector2Int towers, out _);
            foreach (bool far in new[] { false, true })
            {
                MeshBuffer[] buffers = Buffers(world, towers, far);
                ChunkMeshData data = ChunkMeshData.Combine(Groups(buffers));
                Mesh classic = ClassicMesh(Classic(buffers));
                Mesh ours = OurMesh(data);
                string what = $"chunk {towers}{(far ? " (far)" : "")}";
                AssertSameMesh(classic, ours, what);
                if (!far)
                {
                    Assert.Greater(ours.vertexCount, ChunkMeshData.MaxVerticesFor16BitIndices,
                        $"{what}, the tallest building's, has only {ours.vertexCount} vertices: the 32-bit indices are not being checked");
                }
                // Each submesh knows its group, so the streamer draws it with that group's material: the walls, then whichever glass there is.
                var expected = new List<int>();
                if (!buffers[0].IsEmpty || !buffers[1].IsEmpty || !buffers[2].IsEmpty) expected.Add(0);
                if (!buffers[3].IsEmpty) expected.Add(1);
                if (!buffers[4].IsEmpty) expected.Add(2);
                Assert.Greater(expected.Count, 1, $"{what}: a block of towers with no glass");
                CollectionAssert.AreEqual(expected, data.SubMeshGroups, $"{what}: the submeshes' groups (their materials)");
            }
        }

        [Test]
        public void AStreetIsTheSameMeshBothWays()
        {
            var world = new CityWorld(Seed);
            PickChunks(world, out _, out Vector2Int street);
            MeshBuffer[] buffers = Buffers(world, street, false);
            Mesh ours = OurMesh(ChunkMeshData.Combine(Groups(buffers)));
            Assert.Greater(ours.vertexCount, 0, $"chunk {street} built nothing");
            Assert.AreEqual(IndexFormat.UInt16, ours.indexFormat, $"chunk {street}, a street, has {ours.vertexCount} vertices");
            AssertSameMesh(ClassicMesh(Classic(buffers)), ours, $"chunk {street}");
        }

        [Test]
        public void AGroupWithNothingInItGetsNoSubmeshAndAMeshWithNothingAtAllGetsOne()
        {
            var walls = new MeshBuffer();
            walls.AddBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(4.0f, 3.0f, 5.0f));
            var litGlass = new MeshBuffer();
            litGlass.AddBox(new Vector3(1.0f, 1.0f, -0.1f), new Vector3(2.0f, 2.0f, 0.0f));
            var nothing = new MeshBuffer();
            MeshBuffer[][] groups = { new[] { nothing, walls }, new[] { new MeshBuffer() }, new[] { litGlass } };

            ChunkMeshData data = ChunkMeshData.Combine(groups);
            CollectionAssert.AreEqual(new[] { 0, 2 }, data.SubMeshGroups, "the dark glass, with nothing in it, got a submesh");
            Mesh ours = OurMesh(data);
            Assert.AreEqual(2, ours.subMeshCount);
            Assert.AreEqual(walls.Vertices.Count, ours.GetSubMesh(1).firstVertex, "the glass's vertices do not follow the walls'");
            AssertSameMesh(ClassicMesh(Classic(new[] { nothing, walls, new MeshBuffer(), new MeshBuffer(), litGlass })), ours, "two boxes");

            ChunkMeshData empty = ChunkMeshData.Combine(new[] { new[] { new MeshBuffer() }, new[] { new MeshBuffer() } });
            Assert.AreEqual(0, empty.SubMeshGroups.Length);
            Mesh none = OurMesh(empty);
            Assert.AreEqual(0, none.vertexCount);
            Assert.AreEqual(1, none.subMeshCount, "a chunk with nothing at all still has its one (empty) submesh, for its one material");
            Assert.AreEqual(0, none.GetIndexCount(0));
        }

        [Test]
        public void PuttingABlockOfTowersTogetherOnTheMainThreadTakesAFractionOfTheClassicWay()
        {
            var world = new CityWorld(Seed);
            PickChunks(world, out Vector2Int towers, out _);
            MeshBuffer[] buffers = Buffers(world, towers, false);
            ClassicArrays arrays = Classic(buffers);
            ChunkMeshData data = ChunkMeshData.Combine(Groups(buffers));

            // The best of a few of each (a garbage collection or a busy core can land in any one), each after a collection.
            double classicMs = double.MaxValue, oursMs = double.MaxValue;
            for (int run = 0; run < 3; run++)
            {
                System.GC.Collect();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                ClassicMesh(arrays);
                classicMs = System.Math.Min(classicMs, clock.Elapsed.TotalMilliseconds);

                System.GC.Collect();
                clock.Restart();
                OurMesh(data);
                oursMs = System.Math.Min(oursMs, clock.Elapsed.TotalMilliseconds);
            }
            string report = $"chunk {towers}, {data.VertexCount} vertices, {data.IndexCount / 3} triangles: on the main thread {oursMs:0.00} ms, " +
                            $"the classic Mesh API {classicMs:0.00} ms";
            TestContext.WriteLine(report);
            Debug.Log("CityX chunk mesh: " + report);
            Assert.Less(oursMs, classicMs * 0.5, report);
        }
    }
}
