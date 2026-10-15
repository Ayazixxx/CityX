using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The streamer builds chunk after chunk in storage it uses again -- the buffers a chunk's main mesh is built in, the arrays it is laid
    /// out in (ChunkArrayPool), the payload that carries it to the main thread -- instead of making it fresh and throwing it away (2026-10-14:
    /// streaming threw away ~60 MB a second while flying, a garbage collection every tenth of a second, each stopping every thread). What
    /// that must keep: a chunk is a pure function of its coordinates, so one built in storage that held other chunks is exactly the chunk
    /// fresh storage makes; and what it is for: streaming a block of towers in again leaves little garbage.
    /// </summary>
    public sealed class ChunkStorageReuseTests
    {
        private const int Seed = 70485213;

        private CityScale _scaleBefore;
        private readonly List<Object> _made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own districts, where the blocks of towers are
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null) Object.DestroyImmediate(made);
            }
            _made.Clear();
            CityScale.Active = _scaleBefore;
        }

        /// <summary>A streamer that builds the one chunk under where it is asked to (the synchronous path: the spawn pre-stream, captures).</summary>
        private CityStreamer OneChunkStreamer()
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 0;
            streamer.FullDetailRadiusChunks = 0;
            return streamer;
        }

        private static Vector3 Centre(Vector2Int chunk) => new Vector3((chunk.x + 0.5f) * CityLayout.ChunkSize, 0.0f, (chunk.y + 0.5f) * CityLayout.ChunkSize);

        /// <summary>Builds one chunk (dropping the one before: chunks are asked for three or more apart) and returns its main mesh.</summary>
        private static Mesh Build(CityStreamer streamer, Vector2Int chunk)
        {
            streamer.StreamAround(Centre(chunk), 1);
            Transform built = streamer.transform.Find($"Chunk {chunk.x},{chunk.y}");
            Assert.IsNotNull(built, $"chunk {chunk} was not built");
            return built.GetComponent<MeshFilter>().sharedMesh;
        }

        /// <summary>
        /// The biggest chunk (most vertices) of a few round the spawn: a block of towers in the game's own districts. The candidates are the chunks
        /// that OWN the tallest buildings within nine chunks (a building is built by the chunk its centre is in; a grid of every third chunk could
        /// miss every tower of a district of 150 m blocks, as on seed 83920571).
        /// </summary>
        private static Vector2Int BiggestNearSpawn(CityStreamer streamer, CityWorld world, out int vertices)
        {
            world.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
            var owners = new List<(float height, Vector2Int chunk)>();
            for (int dy = -9; dy <= 9; dy++)
            {
                for (int dx = -9; dx <= 9; dx++)
                {
                    foreach (int id in world.BuildingsOwnedBy(cx + dx, cy + dy))
                    {
                        BuildingSite site = world.Building(id);
                        owners.Add((site.Top - site.PodiumBase, new Vector2Int(site.OwnerChunkX, site.OwnerChunkY)));
                    }
                }
            }
            owners.Sort((a, b) => b.height.CompareTo(a.height));
            var candidates = new List<Vector2Int>();
            foreach ((float _, Vector2Int chunk) in owners)
            {
                if (!candidates.Contains(chunk))
                {
                    candidates.Add(chunk);
                }
                if (candidates.Count == 6)
                {
                    break;
                }
            }
            var biggest = new Vector2Int(cx, cy);
            vertices = -1;
            foreach (Vector2Int chunk in candidates)
            {
                int count = Build(streamer, chunk).vertexCount;
                if (count > vertices)
                {
                    vertices = count;
                    biggest = chunk;
                }
            }
            return biggest;
        }

        /// <summary>The chunk's main mesh as fresh storage makes it: new buffers, laid out exactly as the streamer lays it out.</summary>
        private Mesh Fresh(CityWorld world, Vector2Int chunk)
        {
            var b = new MeshBuffer[5];
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = new MeshBuffer();
            }
            var origin = new Vector3(chunk.x * CityLayout.ChunkSize, 0.0f, chunk.y * CityLayout.ChunkSize);
            CityMeshBuilder.BuildChunk(world, chunk.x, chunk.y, origin, b[0], b[1], b[2], b[4], b[3],
                new MeshBuffer(), new GlassDamage(), new List<CityMeshBuilder.CrackedOpening>(), new MeshBuffer(), new MeshBuffer(),
                new MeshBuffer(), new MeshBuffer(), includeSurface: true, far: false, roomGlows: new List<CityMeshBuilder.RoomGlow>());
            ChunkMeshData data = ChunkMeshData.Combine(new[] { new[] { b[0], b[1], b[2] }, new[] { b[3] }, new[] { b[4] } });
            var mesh = new Mesh();
            _made.Add(mesh);
            data.ApplyTo(mesh);
            data.Release();
            return mesh;
        }

        /// <summary>Exactly the same mesh: every value bit for bit (Vector3's == is approximate, so components are compared), every index.</summary>
        private static void AssertSame(Mesh expected, Mesh actual, string what)
        {
            Assert.AreEqual(expected.vertexCount, actual.vertexCount, $"{what}: vertices");
            Vector3[] p0 = expected.vertices, p1 = actual.vertices, n0 = expected.normals, n1 = actual.normals;
            Vector4[] t0 = expected.tangents, t1 = actual.tangents;
            Color32[] c0 = expected.colors32, c1 = actual.colors32;
            Vector2[] u0 = expected.uv, u1 = actual.uv;
            for (int i = 0; i < p0.Length; i++)
            {
                if (!p0[i].Equals(p1[i]) || !n0[i].Equals(n1[i]) || !t0[i].Equals(t1[i]) || !u0[i].Equals(u1[i])
                    || c0[i].r != c1[i].r || c0[i].g != c1[i].g || c0[i].b != c1[i].b || c0[i].a != c1[i].a)
                {
                    Assert.Fail($"{what}: vertex {i} differs: {p0[i]} / {p1[i]}, normal {n0[i]} / {n1[i]}, tangent {t0[i]} / {t1[i]}, " +
                                $"UV {u0[i]} / {u1[i]}, colour {c0[i]} / {c1[i]}");
                }
            }
            Assert.AreEqual(expected.subMeshCount, actual.subMeshCount, $"{what}: submeshes");
            for (int s = 0; s < expected.subMeshCount; s++)
            {
                int[] i0 = expected.GetIndices(s), i1 = actual.GetIndices(s);
                Assert.AreEqual(i0.Length, i1.Length, $"{what}: submesh {s} indices");
                for (int i = 0; i < i0.Length; i++)
                {
                    if (i0[i] != i1[i])
                    {
                        Assert.Fail($"{what}: submesh {s} index {i} is {i1[i]}, fresh storage makes {i0[i]}");
                    }
                }
            }
            Assert.AreEqual(expected.indexFormat, actual.indexFormat, $"{what}: index format");
            Assert.IsTrue(expected.bounds.center.Equals(actual.bounds.center) && expected.bounds.size.Equals(actual.bounds.size), $"{what}: bounds");
        }

        [Test]
        public void AChunkBuiltInStorageThatHeldOtherChunksIsTheChunkFreshStorageMakes()
        {
            CityStreamer streamer = OneChunkStreamer();
            var world = new CityWorld(Seed);
            Vector2Int towers = BiggestNearSpawn(streamer, world, out int towerVertices);
            Assert.Greater(towerVertices, 65000, "no block of towers near the spawn: this would not test 32-bit indices or big storage");

            // The storage has just held the biggest chunk round the spawn and dozens of others: a street, a smaller block, then the towers
            // again, each built where the one before was, must be exactly what new buffers and arrays make.
            world.Plan.TryFindSpawn(out double sx, out double sz);
            var street = new Vector2Int(PlanWorld.ChunkOf(sx), PlanWorld.ChunkOf(sz));
            foreach (Vector2Int chunk in new[] { street, towers + new Vector2Int(3, 0), towers, street })
            {
                Mesh built = Build(streamer, chunk);
                AssertSame(Fresh(world, chunk), built, $"chunk {chunk}");
            }
        }

        [Test]
        public void StreamingABlockOfTowersInAgainLeavesLittleGarbage()
        {
            CityStreamer streamer = OneChunkStreamer();
            var world = new CityWorld(Seed);
            Vector2Int towers = BiggestNearSpawn(streamer, world, out int vertices);
            Build(streamer, towers + new Vector2Int(3, 0));

            // What its main mesh alone is: positions, normals, tangents, colour+UV, indices. Made fresh, its buffers grew to that and more
            // and were thrown away, then it was copied into new arrays, also thrown away: a few times this, every time it streamed in.
            long meshBytes = vertices * (12L + 12L + 16L + 12L) + vertices * 6L;
            long garbage = long.MaxValue;
            for (int attempt = 0; attempt < 4 && garbage == long.MaxValue; attempt++)
            {
                Build(streamer, towers + new Vector2Int(-3, 0));   // drops it
                int collections = System.GC.CollectionCount(0);
                long used = System.GC.GetTotalMemory(false);
                Build(streamer, towers);
                long grew = System.GC.GetTotalMemory(false) - used;
                if (System.GC.CollectionCount(0) == collections)
                {
                    garbage = grew;   // otherwise a collection ran in the middle: the difference says nothing, try again
                }
            }
            Assert.AreNotEqual(long.MaxValue, garbage, "a garbage collection ran every time: nothing was measured");
            TestContext.WriteLine($"a block of towers ({vertices} vertices, main mesh {meshBytes / 1048576.0:0.0} MB) streamed in again left " +
                                  $"{garbage / 1048576.0:0.00} MB");
            Assert.Less(garbage, meshBytes / 4,
                $"streaming a block of towers in again left {garbage / 1048576.0:0.0} MB of garbage, against its main mesh's {meshBytes / 1048576.0:0.0} MB: " +
                "its storage is not being used again");
        }
    }
}
