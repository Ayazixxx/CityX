using System.Collections.Generic;
using System.Linq;
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
            Build(chunkX, chunkY, out ground, out pavement, out structures, out _, out _);
        }

        private static void Build(int chunkX, int chunkY, out MeshBuffer ground,
            out MeshBuffer pavement, out MeshBuffer structures,
            out MeshBuffer litGlass, out MeshBuffer darkGlass)
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            ground = new MeshBuffer();
            pavement = new MeshBuffer();
            structures = new MeshBuffer();
            litGlass = new MeshBuffer();
            darkGlass = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures, litGlass, darkGlass);
        }

        private static void BuildStreetEdge(int chunkX, int chunkY,
            out MeshBuffer pavement, out MeshBuffer guardRail)
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            var ground = new MeshBuffer();
            pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            guardRail = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures, guardRail: guardRail);
        }

        private static bool HasVertexNear(MeshBuffer buffer, float y, float tolerance = 0.05f)
        {
            foreach (Vector3 v in buffer.Vertices)
            {
                if (Mathf.Abs(v.y - y) <= tolerance)
                {
                    return true;
                }
            }
            return false;
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
        ///
        /// THE RULE IS "NOT OVER THE FOOTPRINT", not "nothing facing up", and
        /// the difference is a fin's top cap. Facade ribs stand proud of the
        /// wall and are capped, so they put a few upward-facing slivers in this
        /// buffer, entirely OUTSIDE the plan of the building. Loosening this to
        /// tolerate small upward faces anywhere would have let a real roof
        /// through the day one got split into small triangles; testing where
        /// they are instead separates the two exactly.
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
                    for (int t = 0; t < structures.Triangles.Count; t += 3)
                    {
                        int a = structures.Triangles[t];
                        if (structures.Normals[a].y < 0.5f)
                        {
                            continue;
                        }

                        Vector3 centre = (structures.Vertices[a] +
                                          structures.Vertices[structures.Triangles[t + 1]] +
                                          structures.Vertices[structures.Triangles[t + 2]]) / 3.0f;
                        bool overThePlan =
                            centre.x > site.MinX + 0.001 && centre.x < site.MaxX - 0.001 &&
                            centre.z > site.MinZ + 0.001 && centre.z < site.MaxZ - 0.001;
                        Assert.IsFalse(overThePlan,
                            $"({x},{y}) has an upward-facing facade surface at {centre} inside its own " +
                            "footprint -- a roof got the window texture");
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
        ///
        /// Checks all 8 neighbours, not just the 4 cardinal ones. A diagonal
        /// neighbour is never bounded by the cardinal slope limit the same
        /// way a direct one is (it can compound two independent cardinal
        /// drops), so a corner can need a wall even when neither flanking
        /// edge does -- see AddCornerSkirtIfLower's own doc comment.
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
                    float lowestNeighbour = pad;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }
                            lowestNeighbour = Mathf.Min(lowestNeighbour,
                                world.Terrain.PadHeightFor(x + dx, y + dy));
                        }
                    }

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

        /// <summary>
        /// Wherever a street keeps going but drops onto a lower street chunk
        /// with a real sidewalk to carry it, there is a real flight of steps
        /// -- not just the sheer retaining wall -- reaching all the way from
        /// this chunk's own pad down to the lower one's.
        /// </summary>
        [Test]
        public void AStreetTerraceDropWithASidewalkGetsARealStaircase()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);

                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (!CityLayout.IsStreetChunk(nx, ny))
                        {
                            continue;
                        }
                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad)
                        {
                            continue;
                        }
                        // A junction (both axes divisible by the street period)
                        // gets no sidewalk on either side -- see AddSidewalks's
                        // own doc comment -- so it never has anywhere to carve a
                        // stair into. Only a straight run qualifies here.
                        bool junction = CityLayout.IsStreetChunk(x - 1, y) &&
                                        CityLayout.IsStreetChunk(x + 1, y) &&
                                        CityLayout.IsStreetChunk(x, y - 1) &&
                                        CityLayout.IsStreetChunk(x, y + 1);
                        if (junction)
                        {
                            continue;
                        }

                        BuildStreetEdge(x, y, out MeshBuffer pavement, out _);
                        casesFound++;
                        Assert.IsTrue(HasVertexNear(pavement, pad),
                            $"chunk ({x},{y}) drops to ({nx},{ny}) with a sidewalk to carry a stair, " +
                            $"but no step reaches back up to its own pad at {pad}m");
                        Assert.IsTrue(HasVertexNear(pavement, neighbourPad),
                            $"chunk ({x},{y}) drops to ({nx},{ny}), but no step reaches down to " +
                            $"{neighbourPad}m -- the flight stops short of the lower level");
                    }
                }
            }

            TestContext.WriteLine($"{casesFound} street-vs-street drops checked");
            Assert.Greater(casesFound, 0, "no street-vs-street terrace drop with a sidewalk in the sweep");
        }

        /// <summary>
        /// A junction has no sidewalk on any side (AddSidewalks builds none
        /// there), so even where two streets meet at different terrace
        /// levels there is nowhere to carve a stair into -- just the plain
        /// wall, correctly, rather than one floating over open roadway.
        /// </summary>
        [Test]
        public void AJunctionTerraceDropGetsNoStaircase()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;

            for (int x = -40; x <= 40; x += 4)
            {
                for (int y = -40; y <= 40; y += 4)
                {
                    float pad = world.Terrain.PadHeightFor(x, y);
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad)
                        {
                            continue;
                        }

                        BuildStreetEdge(x, y, out MeshBuffer pavement, out _);
                        casesFound++;
                        Assert.IsFalse(HasVertexNear(pavement, neighbourPad),
                            $"junction ({x},{y}) drops to ({nx},{ny}) with no sidewalk on either side, " +
                            $"but pavement still reaches {neighbourPad}m -- a stair over open roadway");
                    }
                }
            }

            TestContext.WriteLine($"{casesFound} junction drops checked");
            Assert.Greater(casesFound, 0, "no junction terrace drop in the sweep");
        }

        /// <summary>
        /// The short guardrail is there wherever a street-level drop is,
        /// not just wherever a stair happens to be -- a street meeting a
        /// building lot's own lower edge is still a real ledge.
        /// </summary>
        [Test]
        public void AStreetTerraceDropAlwaysGetsAGuardRail()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;

            for (int x = -30; x <= 30; x++)
            {
                for (int y = -30; y <= 30; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    bool anyDrop = false;
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (world.Terrain.PadHeightFor(nx, ny) < pad)
                        {
                            anyDrop = true;
                        }
                    }
                    if (!anyDrop)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    casesFound++;
                    Assert.IsFalse(guardRail.IsEmpty,
                        $"street chunk ({x},{y}) drops to a lower neighbour but built no guardrail");
                }
            }

            TestContext.WriteLine($"{casesFound} street drops checked for a guardrail");
            Assert.Greater(casesFound, 0, "no street terrace drop in the sweep");
        }

        /// <summary>Same chunk, same world -- the stair has to pick the same side every time, not flip on a rebuild.</summary>
        [Test]
        public void StreetEdgeFeaturesAreDeterministic()
        {
            CityWorld world = new CityWorld(Seed);

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    bool anyDrop = false;
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (world.Terrain.PadHeightFor(nx, ny) < pad)
                        {
                            anyDrop = true;
                        }
                    }
                    if (!anyDrop)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out MeshBuffer pavementA, out MeshBuffer guardRailA);
                    BuildStreetEdge(x, y, out MeshBuffer pavementB, out MeshBuffer guardRailB);

                    CollectionAssert.AreEqual(pavementA.Vertices, pavementB.Vertices,
                        $"chunk ({x},{y}) built a different staircase on a second, identical rebuild");
                    CollectionAssert.AreEqual(guardRailA.Vertices, guardRailB.Vertices,
                        $"chunk ({x},{y}) built a different guardrail on a second, identical rebuild");
                    return;
                }
            }

            Assert.Fail("no street terrace drop found to check determinism against");
        }

        /// <summary>
        /// EXPLICIT ASK: "keep making the city more citylike". A lamp needs
        /// nowhere else to stand than the sidewalk AddSidewalks already
        /// builds, so this checks the SAME condition that logic already
        /// relies on: any street chunk with at least one real (non-street)
        /// neighbour gets lamp geometry, regardless of whether that edge
        /// also happens to be a terrace drop -- unlike the guard rail
        /// above, a lamp does not need one.
        /// </summary>
        [Test]
        public void EveryStreetChunkWithARealSidewalkGetsAtLeastOneLamp()
        {
            int casesFound = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    bool hasRealNeighbour = !CityLayout.IsStreetChunk(x - 1, y) ||
                                             !CityLayout.IsStreetChunk(x + 1, y) ||
                                             !CityLayout.IsStreetChunk(x, y - 1) ||
                                             !CityLayout.IsStreetChunk(x, y + 1);
                    if (!hasRealNeighbour)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    casesFound++;
                    Assert.IsFalse(guardRail.IsEmpty,
                        $"street chunk ({x},{y}) has a real sidewalk but built no lamp");
                }
            }

            Assert.Greater(casesFound, 0, "no ordinary street edge in the sweep");
        }

        /// <summary>
        /// A 4-way junction has no sidewalk anywhere (AddSidewalks' own
        /// rule), so it should have no lamp floating in its own middle
        /// either. EXCLUDES a junction that also drops to a lower
        /// neighbour -- that legitimately puts UNRELATED guard-rail
        /// geometry (AddStreetEdgeFeatures, pre-existing) in this same
        /// shared buffer regardless of lamps, so "the buffer is non-empty"
        /// would not actually be testing lamp placement on that case.
        /// </summary>
        [Test]
        public void AFourWayJunctionGetsNoLamp()
        {
            CityWorld world = new CityWorld(Seed);
            int casesFound = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    bool junction = CityLayout.IsStreetChunk(x, y) &&
                                    CityLayout.IsStreetChunk(x - 1, y) &&
                                    CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) &&
                                    CityLayout.IsStreetChunk(x, y + 1);
                    if (!junction)
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    bool anyDrop = world.Terrain.PadHeightFor(x - 1, y) < pad ||
                                    world.Terrain.PadHeightFor(x + 1, y) < pad ||
                                    world.Terrain.PadHeightFor(x, y - 1) < pad ||
                                    world.Terrain.PadHeightFor(x, y + 1) < pad;
                    if (anyDrop)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    casesFound++;
                    Assert.IsTrue(guardRail.IsEmpty,
                        $"junction ({x},{y}) has no sidewalk to stand on but built a lamp anyway");
                }
            }

            Assert.Greater(casesFound, 0, "no junction in the sweep");
        }

        /// <summary>Same chunk, same world -- a lamp has to land in the same place every time, not flip on a rebuild.</summary>
        [Test]
        public void StreetLampsAreDeterministic()
        {
            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    bool hasRealNeighbour = !CityLayout.IsStreetChunk(x - 1, y) ||
                                             !CityLayout.IsStreetChunk(x + 1, y) ||
                                             !CityLayout.IsStreetChunk(x, y - 1) ||
                                             !CityLayout.IsStreetChunk(x, y + 1);
                    if (!hasRealNeighbour)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRailA);
                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRailB);
                    CollectionAssert.AreEqual(guardRailA.Vertices, guardRailB.Vertices,
                        $"chunk ({x},{y}) built different lamp geometry on a second, identical rebuild");
                    return;
                }
            }

            Assert.Fail("no ordinary street edge found to check determinism against");
        }

        /// <summary>The pole actually stands tall rather than being a degenerate sliver -- the lamp head has to clear head height by a real margin to read as a lamp at all.</summary>
        [Test]
        public void AStreetLampReachesItsFullPoleHeight()
        {
            CityWorld world = new CityWorld(Seed);

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    bool hasRealNeighbour = !CityLayout.IsStreetChunk(x - 1, y) ||
                                             !CityLayout.IsStreetChunk(x + 1, y) ||
                                             !CityLayout.IsStreetChunk(x, y - 1) ||
                                             !CityLayout.IsStreetChunk(x, y + 1);
                    if (!hasRealNeighbour)
                    {
                        continue;
                    }

                    BuildStreetEdge(x, y, out _, out MeshBuffer guardRail);
                    float pad = world.Terrain.PadHeightFor(x, y);
                    float expectedHeadTop = pad + CityMeshBuilder.KerbHeight
                        + CityMeshBuilder.LampPoleHeight + CityMeshBuilder.LampHeadHeight;
                    Assert.IsTrue(HasVertexNear(guardRail, expectedHeadTop),
                        $"chunk ({x},{y}) has no lamp geometry reaching the expected head height {expectedHeadTop:0.00}");
                    return;
                }
            }

            Assert.Fail("no ordinary street edge found to check lamp height against");
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

                    // Facade ribs stand proud of the wall they are on, so the
                    // west and south faces reach exactly one fin depth further
                    // out -- and exactly zero further when that wall was not
                    // ribbed. Both are pinned rather than allowing a range,
                    // because "somewhere within half a metre of the wall" would
                    // also pass for a fin built on the wrong wall entirely.
                    BuildingDecor.RibbedFacade ribbed = BuildingDecor.Ribs(
                        world.ChunkSeed(x, y), site.Params.Width, site.Params.Depth,
                        site.Params.Height,
                        -BuildingInterior.DoorWidth * 0.5f, BuildingInterior.DoorWidth * 0.5f);
                    float westFins = 0.0f;
                    float southFins = 0.0f;
                    foreach (BuildingDecor.Rib rib in ribbed.Fins)
                    {
                        if (rib.Normal == Vector3.left)
                        {
                            westFins = ribbed.Depth;
                        }
                        if (rib.Normal == Vector3.back)
                        {
                            southFins = ribbed.Depth;
                        }
                    }

                    // A wing is a second volume attached to one wall, so on the
                    // wall it grew from the facade reaches out to the wing's
                    // own far face instead.
                    double westFace = site.MinX - westFins;
                    double southFace = site.MinZ - southFins;
                    if (CityMeshBuilder.TryWingBox(world, site, out Bounds wing))
                    {
                        westFace = System.Math.Min(westFace, wing.min.x);
                        southFace = System.Math.Min(southFace, wing.min.z);
                    }

                    // Bridges extend past the building, so only the lower
                    // corner and the roof can be compared exactly.
                    Assert.AreEqual(westFace, bounds.min.x, 0.01, $"({x},{y}) west face");
                    Assert.AreEqual(southFace, bounds.min.z, 0.01, $"({x},{y}) south face");
                    Assert.AreEqual(site.PodiumBase, bounds.min.y, 0.01, $"({x},{y}) podium base");
                    Assert.GreaterOrEqual(bounds.max.y, site.Top - 0.01f, $"({x},{y}) roof");
                }
            }

            Assert.Greater(checkedCount, 100, "not enough buildings sampled");
        }

        /// <summary>
        /// NO WING EVER REACHES INTO ANOTHER BUILDING. This is the one thing
        /// the wing's clearance clamp exists for, and the only place it can
        /// actually be checked: BuildingDecor is handed clearances as numbers
        /// and has no way of knowing whether they were measured correctly, so
        /// a clamp that worked on paper and a lot rectangle read one chunk out
        /// would both pass every test in that file.
        ///
        /// Real ground truth, over every anchored building in a wide sweep,
        /// against every other building's real footprint.
        /// </summary>
        [Test]
        public void NoWingGrowsThroughTheBuildingNextDoor()
        {
            CityWorld world = new CityWorld(Seed);
            var footprints = new List<(int X, int Y, Rect Plan)>();

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    footprints.Add((x, y, Rect.MinMaxRect(
                        (float)site.MinX, (float)site.MinZ, (float)site.MaxX, (float)site.MaxZ)));
                }
            }

            int wings = 0;
            foreach ((int x, int y, Rect _) in footprints)
            {
                BuildingSite site = world.BuildingAt(x, y);
                if (!CityMeshBuilder.TryWingBox(world, site, out Bounds box))
                {
                    continue;
                }
                wings++;

                Rect plan = Rect.MinMaxRect(box.min.x, box.min.z, box.max.x, box.max.z);
                foreach ((int otherX, int otherY, Rect other) in footprints)
                {
                    if (otherX == x && otherY == y)
                    {
                        continue;
                    }
                    Assert.IsFalse(plan.Overlaps(other),
                        $"({x},{y})'s wing at {plan} runs into ({otherX},{otherY}) at {other}");
                }
            }

            TestContext.WriteLine($"{wings} wings over {footprints.Count} buildings");
            Assert.Greater(wings, 20, "no building in the sweep grew a wing, so this proved nothing");
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

        /// <summary>
        /// EVERY FACE IS WOUND TO FACE THE WAY IT SAYS IT DOES.
        ///
        /// This one exists because it was already false. Five of AddBox's six
        /// faces were wound backwards -- every wall, kerb and walkway deck in
        /// the city -- and it went unnoticed for exactly the reason a test is
        /// needed here: an inside-out closed box still fills the same
        /// silhouette, so the city looked like a city. What it actually did
        /// was light each surface by a normal pointing away from the camera
        /// and leave the FAR wall's depth in the buffer, which is invisible
        /// until something is put inside the building -- and then the stairs
        /// draw in front of the facade.
        ///
        /// The convention was settled by rendering a quad each way round with
        /// back-face culling on rather than by reasoning about handedness: the
        /// visible one is the one whose Cross(b - a, c - a) points at the
        /// camera, along the outward normal.
        /// </summary>
        [Test]
        public void EveryFaceIsWoundTheWayItsNormalClaims()
        {
            int triangles = 0;

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    Build(x, y, out MeshBuffer ground, out MeshBuffer pavement, out MeshBuffer structures);
                    foreach (MeshBuffer buffer in new[] { ground, pavement, structures })
                    {
                        for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
                        {
                            Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                            Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                            Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                            Vector3 stated = buffer.Normals[buffer.Triangles[t]];

                            float facing = Vector3.Dot(Vector3.Cross(b - a, c - a), stated);
                            Assert.Greater(facing, 0.0f,
                                $"chunk ({x},{y}) has a triangle claiming to face {stated} that is "
                                + "wound the other way -- it renders from behind and writes the "
                                + "wrong surface's depth");
                            triangles++;
                        }
                    }
                }
            }

            TestContext.WriteLine($"{triangles} triangles checked");
            Assert.Greater(triangles, 10000, "not enough geometry in the sweep to mean anything");
        }

        /// <summary>
        /// THE DOOR IS A REAL HOLE, not a painted-on rectangle.
        ///
        /// IT USED TO MEASURE THE WHOLE WALL against a solid rectangle less a
        /// doorway, and that stopped meaning anything the moment the wall grew
        /// real windows -- it failed with 619m2 against an expected 816m2,
        /// which is the windows, not a missing door. Rewriting it to subtract
        /// the window area too would have made the test recompute the layout it
        /// is supposed to be checking.
        ///
        /// So it measures the one rectangle the claim is actually about: how
        /// much wall stands in the doorway. The answer has to be none, whatever
        /// the rest of the wall is doing, and the second assertion keeps that
        /// from passing because the wall was never built at all.
        /// </summary>
        [Test]
        public void TheFrontWallHasADoorwayMissingFromIt()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out _, out MeshBuffer structures);

                    float centreX = (float)site.CentreX;
                    float half = BuildingInterior.DoorWidth * 0.5f;
                    var doorway = Rect.MinMaxRect(
                        centreX - half, site.BaseHeight,
                        centreX + half, site.BaseHeight + BuildingInterior.DoorHeight(site.Params.Height));

                    float blocking = AreaInUprightRect(structures, (float)site.MaxZ, doorway);
                    Assert.AreEqual(0.0f, blocking, 0.001f,
                        $"({x},{y}) has {blocking:0.00}m2 of wall standing in its own doorway");

                    float wall = AreaInPlane(structures, (float)site.MaxZ, 2, Rect.zero, Rect.zero);
                    Assert.Greater(wall, site.Params.Width * 2.0f,
                        $"({x},{y})'s front wall is barely there, so an empty doorway proves nothing");
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// A WINDOW IS A HOLE THROUGH THE WALL WITH GLASS IN IT.
        ///
        /// Three claims, and each one fails differently if it is wrong. The
        /// pane count has to match what WindowGrid says is glazed, or the
        /// geometry and the layout have drifted apart. No wall may stand inside
        /// a pane's own rectangle, or the opening was never cut and the glass
        /// is a sticker on concrete -- which is exactly the bug the Godot build
        /// reported as "you cant see the outside or inside through them" while
        /// the glass material itself was perfectly transparent. And the same
        /// has to hold for the INWARD wall, or a window is a recess rather than
        /// a way of seeing into a room.
        /// </summary>
        [Test]
        public void EveryGlazedCellIsARealOpeningWithAPaneInIt()
        {
            CityWorld world = new CityWorld(Seed);
            int panes = 0;
            int buildings = 0;

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    buildings++;

                    Build(x, y, out _, out MeshBuffer concrete, out MeshBuffer facade,
                        out MeshBuffer lit, out MeshBuffer dark);

                    // Two triangles a pane, in one buffer or the other.
                    int built = (lit.Triangles.Count + dark.Triangles.Count) / 6;
                    Assert.AreEqual(ExpectedPanes(world, site), built,
                        $"({x},{y}) glazed {ExpectedPanes(world, site)} cells but hung {built} panes");
                    panes += built;

                    // The wall at maximum Z, checked pane by pane. One wall is
                    // enough to catch a mismatch and keeps the sweep quick.
                    CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                    CellLayout columns = WindowGrid.ColumnsFor(site.Params.Width);
                    float inset = WindowGrid.PaneInset;
                    int seed = world.ChunkSeed(x, y);

                    for (int row = 1; row < rows.Count; row++)
                    {
                        for (int column = 0; column < columns.Count; column++)
                        {
                            if (!WindowGrid.IsGlazed(seed, WindowGrid.Wall.North, column, row))
                            {
                                continue;
                            }

                            float cellX = (float)site.MinX + column * columns.Size;
                            float cellY = site.BaseHeight + row * rows.Size;
                            float insetTop = WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height)
                                ? WindowGrid.PaneInsetVerticalSlab
                                : WindowGrid.PaneInsetVerticalClear;
                            var pane = Rect.MinMaxRect(
                                cellX + columns.Size * inset,
                                cellY + rows.Size * WindowGrid.PaneInsetVerticalClear,
                                cellX + columns.Size * (1.0f - inset),
                                cellY + rows.Size * (1.0f - insetTop));

                            float blocked = AreaInUprightRect(facade, (float)site.MaxZ, pane);
                            Assert.AreEqual(0.0f, blocked, 0.001f,
                                $"({x},{y}) has {blocked:0.000}m2 of facade across the window at " +
                                $"column {column}, row {row} -- the opening was never cut");

                            // A SKYBRIDGE CAN LEGITIMATELY STAND IN FRONT OF ONE OF THESE.
                            // BuildBridges lands a deck at a real floor boundary on the
                            // north wall (BuildSpan's own "EveryBridgeLandsOnAWholeFloor"
                            // contract), flush against this building's exterior at
                            // that exact Z -- and its box goes into the SAME buffer
                            // (flatConcrete, here "concrete") because a deck is
                            // concrete, not facade. Neither system knows about the
                            // other, so wherever a bridge's own floor happens to be a
                            // glazed row's floor too, the deck's flush end-cap sits
                            // right where this test is looking, on the inward side
                            // only -- BuildBridges never touches structures, which is
                            // why blocked (above) never sees it. Confirmed directly:
                            // dumping the triangles at (-6,-2) col 7 row 9 gave a box
                            // exactly BridgeWidth (4.0m) by BridgeThickness (0.6m),
                            // flush at site.MaxZ. That is a real deck landing on a
                            // real window, not a missed cut -- so the tolerance below
                            // is sized to one whole end-cap (4.0 * 0.6 = 2.4m2) rather
                            // than to the one site that happened to surface it, which
                            // is why a second, different building hit the identical
                            // number before this was understood. Anything past that
                            // is still a real regression: a whole uncut pane runs to
                            // several square metres at this building's own row height.
                            float inside = AreaInUprightRect(concrete, (float)site.MaxZ, pane);
                            Assert.AreEqual(0.0f, inside,
                                CityMeshBuilder.BridgeWidth * CityMeshBuilder.BridgeThickness + 0.01f,
                                $"({x},{y}) cut the outward wall at column {column}, row {row} but " +
                                "left the inward one solid -- you cannot see into the room");
                        }
                    }
                }
            }

            TestContext.WriteLine($"{panes} panes over {buildings} buildings");
            Assert.Greater(buildings, 20, "not enough buildings sampled");
            Assert.Greater(panes, 1000, "not enough glass sampled to mean anything");
        }

        /// <summary>
        /// THE DOOR FILLS THE HOLE THE WALL BUILDER CUT, exactly.
        ///
        /// Two numbers arrived at independently -- the opening comes from
        /// AddWallAround, the leaf from BuildingDoor -- and they have to agree
        /// or it shows immediately: a leaf narrower than its opening leaves a
        /// gap you can walk through with the door shut, and a wider one clips
        /// into the wall as it swings.
        ///
        /// It also hinges on the LEFT edge, which is what makes it swing inward
        /// clear of the opening rather than sweeping across it.
        /// </summary>
        [Test]
        public void EveryDoorLeafFillsItsOwnDoorway()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    BuildingDoor.Panel panel = BuildingDoor.PanelFor(site);

                    Assert.AreEqual(BuildingInterior.DoorWidth, panel.Width, 0.001f);
                    Assert.AreEqual(BuildingInterior.DoorHeight(site.Params.Height),
                        panel.Height, 0.001f, "the leaf is not the height of its own opening");

                    // Hinged on the opening's left edge, standing in the
                    // entrance wall, on the lobby floor.
                    float half = BuildingInterior.DoorWidth * 0.5f;
                    Assert.AreEqual((float)site.CentreX - half, panel.Hinge.x, 0.001f,
                        "the door does not hinge on the left edge of its opening");
                    Assert.AreEqual((float)site.MaxZ, panel.Hinge.z, 0.001f,
                        "the door is not in the entrance wall");
                    Assert.AreEqual(site.BaseHeight, panel.Hinge.y, 0.001f,
                        "the door does not stand on the lobby floor");

                    // The leaf reaches exactly across, no further.
                    Assert.AreEqual((float)site.CentreX + half, panel.Hinge.x + panel.Width, 0.001f);
                    Assert.LessOrEqual(panel.Hinge.y + panel.Height, site.Top,
                        "the door is taller than the building");
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// LANE PAINT GOES ON ROADS AND STAYS ON THEM.
        ///
        /// Two failures worth catching. Paint on a lot is paint on somebody's
        /// floor. And paint that reaches past the road onto the pavement is the
        /// giveaway that a crossing was sized against the chunk rather than
        /// against the street it crosses -- which is the bug Godot's own
        /// _add_crosswalks comment records fixing, where every band's outermost
        /// stripe overlapped the perpendicular band's.
        /// </summary>
        [Test]
        public void LanePaintOnlyEverLandsOnRoad()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);
            int junctions = 0;
            int runs = 0;

            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    var markings = new MeshBuffer();
                    CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                        new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                        null, null, null, null, null, markings);

                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        Assert.IsTrue(markings.IsEmpty, $"({x},{y}) is a lot with lane paint on it");
                        continue;
                    }

                    Assert.IsFalse(markings.IsEmpty, $"({x},{y}) is a street with no markings");

                    bool junction = CityLayout.IsStreetChunk(x - 1, y) && CityLayout.IsStreetChunk(x + 1, y) &&
                                    CityLayout.IsStreetChunk(x, y - 1) && CityLayout.IsStreetChunk(x, y + 1);
                    if (junction)
                    {
                        junctions++;
                    }
                    else
                    {
                        runs++;
                    }

                    float chunk = CityLayout.ChunkSize;
                    float minX = x * chunk;
                    float minZ = y * chunk;
                    float pad = world.Terrain.PadHeightFor(x, y);

                    foreach (Vector3 v in markings.Vertices)
                    {
                        Assert.AreEqual(pad + CityMeshBuilder.MarkingLift, v.y, 0.001f,
                            "paint is not lying on the road");
                        Assert.GreaterOrEqual(v.x, minX - 0.001f);
                        Assert.LessOrEqual(v.x, minX + chunk + 0.001f);
                        Assert.GreaterOrEqual(v.z, minZ - 0.001f);
                        Assert.LessOrEqual(v.z, minZ + chunk + 0.001f);

                        // The crossings are sized against the street they cross,
                        // so nothing may reach out onto a pavement.
                        if (!junction)
                        {
                            continue;
                        }
                        bool acrossX = v.x > minX + CityMeshBuilder.SidewalkWidth - 0.001f &&
                                       v.x < minX + chunk - CityMeshBuilder.SidewalkWidth + 0.001f;
                        bool acrossZ = v.z > minZ + CityMeshBuilder.SidewalkWidth - 0.001f &&
                                       v.z < minZ + chunk - CityMeshBuilder.SidewalkWidth + 0.001f;
                        Assert.IsTrue(acrossX || acrossZ,
                            $"({x},{y}) has a crossing stripe at {v} out in the corner");
                    }
                }
            }

            TestContext.WriteLine($"{runs} straight runs, {junctions} junctions");
            Assert.Greater(junctions, 3, "not enough junctions sampled");
            Assert.Greater(runs, 20, "not enough straight street sampled");
        }

        /// <summary>
        /// A BROKEN WINDOW STAYS BROKEN, AND THE HOLE STAYS A HOLE.
        ///
        /// The dangerous failure here is not that the glass fails to change --
        /// that is visible instantly. It is that the OPENING gets rebuilt along
        /// with it: the wall is cut around each glazed cell, so a builder that
        /// consulted damage in the wrong place would reinstate solid concrete
        /// where a shattered window used to be, and the window a player just
        /// smashed would become a wall they cannot walk through.
        /// </summary>
        [Test]
        public void BreakingAWindowChangesTheGlassAndNotTheHole()
        {
            CityWorld world = new CityWorld(Seed);
            SkybridgeLayout bridges = new SkybridgeLayout(Seed);

            (int x, int y) = FirstBuilding(world);
            BuildingSite site = world.BuildingAt(x, y);
            var damage = new GlassDamage();

            int wallBefore = BuildWith(world, bridges, x, y, damage,
                out int litBefore, out int darkBefore, out int crackedBefore);
            Assert.AreEqual(0, crackedBefore, "an untouched city has cracked glass in it");
            Assert.Greater(litBefore + darkBefore, 0, "this building has no glass to break");

            // Find a window that is really there, and hit it.
            PaneKey key = AnyPaneOn(world, site);
            Assert.AreEqual(PaneState.Cracked, damage.Hit(key));

            // One quad, which is six indices -- these are index counts, not
            // triangle counts, and the difference cost a confused test run.
            const int Quad = 6;

            var openings = new List<CityMeshBuilder.CrackedOpening>();
            int wallCracked = BuildWith(world, bridges, x, y, damage,
                out int litCracked, out int darkCracked, out int crackedNow, openings);

            // The opening a body can climb through. One per cracked pane, at
            // the pane, and with real depth -- a trigger with no thickness is
            // stepped over between two physics frames at a run.
            Assert.AreEqual(1, openings.Count, "a cracked pane got no opening, or several");
            Assert.AreEqual(key, openings[0].Key);
            Assert.GreaterOrEqual(openings[0].Size.x, CityMeshBuilder.CrackedOpeningDepth - 0.001f);
            Assert.GreaterOrEqual(openings[0].Size.z, CityMeshBuilder.CrackedOpeningDepth - 0.001f);
            Assert.Greater(openings[0].Size.y, 0.5f, "the opening is not window-shaped");
            Assert.AreEqual(Quad, crackedNow,
                "the cracked pane is not in the cracked buffer, or took something with it");
            Assert.AreEqual(litBefore + darkBefore - Quad, litCracked + darkCracked,
                "the pane left the intact buffers as exactly one quad");
            Assert.AreEqual(wallBefore, wallCracked,
                "cracking a window changed the wall around it");

            Assert.AreEqual(PaneState.Shattered, damage.Hit(key));

            int wallGone = BuildWith(world, bridges, x, y, damage,
                out int litGone, out int darkGone, out int crackedGone);
            Assert.AreEqual(0, crackedGone, "a shattered pane is still drawn");
            Assert.AreEqual(litBefore + darkBefore - Quad, litGone + darkGone);
            Assert.AreEqual(wallBefore, wallGone,
                "the wall grew back over a window somebody smashed");
        }

        /// <summary>Builds one chunk against a damage record and counts triangles.</summary>
        private static int BuildWith(CityWorld world, SkybridgeLayout bridges, int x, int y,
            GlassDamage damage, out int lit, out int dark, out int cracked,
            List<CityMeshBuilder.CrackedOpening> openings = null)
        {
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            var litGlass = new MeshBuffer();
            var darkGlass = new MeshBuffer();
            var crackedGlass = new MeshBuffer();

            CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                ground, pavement, structures, litGlass, darkGlass, crackedGlass, damage, openings);

            lit = litGlass.Triangles.Count;
            dark = darkGlass.Triangles.Count;
            cracked = crackedGlass.Triangles.Count;
            return structures.Triangles.Count;
        }

        private static (int, int) FirstBuilding(CityWorld world)
        {
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (site.Exists && site.AnchorX == x && site.AnchorY == y &&
                        site.Params.Width > 14.0f)
                    {
                        return (x, y);
                    }
                }
            }
            Assert.Fail("no building near the origin");
            return (0, 0);
        }

        private static PaneKey AnyPaneOn(CityWorld world, BuildingSite site)
        {
            int seed = world.ChunkSeed(site.AnchorX, site.AnchorY);
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = WindowGrid.ColumnsFor(site.Params.Width);

            for (int row = 1; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (WindowGrid.IsGlazed(seed, WindowGrid.Wall.North, column, row))
                    {
                        return new PaneKey(site.AnchorX, site.AnchorY,
                            WindowGrid.Wall.North, column, row);
                    }
                }
            }
            Assert.Fail("this building's front wall has no windows at all");
            return default;
        }

        /// <summary>How many cells WindowGrid glazes on a building, doorway aside.</summary>
        private static int ExpectedPanes(CityWorld world, BuildingSite site)
        {
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            int seed = world.ChunkSeed(site.AnchorX, site.AnchorY);
            float centreX = (float)site.CentreX;
            float doorU0 = centreX - BuildingInterior.DoorWidth * 0.5f;
            float doorU1 = centreX + BuildingInterior.DoorWidth * 0.5f;

            int total = 0;
            foreach ((WindowGrid.Wall wall, float span) in new[]
                     {
                         (WindowGrid.Wall.West, site.Params.Depth),
                         (WindowGrid.Wall.East, site.Params.Depth),
                         (WindowGrid.Wall.South, site.Params.Width),
                         (WindowGrid.Wall.North, site.Params.Width),
                     })
            {
                CellLayout columns = WindowGrid.ColumnsFor(span);
                for (int row = 0; row < rows.Count; row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        if (!WindowGrid.IsGlazed(seed, wall, column, row))
                        {
                            continue;
                        }
                        // The entrance row of the door wall loses whatever
                        // cells the opening passes through.
                        if (wall == WindowGrid.Wall.North && row == 0)
                        {
                            float cellU0 = (float)site.MinX + column * columns.Size;
                            if (cellU0 + columns.Size > doorU0 && cellU0 < doorU1)
                            {
                                continue;
                            }
                        }
                        total++;
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Area of geometry standing in an upright rectangle on a wall at fixed
        /// Z. <paramref name="area"/> is in X and Y, unlike
        /// <see cref="AreaInPlane"/>'s plan rectangles.
        ///
        /// Faces BOTH ways, because the outward and inward copies of a wall sit
        /// in the same plane with opposite normals, and asking about one of
        /// them means not filtering the other out by accident.
        /// </summary>
        private static float AreaInUprightRect(MeshBuffer buffer, float atZ, Rect area)
        {
            float total = 0.0f;
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                if (Mathf.Abs(a.z - atZ) > 0.001f || Mathf.Abs(b.z - atZ) > 0.001f ||
                    Mathf.Abs(c.z - atZ) > 0.001f)
                {
                    continue;
                }

                Vector3 centroid = (a + b + c) / 3.0f;
                if (centroid.x <= area.xMin || centroid.x >= area.xMax ||
                    centroid.y <= area.yMin || centroid.y >= area.yMax)
                {
                    continue;
                }
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        /// <summary>
        /// NO GROUND UNDER A BUILDING. A building's lobby floor sits at
        /// BaseHeight and on level ground so does the chunk's own surface, so
        /// leaving both means two coplanar upward faces at the same height --
        /// which is not a floor, it is a z-fight you only notice once you can
        /// walk in and stand on it.
        /// </summary>
        [Test]
        public void TheGroundIsCutAwayUnderEveryBuilding()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -14; x <= 14; x++)
            {
                for (int y = -14; y <= 14; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out MeshBuffer ground, out _);
                    Rect plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);

                    for (int t = 0; t + 2 < ground.Triangles.Count; t += 3)
                    {
                        // Only the walkable surface. Retaining walls are
                        // vertical and a 2x2 building covers its own internal
                        // chunk edges, so a skirt buried under one is normal.
                        if (ground.Normals[ground.Triangles[t]].y < 0.5f)
                        {
                            continue;
                        }
                        Vector3 a = ground.Vertices[ground.Triangles[t]];
                        Vector3 b = ground.Vertices[ground.Triangles[t + 1]];
                        Vector3 c = ground.Vertices[ground.Triangles[t + 2]];
                        Vector3 centroid = (a + b + c) / 3.0f;
                        Assert.IsFalse(Covers(plan, centroid),
                            $"chunk ({x},{y}) still has ground at {centroid} inside the footprint "
                            + $"of the building at ({site.AnchorX},{site.AnchorY})");
                    }
                }
            }

            Assert.Greater(checkedCount, 200, "not enough built-on chunks sampled");
        }

        /// <summary>
        /// A FLOOR TO STAND ON AT EVERY STOREY, and the roof opened for the
        /// top flight. Without the opening the last staircase climbs into a
        /// solid ceiling and the roof is unreachable.
        /// </summary>
        [Test]
        public void EveryStoreyGetsAFloorAndTheRoofGetsItsStairwell()
        {
            CityWorld world = new CityWorld(Seed);
            int checkedCount = 0;

            for (int x = -12; x <= 12; x++)
            {
                for (int y = -12; y <= 12; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    checkedCount++;

                    Build(x, y, out _, out MeshBuffer concrete, out _);

                    float height = site.Params.Height;
                    float storey = BuildingInterior.StoreyHeight(height);
                    int floors = BuildingInterior.FloorCountFor(height);
                    float footprint = site.Params.Width * site.Params.Depth;

                    var plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);

                    float run = BuildingInterior.RunFor(
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f);

                    // The lobby, whole except where a basement is reached
                    // through it. ABasementIsARoomWithAHoleInTheLobbyAboveIt
                    // owns that opening; here it is only subtracted so the
                    // floor's own area still adds up.
                    Rect cellar = Rect.zero;
                    if (site.Params.HasBasement)
                    {
                        Rect local = BuildingInterior.StairwellFor(
                            BuildingInterior.BasementStairCorner,
                            site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                        cellar = new Rect(local.x + (float)site.CentreX,
                            local.y + (float)site.CentreZ, local.width, local.height);
                    }
                    Assert.AreEqual(footprint - cellar.width * cellar.height,
                        AreaInPlane(concrete, site.BaseHeight, 1, plan, cellar), 0.05f,
                        $"({x},{y}) has no lobby floor -- you walk in the door and fall down "
                        + "the inside of the podium");

                    for (int floor = 0; floor < floors; floor++)
                    {
                        float top = site.BaseHeight
                                    + BuildingInterior.FloorTop(floor, floors, height, storey);
                        Rect local = BuildingInterior.StairwellFor(
                            floor, site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                        var hole = new Rect(
                            local.x + (float)site.CentreX, local.y + (float)site.CentreZ,
                            local.width, local.height);

                        // THE OPENING IS EXCLUDED, not just subtracted. The
                        // topmost step's tread lands flush with the storey
                        // above -- that is the landing you step off onto --
                        // so it lies in this exact plane, inside the hole.
                        // Measuring the slab means measuring outside it.
                        Assert.AreEqual(footprint - hole.width * hole.height,
                            AreaInPlane(concrete, top, 1, plan, hole), 0.05f,
                            $"({x},{y}) floor {floor}'s ceiling is not the footprint with its "
                            + "stairwell taken out of it");
                    }
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
        }

        /// <summary>
        /// A BASEMENT IS A REAL ROOM WITH A WAY IN, and a building without one
        /// still has an unbroken lobby floor. The second half matters as much
        /// as the first: a hole cut in every lobby would be a pit in nine
        /// buildings out of ten.
        /// </summary>
        [Test]
        public void ABasementIsARoomWithAHoleInTheLobbyAboveIt()
        {
            CityWorld world = new CityWorld(Seed);
            int cellars = 0;
            int plain = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer concrete, out _);
                    var plan = Rect.MinMaxRect((float)site.MinX, (float)site.MinZ,
                        (float)site.MaxX, (float)site.MaxZ);
                    float footprint = site.Params.Width * site.Params.Depth;
                    float storey = BuildingInterior.StoreyHeight(site.Params.Height);
                    float run = BuildingInterior.RunFor(
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f);
                    Rect hole = BuildingInterior.StairwellFor(BuildingInterior.BasementStairCorner,
                        site.Params.Width * 0.5f, site.Params.Depth * 0.5f, run).Hole;
                    float opening = hole.width * hole.height;

                    if (!site.Params.HasBasement)
                    {
                        plain++;
                        Assert.AreEqual(footprint,
                            AreaInPlane(concrete, site.BaseHeight, 1, plan, Rect.zero), 0.05f,
                            $"({x},{y}) has no basement but its lobby floor is not solid");
                        Assert.IsFalse(HasSurfaceBelow(concrete, plan, site.BaseHeight - 0.5f),
                            $"({x},{y}) has no basement but built geometry under its lobby");
                        continue;
                    }

                    cellars++;
                    var world_hole = new Rect(hole.x + (float)site.CentreX,
                        hole.y + (float)site.CentreZ, hole.width, hole.height);
                    Assert.AreEqual(footprint - opening,
                        AreaInPlane(concrete, site.BaseHeight, 1, plan, world_hole), 0.05f,
                        $"({x},{y}) has a basement but no way down into it");

                    // And a floor at the bottom of it, or the way down is a pit.
                    Assert.AreEqual(footprint,
                        AreaInPlane(concrete, site.BaseHeight - storey, 1, plan, Rect.zero), 0.05f,
                        $"({x},{y})'s basement has no floor");
                }
            }

            TestContext.WriteLine($"{cellars} basements, {plain} buildings without one");
            Assert.Greater(cellars, 5, "no basements in the sweep, so this proved nothing");
            Assert.Greater(plain, cellars * 4, "basements are supposed to be rare");
        }

        /// <summary>Is there any geometry under this footprint below a height?</summary>
        private static bool HasSurfaceBelow(MeshBuffer buffer, Rect plan, float below)
        {
            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                Vector3 v = buffer.Vertices[i];
                if (v.y < below && Covers(plan, v))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// EVERY ROOF TYPE THE GENERATOR ROLLS ACTUALLY BUILDS SOMETHING.
        ///
        /// The roof was picked, stored and asserted against its own table for
        /// a long time while nothing read it, so every rooftop in the city was
        /// a bare plane and no test noticed -- the data was perfect and unused.
        /// This checks the other end: a building whose roof is not flat has
        /// geometry standing above its own roofline, and a flat one does not.
        /// </summary>
        [Test]
        public void EveryRoofTypeBuildsSomethingOnTheRoof()
        {
            CityWorld world = new CityWorld(Seed);
            var seen = new Dictionary<string, int>();

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }

                    Build(x, y, out _, out MeshBuffer concrete, out _);

                    // WIDENED BY A HAIR, because a parapet is a rim wall flush
                    // with the roof edge: every one of its vertices sits
                    // exactly ON the footprint boundary in at least one axis,
                    // and strict containment throws all of them away. The
                    // nearest neighbouring building is metres off, so this
                    // cannot pick up anything but this roof.
                    const float edge = 0.01f;
                    var plan = Rect.MinMaxRect((float)site.MinX - edge, (float)site.MinZ - edge,
                        (float)site.MaxX + edge, (float)site.MaxZ + edge);

                    // Anything standing proud of the roof, inside the
                    // footprint. Skybridges land below the roofline and other
                    // buildings are elsewhere, so this is the props alone.
                    bool anythingUpThere = false;
                    foreach (Vector3 v in concrete.Vertices)
                    {
                        if (v.y > site.Top + 0.01f && Covers(plan, v))
                        {
                            anythingUpThere = true;
                            break;
                        }
                    }

                    string roof = string.IsNullOrEmpty(site.Params.Roof) ? "flat" : site.Params.Roof;
                    seen.TryGetValue(roof, out int count);
                    seen[roof] = count + 1;

                    if (roof == "flat")
                    {
                        Assert.IsFalse(anythingUpThere,
                            $"({x},{y}) has a flat roof but something is standing on it");
                    }
                    else
                    {
                        Assert.IsTrue(anythingUpThere,
                            $"({x},{y}) rolled a '{roof}' roof and nothing was built for it");
                    }
                }
            }

            TestContext.WriteLine(string.Join(", ",
                seen.Select(pair => $"{pair.Key} {pair.Value}")));

            // Every type in the table has to actually turn up, or this could
            // pass while one of them silently built nothing.
            foreach (string type in CityChunkGenerator.RoofTypes)
            {
                string roof = string.IsNullOrEmpty(type) ? "flat" : type;
                Assert.IsTrue(seen.ContainsKey(roof),
                    $"no building in the sweep rolled a '{roof}' roof, so it went unchecked");
            }
        }

        /// <summary>
        /// What the staircases actually cost, pinned rather than guessed at.
        ///
        /// THE TOP FLOOR SWALLOWS WHATEVER THE FLOOR CAP LEFT OVER, which is
        /// city_chunk.gd's own rule and is carried across unchanged. Nearly two
        /// thirds of this city hits that cap, so nearly two thirds of its
        /// buildings have a top floor several times taller than any other, and
        /// a flight of steps that climbs all of it at a real 0.25m riser. That
        /// is where almost all of this number comes from. It is pinned here so
        /// that changing the rule -- which is a design call, not a porting one
        /// -- shows up as a number moving rather than as a vague feeling that
        /// the city got heavier.
        /// </summary>
        [Test]
        public void TheStairsAreAKnownCostRatherThanASurprise()
        {
            CityWorld world = new CityWorld(Seed);
            long steps = 0;
            int buildings = 0;
            int capped = 0;
            int worst = 0;

            for (int x = -20; x <= 20; x++)
            {
                for (int y = -20; y <= 20; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!site.Exists || site.AnchorX != x || site.AnchorY != y)
                    {
                        continue;
                    }
                    buildings++;

                    float height = site.Params.Height;
                    float storey = BuildingInterior.StoreyHeight(height);
                    int floors = BuildingInterior.FloorCountFor(height);
                    if (floors == BuildingInterior.MaxFloors)
                    {
                        capped++;
                    }

                    int here = 0;
                    for (int floor = 0; floor < floors; floor++)
                    {
                        here += BuildingInterior.StepsFor(
                            BuildingInterior.FloorTop(floor, floors, height, storey) - floor * storey);
                    }
                    steps += here;
                    worst = Mathf.Max(worst, here);
                }
            }

            float mean = steps / (float)buildings;
            TestContext.WriteLine($"{buildings} buildings, {capped} at the floor cap");
            TestContext.WriteLine($"{steps} steps, {mean:0} per building, worst {worst}");

            Assert.Greater(capped / (float)buildings, 0.5f,
                "most of this city used to hit the floor cap; if that stopped being true the "
                + "top-floor rule is worth another look");
            Assert.Less(mean, 400.0f,
                $"a building averages {mean:0} steps, which is {mean * 10:0} triangles of "
                + "staircase each -- something made the top floor taller still");
        }

        /// <summary>
        /// Area of every face lying in one axis plane and facing along it.
        /// <paramref name="axis"/> is 0 for X, 1 for Y, 2 for Z.
        ///
        /// Both rectangles are in X and Z, and an empty one means no filter.
        /// <paramref name="within"/> keeps the measurement to one building --
        /// skybridges land on whole storeys, so a neighbour's walkway deck can
        /// sit in the exact plane of this building's floor and be counted as
        /// floor. <paramref name="exclude"/> drops the stairwell opening.
        /// </summary>
        private static float AreaInPlane(MeshBuffer buffer, float at, int axis,
            Rect within, Rect exclude)
        {
            float total = 0.0f;
            for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
            {
                Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                if (Mathf.Abs(a[axis] - at) > 0.001f ||
                    Mathf.Abs(b[axis] - at) > 0.001f ||
                    Mathf.Abs(c[axis] - at) > 0.001f)
                {
                    continue;
                }

                // Only the side that faces out of the plane, or a slab's own
                // underside would be counted along with its top.
                if (buffer.Normals[buffer.Triangles[t]][axis] <= 0.0f)
                {
                    continue;
                }

                Vector3 centroid = (a + b + c) / 3.0f;
                if (Covers(exclude, centroid) || (within.width > 0.0f && !Covers(within, centroid)))
                {
                    continue;
                }
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        private static bool Covers(Rect area, Vector3 point)
        {
            return area.width > 0.0f &&
                   point.x > area.xMin && point.x < area.xMax &&
                   point.z > area.yMin && point.z < area.yMax;
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
