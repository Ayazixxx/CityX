using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The geometry the plan city turns into: a frame on MeshBuffer that turns a building to face its street, and the ground of a
    /// chunk (terrain, roads, pavement) that has to meet its neighbours exactly.
    /// </summary>
    public sealed class PlanChunkMeshTests
    {
        private const int Seed = 61928374;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        // ---- MeshBuffer.SetFrame ----

        [Test]
        public void WithNoFrameAQuadIsWrittenAsGiven()
        {
            var buffer = new MeshBuffer();
            buffer.AddQuad(new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
                Vector3.back, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            Assert.AreEqual(new Vector3(1, 1, 0), buffer.Vertices[2]);
            Assert.AreEqual(Vector3.back, buffer.Normals[0]);
        }

        [Test]
        public void AFrameTurnsAndMovesEveryPointAndNormal()
        {
            // axis (0, 1): local +X is world +Z, so local +Z is world -X.
            var buffer = new MeshBuffer();
            buffer.SetFrame(new Vector3(10, 0, 20), 0.0, 1.0);
            buffer.AddQuad(new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 1, 0), new Vector3(0, 1, 0),
                Vector3.back, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);

            Vector3 moved = buffer.Vertices[1];   // local (2, 0, 0)
            Assert.AreEqual(10.0f, moved.x, 1e-4f);
            Assert.AreEqual(22.0f, moved.z, 1e-4f);
            // local -Z (back) is world +X.
            Assert.AreEqual(1.0f, buffer.Normals[0].x, 1e-4f);
            Assert.AreEqual(0.0f, buffer.Normals[0].z, 1e-4f);
        }

        [Test]
        public void ATurnedBoxIsStillWoundTheWayItsNormalsClaim()
        {
            var buffer = new MeshBuffer();
            buffer.SetFrame(new Vector3(5, 3, -7), 0.6, -0.8);   // an awkward angle
            buffer.AddBox(new Vector3(-3, 0, -2), new Vector3(3, 9, 2));
            AssertWound(buffer, "a turned box");
        }

        [Test]
        public void TheFrameAgreesWithTheBuildingSiteConversion()
        {
            BuildingSite site = FirstTurnedBuilding();
            var buffer = new MeshBuffer();
            buffer.SetFrame(new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ), site.AxisX, site.AxisZ);
            Vector3 local = new Vector3(4.0f, 2.0f, -3.0f);
            Vector3 viaBuffer = buffer.FramePoint(local);
            site.LocalToWorld(local.x, local.z, out double wx, out double wz);
            Assert.AreEqual((float)wx, viaBuffer.x, 1e-3f);
            Assert.AreEqual((float)wz, viaBuffer.z, 1e-3f);
            site.WorldToLocal(wx, wz, out double lx, out double lz);
            Assert.AreEqual(local.x, lx, 1e-6);
            Assert.AreEqual(local.z, lz, 1e-6);
        }

        [Test]
        public void ABuildingIsBuiltWithItsDoorWallFacingTheStreet()
        {
            BuildingSite site = FirstTurnedBuilding();
            Vector3 front = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
            // The front wall's outward normal is the outward normal of the building's frontage: it points at the road.
            Assert.AreEqual(-site.AxisZ, front.x, 1e-5);
            Assert.AreEqual(site.AxisX, front.z, 1e-5);
        }

        // ---- chunks ----

        [Test]
        public void ANeighbouringChunkMeetsTheSurfaceExactlyOnTheSharedEdge()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);

            foreach ((int dx, int dy) in new[] { (1, 0), (0, 1) })
            {
                // The surface alone (terrain, roads, decks, pavement): a building stands whole in the chunk that owns its centre and is not
                // clipped to it, so a building corner that happens to fall within a millimetre of the border is not a seam.
                var a = Surface(cx, cy);
                var b = Surface(cx + dx, cy + dy);
                HashSet<(int, int)> edgeA = BoundaryPoints(a, cx, cy, dx, dy, true);
                HashSet<(int, int)> edgeB = BoundaryPoints(b, cx + dx, cy + dy, dx, dy, false);
                Assert.Greater(edgeA.Count, 5, "chunk has no surface on its shared edge");
                var onlyA = new List<(int, int)>(edgeA);
                onlyA.RemoveAll(edgeB.Contains);
                var onlyB = new List<(int, int)>(edgeB);
                onlyB.RemoveAll(edgeA.Contains);
                Assert.IsTrue(edgeA.SetEquals(edgeB),
                    $"chunks ({cx},{cy}) and ({cx + dx},{cy + dy}) do not meet exactly on their shared edge: " +
                    $"{edgeA.Count} points vs {edgeB.Count}; only in the first (along cm, height cm): {string.Join(" ", onlyA)} {Describe(a, onlyA, dx)}; " +
                    $"only in the second: {string.Join(" ", onlyB)} {Describe(b, onlyB, dx)}");
            }
        }

        [Test]
        public void EveryFaceOfTheSurfaceAndBuildingsIsWoundTheWayItsNormalClaims()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    var buffers = Chunk(cx + dx, cy + dy);
                    foreach (MeshBuffer buffer in buffers)
                    {
                        AssertWound(buffer, $"chunk ({cx + dx},{cy + dy})");
                    }
                }
            }
        }

        [Test]
        public void NoGeometryContainsNaNOrInfinity()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dy = -3; dy <= 3; dy++)
                {
                    foreach (MeshBuffer buffer in Chunk(cx + dx, cy + dy))
                    {
                        foreach (Vector3 v in buffer.Vertices)
                        {
                            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                           float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z),
                                $"a bad vertex in chunk ({cx + dx},{cy + dy})");
                        }
                    }
                }
            }
        }

        [Test]
        public void ABuildingsChunkHasWallsAndGlassAndARoof()
        {
            BuildingSite site = FirstTurnedBuilding();
            MeshBuffer[] buffers = Chunk(site.OwnerChunkX, site.OwnerChunkY);   // ground, pavement, structures, dark, lit
            Assert.Greater(buffers[2].Triangles.Count, 100, "no walls");
            Assert.Greater(buffers[3].Triangles.Count + buffers[4].Triangles.Count, 10, "no glass");
        }

        [Test]
        public void TheFarVersionOfAChunkIsLighterThanTheFullOne()
        {
            BuildingSite site = FirstTurnedBuilding();
            int full = 0, far = 0;
            foreach (MeshBuffer b in Chunk(site.OwnerChunkX, site.OwnerChunkY, false)) full += b.Triangles.Count;
            foreach (MeshBuffer b in Chunk(site.OwnerChunkX, site.OwnerChunkY, true)) far += b.Triangles.Count;
            Assert.Less(far, full, "the far chunk is not lighter");
        }

        [Test]
        public void ABuildingBuildsTheSameWhateverChunkOriginItIsGiven()
        {
            BuildingSite site = FirstTurnedBuilding();
            var a = new MeshBuffer[5];
            var b = new MeshBuffer[5];
            for (int i = 0; i < 5; i++) { a[i] = new MeshBuffer(); b[i] = new MeshBuffer(); }
            var oa = new Vector3(0.0f, 0.0f, 0.0f);
            var ob = new Vector3(1000.0f, 0.0f, -500.0f);
            CityMeshBuilder.BuildSite(World, site, oa, a[2], a[1], a[4], a[3], null, null, null, null, false);
            CityMeshBuilder.BuildSite(World, site, ob, b[2], b[1], b[4], b[3], null, null, null, null, false);
            Assert.AreEqual(a[2].Vertices.Count, b[2].Vertices.Count);
            for (int i = 0; i < a[2].Vertices.Count; i += 53)
            {
                Vector3 d = (a[2].Vertices[i] + oa) - (b[2].Vertices[i] + ob);   // chunk-local plus the origin is the world position
                Assert.Less(d.magnitude, 1e-2f);
            }
        }

        // ---- helpers ----

        private static BuildingSite FirstTurnedBuilding()
        {
            foreach (BuildingSite site in TestSites.NearSpawn(World, 14))
            {
                if (Math.Abs(site.AxisX) > 0.3 && Math.Abs(site.AxisZ) > 0.3 && site.Params.Width > 12.0f && site.Params.Height > 15.0f)
                {
                    return site;
                }
            }
            Assert.Fail("no turned building near the spawn");
            return default;
        }

        /// <summary>One chunk's buffers in the streamer's order: ground, pavement, structures, dark glass, lit glass.</summary>
        private static MeshBuffer[] Chunk(int cx, int cy, bool far = false)
        {
            var buffers = new MeshBuffer[5];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
            CityMeshBuilder.BuildChunk(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3],
                far: far);
            return buffers;
        }

        /// <summary>One chunk's surface only (CitySurfaceBuilder, no buildings): ground, pavement, then its props, paint and rails.</summary>
        private static MeshBuffer[] Surface(int cx, int cy)
        {
            var buffers = new MeshBuffer[6];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
            CitySurfaceBuilder.Build(World, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[3], buffers[4], buffers[5], false);
            return buffers;
        }

        /// <summary>The surface vertices (ground and pavement) on one edge of a chunk, as rounded (along, height) pairs.</summary>
        private static HashSet<(int, int)> BoundaryPoints(MeshBuffer[] chunk, int cx, int cy, int dx, int dy, bool leavingSide)
        {
            var points = new HashSet<(int, int)>();
            float size = CityLayout.ChunkSize;
            foreach (MeshBuffer buffer in new[] { chunk[0], chunk[1] })
            {
                foreach (Vector3 v in buffer.Vertices)
                {
                    float across = dx != 0 ? v.x : v.z;
                    float along = dx != 0 ? v.z : v.x;
                    float edge = leavingSide ? size : 0.0f;
                    if (Math.Abs(across - edge) > 1e-3f) continue;
                    points.Add(((int)Math.Round(along * 100.0f), (int)Math.Round(v.y * 100.0f)));
                }
            }
            return points;
        }

        /// <summary>For a failure message: the triangles (buffer, three chunk-local corners, normal) that use each of the given boundary points.</summary>
        private static string Describe(MeshBuffer[] chunk, List<(int, int)> points, int dx)
        {
            var text = new System.Text.StringBuilder();
            string[] names = { "ground", "pavement" };
            for (int b = 0; b < 2; b++)
            {
                MeshBuffer buffer = chunk[b];
                for (int i = 0; i + 2 < buffer.Triangles.Count; i += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 v = buffer.Vertices[buffer.Triangles[i + k]];
                        float along = dx != 0 ? v.z : v.x;
                        var key = ((int)Math.Round(along * 100.0f), (int)Math.Round(v.y * 100.0f));
                        if (!points.Contains(key)) continue;
                        text.Append($"[{names[b]} tri {i / 3}: {buffer.Vertices[buffer.Triangles[i]]} {buffer.Vertices[buffer.Triangles[i + 1]]} {buffer.Vertices[buffer.Triangles[i + 2]]} n {buffer.Normals[buffer.Triangles[i]]}] ");
                        break;
                    }
                }
            }
            return text.ToString();
        }

        private static void AssertWound(MeshBuffer buffer, string what)
        {
            int bad = 0;
            for (int i = 0; i + 2 < buffer.Triangles.Count; i += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[i]];
                Vector3 b = buffer.Vertices[buffer.Triangles[i + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[i + 2]];
                Vector3 n = buffer.Normals[buffer.Triangles[i]];
                Vector3 geometric = Vector3.Cross(b - a, c - a);
                if (geometric.sqrMagnitude < 1e-10f) continue;
                if (Vector3.Dot(geometric, n) < 0.0f) bad++;
            }
            Assert.AreEqual(0, bad, $"{what}: {bad} triangles are wound against their own normal");
        }
    }
}
