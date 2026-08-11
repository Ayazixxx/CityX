using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The geometry itself.
    ///
    /// Screenshots are the point of this phase, but a screenshot is a bad way
    /// to establish whether a retaining wall was generated at all -- buildings
    /// hide most of the ground, so "I cannot see a problem" and "there is no
    /// problem" look identical. These check the things a picture is bad at,
    /// and leave the picture to answer whether it looks right.
    /// </summary>
    public class CityMeshBuilderTests
    {
        private const int Seed = 20260810;

        private static void Build(int chunkX, int chunkY, out MeshBuffer ground, out MeshBuffer structures)
        {
            Build(chunkX, chunkY, out ground, out _, out structures);
        }

        private static void Build(int chunkX, int chunkY,
            out MeshBuffer ground, out MeshBuffer pavement, out MeshBuffer structures)
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            ground = new MeshBuffer();
            pavement = new MeshBuffer();
            structures = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures);
        }

        /// <summary>
        /// Pavement runs along streets and stops at junctions.
        ///
        /// The junction case is the one worth pinning: it is not written as a
        /// special case anywhere, it falls out of the street grid, and it would
        /// silently stop being true if the street period ever changed.
        ///
        /// ONLY STREET CHUNKS ARE INSPECTED, deliberately. The flat-concrete
        /// buffer also carries roofs and walkway decks, so a buildable chunk
        /// having something in it proves nothing either way -- asserting on it
        /// would just be asserting that the chunk has a building.
        /// </summary>
        [Test]
        public void PavementRunsAlongStreetsAndStopsAtJunctions()
        {
            int junctions = 0;
            int runs = 0;

            for (int x = -24; x <= 24; x++)
            {
                for (int y = -24; y <= 24; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    Build(x, y, out _, out MeshBuffer flatConcrete, out _);

                    bool junction = CityLayout.IsStreetChunk(x - 1, y) && CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) && CityLayout.IsStreetChunk(x, y + 1);
                    if (junction)
                    {
                        junctions++;
                        Assert.IsTrue(flatConcrete.IsEmpty,
                            $"({x},{y}) is a junction -- pavement there would block the crossing");
                    }
                    else
                    {
                        runs++;
                        Assert.IsFalse(flatConcrete.IsEmpty,
                            $"({x},{y}) is a street beside buildings but has no pavement");
                    }
                }
            }

            TestContext.WriteLine($"{runs} street runs with pavement, {junctions} junctions without");
            Assert.Greater(junctions, 20, "not enough junctions sampled");
            Assert.Greater(runs, 100, "not enough street runs sampled");
        }

        /// <summary>
        /// No building wall may carry the roof. Directly guards the thing that
        /// looked worst in the first textured render -- window texture printed
        /// across the top of every building.
        /// </summary>
        [Test]
        public void BuildingRoofsAreNotPartOfTheFacade()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out _, out MeshBuffer structures);
                    foreach (Vector3 normal in structures.Normals)
                    {
                        Assert.Less(normal.y, 0.5f,
                            $"({x},{y}) has an upward-facing wall surface -- a roof got the window facade");
                    }
                }
            }

            Assert.Greater(checkedCount, 100, "not enough buildings sampled");
        }

        [Test]
        public void EveryChunkGetsGroundAtItsOwnPadHeight()
        {
            CityWorld world = new CityWorld(Seed);
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Build(x, y, out MeshBuffer ground, out _);
                    Assert.IsFalse(ground.IsEmpty, $"chunk ({x},{y}) produced no ground at all");

                    float pad = world.Terrain.PadHeightFor(x, y);
                    int atPad = 0;
                    foreach (Vector3 v in ground.Vertices)
                    {
                        if (Mathf.Abs(v.y - pad) < 0.001f)
                        {
                            atPad++;
                        }
                    }
                    Assert.GreaterOrEqual(atPad, 4,
                        $"chunk ({x},{y}) has no surface sitting at its pad height of {pad}m");
                }
            }
        }

        /// <summary>
        /// The one a picture genuinely cannot answer. A missing retaining wall
        /// is a slot of daylight along a terrace edge, visible only if you
        /// happen to be standing at the right spot on the low side.
        /// </summary>
        [Test]
        public void ATerraceEdgeAlwaysGetsARetainingWall()
        {
            CityWorld world = new CityWorld(Seed);
            int edgesFound = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    float pad = world.Terrain.PadHeightFor(x, y);
                    float lowestNeighbour = Mathf.Min(
                        Mathf.Min(world.Terrain.PadHeightFor(x - 1, y), world.Terrain.PadHeightFor(x + 1, y)),
                        Mathf.Min(world.Terrain.PadHeightFor(x, y - 1), world.Terrain.PadHeightFor(x, y + 1)));

                    Build(x, y, out MeshBuffer ground, out _);

                    float lowestVertex = float.MaxValue;
                    foreach (Vector3 v in ground.Vertices)
                    {
                        if (v.y < lowestVertex)
                        {
                            lowestVertex = v.y;
                        }
                    }

                    if (lowestNeighbour < pad)
                    {
                        edgesFound++;
                        Assert.LessOrEqual(lowestVertex, lowestNeighbour + 0.001f,
                            $"chunk ({x},{y}) sits at {pad}m beside ground at {lowestNeighbour}m, but its " +
                            $"geometry stops at {lowestVertex}m -- there is a gap you can see through");
                    }
                    else
                    {
                        Assert.AreEqual(pad, lowestVertex, 0.001f,
                            $"chunk ({x},{y}) has no lower neighbour but still built a skirt down to " +
                            $"{lowestVertex}m -- that is a wall buried inside solid ground");
                    }
                }
            }

            TestContext.WriteLine($"{edgesFound} terrace edges checked");
            Assert.Greater(edgesFound, 100, "not enough terrace edges in the sweep to mean anything");
        }

        [Test]
        public void ABuildingsBoxMatchesItsFootprintAndHeight()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer structures);
                    Assert.IsFalse(structures.IsEmpty, $"({x},{y}) has a building but produced no geometry");
                    checkedCount++;

                    Bounds bounds = BoundsOf(structures);
                    // Bridges extend past the building, so only the lower
                    // corner and the roof can be compared exactly.
                    Assert.AreEqual(site.MinX, bounds.min.x, 0.01, $"({x},{y}) west face");
                    Assert.AreEqual(site.MinZ, bounds.min.z, 0.01, $"({x},{y}) south face");
                    Assert.AreEqual(site.PodiumBase, bounds.min.y, 0.01, $"({x},{y}) podium base");
                    Assert.GreaterOrEqual(bounds.max.y, site.Top - 0.01f, $"({x},{y}) roof");
                }
            }

            Assert.Greater(checkedCount, 100, "not enough buildings sampled");
        }

        /// <summary>
        /// A multi-chunk building must be built once, by its anchor. Building it
        /// from every chunk it covers would stack four identical towers in the
        /// same place -- invisible from outside, and quadruple the triangles.
        /// </summary>
        [Test]
        public void MultiChunkBuildingsAreBuiltOnlyByTheirAnchor()
        {
            CityWorld world = new CityWorld(Seed);
            int covered = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (world.RoleAt(x, y).Kind != LotKind.Consumed)
                    {
                        continue;
                    }
                    covered++;
                    Build(x, y, out _, out MeshBuffer structures);
                    Assert.IsTrue(structures.IsEmpty,
                        $"covered chunk ({x},{y}) built its own copy of a neighbour's building");
                }
            }

            Assert.Greater(covered, 50, "no covered chunks in the sweep");
        }

        [Test]
        public void NoGeometryContainsNaNOrInfinity()
        {
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, structures })
                    {
                        foreach (Vector3 v in buffer.Vertices)
                        {
                            Assert.IsTrue(IsFinite(v), $"({x},{y}) has a vertex at {v}");
                        }
                        foreach (Vector3 n in buffer.Normals)
                        {
                            Assert.IsTrue(IsFinite(n), $"({x},{y}) has a normal of {n}");
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryTriangleIndexIsInRange()
        {
            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, structures })
                    {
                        Assert.AreEqual(0, buffer.Triangles.Count % 3, $"({x},{y}) has a partial triangle");
                        Assert.AreEqual(buffer.Vertices.Count, buffer.Normals.Count, $"({x},{y}) normals");
                        Assert.AreEqual(buffer.Vertices.Count, buffer.Uvs.Count, $"({x},{y}) uvs");
                        foreach (int index in buffer.Triangles)
                        {
                            Assert.GreaterOrEqual(index, 0, $"({x},{y}) negative index");
                            Assert.Less(index, buffer.Vertices.Count, $"({x},{y}) index past the end");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A walkway deck has to actually reach both buildings. An inverted box
        /// -- which is what you get if a neighbour's footprint starts before
        /// this one ends -- renders as a hole rather than as an error.
        /// </summary>
        [Test]
        public void WalkwayDecksHavePositiveExtentAndSitAtTheirStatedHeight()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            int decks = 0;

            for (int x = -40; x <= 40; x++)
            {
                for (int y = -40; y <= 40; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    foreach (BuildingSite neighbour in new[]
                             {
                                 SkybridgeLayout.EastNeighbour(world, site),
                                 SkybridgeLayout.NorthNeighbour(world, site),
                             })
                    {
                        if (!neighbour.Exists)
                        {
                            continue;
                        }
                        foreach (Skybridge bridge in bridges.BridgesBetween(site, neighbour))
                        {
                            decks++;
                            // The gap the deck has to span, measured face to face.
                            double gap = neighbour.MinX > site.MaxX
                                ? neighbour.MinX - site.MaxX
                                : neighbour.MinZ - site.MaxZ;
                            Assert.Greater(gap, 0.0,
                                $"({x},{y}) is bridged to a neighbour whose footprint overlaps it, so the " +
                                "deck would be inside out");
                            Assert.Less(gap, CityLayout.ChunkSize * 2.0,
                                $"({x},{y}) has a {gap:0.0}m walkway -- that is further than a neighbour " +
                                "should ever be");
                        }
                    }
                }
            }

            TestContext.WriteLine($"{decks} walkway decks checked");
            Assert.Greater(decks, 30, "not enough walkways sampled");
        }

        private static Bounds BoundsOf(MeshBuffer buffer)
        {
            Bounds bounds = new Bounds(buffer.Vertices[0], Vector3.zero);
            foreach (Vector3 v in buffer.Vertices)
            {
                bounds.Encapsulate(v);
            }
            return bounds;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }
    }
}
