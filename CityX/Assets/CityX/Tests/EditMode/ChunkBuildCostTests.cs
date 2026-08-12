using System.Collections.Generic;
using System.Diagnostics;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What a chunk costs to build, upload and cook.
    ///
    /// STREAMING COST IS THE THING THIS PROJECT WATCHES. The Godot build spent
    /// a whole task hunting a chunk-streaming hitch, and hollowing the
    /// buildings out multiplied what a chunk carries -- floor slabs, partition
    /// walls, and a stack of step boxes per storey, all of which the chunk's
    /// own MeshCollider then has to cook. Something that shows up as "the game
    /// stutters when I run down a street" should fail here first.
    ///
    /// THE BOUNDS ARE DELIBERATELY SLACK, several times the measured cost.
    /// Wall-clock in a test is at the mercy of whatever else the machine is
    /// doing, so a tight bound would fail for reasons that have nothing to do
    /// with the code. These are set to catch an order-of-magnitude regression
    /// -- somebody putting a per-vertex allocation in the step loop -- not to
    /// police a few hundred microseconds.
    /// </summary>
    public class ChunkBuildCostTests
    {
        private const int Seed = 20260810;

        [Test]
        public void AChunkIsStillCheapEnoughToStream()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            var holder = new GameObject("cost-probe");
            var watch = new Stopwatch();

            double geometryMs = 0.0;
            double uploadMs = 0.0;
            double cookMs = 0.0;
            double buildingChunkMs = 0.0;
            int buildingChunks = 0;
            long triangles = 0;
            int chunks = 0;

            try
            {
                for (int x = -10; x <= 10; x++)
                {
                    for (int y = -10; y <= 10; y++)
                    {
                        chunks++;
                        var ground = new MeshBuffer();
                        var pavement = new MeshBuffer();
                        var structures = new MeshBuffer();

                        watch.Restart();
                        CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                            ground, pavement, structures);
                        watch.Stop();
                        double geometry = watch.Elapsed.TotalMilliseconds;
                        geometryMs += geometry;

                        BuildingSite site = world.BuildingAt(x, y);
                        if (site.Exists && site.AnchorX == x && site.AnchorY == y)
                        {
                            buildingChunks++;
                            buildingChunkMs += geometry;
                        }

                        var vertices = new List<Vector3>();
                        var normals = new List<Vector3>();
                        var uvs = new List<Vector2>();
                        var indices = new List<int>();
                        foreach (MeshBuffer buffer in new[] { ground, pavement, structures })
                        {
                            int offset = vertices.Count;
                            vertices.AddRange(buffer.Vertices);
                            normals.AddRange(buffer.Normals);
                            uvs.AddRange(buffer.Uvs);
                            foreach (int index in buffer.Triangles)
                            {
                                indices.Add(index + offset);
                            }
                        }
                        triangles += indices.Count / 3;

                        watch.Restart();
                        var mesh = new Mesh
                        {
                            indexFormat = vertices.Count > 65000
                                ? UnityEngine.Rendering.IndexFormat.UInt32
                                : UnityEngine.Rendering.IndexFormat.UInt16,
                        };
                        mesh.SetVertices(vertices);
                        mesh.SetNormals(normals);
                        mesh.SetUVs(0, uvs);
                        mesh.SetTriangles(indices, 0);
                        mesh.RecalculateTangents();
                        mesh.RecalculateBounds();
                        watch.Stop();
                        uploadMs += watch.Elapsed.TotalMilliseconds;

                        var go = new GameObject($"chunk {x},{y}");
                        go.transform.SetParent(holder.transform, false);
                        watch.Restart();
                        go.AddComponent<MeshCollider>().sharedMesh = mesh;
                        watch.Stop();
                        cookMs += watch.Elapsed.TotalMilliseconds;
                    }
                }
            }
            finally
            {
                foreach (MeshCollider collider in holder.GetComponentsInChildren<MeshCollider>())
                {
                    Object.DestroyImmediate(collider.sharedMesh);
                }
                Object.DestroyImmediate(holder);
            }

            float perChunk = (float)((geometryMs + uploadMs + cookMs) / chunks);
            float perBuilding = (float)(buildingChunkMs / Mathf.Max(1, buildingChunks));
            float trianglesPerChunk = triangles / (float)chunks;

            TestContext.WriteLine($"{chunks} chunks, {triangles} triangles ({trianglesPerChunk:0} each)");
            TestContext.WriteLine($"geometry {geometryMs / chunks:0.00}ms, "
                                  + $"upload {uploadMs / chunks:0.00}ms, cook {cookMs / chunks:0.00}ms");
            TestContext.WriteLine($"all in {perChunk:0.00}ms per chunk, "
                                  + $"{perBuilding:0.00}ms of geometry per chunk with a building");

            // CityStreamer builds ChunkBuildBudgetPerFrame chunks a frame, so
            // this number times that budget is what a frame can lose to
            // streaming. Measured at ~0.8ms.
            Assert.Less(perChunk, 5.0f,
                $"a chunk takes {perChunk:0.00}ms to build, upload and cook, so a frame that "
                + "builds a full budget of them loses most of itself to streaming");
            Assert.Less(perBuilding, 3.0f,
                $"a chunk with a building takes {perBuilding:0.00}ms of pure geometry");
            Assert.Less(trianglesPerChunk, 4000.0f,
                $"a chunk averages {trianglesPerChunk:0} triangles -- something inside the "
                + "buildings grew a lot");
        }
    }
}
