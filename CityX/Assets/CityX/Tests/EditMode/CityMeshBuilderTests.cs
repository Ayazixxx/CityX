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

                    // Bridges extend past the building, so only the lower
                    // corner and the roof can be compared exactly.
                    Assert.AreEqual(site.MinX - westFins, bounds.min.x, 0.01, $"({x},{y}) west face");
                    Assert.AreEqual(site.MinZ - southFins, bounds.min.z, 0.01, $"({x},{y}) south face");
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
        /// THE DOOR IS A REAL HOLE, not a painted-on rectangle. Measured as
        /// area rather than by looking for missing vertices, because a wall
        /// built as four strips around an opening has vertices ON the
        /// opening's edge either way -- only the area tells them apart.
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

                    // The whole wall, if it were solid, against what was built.
                    float wallHeight = site.Top - site.PodiumBase;
                    float solid = site.Params.Width * wallHeight;
                    float doorway = BuildingInterior.DoorWidth * BuildingInterior.DoorHeight(site.Params.Height);
                    float built = AreaInPlane(structures, (float)site.MaxZ, 2, Rect.zero, Rect.zero);

                    Assert.AreEqual(solid - doorway, built, 0.05f,
                        $"({x},{y})'s front wall came out {built:0.00}m2 against a solid "
                        + $"{solid:0.00}m2 less a {doorway:0.00}m2 doorway -- there is no way in");
                }
            }

            Assert.Greater(checkedCount, 40, "not enough buildings sampled");
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
